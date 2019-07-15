using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Markdig;
using OY.TotalCommander.TcPluginInterface.Lister;
using System.Linq;

namespace MarkdownViewer
{
    public partial class ViewerControl : UserControl
    {
        private Encoding encoding = Encoding.UTF8;
        private const String CONTAINER_HTML = "<!DOCTYPE html>" +
            "<html>" +
            "<head>" +
            "  <base href=\"file://localhost/{2}/\" />" +
            "  <meta charset=\"utf-8\">" +
            "  <meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />" +
            "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, user-scalable=yes\">" +
            "  <style type=\"text/css\">code{{white-space: pre;}}</style>" +
            "  <style type=\"text/css\">" +
            "   {1}" +
            "  </style>" +
            "</head>" +
            "<body>" +
            "<article class=\"markdown-body\">" +
            "{0}" +
            "</article>" +
            "</body>" +
            "</html>";

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
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                String markdownHTML = Markdown.ToHtml(markdownContent, pipeline);
                String style = Properties.Resources.github_markdown2;
                // fixed cannot load local file problem
                String html = String.Format(CONTAINER_HTML, markdownHTML, style, Path.GetDirectoryName(fileName));
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
