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

        public ViewerControl(ListerPlugin listerPlugin)
        {
            InitializeComponent();
            this.listerPlugin = listerPlugin;
            
            // Initialize WebView2 synchronously to avoid timing issues
            InitializeWebView2();
        }

        private async void InitializeWebView2()
        {
            try
            {
                var options = new CoreWebView2EnvironmentOptions("--allow-file-access-from-files");
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                await webView2.EnsureCoreWebView2Async(env);
                
                webView2.CoreWebView2.Settings.IsScriptEnabled = true;
                webView2.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                
                webView2.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                
                ShowLoading(false);
                TraceLog("WebView2 initialized");
            }
            catch (Exception ex)
            {
                TraceLog("WebView2 init error: " + ex.Message);
                ShowLoading(false);
            }
        }

        private async void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                ShowLoading(false);
                InjectKeyboardHandler();
            }
            else
            {
                TraceLog("Navigation failed: " + e.WebErrorStatus);
                ShowLoading(false);
            }
        }

        private async void InjectKeyboardHandler()
        {
            try
            {
                string script = @"document.addEventListener('keydown', function(e) {
                    var keys = [27, 49, 50, 51, 52, 53, 54, 55];
                    if (keys.indexOf(e.keyCode) !== -1) {
                        window.chrome.webview.hostObjects.callback.OnKeyPressed(e.keyCode);
                    }
                });";
                await webView2.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                TraceLog("InjectKeyboardHandler error: " + ex.Message);
            }
        }

        public async System.Threading.Tasks.Task ExecuteScriptAsync(string script)
        {
            if (webView2.CoreWebView2 != null)
            {
                await webView2.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        public async System.Threading.Tasks.Task PrintAsync()
        {
            if (webView2.CoreWebView2 != null)
            {
                await webView2.CoreWebView2.ExecuteScriptAsync("window.print()");
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

        private void TraceLog(string message)
        {
            if (listerPlugin is MarkdownViewer)
            {
                ((MarkdownViewer)listerPlugin).Log(message);
            }
        }

        public void FileLoad(String fileName)
        {
            ShowLoading(true);
            
            // Parse markdown in worker thread
            new Thread(() => ParseMarkdownFile(fileName)).Start();
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
                    markdownHTML = DecodeImagePath(markdownHTML);

                    var buildDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var tmplFilePath = buildDir + @"\" + TMPL_FILE_NAME;
                    var markdownTmpl = File.ReadAllText(tmplFilePath);

                    var styleFilePath = buildDir + @"\" + CSS_FILE_NAME;
                    var style = File.ReadAllText(styleFilePath);

                    String dirPath = Path.GetDirectoryName(fileName);
                    String normalizedDirPath = dirPath.Replace("\\", "/");
                    String html = markdownTmpl.Replace("{0}", normalizedDirPath).Replace("{1}", style).Replace("{2}", markdownHTML);

                    // Navigate directly with NavigateToString - simpler and faster
                    this.Invoke(new Action(() =>
                    {
                        try
                        {
                            if (webView2.CoreWebView2 != null)
                            {
                                webView2.CoreWebView2.NavigateToString(html);
                            }
                        }
                        catch (Exception ex)
                        {
                            TraceLog("Navigate error: " + ex.Message);
                            ShowLoading(false);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                TraceLog("Parse error: " + ex.ToString());
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
                            return $"{attr}=\"{Uri.UnescapeDataString(url)}\"";
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
