using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Markdig;
using OY.TotalCommander.TcPluginInterface.Lister;
using System.Linq;
using System.Reflection;
using Pek.Markdig.HighlightJs;

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
            using (StreamReader sr = new StreamReader(fileName, encoding))
            {
                String markdownContent = sr.ReadToEnd();
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UseHighlightJs()
                    .Build();
                String markdownHTML = Markdown.ToHtml(markdownContent, pipeline);
                             
                // read markdown tmpl from file
                var buildDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var tmplFilePath = buildDir + @"\" + TMPL_FILE_NAME;
                var markdownTmpl = File.ReadAllText(tmplFilePath);

                // read style content from file
                var styleFilePath = buildDir + @"\" + CSS_FILE_NAME;
                var style = File.ReadAllText(styleFilePath);

                String html = String.Format(markdownTmpl, Path.GetDirectoryName(fileName), style, markdownHTML);
                this.webBrowser1.DocumentText = html;
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
    }
}
