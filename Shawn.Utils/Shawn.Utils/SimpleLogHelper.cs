using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Shawn.Utils
{
    public static class SimpleLogHelper
    {
        public enum EnumLogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Fatal,
            Disabled,
        }

        public enum EnumLogFileType
        {
            Txt,
            MarkDown,
        }

        public static string LogFileName
        {
            get => _simpleLogHelper.DebugFileName;
            set
            {
                _simpleLogHelper.DebugFileName = value;
                _simpleLogHelper.InfoFileName = value;
                _simpleLogHelper.WarningFileName = value;
                _simpleLogHelper.ErrorFileName = value;
                _simpleLogHelper.FatalFileName = value;
            }
        }

        //public static string DebugFileName
        //{
        //    get => _simpleLogHelper.DebugFileName;
        //    set => _simpleLogHelper.DebugFileName = value;
        //}
        //public static string InfoFileName
        //{
        //    get => _simpleLogHelper.InfoFileName;
        //    set => _simpleLogHelper.InfoFileName = value;
        //}
        //public static string WarningFileName
        //{
        //    get => _simpleLogHelper.WarningFileName;
        //    set => _simpleLogHelper.WarningFileName = value;
        //}
        //public static string ErrorFileName
        //{
        //    get => _simpleLogHelper.ErrorFileName;
        //    set => _simpleLogHelper.ErrorFileName = value;
        //}
        //public static string FatalFileName
        //{
        //    get => _simpleLogHelper.FatalFileName;
        //    set => _simpleLogHelper.FatalFileName = value;
        //}

        public static EnumLogLevel PrintLogLevel
        {
            get => _simpleLogHelper.PrintLogLevel;
            set => _simpleLogHelper.PrintLogLevel = value;
        }

        public static EnumLogLevel WriteLogLevel
        {
            get => _simpleLogHelper.WriteLogLevel;
            set => _simpleLogHelper.WriteLogLevel = value;
        }

        public static long LogFileMaxSizeMb
        {
            get => _simpleLogHelper.LogFileMaxSizeMegabytes;
            set => _simpleLogHelper.LogFileMaxSizeMegabytes = value;
        }

        public static EnumLogFileType LogFileType
        {
            get => _simpleLogHelper.LogFileType;
            set => _simpleLogHelper.LogFileType = value;
        }

        private static SimpleLogHelperObject _simpleLogHelper = new SimpleLogHelperObject();
        public static SimpleLogHelperObject StaticInstance => _simpleLogHelper;

        public static void SetLogger(SimpleLogHelperObject logger)
        {
            _simpleLogHelper = logger;
        }

        public static void Debug(params object[] o)
        {
            _simpleLogHelper.Debug(o);
        }

        public static void DebugInfo(params object[] o)
        {
#if DEBUG
            _simpleLogHelper.Info(o);
#else
            _simpleLogHelper.Debug(o);
#endif
        }
        public static void DebugWarning(params object[] o)
        {
#if DEBUG
            _simpleLogHelper.Warning(o);
#else
            _simpleLogHelper.Debug(o);
#endif
        }
        public static void DebugError(params object[] o)
        {
#if DEBUG
            _simpleLogHelper.Error(o);
#else
            _simpleLogHelper.Debug(o);
#endif
        }

        public static void Info(params object[] o)
        {
            _simpleLogHelper.Info(o);
        }

        public static void Warning(params object[] o)
        {
            _simpleLogHelper.Warning(o);
        }

        public static void Error(params object[] o)
        {
            _simpleLogHelper.Error(o);
        }

        public static void Fatal(params object[] o)
        {
            _simpleLogHelper.Fatal(o);
        }

        public static string GetLog(int lastLineCount = 50)
        {
            // append date
            var withOutExtension = LogFileName.Substring(0, LogFileName.LastIndexOf(".", StringComparison.Ordinal));
            var logFileName = $"{withOutExtension}_{DateTime.Now.ToString("yyyyMMdd")}{new FileInfo(LogFileName).Extension}";
            return _simpleLogHelper.GetLog(logFileName, lastLineCount);
        }

        public static string GetFileFullName()
        {
            return _simpleLogHelper.GetFileFullName(EnumLogLevel.Info);
        }
    }

    public class SimpleLogHelperObject
    {
        public SimpleLogHelperObject(string logFileName = "")
        {
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                DebugFileName = logFileName;
                InfoFileName = logFileName;
                WarningFileName = logFileName;
                ErrorFileName = logFileName;
                FatalFileName = logFileName;
            }
        }

        public SimpleLogHelperObject(
            string debugLogFileName,
            string infoLogFileName,
            string warningLogFileName,
            string errorLogFileName,
            string fatalLogFileName)
        {
            if (!string.IsNullOrWhiteSpace(debugLogFileName))
                DebugFileName = debugLogFileName;
            if (!string.IsNullOrWhiteSpace(infoLogFileName))
                InfoFileName = infoLogFileName;
            if (!string.IsNullOrWhiteSpace(warningLogFileName))
                WarningFileName = warningLogFileName;
            if (!string.IsNullOrWhiteSpace(errorLogFileName))
                ErrorFileName = errorLogFileName;
            if (!string.IsNullOrWhiteSpace(fatalLogFileName))
                FatalFileName = fatalLogFileName;
        }

        public string DebugFileName { get; set; } = "simple.log.md";
        public string InfoFileName { get; set; } = "simple.log.md";
        public string WarningFileName { get; set; } = "simple.log.md";
        public string ErrorFileName { get; set; } = "simple.log.md";
        public string FatalFileName { get; set; } = "simple.log.md";

        /// <summary>
        /// if log file size over this vale, old log file XXXXX.log will be moved to XXXXX.001.log
        /// </summary>
        public long LogFileMaxSizeMegabytes { get; set; } = 10;

        public SimpleLogHelper.EnumLogLevel PrintLogLevel { get; set; } = SimpleLogHelper.EnumLogLevel.Debug;
        public SimpleLogHelper.EnumLogLevel WriteLogLevel { get; set; } = SimpleLogHelper.EnumLogLevel.Info;
        public SimpleLogHelper.EnumLogFileType LogFileType { get; set; } = SimpleLogHelper.EnumLogFileType.MarkDown;

        /// <summary>
        /// del log files created before LogFileMaxHistoryDays if LogFileMaxHistoryDays > 0
        /// </summary>
        public uint LogFileMaxHistoryDays { get; set; } = 30;
        public long LogFileMaxByteSize { get; set; } = 1024 * 1024 * 10; // kb -> mb -> * 10

        private readonly object _obj = new object();

        public void Debug(params object[] o)
        {
            MakeLog(SimpleLogHelper.EnumLogLevel.Debug, o);
        }

        public void Info(params object[] o)
        {
            MakeLog(SimpleLogHelper.EnumLogLevel.Info, o);
        }

        public void Warning(params object[] o)
        {
            MakeLog(SimpleLogHelper.EnumLogLevel.Warning, o);
        }

        public void Error(params object[] o)
        {
            MakeLog(SimpleLogHelper.EnumLogLevel.Error, o);
        }

        public void Fatal(params object[] o)
        {
            MakeLog(SimpleLogHelper.EnumLogLevel.Fatal, o);
        }

        private static void SetConsoleColor(SimpleLogHelper.EnumLogLevel level)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            switch (level)
            {
                case SimpleLogHelper.EnumLogLevel.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    break;
                case SimpleLogHelper.EnumLogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case SimpleLogHelper.EnumLogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case SimpleLogHelper.EnumLogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case SimpleLogHelper.EnumLogLevel.Fatal:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.Red;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        public string GetFileFullName(SimpleLogHelper.EnumLogLevel enumLogLevel)
        {
            var logFileName = enumLogLevel switch
            {
                SimpleLogHelper.EnumLogLevel.Debug => DebugFileName,
                SimpleLogHelper.EnumLogLevel.Info => InfoFileName,
                SimpleLogHelper.EnumLogLevel.Warning => WarningFileName,
                SimpleLogHelper.EnumLogLevel.Error => ErrorFileName,
                SimpleLogHelper.EnumLogLevel.Fatal => FatalFileName,
                _ => throw new ArgumentOutOfRangeException(nameof(enumLogLevel), enumLogLevel, null)
            };


            // append date
            var withOutExtension = logFileName.Substring(0, logFileName.LastIndexOf(".", StringComparison.Ordinal));
            logFileName = $"{withOutExtension}_{DateTime.Now.ToString("yyyyMMdd")}{new FileInfo(logFileName).Extension}";

            return logFileName;
        }

        private DateTime _lastCleanupTime = DateTime.MinValue;
        private void CleanUpLogFiles(FileInfo fi)
        {
            // 最近X分钟内清理过日志，则跳过
            if ((DateTime.Now - _lastCleanupTime).TotalMinutes < 15)
                return;
            _lastCleanupTime = DateTime.Now;

            // clean history
            if (LogFileMaxHistoryDays <= 0) return;
            var di = fi.Directory!;
            var fis = di.GetFiles($"*{fi.Extension}").Where(f => f.CreationTime < DateTime.Today).ToArray();
            foreach (var fileInfo in fis)
            {
                if ((DateTime.Now - fileInfo.CreationTime).Days > LogFileMaxHistoryDays)
                {
                    fileInfo.Delete();
                    continue;
                }

                var dateStr = fileInfo.Name.Replace(fileInfo.Extension, "");
                // ReSharper disable once StringLastIndexOfIsCultureSpecific.1
                dateStr = dateStr.Substring(dateStr.LastIndexOf("_") + 1);
                if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, DateTimeStyles.None, out var date)
                         && date < DateTime.Now.AddDays(-1 * LogFileMaxHistoryDays))
                {
                    fileInfo.Delete();
                }
            }

            // clean over size
            fis = di.GetFiles($"*{fi.Extension}").Where(f => f.CreationTime < DateTime.Today).OrderByDescending(f => f.CreationTime).ToArray();
            long byteLength = 0;
            foreach (var fileInfo in fis)
            {
                byteLength += fileInfo.Length;
                if (byteLength > LogFileMaxByteSize)
                {
                    fileInfo.Delete();
                }
            }
        }

        private void MoveIfLogOverSize(string logFilePath)
        {
            if (!File.Exists(logFilePath)) return;
            var fi = new FileInfo(logFilePath);
            long maxLength = 1024 * 1024 * LogFileMaxSizeMegabytes;
            // over size then move to xxxx.md -> xxxx.001.md
            if (fi.Length <= maxLength) return;
            int i = 1;
            var d = Math.Max(i.ToString().Length, 3);
            string newName = "";
            while (true)
            {
                var withOutExtension = logFilePath.Substring(0, logFilePath.LastIndexOf(".", StringComparison.Ordinal));
                newName = $"{withOutExtension}_{i.ToString($"D{d}")}{fi.Extension}";

                if (!File.Exists($"{newName}"))
                {
                    break;
                }
                ++i;
            }
            File.Move(logFilePath, newName);
        }

        private string GetLogLevelString(SimpleLogHelper.EnumLogLevel enumLogLevel, SimpleLogHelper.EnumLogFileType type)
        {
            string levelString = enumLogLevel.ToString();
            if (type == SimpleLogHelper.EnumLogFileType.MarkDown)
                levelString = $"`{levelString}`";
            //if (type == SimpleLogHelper.EnumLogFileType.MarkDown)
            //{
            //    levelString = enumLogLevel switch
            //    {
            //        SimpleLogHelper.EnumLogLevel.Debug => $"<font color=Green>{enumLogLevel}</font>",
            //        SimpleLogHelper.EnumLogLevel.Info => $"<font color=Blue>{enumLogLevel}</font>",
            //        SimpleLogHelper.EnumLogLevel.Warning => $"<font color=Yellow>{enumLogLevel}</font>",
            //        SimpleLogHelper.EnumLogLevel.Error => $"*<font color=Red>{enumLogLevel}</font>*",
            //        SimpleLogHelper.EnumLogLevel.Fatal => $"<u>**<font color=Red>{enumLogLevel}</font>**</u>",
            //        _ => throw new ArgumentOutOfRangeException(nameof(enumLogLevel), enumLogLevel, null)
            //    };
            //}
            return levelString;
        }


        private void MakeLog(SimpleLogHelper.EnumLogLevel enumLogLevel, params object[] o)
        {
            var dt = DateTime.Now;
            var tid = Thread.CurrentThread.ManagedThreadId;
            // skipFrames 表示要忽略的帧数
            // true 表示需要文件信息
            string fileName = "";
            string func = "";
            int line = 0;
            var stack = new StackTrace(3, true);
            if (stack.FrameCount > 0)
            {
                var frame = stack.GetFrame(0);
                fileName = frame!.GetFileName() ?? "";
                //fileName = new FileInfo(fileName).Name;
                if (fileName.Contains("/"))
                    fileName = fileName.Substring(fileName.LastIndexOf("/", StringComparison.Ordinal) + 1);
                if (fileName.Contains("\\"))
                    fileName = fileName.Substring(fileName.LastIndexOf("\\") + 1);
                line = frame.GetFileLineNumber();
                func = frame.GetMethod()?.Name ?? "";
            }

            Print(enumLogLevel, fileName, func, line, tid, dt, o);
            WriteLog(enumLogLevel, fileName, func, line, tid, dt, o);
        }
        private void Print(SimpleLogHelper.EnumLogLevel enumLogLevel, string fileName, string method, int line, int threadId, DateTime? dt = null, params object[] o)
        {
            if (enumLogLevel < PrintLogLevel) return;

            dt ??= DateTime.Now;
            SetConsoleColor(enumLogLevel);
            Console.Write($"[T:{threadId:D3}][{dt:HH:mm:ss.fff}]\t");
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.Write($"{enumLogLevel}");
            SetConsoleColor(enumLogLevel);
            Console.Write($"\t");

            if (enumLogLevel >= SimpleLogHelper.EnumLogLevel.Warning)
            {
                Console.Write($"[{fileName}({method}:{line})]\t");
            }

            foreach (var obj in o)
            {
                Console.WriteLine(obj);
                if (o[0] is Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                    if (e.InnerException != null)
                    {
                        Console.WriteLine(e.InnerException.Message);
                        Console.WriteLine(e.InnerException.StackTrace);
                    }
                }
            }
            Console.ResetColor();
        }
        private void WriteLog(SimpleLogHelper.EnumLogLevel enumLogLevel, string fileName, string method, int line, int threadId, DateTime? dt = null, params object[] o)
        {
            if (enumLogLevel < WriteLogLevel) return;

            try
            {
                dt ??= DateTime.Now;
                lock (_obj)
                {
                    string logFileName = GetFileFullName(enumLogLevel);

                    var fi = new FileInfo(logFileName);
                    if (fi?.Directory?.Exists == false)
                        fi.Directory.Create();

                    // clean history
                    CleanUpLogFiles(fi!);

                    // over size then move to xxxx.md -> xxxx.001.md
                    MoveIfLogOverSize(logFileName);

                    string levelString = GetLogLevelString(enumLogLevel, LogFileType);
                    string prefix = "";
                    if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                        prefix = "> ";

                    using var sw = new StreamWriter(new FileStream(logFileName, FileMode.Append), Encoding.UTF8);

                    sw.Write($"[T:{threadId:D3}][{dt:HH:mm:ss.fff}]\t{levelString}\t\t");
                    if (enumLogLevel >= SimpleLogHelper.EnumLogLevel.Warning)
                    {
                        sw.Write($"[{fileName}({method}:{line})]\t");
                    }

                    if (o.Length == 1 && o[0] is string msg)
                    {
                        sw.WriteLine(msg);
                    }
                    else
                    {
                        sw.WriteLine();
                        foreach (var obj in o)
                        {
                            sw.Write(prefix);
                            sw.WriteLine(obj);
                            if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                                sw.WriteLine();
                            if (o[0] is Exception e)
                            {
                                sw.WriteLine(prefix + "StackTrace: " + e.StackTrace);
                                if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                                    sw.WriteLine();
                                if (e.InnerException != null)
                                {
                                    sw.WriteLine(prefix + "InnerException: " + e.InnerException.Message);
                                    if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                                        sw.WriteLine();
                                    sw.WriteLine(prefix + "Inner StackTrace: " + e.InnerException.StackTrace);
                                    if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                                        sw.WriteLine();
                                }
                            }
                        }
                    }

                    if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                        sw.WriteLine();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public string GetLog(string logFilePath, int lastLineCount = 50)
        {
            if (!File.Exists(logFilePath))
                return "";

            var lines = File.ReadAllLines(logFilePath, Encoding.UTF8);
            var logs = new List<string>();
            for (int i = lines.Length - 1; i >= 0 && lastLineCount > 0; i--)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;

                logs.Add(lines[i]);
                if (lines[i].IndexOf("[ThreadId:", StringComparison.Ordinal) > 0)
                {
                    --lastLineCount;
                }
            }
            var ret = new StringBuilder();
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                ret.AppendLine(logs[i]);
            }
            return ret.ToString();
        }
    }
}