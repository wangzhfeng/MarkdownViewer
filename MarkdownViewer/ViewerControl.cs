using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using Markdig;
using OY.TotalCommander.TcPluginInterface.Lister;
using System.Linq;
using System.Reflection;
using Pek.Markdig.HighlightJs;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace MarkdownViewer
{
    public partial class ViewerControl : UserControl
    {
        private Encoding encoding = Encoding.UTF8;
        private const String TMPL_FILE_NAME = "markdown_tmpl.txt";
        private const String CSS_FILE_NAME = "markdown_css.txt";

        private ListerPlugin listerPlugin;
        private bool isWebViewInitialized = false;
        private bool hasLoadedContent = false;

        public ViewerControl(ListerPlugin listerPlugin)
        {
            InitializeComponent();
            this.listerPlugin = listerPlugin;
            
            // Initialize WebView2 asynchronously
            InitializeWebView2();
        }

        private async void InitializeWebView2()
        {
            try
            {
                // Create CoreWebView2Environment with user data folder
                var env = await CoreWebView2Environment.CreateAsync(null, null);
                await webView2.EnsureCoreWebView2Async(env);
                isWebViewInitialized = true;
                
                // Configure WebView2 settings
                webView2.CoreWebView2.Settings.IsScriptEnabled = true;
                webView2.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView2.CoreWebView2.Settings.IsWebMessageEnabled = true;
                
                // Subscribe to NavigationCompleted event
                webView2.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                
                System.Diagnostics.Trace.WriteLine("WebView2 initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("WebView2 initialization error: " + ex.Message);
                System.Diagnostics.Trace.WriteLine("Stack trace: " + ex.StackTrace);
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                // Inject keyboard handler after navigation completes
                InjectKeyboardHandler();
            }
        }

        private async void InjectKeyboardHandler()
        {
            try
            {
                // Inject JavaScript to capture keyboard events and forward to parent
                string script = @"
                    document.addEventListener('keydown', function(e) {
                        var keys = [27, 49, 50, 51, 52, 53, 54, 55]; // Esc, 1-7
                        if (keys.indexOf(e.keyCode) !== -1) {
                            window.chrome.webview.hostObjects.callback.OnKeyPressed(e.keyCode);
                        }
                    });
                ";
                await webView2.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("InjectKeyboardHandler error: " + ex.Message);
            }
        }

        /// <summary>
        /// Execute JavaScript in the WebView2
        /// </summary>
        public async System.Threading.Tasks.Task ExecuteScriptAsync(string script)
        {
            try
            {
                if (isWebViewInitialized && webView2.CoreWebView2 != null)
                {
                    await webView2.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("ExecuteScriptAsync error: " + ex.Message);
            }
        }

        /// <summary>
        /// Print the current content using browser's print dialog
        /// </summary>
        public async System.Threading.Tasks.Task PrintAsync()
        {
            try
            {
                if (isWebViewInitialized && webView2.CoreWebView2 != null)
                {
                    await webView2.CoreWebView2.ExecuteScriptAsync("window.print()");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("PrintAsync error: " + ex.Message);
            }
        }

        public void FileLoad(String fileName)
        {
            System.Diagnostics.Trace.WriteLine("FileLoad called for: " + fileName);
            
            // If WebView2 has already loaded content, reinitialize it to avoid blank page issue
            if (hasLoadedContent && isWebViewInitialized)
            {
                System.Diagnostics.Trace.WriteLine("Reinitializing WebView2 for new content");
                hasLoadedContent = false;
                isWebViewInitialized = false;
                
                // Dispose and recreate the WebView2 control
                webView2.Dispose();
                
                var newWebView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
                newWebView2.AllowExternalDrop = true;
                newWebView2.CreationProperties = null;
                newWebView2.DefaultBackgroundColor = System.Drawing.Color.White;
                newWebView2.Dock = System.Windows.Forms.DockStyle.Fill;
                newWebView2.Location = new System.Drawing.Point(0, 0);
                newWebView2.Name = "webView2";
                newWebView2.TabIndex = 0;
                newWebView2.ZoomFactor = 1D;
                
                this.Controls.Clear();
                this.Controls.Add(newWebView2);
                this.webView2 = newWebView2;
                
                // Reinitialize WebView2 asynchronously
                InitializeWebView2();
            }
            
            // Wait for WebView2 to be initialized if needed
            int waitCount = 0;
            while (!isWebViewInitialized && waitCount < 50) // Wait up to 5 seconds
            {
                Thread.Sleep(100);
                waitCount++;
            }
            
            if (!isWebViewInitialized)
            {
                System.Diagnostics.Trace.WriteLine("WebView2 not initialized after waiting, proceeding anyway");
            }
            
            // parse markdown file in worker thread
            Thread threadObj = new Thread(new ThreadStart(delegate
            {
                ParseMarkdownFile(fileName);
            }));
            threadObj.Start();
        }

        private void ParseMarkdownFile(String fileName)
        {
            try
            {
                using (StreamReader sr = new StreamReader(fileName, encoding))
                {
                    String markdownContent = sr.ReadToEnd();
                    var pipeline = new MarkdownPipelineBuilder()
                        .UseAdvancedExtensions()
                        .UseEmojiAndSmiley()
                        .UseYamlFrontMatter()
                        .UseFootnotes()
                        .UseHighlightJs()
                        .Build();
                    String markdownHTML = Markdown.ToHtml(markdownContent, pipeline);
                    
                    // Fix issue #15: Decode URL-encoded Chinese characters in image paths
                    // Markdig automatically encodes URLs, but Windows file:// protocol needs raw Unicode
                    markdownHTML = DecodeImagePath(markdownHTML);

                    // read markdown tmpl from file
                    var buildDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var tmplFilePath = buildDir + @"\" + TMPL_FILE_NAME;
                    var markdownTmpl = File.ReadAllText(tmplFilePath);

                    // read style content from file
                    var styleFilePath = buildDir + @"\" + CSS_FILE_NAME;
                    var style = File.ReadAllText(styleFilePath);

                    // Fix issue #15: Keep Chinese characters as-is without encoding
                    // Windows file:/// protocol supports Unicode paths directly
                    String dirPath = Path.GetDirectoryName(fileName);
                    // Use forward slashes and file:/// prefix for absolute path
                    String normalizedDirPath = "file:///" + dirPath.Replace("\\", "/");
                    // Use Replace instead of String.Format to avoid FormatException from curly braces in HTML
                    String html = markdownTmpl.Replace("{0}", normalizedDirPath).Replace("{1}", style).Replace("{2}", markdownHTML);

                    Action act = delegate ()
                    {
                        try
                        {
                            if (isWebViewInitialized && webView2.CoreWebView2 != null)
                            {
                                // Navigate to the new content
                                webView2.CoreWebView2.NavigateToString(html);
                                
                                // Mark that content has been loaded
                                hasLoadedContent = true;
                                
                                System.Diagnostics.Trace.WriteLine("WebView2 NavigateToString called for: " + fileName);
                            }
                            else
                            {
                                System.Diagnostics.Trace.WriteLine("WebView2 not initialized yet. isWebViewInitialized=" + isWebViewInitialized + 
                                    ", CoreWebView2=" + (webView2.CoreWebView2 != null));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine("Error navigating WebView2: " + ex.Message);
                            System.Diagnostics.Trace.WriteLine("Stack trace: " + ex.StackTrace);
                        }
                    };
                    
                    // Ensure handle is created before invoking
                    if (!this.IsHandleCreated)
                    {
                        // Force handle creation if needed
                        this.CreateControl();
                    }
                    this.Invoke(act);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("ParseMarkdownFile error: " + ex.ToString());
            }
        }

        /// <summary>
        /// Decode URL-encoded Chinese characters in image/source paths
        /// Markdig encodes paths like %E4%B8%AD%E6%96%87 but Windows file:// needs raw Unicode
        /// </summary>
        private String DecodeImagePath(String html)
        {
            // Match src="..." or href="..." in img, a, source tags
            return Regex.Replace(html,
                "(src|href)=\"([^\"]+)\"",
                match =>
                {
                    string attr = match.Groups[1].Value;
                    string url = match.Groups[2].Value;
                    
                    // Only decode file:// URLs or relative paths with percent encoding
                    if (url.Contains("%") && (url.StartsWith("file://") || !url.StartsWith("http")))
                    {
                        try
                        {
                            string decoded = Uri.UnescapeDataString(url);
                            return $"{attr}=\"{decoded}\"";
                        }
                        catch
                        {
                            // If decoding fails, keep original
                            return match.Value;
                        }
                    }
                    return match.Value;
                });
        }
    }
}
