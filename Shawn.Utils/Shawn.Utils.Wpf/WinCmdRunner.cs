using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;

namespace Shawn.Utils.Wpf
{
    public static class WinCmdRunner
    {
        /// <summary>
        /// cmd by cmd.exe
        /// </summary>
        /// <returns>[0] = output info，[1] = ret code</returns>
        public static string[] RunCmdSync(string cmd, bool isHideWindow = false)
        {
            var pro = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WindowStyle = isHideWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                    UseShellExecute = isHideWindow == false,
                    CreateNoWindow = isHideWindow,
                }
            };
            //pro.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            pro.Start();
            pro.StandardInput.WriteLine(cmd);
            pro.StandardInput.WriteLine("---------------split_line---------------");// add a symble for exit code
            pro.StandardInput.WriteLine("exit");// add a symble for exit code
            pro.StandardInput.AutoFlush = true;
            var output = pro.StandardOutput.ReadToEnd();
            pro.WaitForExit();
            pro.Close();

            var content = output.Substring(output.IndexOf(cmd + "\r\n") + (cmd + "\r\n").Length);
            content = content.Substring(0, content.IndexOf("---------------split_line---------------\r\n"));
            content = content.Substring(0, content.LastIndexOf("\r\n")).Trim(new[] { '\r', '\n', ' ' });

            var retCode = output.Substring(output.LastIndexOf("---------------split_line---------------\r\n") + "---------------split_line---------------\r\n".Length);
            retCode = retCode.Substring(0, retCode.IndexOf("\r\n")).Trim(new[] { '\r', '\n', ' ' });
            return new[] { content, retCode };
        }

        /// <summary>
        /// cmd by cmd.exe
        /// </summary>
        public static void RunCmdAsync(string cmd, bool isHideWindow = false)
        {
            var pro = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WindowStyle = isHideWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                    UseShellExecute = !isHideWindow,
                    CreateNoWindow = isHideWindow,
                }
            };
            pro.Start();
            pro.StandardInput.WriteLine(cmd);
            pro.StandardInput.WriteLine("---------------split_line---------------");// add a symble for exit code
            pro.StandardInput.WriteLine("exit");// add a symble for exit code
        }

        /// <summary>
        /// return ExitCode
        /// </summary>
        /// <returns></returns>
        public static int RunFile(string filePath,
            string arguments = "",
            bool isAsync = false,
            bool isHideWindow = false,
            bool useShellExcute = true,
            DirectoryInfo? workingDirectory = null,
            Dictionary<string, string>? envVariables = null)
        {
            useShellExcute = useShellExcute && !(envVariables?.Count > 0);
            isHideWindow = isHideWindow && !useShellExcute;
            var psi = new ProcessStartInfo()
            {
                FileName = filePath,
                Arguments = arguments,
                WindowStyle = isHideWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                UseShellExecute = useShellExcute,
                CreateNoWindow =  isHideWindow,
            };

            if (workingDirectory != null)
            {
                psi.WorkingDirectory = workingDirectory.FullName;
            }

            if (envVariables != null)
            {
                foreach (var kv in envVariables)
                {
                    if (psi.EnvironmentVariables.ContainsKey(kv.Key))
                        psi.EnvironmentVariables[kv.Key] = kv.Value;
                    else
                        psi.EnvironmentVariables.Add(kv.Key, kv.Value);
                }
            }

            var pro = new Process
            {
                StartInfo = psi,
            };
            pro.Start();

            if (isAsync == false)
            {
                pro.WaitForExit();
                return pro.ExitCode;
            }
            return 0;
        }

        /// <summary>
        /// 解析单行命令中 exe 路径存在的空格或引号包裹的 "EXE 路径"，返回 EXE 路径 + 参数 + 工作目录
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static Tuple<string, string, DirectoryInfo?, bool> DisassembleOneLineScriptCmd(string cmd)
        {
            var parameters = "";
            var useShellExcute = true;
            cmd = cmd.Trim();
            var file = cmd;
            if (File.Exists(file))
            {
            }
            else if (cmd.StartsWith(@""""))
            {
                var i = cmd.IndexOf('"', 1);
                file = cmd.Substring(1, i - 1).Trim();
                parameters = cmd.Substring(i + 1).Trim();
            }
            else if (cmd.IndexOf(" ", StringComparison.Ordinal) > 0)
            {
                var s = cmd.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                var st = new Queue<string>(s);
                var filePath = st.Dequeue();
                while (st.Count > 0)
                {
                    if (File.Exists(filePath))
                    {
                        file = filePath;
                        parameters = string.Join(" ", st);
                        break;
                    }
                    filePath += " " + st.Dequeue();
                }
            }

            var tmp = CheckFileExistsAndFullName(file);
            if (tmp.Item1)
            {
                file = tmp.Item2;
            }

            DirectoryInfo? workDirectory = null;
            if (File.Exists(file))
            {
                workDirectory = new FileInfo(file).Directory;
                var ext = Path.GetExtension(file).ToLower();
                useShellExcute = false;
                if (ext == ".py")
                {
                    parameters = file + " " + parameters;
                    file = "python";
                }
                //else if (ext == ".bat" || ext == ".cmd")
                //{
                //    parameters = $" /c {file} {parameters}";
                //    file = "cmd";
                //}
                else if (ext == ".ps1")
                {
                    parameters = file + " " + parameters;
                    file = "powershell.exe";
                }
                else
                {
                    useShellExcute = true;
                }
            }

            return new Tuple<string, string, DirectoryInfo?, bool>(file, parameters, workDirectory, useShellExcute);
        }

        /// <summary>
        /// input file name, if environment variable exists, it will be expanded
        /// return (isExists, fullName)
        /// </summary>
        public static Tuple<bool, string> CheckFileExistsAndFullName(string fileName)
        {
            // 判断是否有环境变量
            fileName = Environment.ExpandEnvironmentVariables(fileName);

            if (Path.IsPathRooted(fileName))
            {
                return new Tuple<bool, string>(File.Exists(fileName), Path.GetFullPath(fileName));
            }

            if (File.Exists(fileName))
            {
                return new Tuple<bool, string>(true, Path.GetFullPath(fileName));
            }


            if (Path.GetDirectoryName(fileName) == string.Empty)
            {
                var file = fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".exe";
                var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
                foreach (string test in paths)
                {
                    string path = Path.Combine(test.Trim(), file);
                    if (File.Exists(path))
                        return new Tuple<bool, string>(true, Path.GetFullPath(path));
                }
            }
            return new Tuple<bool, string>(false, Path.GetFullPath(fileName));
        }
    }
}