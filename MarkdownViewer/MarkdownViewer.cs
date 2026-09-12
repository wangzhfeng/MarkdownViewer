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

            // [调试] 插件被 TC 加载时做一次环境自检，结果写入 %TEMP%\MarkdownViewer_debug.log
            DebugLog.Write("========== MarkdownViewer 插件实例化 ==========");
            try
            {
                if (pluginSettings != null)
                {
                    var settingList = new System.Collections.Generic.List<string>();
                    foreach (string key in pluginSettings.Keys)
                    {
                        settingList.Add(key + "=" + pluginSettings[key]);
                    }
                    DebugLog.Write("Plugin settings: " + (settingList.Count > 0 ? string.Join(";", settingList.ToArray()) : "(空)"));
                }
                else
                {
                    DebugLog.Write("Plugin settings: (null)");
                }

                string buildDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                DebugLog.LogEnvironment(buildDir);
            }
            catch (Exception ex)
            {
                DebugLog.Exception("LogEnvironment", ex);
            }
        }

        private ArrayList controls = new ArrayList();

        /// <summary>
        /// 输出日志到 TC 插件日志系统
        /// </summary>
        public void Log(string message)
        {
            // [调试] 双写：TC 插件日志 + 文件日志
            TraceProc(System.Diagnostics.TraceLevel.Info, message);
            DebugLog.Write("[ViewerControl] " + message);
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
            DebugLog.Write("Load() 进入: fileToLoad=\"{0}\", showFlags={1}", fileToLoad, showFlags);
            if (!String.IsNullOrEmpty(fileToLoad))
            {

                String ext = Path.GetExtension(fileToLoad);
                String fileName = Path.GetFileNameWithoutExtension(fileToLoad);

                TraceProc(System.Diagnostics.TraceLevel.Info, "fileName: " + fileName + ", ext: " + ext);
                DebugLog.Write("Load() 扩展名检查: ext=\"{0}\", 支持列表=\"{1}\"", ext, AllowedExtensions);

                // 如果文件扩展名不在支持之列则直接返回
                if (AllowedExtensions.IndexOf(ext, StringComparison.InvariantCultureIgnoreCase) < 0)
                {
                    DebugLog.Write("Load() 扩展名不支持，返回 null（如果 TC 调用了本插件，说明 DetectString/关联配置有问题）");
                    return null;
                }

                try
                {
                    viewerControl = new ViewerControl(this);
                    viewerControl.FileLoad(fileToLoad);
                    FocusedControl = viewerControl.webView2;
                    viewerControl.Focus();

                    controls.Add(viewerControl);
                    DebugLog.Write("Load() 完成，返回 ViewerControl");
                }
                catch (Exception ex)
                {
                    // [调试] Load 阶段抛异常会被 TC 静默吞掉，表现为"无法预览"——必须记录
                    DebugLog.Exception("Load() 创建 ViewerControl 或 FileLoad", ex);
                    TraceProc(System.Diagnostics.TraceLevel.Error, "Load error: " + ex.Message);
                    viewerControl = null;
                }
            }
            else
            {
                DebugLog.Write("Load() fileToLoad 为空");
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
                try
                {
                    // Dispose the web browser to release focus properly
                    // Wrap in try-catch to handle race condition where control may already be disposed
                    viewerControl.webView2?.Dispose();
                }
                catch (Exception ex)
                {
                    // Ignore disposal errors - control may already be disposed
                    TraceProc(System.Diagnostics.TraceLevel.Warning, "CloseWindow disposal error: " + ex.Message);
                }
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
                // Note: ListerCommand.FindNext/FindPrev are not supported by TC Lister SDK
                // F3/Shift+F3 handling is done via JavaScript keyboard listener in ViewerControl
            }
            return ListerResult.OK;
        }


        public override ListerResult SearchText(object control, string searchString, SearchParameter searchParameter)
        {
            ViewerControl viewerControl = (ViewerControl)control;
            
            if (!String.IsNullOrEmpty(searchString))
            {
                // 异步执行搜索，不阻塞 TC UI
                Task.Run(async () => 
                {
                    await viewerControl.SearchTextInWebView2Async(searchString, searchParameter);
                });
            }
            
            return ListerResult.OK;
        }

        public override int NotificationReceived(object control, int message, int wParam, int lParam)
        {
            ViewerControl viewerControl = (ViewerControl)control;
            return 0;
        }

    }

}
