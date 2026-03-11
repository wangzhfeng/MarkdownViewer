using System;
using System.Collections.Specialized;
using System.Collections;
using OY.TotalCommander.TcPluginInterface.Lister;
using System.IO;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Threading.Tasks;

namespace MarkdownViewer
{
    public class MarkdownViewer : ListerPlugin
    {

        public const String AllowedExtensions = ".md,.markdown,.mk";

        public MarkdownViewer(StringDictionary pluginSettings) : base(pluginSettings)
        {

            if (String.IsNullOrEmpty(Title))
            {
                Title = "Markdown Viewer";
            }

            DetectString = "EXT=\"MD\" | EXT=\"MARKDOWN\" | EXT=\"MK\"";

        }

        private ArrayList controls = new ArrayList();

        /// <summary>
        /// 输出日志到 TC 插件日志系统
        /// </summary>
        public void Log(string message)
        {
            TraceProc(System.Diagnostics.TraceLevel.Info, message);
        }

        /// <summary>
        /// 载入插件
        /// </summary>
        /// <param name="fileToLoad"></param>
        /// <param name="showFlags"></param>
        /// <returns></returns>
        public override object Load(string fileToLoad, ShowFlags showFlags)
        {
            ViewerControl viewerControl = null;
            if (!String.IsNullOrEmpty(fileToLoad))
            {

                String ext = Path.GetExtension(fileToLoad);
                String fileName = Path.GetFileNameWithoutExtension(fileToLoad);

                TraceProc(System.Diagnostics.TraceLevel.Info, "fileName: " + fileName + ", ext: " + ext);

                // 如果文件扩展名不在支持之列则直接返回
                if (AllowedExtensions.IndexOf(ext, StringComparison.InvariantCultureIgnoreCase) < 0)
                {
                    return null;
                }

                viewerControl = new ViewerControl(this);
                viewerControl.FileLoad(fileToLoad);
                FocusedControl = viewerControl.webView2;
                viewerControl.Focus();

                controls.Add(viewerControl);
             
            }
            
            return viewerControl;
        }

        /// <summary>
        /// 
        /// Is called when a user closes lister, or loads a different file.
        /// </summary>
        /// <param name="control"></param>
        public override void CloseWindow(object control)
        {
            // Fix issue #6/#13: Restore focus to Total Commander after closing viewer
            ViewerControl viewerControl = control as ViewerControl;
            if (viewerControl != null)
            {
                // Dispose the web browser to release focus properly
                viewerControl.webView2?.Dispose();
            }
            
            controls.Remove(control);
        }

        /// <summary>
        /// Is called when click print button
        /// </summary>
        /// <param name="control"></param>
        /// <param name="fileToPrint"></param>
        /// <param name="defPrinter"></param>
        /// <param name="printFlags"></param>
        /// <param name="margins"></param>
        /// <returns></returns>
        public override ListerResult Print(object control, string fileToPrint, string defPrinter, PrintFlags printFlags, PrintMargins margins)
        {
            TraceProc(System.Diagnostics.TraceLevel.Info, "Print fileToPrint:" + fileToPrint + ", defPrinter: " + defPrinter);
            ViewerControl viewerControl = (ViewerControl)control;
            viewerControl.PrintAsync();
            return ListerResult.OK;
        }

        public override ListerResult SendCommand(object control, ListerCommand command, ShowFlags parameter)
        {
            ViewerControl viewerControl = (ViewerControl)control;
            switch (command)
            {
                case ListerCommand.Copy:
                    viewerControl.ExecuteScriptAsync("document.execCommand('copy')");
                    break;
                case ListerCommand.SelectAll:
                    viewerControl.ExecuteScriptAsync("document.execCommand('selectAll')");
                    break;
            }
            return ListerResult.OK;
        }


        public override ListerResult SearchText(object control, string searchString, SearchParameter searchParameter)
        {
            ViewerControl viewerControl = (ViewerControl)control;
            
            return ListerResult.OK;
        }

        public override int NotificationReceived(object control, int message, int wParam, int lParam)
        {
            ViewerControl viewerControl = (ViewerControl)control;
            return 0;
        }

    }

}
