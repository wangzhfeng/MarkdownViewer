using System;
using System.IO;
using System.Text;

namespace MarkdownViewer
{
    /// <summary>
    /// 文件级调试日志：写入 %TEMP%\MarkdownViewer_debug.log
    ///
    /// 用途：在新机器上排查"插件装了但无法预览"的问题。
    /// TraceProc 依赖 TC 用户开启"插件日志"，新机器上基本看不到输出；
    /// 文件日志不依赖任何配置，装完插件复现一次即可拿日志分析。
    ///
    /// 关键检查点（按历史故障概率排序）：
    ///   1. WebView2 Runtime 是否安装（GetAvailableBrowserVersionString）
    ///   2. .NET Framework 4.8 是否可用
    ///   3. 模板/CSS/assets 是否随插件部署到位
    ///   4. Load/Parse 各步骤异常
    /// </summary>
    public static class DebugLog
    {
        private static readonly object lockObj = new object();
        private static string logPath;

        public static string LogPath
        {
            get
            {
                if (logPath == null)
                {
                    try
                    {
                        logPath = Path.Combine(Path.GetTempPath(), "MarkdownViewer_debug.log");
                    }
                    catch
                    {
                        logPath = "MarkdownViewer_debug.log"; // 兜底：当前目录
                    }
                }
                return logPath;
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (lockObj)
                {
                    File.AppendAllText(LogPath,
                        string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] [pid={1}] {2}\r\n",
                            DateTime.Now, System.Diagnostics.Process.GetCurrentProcess().Id, message),
                        Encoding.UTF8);
                }
            }
            catch
            {
                // 日志失败绝不能影响插件运行
            }
        }

        public static void Write(string format, params object[] args)
        {
            try { Write(string.Format(format, args)); } catch { }
        }

        public static void Exception(string context, Exception ex)
        {
            try
            {
                Write("EXCEPTION [{0}]: {1}\r\n    Stack: {2}",
                    context, ex.Message,
                    ex.StackTrace != null ? ex.StackTrace.Replace("\r\n", "\r\n    ") : "(null)");
                if (ex.InnerException != null)
                {
                    Write("  InnerException: {0}: {1}", ex.InnerException.GetType().FullName, ex.InnerException.Message);
                }
            }
            catch { }
        }

        /// <summary>
        /// 插件初始化时的环境自检：一次性把影响预览的所有环境因素写进日志。
        /// </summary>
        public static void LogEnvironment(string buildDir)
        {
            try
            {
                Write("========== MarkdownViewer 环境自检 ==========");
                Write("OS: {0} {1}-bit, CLR: {2}",
                    Environment.OSVersion.VersionString,
                    Environment.Is64BitOperatingSystem ? "64" : "32",
                    Environment.Version);
                Write("Is64BitProcess: {0}", Environment.Is64BitProcess);
                Write("Assembly location: {0}", System.Reflection.Assembly.GetExecutingAssembly().Location);
                Write("Build dir: {0}", buildDir);

                // .NET Framework 4.8 检测（Release >= 528040 即 4.8）
                try
                {
                    using (var key = Microsoft.Win32.RegistryKey.OpenBaseKey(
                        Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32)
                        .OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                    {
                        if (key != null)
                        {
                            var release = key.GetValue("Release");
                            Write(".NET Framework 4.x Release: {0} (4.8 需要 >= 528040)", release);
                        }
                        else
                        {
                            Write(".NET Framework 4.x: 注册表项不存在（未安装 4.x 完整版！）");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Write(".NET Framework 版本检测失败: {0}", ex.Message);
                }

                // WebView2 Runtime 检测 —— 新机器最常见的故障点
                try
                {
                    string ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
                    Write("WebView2 Runtime: 已安装, 版本 {0} (插件需要 >= 1.0.3856.49)", ver);
                }
                catch (Exception ex)
                {
                    Write("WebView2 Runtime: 未找到或不可用！({0})", ex.Message);
                    Write("  -> 这是无法预览的最可能原因。请安装 Evergreen WebView2 Runtime:");
                    Write("  -> https://developer.microsoft.com/microsoft-edge/webview2/");
                }

                // 模板 / CSS / assets 部署检查
                CheckFile(buildDir, "markdown_tmpl.txt");
                CheckFile(buildDir, "markdown_css.txt");
                string assetsDir = Path.Combine(buildDir, "assets");
                if (Directory.Exists(assetsDir))
                {
                    int count = Directory.GetFiles(assetsDir, "*.*", SearchOption.AllDirectories).Length;
                    Write("assets 目录: 存在, {0} 个文件 ({1})", count, assetsDir);
                }
                else
                {
                    Write("assets 目录: 不存在！({0}) —— KaTeX/Mermaid/highlight.js 将无法加载", assetsDir);
                }

                Write("=============================================");
            }
            catch (Exception ex)
            {
                Write("LogEnvironment 异常: {0}", ex.Message);
            }
        }

        private static void CheckFile(string buildDir, string fileName)
        {
            string path = Path.Combine(buildDir, fileName);
            if (File.Exists(path))
            {
                Write("{0}: 存在 ({1}, {2} bytes)", fileName, path, new FileInfo(path).Length);
            }
            else
            {
                Write("{0}: 不存在！({1}) —— 将导致 Parse 阶段异常", fileName, path);
            }
        }
    }
}
