using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Markdig;
using OY.TotalCommander.TcPluginInterface.Lister;

namespace MarkdownViewer
{
    public partial class ViewerControl : UserControl
    {

        private Encoding encoding = Encoding.UTF8;
        private const String CONTAINER_HTML = "<!DOCTYPE html>" +
            "<html>" +
            "<head>" +
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
                String html = String.Format(CONTAINER_HTML, markdownHTML, style);
                this.webBrowser1.DocumentText = html;
            }
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            webBrowser1.Document.Body.KeyPress += MyKeyPressHandler;
        }

        private void MyKeyPressHandler(object sender, HtmlElementEventArgs e)
        {
            if (e.KeyPressedCode == 27)
            {
                listerPlugin.SendKeyToParentWindow(27);
            }
        }
    }
}
