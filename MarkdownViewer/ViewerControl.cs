using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Markdig;

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


        public ViewerControl()
        {
            InitializeComponent();
        }

       
        public void FileLoad(String fileName)
        {
            using (StreamReader sr = new StreamReader(fileName, encoding))
            {
                String markdownContent = sr.ReadToEnd();
                var pipline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                String markdownHTML = Markdown.ToHtml(markdownContent, pipline);
                String style = Properties.Resources.github_markdown2;
                String html = String.Format(CONTAINER_HTML, markdownHTML, style);
                this.webBrowser1.DocumentText = html;
            }
        }
    }
}
