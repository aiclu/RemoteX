using System;
using System.Diagnostics;
using Shawn.Utils;

namespace _1RM.Utils.Tracing
{
    /// <summary>
    /// 性能插桩。统计各阶段耗时并输出到 Debug 日志，用于建立/对比性能基线。
    /// Release 构建下方法体被裁剪为直接执行 action()（等价于无插桩调用），调用点无需条件编译。
    /// </summary>
    internal static class PerfTracer
    {
        public static void Measure(string stage, Action action)
        {
#if DEV
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            SimpleLogHelper.Debug($"[Perf] {stage}: {sw.Elapsed.TotalMilliseconds:F1} ms");
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
            SimpleLogHelper.Debug($"[Perf] {stage}: {sw.Elapsed.TotalMilliseconds:F1} ms");
            return ret;
#else
            return func();
#endif
        }
    }
}
