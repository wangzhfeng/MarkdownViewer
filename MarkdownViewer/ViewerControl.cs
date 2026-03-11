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
using Microsoft.Web.WebView2.WinForms;

namespace MarkdownViewer
{
    public partial class ViewerControl : UserControl
    {
        private Encoding encoding = Encoding.UTF8;
        private const String TMPL_FILE_NAME = "markdown_tmpl.txt";
        private const String CSS_FILE_NAME = "markdown_css.txt";

        private ListerPlugin listerPlugin;
        private string tempHtmlFile = null;

        public ViewerControl(ListerPlugin listerPlugin)
        {
            InitializeComponent();
            this.listerPlugin = listerPlugin;
            
            // Initialize WebView2 asynchronously
            InitializeWebView2();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            
            // Clean up temp HTML file when control is destroyed
            if (!String.IsNullOrEmpty(tempHtmlFile) && File.Exists(tempHtmlFile))
            {
                try { File.Delete(tempHtmlFile); } catch { }
                tempHtmlFile = null;
            }
        }

        private async void InitializeWebView2()
        {
            try
            {
                // Create environment with options to allow local file access
                var options = new CoreWebView2EnvironmentOptions(null);
                // Add command line switches to allow file access
                string additionalArgs = "--allow-file-access-from-files --disable-web-security --allow-file-access";
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                await webView2.EnsureCoreWebView2Async(env);
                
                // Configure WebView2 settings
                webView2.CoreWebView2.Settings.IsScriptEnabled = true;
                webView2.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView2.CoreWebView2.Settings.IsWebMessageEnabled = true;
                
                // Add file:// protocol to allowed origin list
                webView2.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "appassets.assets", 
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    CoreWebView2HostResourceAccessKind.Allow);
                
                // Subscribe to NavigationCompleted event
                webView2.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                
                // Hide loading panel when ready
                ShowLoading(false);
                
                System.Diagnostics.Trace.WriteLine("WebView2 initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("WebView2 initialization error: " + ex.Message);
                ShowLoading(false);
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine("NavigationCompleted: " + (e.IsSuccess ? "Success" : "Failed - " + e.WebErrorStatus));
            
            // Hide loading panel after navigation completes
            ShowLoading(false);
            
            if (e.IsSuccess)
            {
                InjectKeyboardHandler();
            }
        }

        private async void InjectKeyboardHandler()
        {
            try
            {
                string script = @"
                    document.addEventListener('keydown', function(e) {
                        var keys = [27, 49, 50, 51, 52, 53, 54, 55];
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

        public async System.Threading.Tasks.Task ExecuteScriptAsync(string script)
        {
            try
            {
                if (webView2.CoreWebView2 != null)
                {
                    await webView2.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("ExecuteScriptAsync error: " + ex.Message);
            }
        }

        public async System.Threading.Tasks.Task PrintAsync()
        {
            try
            {
                if (webView2.CoreWebView2 != null)
                {
                    await webView2.CoreWebView2.ExecuteScriptAsync("window.print()");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("PrintAsync error: " + ex.Message);
            }
        }

        private void ShowLoading(bool show)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowLoading(show)));
                return;
            }

            if (loadingPanel != null)
            {
                loadingPanel.Visible = show;
            }
        }

        public void FileLoad(String fileName)
        {
            // Show loading indicator
            ShowLoading(true);
            
            // Parse markdown file in worker thread
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
                    markdownHTML = DecodeImagePath(markdownHTML);

                    // Read markdown template from file
                    var buildDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var tmplFilePath = buildDir + @"\" + TMPL_FILE_NAME;
                    var markdownTmpl = File.ReadAllText(tmplFilePath);

                    // Read style content from file
                    var styleFilePath = buildDir + @"\" + CSS_FILE_NAME;
                    var style = File.ReadAllText(styleFilePath);

                    // Fix issue #15: Keep Chinese characters as-is without encoding
                    String dirPath = Path.GetDirectoryName(fileName);
                    String normalizedDirPath = dirPath.Replace("\\", "/");
                    String html = markdownTmpl.Replace("{0}", normalizedDirPath).Replace("{1}", style).Replace("{2}", markdownHTML);

                    System.Diagnostics.Trace.WriteLine("Markdown parsed successfully, HTML length: " + html.Length);

                    // Save HTML to temp file and navigate to it (avoids CORS issues)
                    String tempFile = Path.Combine(Path.GetTempPath(), "markdownviewer_" + Path.GetFileName(fileName) + ".html");
                    File.WriteAllText(tempFile, html, Encoding.UTF8);
                    
                    // Clean up previous temp file
                    if (!String.IsNullOrEmpty(tempHtmlFile) && File.Exists(tempHtmlFile))
                    {
                        try { File.Delete(tempHtmlFile); } catch { }
                    }
                    tempHtmlFile = tempFile;

                    // Load HTML content on UI thread
                    Action act = delegate ()
                    {
                        try
                        {
                            if (webView2.CoreWebView2 != null)
                            {
                                webView2.CoreWebView2.Navigate("file:///" + tempFile.Replace("\\", "/"));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine("Error navigating WebView2: " + ex.Message);
                            ShowLoading(false);
                        }
                    };
                    
                    if (!this.IsHandleCreated)
                    {
                        this.CreateControl();
                    }
                    this.Invoke(act);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("ParseMarkdownFile error: " + ex.ToString());
                ShowLoading(false);
            }
        }

        private String DecodeImagePath(String html)
        {
            return Regex.Replace(html,
                "(src|href)=\"([^\"]+)\"",
                match =>
                {
                    string attr = match.Groups[1].Value;
                    string url = match.Groups[2].Value;
                    
                    if (url.Contains("%") && (url.StartsWith("file://") || !url.StartsWith("http")))
                    {
                        try
                        {
                            string decoded = Uri.UnescapeDataString(url);
                            return $"{attr}=\"{decoded}\"";
                        }
                        catch
                        {
                            return match.Value;
                        }
                    }
                    return match.Value;
                });
        }
    }
}
