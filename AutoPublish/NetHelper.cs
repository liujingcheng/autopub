using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AutoPublish
{
    public class NetHelper
    {
        /// <summary>
        /// 用ping命令判断远程服务器是否能连接上
        /// create by ljc 2012-02-03
        /// </summary>
        /// <param name="ip">远程服务器ip地址</param>
        /// <returns>true:能连通;false:不能连通</returns>
        private static bool IsNetConnected(string ip)
        {
            try
            {
                var ping = new Ping();
                var reply = ping.Send(IPAddress.Parse(ip));
                if (reply != null && reply.Status == IPStatus.Success)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ping远程服务器时发生异常\n\r"+ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 判断用net use命令能否连上远程服务器上的指定目录
        /// </summary>
        /// <param name="fullPath">远程目录全路径(例如:\\192.168.10.140\movie\电影\2013.08.17</param>
        /// <param name="userName"></param>
        /// <param name="passWord"></param>
        /// <returns></returns>
        public static bool NetUseDirectory(string fullPath, string userName, string passWord)
        {
            if (fullPath == null)
            {
                return false;
            }

            string ip;
            string path;
            GetIpAndPath(fullPath, out ip, out path);

            return NetUseDirectory(ip, path, userName, passWord);
        }
        /// <summary>
        /// 判断用net use命令能否连上远程服务器上的指定目录
        /// create by ljc 2012-02-03
        /// </summary>
        /// <param name="ip">ip地址(例:192.1680.10.151)</param>
        /// <param name="directory">目录（例：“test\bps”）</param>
        /// <param name="userName">用户名</param>
        /// <param name="passWord">密码</param>
        /// <returns>true:能连上;false:不能连上</returns>
        public static bool NetUseDirectory(string ip, string directory, string userName, string passWord)
        {
            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(directory))
            {
                return false;
            }
            ip = ip.Trim('\\');
            directory = directory.Trim('\\');

            if (!IsNetConnected(ip))
            {
                Console.WriteLine("用ping命令判断出程服务器无法被连接上");
                return false;
            }

            bool Flag = true;
            Process proc = new Process();
            try
            {
                proc.StartInfo.FileName = "cmd.exe ";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardInput = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();

                string dosLine;
                if (string.IsNullOrEmpty(userName))
                {
                    dosLine = @"net use \\" + ip + "\\" + directory;
                }
                else
                {
                    dosLine = @"net use \\" + ip + "\\" + directory + " \"" + passWord + "\" /user:\"" + userName + "\"";
                }

                proc.StandardInput.WriteLine(dosLine);
                proc.StandardInput.WriteLine("exit");
                while (proc.HasExited == false)
                {
                    proc.WaitForExit(1000);
                }
                string errormsg = proc.StandardError.ReadToEnd();
                if (errormsg != "")
                {
                    Console.WriteLine(string.Format("执行命令后得到错误消息,命令:{0},错误消息:{1},userName={2},passWord={3}", dosLine, errormsg, userName ?? "null", passWord ?? "null"));
                    Flag = false;
                }
                proc.StandardError.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0}方法异常：{1}", MethodBase.GetCurrentMethod().Name, ex.Message), ex);
                Flag = false;
            }
            finally
            {
                try
                {
                    proc.Close();
                    proc.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("{0}方法异常：{1}", MethodBase.GetCurrentMethod().Name, ex.Message), ex);
                    Flag = false;
                }
            }
            return Flag;
        }

        /// <summary>
        /// 删除net use远程连接
        /// create by ljc 2012-02-03
        /// </summary>
        public static void DeleteNetUse()
        {
            //bool Flag = true;
            Process proc = new Process();
            try
            {
                proc.StartInfo.FileName = "cmd.exe ";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardInput = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                string dosLine = @"net use * /d /y";
                proc.StandardInput.WriteLine(dosLine);
                proc.StandardInput.WriteLine("exit");
                while (proc.HasExited == false)
                {
                    proc.WaitForExit(1000);
                }
                string errormsg = proc.StandardError.ReadToEnd();
                errormsg = proc.StandardError.ReadToEnd();
                if (errormsg != "")
                {
                    Console.WriteLine(errormsg);

                }
                proc.StandardError.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("" + ex.Message);
            }
            finally
            {
                try
                {
                    proc.Close();
                    proc.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("" + ex.Message);
                }
            }

        }

        /// <summary>
        /// 获取远程服务器上指定目录下的所有文件的文件名（含路径）
        /// create by ljc 2012-02-03
        /// </summary>
        /// <param name="ip">远程服务器IP</param>
        /// <param name="directory">目录</param>
        /// <returns></returns>
        public string[] GetAllFileNames(string ip, string directory)
        {
            try
            {
                string[] zips = Directory.GetFiles(@"\\" + ip + directory, "*", SearchOption.AllDirectories);
                return zips;
            }
            catch (Exception ex)
            {
                Console.WriteLine("获取远程服务器指定目录下的所有文件异常", ex);
                throw;
            }
        }


        /// <summary>
        /// 获取给定字符串中的IP地址
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetIP(string path)
        {
            if (path == null)
            {
                return null;
            }

            string IP = null;

            //获取文件路径中的IP
            MatchCollection mc = Regex.Matches(path, @"((?:(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d)))\.){3}(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d))))");

            if (mc.Count > 0)
            {
                IP = mc[0].Value;
            }

            return IP;
        }

        /// <summary>
        /// 将全路径分隔成IP和相对路径(比如"\\192.168.10.140\movie\电影\2013.08.17\"会被分隔成192.168.10.140和电影\2013.08.17)
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="ip">分隔后的ip(不含斜杠\)</param>
        /// <param name="path">(不含斜杠\)</param>
        public static void GetIpAndPath(string fullPath, out string ip, out string path)
        {
            ip = null;
            path = null;

            if (fullPath == null)
            {
                return;
            }

            //获取文件路径中的IP
            MatchCollection mc = Regex.Matches(fullPath, @"((?:(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d)))\.){3}(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d))))");
            if (mc.Count <= 0)
            {
                return;
            }
            ip = mc[0].Value;
            path = fullPath.Substring(fullPath.IndexOf(ip) + ip.Length);

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(path))
            {
                return;
            }
            ip = ip.Trim('\\');
            path = path.Trim('\\');
        }

        /// <summary>
        /// 获取共享目录名(例:\\192.168.10.240\share\ljc 得到的结果是share)
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public static void GetIPAndShareDir(string fullPath, out string IP, out string shareDir)
        {
            shareDir = null;
            IP = GetIP(fullPath);
            if (IP == null || !fullPath.StartsWith(@"\\"))
            {
                return;
            }

            string[] path = fullPath.Split('\\');
            if (path.Length >= 4)
            {
                shareDir = path[3];
            }
        }
    }
}
