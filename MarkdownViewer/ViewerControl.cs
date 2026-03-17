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
        private CoreWebView2Environment webView2Environment = null;

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
                webView2Environment = await CoreWebView2Environment.CreateAsync(null, null, options);
                await webView2.EnsureCoreWebView2Async(webView2Environment);
                
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
                // Parse message to check if it's an export PDF request
                else if (message.Contains("\"exportPdf\""))
                {
                    TraceLog("Export PDF requested");
                    
                    // Generate default output path (same directory as markdown file, .pdf extension)
                    string outputPath = GenerateDefaultPdfPath();
                    
                    // Execute PDF export on UI thread (WebView2 must be accessed from UI thread)
                    this.Invoke(new Action(async () => await ExportPdfAsync(outputPath)));
                }
                // Parse message to check if it's an external link request
                else if (message.Contains("\"externalLink\""))
                {
                    TraceLog("External link requested");
                    
                    // Extract URL from JSON
                    int urlStart = message.IndexOf("\"url\":\"") + 7;
                    int urlEnd = message.IndexOf("\"", urlStart);
                    if (urlStart > 6 && urlEnd > urlStart)
                    {
                        string url = message.Substring(urlStart, urlEnd - urlStart);
                        url = System.Uri.UnescapeDataString(url);
                        
                        TraceLog($"Opening external link: {url}");
                        
                        // Open in default browser
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            TraceLog($"Failed to open external link: {ex.Message}");
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
                    // Vim keys: j=74, k=75, d=68, u=85, f=70, b=66, g=71, h=72, l=76
                    // Function keys: F3=114 (find next), F4=115 (find prev)
                    // Other keys: ESC=27, 1-6=49-54, M=77, O=79, T=84, ?=191
                    var keys = [27, 49, 50, 51, 52, 53, 54, 55, 66, 68, 70, 71, 72, 74, 75, 76, 77, 79, 84, 85, 114, 115, 191];
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
        /// 导出当前文档为 PDF 文件
        /// </summary>
        public async System.Threading.Tasks.Task ExportPdfAsync(string outputPath)
        {
            if (webView2.CoreWebView2 == null || String.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                TraceLog($"ExportPdfAsync: {outputPath}");

                // 1. 创建打印设置
                var printSettings = webView2Environment.CreatePrintSettings();
                printSettings.Orientation = CoreWebView2PrintOrientation.Portrait;  // 纵向
                printSettings.ScaleFactor = 1.0;  // 100% 缩放
                printSettings.MarginTop = 0.4;  // 上边距 (英寸)
                printSettings.MarginBottom = 0.4;  // 下边距
                printSettings.MarginLeft = 0.4;  // 左边距
                printSettings.MarginRight = 0.4;  // 右边距
                printSettings.ShouldPrintBackgrounds = true;  // 打印背景色和图片
                printSettings.ShouldPrintHeaderAndFooter = false;  // 不打印页眉页脚
                printSettings.ShouldPrintSelectionOnly = false;  // 打印全部内容

                // 2. 导出 PDF
                var success = await webView2.CoreWebView2.PrintToPdfAsync(outputPath, printSettings);

                if (success)
                {
                    TraceLog($"PDF export success: {outputPath}");
                }
                else
                {
                    TraceLog($"PDF export failed");
                }
            }
            catch (Exception ex)
            {
                TraceLog("ExportPdfAsync error: " + ex.Message);
            }
        }

        // 搜索相关字段
        private string lastSearchText = null;
        private CoreWebView2FindOptions findOptions = null;
        private CoreWebView2Find finder = null;

        /// <summary>
        /// 使用 WebView2 原生 Find API 搜索文本 (WinRT 1.0.3856.49)
        /// </summary>
        public async System.Threading.Tasks.Task SearchTextInWebView2Async(
            string searchText, 
            OY.TotalCommander.TcPluginInterface.Lister.SearchParameter searchParameter)
        {
            if (webView2.CoreWebView2 == null || String.IsNullOrEmpty(searchText))
            {
                return;
            }

            try
            {
                TraceLog($"SearchTextInWebView2Async: '{searchText}'");

                // 1. 获取 Find 对象
                finder = webView2.CoreWebView2.Find;

                // 2. 使用 Environment.CreateFindOptions() 创建选项对象 (不能用 new)
                findOptions = webView2Environment.CreateFindOptions();
                findOptions.FindTerm = searchText;
                findOptions.IsCaseSensitive = searchParameter.HasFlag(OY.TotalCommander.TcPluginInterface.Lister.SearchParameter.MatchCase);
                findOptions.ShouldMatchWord = searchParameter.HasFlag(OY.TotalCommander.TcPluginInterface.Lister.SearchParameter.WholeWords);
                findOptions.ShouldHighlightAllMatches = true;
                findOptions.SuppressDefaultFindDialog = true;  // 隐藏默认查找 UI

                lastSearchText = searchText;

                // 3. 启动查找会话 (不显示默认查找栏)
                await finder.StartAsync(findOptions);

                // 4. 等待 MatchCountChanged 事件获取结果
                int matchCount = finder.MatchCount;
                TraceLog($"Search result: {matchCount} matches");

                // 5. 滚动到第一个匹配项
                if (matchCount > 0)
                {
                    await ScrollToFirstMatch();
                }
            }
            catch (Exception ex)
            {
                TraceLog("SearchTextInWebView2Async error: " + ex.Message);
            }
        }

        /// <summary>
        /// 查找下一个匹配项 (F3)
        /// </summary>
        public async System.Threading.Tasks.Task FindNextAsync()
        {
            if (webView2.CoreWebView2 == null || String.IsNullOrEmpty(lastSearchText))
            {
                return;
            }

            try
            {
                TraceLog("FindNextAsync");

                // 确保查找会话已启动
                if (finder == null)
                {
                    finder = webView2.CoreWebView2.Find;
                    findOptions = webView2Environment.CreateFindOptions();
                    findOptions.FindTerm = lastSearchText;
                    findOptions.IsCaseSensitive = false;
                    findOptions.ShouldMatchWord = false;
                    findOptions.ShouldHighlightAllMatches = true;
                    findOptions.SuppressDefaultFindDialog = true;
                    await finder.StartAsync(findOptions);
                }

                // 导航到下一个匹配
                finder.FindNext();

                TraceLog($"FindNext: ActiveMatchIndex={finder.ActiveMatchIndex}, MatchCount={finder.MatchCount}");
            }
            catch (Exception ex)
            {
                TraceLog("FindNextAsync error: " + ex.Message);
            }
        }

        /// <summary>
        /// 查找上一个匹配项 (F4)
        /// </summary>
        public async System.Threading.Tasks.Task FindPreviousAsync()
        {
            if (webView2.CoreWebView2 == null || String.IsNullOrEmpty(lastSearchText))
            {
                return;
            }

            try
            {
                TraceLog("FindPreviousAsync");

                // 确保查找会话已启动
                if (finder == null)
                {
                    finder = webView2.CoreWebView2.Find;
                    findOptions = webView2Environment.CreateFindOptions();
                    findOptions.FindTerm = lastSearchText;
                    findOptions.IsCaseSensitive = false;
                    findOptions.ShouldMatchWord = false;
                    findOptions.ShouldHighlightAllMatches = true;
                    findOptions.SuppressDefaultFindDialog = true;
                    await finder.StartAsync(findOptions);
                }

                // 导航到上一个匹配
                finder.FindPrevious();

                TraceLog($"FindPrevious: ActiveMatchIndex={finder.ActiveMatchIndex}, MatchCount={finder.MatchCount}");
            }
            catch (Exception ex)
            {
                TraceLog("FindPreviousAsync error: " + ex.Message);
            }
        }

        /// <summary>
        /// 滚动到第一个匹配项
        /// </summary>
        private async System.Threading.Tasks.Task ScrollToFirstMatch()
        {
            try
            {
                // 使用 JavaScript 查找并滚动到第一个高亮匹配项
                string scrollScript = @"
                    (function() {
                        // 查找 WebView2 原生查找高亮的元素 (mark 标签或带背景的 span)
                        var marked = document.querySelector('mark') || 
                                     document.querySelector('span[style*=""background-color""]') ||
                                     document.querySelector('.webview2-find-highlight');
                        
                        if (marked) {
                            marked.scrollIntoView({ behavior: 'smooth', block: 'center' });
                            console.log('Scrolled to first match');
                        } else {
                            // 如果没有找到高亮，滚动到顶部
                            window.scrollTo({ top: 0, behavior: 'smooth' });
                        }
                    })();
                ";

                await webView2.CoreWebView2.ExecuteScriptAsync(scrollScript);
                TraceLog("Scrolled to first match");
            }
            catch (Exception ex)
            {
                TraceLog("ScrollToFirstMatch error: " + ex.Message);
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

        /// <summary>
        /// 生成默认 PDF 输出路径 (与 Markdown 文件同目录，同名 .pdf 扩展名)
        /// </summary>
        private string GenerateDefaultPdfPath()
        {
            if (String.IsNullOrEmpty(currentTempFile))
            {
                TraceLog("GenerateDefaultPdfPath failed: currentTempFile is null or empty");
                return null;
            }

            try
            {
                // Temp file is like: C:\Users\xxx\AppData\Local\Temp\markdownviewer_xxx.html
                // We want to save PDF to the same directory as the original markdown file
                var tempFileName = Path.GetFileNameWithoutExtension(currentTempFile);
                // Remove "markdownviewer_" prefix
                var originalName = tempFileName.Replace("markdownviewer_", "");
                
                TraceLog($"GenerateDefaultPdfPath: tempFile={currentTempFile}, originalName={originalName}, currentFileDir={currentFileDir}");
                
                // Get the directory of the current markdown file
                if (!String.IsNullOrEmpty(currentFileDir))
                {
                    var outputPath = Path.Combine(currentFileDir, originalName + ".pdf");
                    TraceLog($"GenerateDefaultPdfPath success: {outputPath}");
                    return outputPath;
                }
                
                // Fallback to temp directory
                var fallbackPath = Path.Combine(Path.GetTempPath(), originalName + ".pdf");
                TraceLog($"GenerateDefaultPdfPath fallback: {fallbackPath}");
                return fallbackPath;
            }
            catch (Exception ex)
            {
                TraceLog($"GenerateDefaultPdfPath error: {ex.Message}");
                return null;
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
            // F3 (114) - Find next
            else if (keyCode == 114)
            {
                TraceLog("Find next (F3)");
                Task.Run(async () => await FindNextAsync());
            }
            // Shift+F3 - Find previous (we use F4=115 as alternative since Shift+F3 is hard to detect)
            else if (keyCode == 115)
            {
                TraceLog("Find previous (F4)");
                Task.Run(async () => await FindPreviousAsync());
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
            // P (80) - Export PDF
            else if (keyCode == 80)
            {
                TraceLog("Export PDF requested");
                string outputPath = GenerateDefaultPdfPath();
                if (!String.IsNullOrEmpty(outputPath))
                {
                    // Execute on UI thread (WebView2 must be accessed from UI thread)
                    this.Invoke(new Action(async () => await ExportPdfAsync(outputPath)));
                }
                else
                {
                    TraceLog("Export PDF failed: could not generate output path");
                }
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
