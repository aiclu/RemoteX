using System;
using System.Diagnostics;
using System.IO;
using Shawn.Utils;

namespace _1RM.Utils.Tracing
{
    /// <summary>
    /// 性能插桩。统计各阶段耗时并输出到 Debug 日志，用于建立/对比性能基线。
    /// DEV 构建下额外追加写入独立文件 {BaseDir}/.logs/RemoteX.perf.md，便于实测后离线分析。
    /// Release 构建下方法体被裁剪为直接执行 action()（等价于无插桩调用），调用点无需条件编译。
    /// </summary>
    internal static class PerfTracer
    {
#if DEV
        private static readonly object FileLock = new object();
        private static string? _perfFilePath;
        private static string PerfFilePath
        {
            get
            {
                if (_perfFilePath != null) return _perfFilePath;
                try
                {
                    var dir = AppDomain.CurrentDomain.BaseDirectory;
                    _perfFilePath = Path.Combine(dir, ".logs", "RemoteX.perf.md");
                }
                catch
                {
                    _perfFilePath = Path.Combine(Path.GetTempPath(), "RemoteX.perf.md");
                }
                return _perfFilePath;
            }
        }

        private static void Log(string message)
        {
            SimpleLogHelper.Debug(message);
            try
            {
                lock (FileLock)
                {
                    var path = PerfFilePath;
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // perf logging must never break the app
            }
        }
#endif

        public static void Measure(string stage, Action action)
        {
#if DEV
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            Log($"[Perf] {stage}: {sw.Elapsed.TotalMilliseconds:F1} ms");
#else
            action();
#endif
        }

        public static T Measure<T>(string stage, Func<T> func)
        {
#if DEV
            var sw = Stopwatch.StartNew();
            var ret = func();
            sw.Stop();
            Log($"[Perf] {stage}: {sw.Elapsed.TotalMilliseconds:F1} ms");
            return ret;
#else
            return func();
#endif
        }
    }
}
