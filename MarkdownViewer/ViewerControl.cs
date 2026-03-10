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
        }

       
        public void FileLoad(String fileName)
        {

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

                    Action act = delegate () { 
                        try 
                        {
                            this.webBrowser1.DocumentText = html; 
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine("Error setting DocumentText: " + ex.Message);
                        }
                    };
                    // fixed 在创建窗口句柄之前，不能在控件上调用 Invoke 或 BeginInvoke
                    while (!this.IsHandleCreated)
                    {
                        ;
                    }
                    this.Invoke(act);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("ParseMarkdownFile error: " + ex.ToString());
            }
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            webBrowser1.Document.Body.KeyPress += MyKeyPressHandler;
        }

        private void MyKeyPressHandler(object sender, HtmlElementEventArgs e)
        {
            int[] keys = new int[] { 27, 49, 50, 51, 52, 53, 54, 55 }; // Esc, 1-7
            if (keys.Contains(e.KeyPressedCode))
            {
                listerPlugin.SendKeyToParentWindow(e.KeyPressedCode);
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
