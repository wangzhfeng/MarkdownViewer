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
        private string currentTempFile = null;
        private bool isWebViewInitialized = false;
        private string pendingFileToLoad = null;

        public ViewerControl(ListerPlugin listerPlugin)
        {
            InitializeComponent();
            this.listerPlugin = listerPlugin;
            
            InitializeWebView2();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            // Cleanup temp file
            if (!String.IsNullOrEmpty(currentTempFile) && File.Exists(currentTempFile))
            {
                try { File.Delete(currentTempFile); } catch { }
            }
        }

        private string currentFileDir = null;

        private async void InitializeWebView2()
        {
            try
            {
                var options = new CoreWebView2EnvironmentOptions("--allow-file-access-from-files");
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                await webView2.EnsureCoreWebView2Async(env);
                
                webView2.CoreWebView2.Settings.IsScriptEnabled = true;
                webView2.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                
                // Add host object for keyboard callback
                webView2.CoreWebView2.AddHostObjectToScript("callback", new KeyboardCallback(this));
                
                // Subscribe to WebMessageReceived for markdown link clicks
                webView2.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                
                webView2.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                
                isWebViewInitialized = true;
                ShowLoading(false);
                TraceLog("WebView2 initialized");
                
                // If there's a pending file to load, load it now
                if (!String.IsNullOrEmpty(pendingFileToLoad))
                {
                    TraceLog("Loading pending file: " + pendingFileToLoad);
                    ParseMarkdownFile(pendingFileToLoad);
                    pendingFileToLoad = null;
                }
            }
            catch (Exception ex)
            {
                TraceLog("WebView2 init error: " + ex.Message);
                ShowLoading(false);
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.WebMessageAsJson;
                TraceLog("WebMessageReceived: " + message);
                
                // Parse message to check if it's a markdown link click
                if (message.Contains("\"markdownLink\""))
                {
                    // Extract path from JSON (simple parsing)
                    int pathStart = message.IndexOf("\"path\":\"") + 8;
                    int pathEnd = message.IndexOf("\"", pathStart);
                    if (pathStart > 7 && pathEnd > pathStart)
                    {
                        string targetPath = message.Substring(pathStart, pathEnd - pathStart);
                        
                        // The path is already resolved by JavaScript, just unescape it
                        targetPath = System.Uri.UnescapeDataString(targetPath);
                        
                        TraceLog("Markdown link clicked: " + targetPath);
                        
                        // Load the file in the current viewer (preview mode)
                        if (File.Exists(targetPath))
                        {
                            ShowLoading(true);
                            FileLoad(targetPath);
                        }
                        else
                        {
                            TraceLog("File not found: " + targetPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TraceLog("Error processing web message: " + ex.Message);
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
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
                    // Vim keys: j=74, k=75, d=68, u=85, f=70, b=66, g=71, h=72, l=76, G=71
                    // Other keys: ESC=27, 1-6=49-54, M=77, O=79, T=84, ?=191
                    var keys = [27, 49, 50, 51, 52, 53, 54, 55, 66, 68, 70, 71, 72, 74, 75, 76, 77, 79, 84, 85, 191];
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

        /// <summary>
        /// 使用 JavaScript 实现搜索功能
        /// </summary>
        public async System.Threading.Tasks.Task SearchTextAsync(
            string searchText, 
            OY.TotalCommander.TcPluginInterface.Lister.SearchParameter searchParameter)
        {
            if (webView2.CoreWebView2 == null || String.IsNullOrEmpty(searchText))
            {
                return;
            }

            try
            {
                TraceLog($"SearchTextAsync: '{searchText}'");

                // 转义搜索文本中的特殊字符
                string escapedSearchText = searchText.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");

                // 构建搜索选项
                bool matchCase = searchParameter.HasFlag(OY.TotalCommander.TcPluginInterface.Lister.SearchParameter.MatchCase);
                bool wholeWords = searchParameter.HasFlag(OY.TotalCommander.TcPluginInterface.Lister.SearchParameter.WholeWords);

                string script = $@"
                    (function() {{
                        try {{
                            var searchText = '{escapedSearchText}';
                            var matchCase = {matchCase.ToString().ToLower()};
                            var wholeWords = {wholeWords.ToString().ToLower()};
                            
                            // 清除之前的高亮
                            document.querySelectorAll('.search-highlight').forEach(function(el) {{
                                var parent = el.parentNode;
                                var textNode = document.createTextNode(el.textContent);
                                parent.replaceChild(textNode, el);
                                parent.normalize();
                            }});
                            
                            if (!searchText) {{
                                console.log('Search: empty text');
                                return 0;
                            }}
                            
                            // 构建正则表达式
                            var regexText = searchText.replace(/[.*+?^${{}}()|[\\]\\\\]/g, '\\\\$&');
                            if (wholeWords) {{
                                regexText = '\\\\b' + regexText + '\\\\b';
                            }}
                            var flags = matchCase ? 'g' : 'gi';
                            var regex = new RegExp(regexText, flags);
                            
                            // 使用 TreeWalker 遍历文本节点
                            var markdownBody = document.getElementById('markdownBody');
                            if (!markdownBody) {{
                                console.log('Search: no markdownBody');
                                return 0;
                            }}
                            
                            var walker = document.createTreeWalker(
                                markdownBody,
                                NodeFilter.SHOW_TEXT,
                                {{
                                    acceptNode: function(node) {{
                                        var parent = node.parentNode;
                                        while (parent) {{
                                            if (parent.tagName === 'SCRIPT' || parent.tagName === 'STYLE') {{
                                                return NodeFilter.FILTER_REJECT;
                                            }}
                                            parent = parent.parentNode;
                                        }}
                                        if (node.nodeValue && node.nodeValue.trim()) {{
                                            return NodeFilter.FILTER_ACCEPT;
                                        }}
                                        return NodeFilter.FILTER_REJECT;
                                    }}
                                }}
                            );
                            
                            var matchCount = 0;
                            var firstMatchElement = null;
                            
                            // 遍历并高亮匹配项
                            while (walker.nextNode()) {{
                                var node = walker.currentNode;
                                var text = node.nodeValue;
                                var matches = [];
                                var match;
                                
                                regex.lastIndex = 0;
                                while ((match = regex.exec(text)) !== null) {{
                                    matches.push({{
                                        index: match.index,
                                        length: match[0].length,
                                        text: match[0]
                                    }});
                                }}
                                
                                if (matches.length === 0) continue;
                                
                                // 从后向前处理，避免索引偏移
                                for (var i = matches.length - 1; i >= 0; i--) {{
                                    var m = matches[i];
                                    var before = node.nodeValue.substring(0, m.index);
                                    var after = node.nodeValue.substring(m.index + m.length);
                                    
                                    var highlight = document.createElement('span');
                                    highlight.className = 'search-highlight';
                                    highlight.textContent = m.text;
                                    
                                    var parent = node.parentNode;
                                    if (after) {{
                                        var afterNode = document.createTextNode(after);
                                        parent.insertBefore(afterNode, node.nextSibling);
                                    }}
                                    parent.insertBefore(highlight, node.nextSibling);
                                    if (!firstMatchElement) firstMatchElement = highlight;
                                    node.nodeValue = before;
                                    matchCount++;
                                }}
                            }}
                            
                            // 滚动到第一个匹配项
                            if (firstMatchElement && matchCount > 0) {{
                                setTimeout(function() {{
                                    firstMatchElement.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
                                }}, 100);
                            }}
                            
                            console.log('Search: found ' + matchCount + ' matches');
                            return matchCount;
                        }} catch (e) {{
                            console.log('Search error:', e);
                            return 0;
                        }}
                    }})();
                ";

                await webView2.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                TraceLog("SearchTextAsync error: " + ex.Message);
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
            
            // If WebView2 is not initialized yet, store the file path and wait
            if (!isWebViewInitialized)
            {
                pendingFileToLoad = fileName;
                TraceLog("WebView2 not ready, pending file: " + fileName);
                return;
            }
            
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
                    currentFileDir = dirPath; // Store for link resolution
                    // Escape backslashes for JavaScript string
                    String escapedDirPath = dirPath.Replace("\\", "\\\\");
                    String html = markdownTmpl.Replace("{0}", escapedDirPath).Replace("{1}", style).Replace("{2}", markdownHTML);

                    // Save to temp file and navigate to it
                    String tempFile = Path.Combine(Path.GetTempPath(), "markdownviewer_" + Path.GetFileName(fileName) + ".html");
                    File.WriteAllText(tempFile, html, Encoding.UTF8);
                    
                    // Cleanup previous temp file
                    if (!String.IsNullOrEmpty(currentTempFile) && File.Exists(currentTempFile))
                    {
                        try { File.Delete(currentTempFile); } catch { }
                    }
                    currentTempFile = tempFile;

                    // Navigate to temp file
                    this.Invoke(new Action(() =>
                    {
                        try
                        {
                            if (webView2.CoreWebView2 != null)
                            {
                                var fileUri = new Uri("file:///" + tempFile.Replace("\\", "/"));
                                webView2.CoreWebView2.Navigate(fileUri.AbsoluteUri);
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

        /// <summary>
        /// Handle key press from JavaScript
        /// </summary>
        public void OnKeyPressed(int keyCode)
        {
            TraceLog("Key pressed: " + keyCode);
            
            // ESC (27) - Close preview (existing behavior)
            if (keyCode == 27)
            {
                this.Invoke(new Action(() => {
                    listerPlugin.CloseWindow(this);
                }));
            }
            // ? (191) - Show shortcut help (handled by JavaScript, just log it)
            else if (keyCode == 191)
            {
                TraceLog("Shortcut help requested");
            }
            // Vim keys (handled by JavaScript, just log them)
            else if (keyCode == 74) // j
            {
                TraceLog("Vim: scroll down (j)");
            }
            else if (keyCode == 75) // k
            {
                TraceLog("Vim: scroll up (k)");
            }
            else if (keyCode == 68) // d
            {
                TraceLog("Vim: scroll down half page (d)");
            }
            else if (keyCode == 85) // u
            {
                TraceLog("Vim: scroll up half page (u)");
            }
            else if (keyCode == 70) // f
            {
                TraceLog("Vim: scroll down full page (f)");
            }
            else if (keyCode == 66) // b
            {
                TraceLog("Vim: scroll up full page (b)");
            }
            else if (keyCode == 71) // g/G
            {
                TraceLog("Vim: scroll to top/bottom (g/G)");
            }
            else if (keyCode == 72) // h
            {
                TraceLog("Vim: scroll left (h)");
            }
            else if (keyCode == 76) // l
            {
                TraceLog("Vim: scroll right (l)");
            }
            // M (77) - Toggle layout (handled by JavaScript, just log it)
            else if (keyCode == 77)
            {
                TraceLog("Layout toggle requested");
            }
            // T (84) - Toggle theme (handled by JavaScript, just log it)
            else if (keyCode == 84)
            {
                TraceLog("Theme toggle requested");
            }
            // O (79) - Toggle outline (handled by JavaScript, just log it)
            else if (keyCode == 79)
            {
                TraceLog("Outline toggle requested");
            }
            // 1-6 (49-54) - Jump to heading (handled by JavaScript, just log it)
            else if (keyCode >= 49 && keyCode <= 54)
            {
                TraceLog("Jump to heading level " + (keyCode - 48));
            }
        }
    }

    /// <summary>
    /// Callback class for JavaScript to call C# methods
    /// </summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    public class KeyboardCallback
    {
        private ViewerControl viewerControl;

        public KeyboardCallback(ViewerControl viewerControl)
        {
            this.viewerControl = viewerControl;
        }

        public void OnKeyPressed(int keyCode)
        {
            viewerControl.OnKeyPressed(keyCode);
        }
    }
}
