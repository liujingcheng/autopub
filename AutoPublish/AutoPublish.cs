using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;

namespace AutoPublish
{
    public class AutoPublish
    {
        private readonly string _userName = ConfigurationManager.AppSettings["UserName"];
        private readonly string _password = ConfigurationManager.AppSettings["Password"];

        private readonly string _remoteDirPath = ConfigurationManager.AppSettings["RemoteDirPath"];
        private readonly string _localDirPath = ConfigurationManager.AppSettings["LocalDirPath"];

        private readonly string _needCopyDescendantDirStr = ConfigurationManager.AppSettings["NeedCopyDescendantDir"];
        private readonly string _exceptNamesStr = ConfigurationManager.AppSettings["ExceptNames"];

        private readonly bool _needCopyDescendantDir;//是否包括子文件夹内的文件

        private readonly string[] _exceptNames = { };

        public AutoPublish()
        {
            bool.TryParse(_needCopyDescendantDirStr, out _needCopyDescendantDir);
            _exceptNames = _exceptNamesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public void Publish()
        {
            if (CanRemoteFileConnected())
            {
                return;
            }

            Console.WriteLine("开始发布...");

            var localFilePathsTemp = Directory.GetFiles(_localDirPath, "*", _needCopyDescendantDir ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            List<string> localFilePaths = localFilePathsTemp.Where(localFilePath => !_exceptNames.Any(localFilePath.Contains)).ToList();

            var remoteFilePaths = Directory.GetFiles(_remoteDirPath, "*", _needCopyDescendantDir ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            if (IsLocalXmlFileExist(out var localXmlPath))
            {
                return;
            }

            var remoteXmlPath = _remoteDirPath + "\\UpdateList.xml";
            if (!File.Exists(remoteXmlPath))
            //远程目录不存在Xml文件的情形暂不考虑
            {
                throw new Exception("远程目录不存在Xml文件");
            }

            foreach (var localFilePath in localFilePaths)
            {
                if (localFilePath == localXmlPath || localFilePath.Contains("\\Log\\")) continue;//忽略UpdateList.xml文件和Log文件夹

                var fileName = GetNamePath(localFilePath, _localDirPath);
                if (fileName != null && !remoteFilePaths.Any(q => q.EndsWith(fileName)))
                {
                    var fileInfo = new FileInfo(localFilePath);
                    var remoteFilePath = _remoteDirPath + fileName;
                    var remoteFileDir = Path.GetDirectoryName(remoteFilePath);
                    if (remoteFileDir != null && !Directory.Exists(remoteFileDir))
                    {
                        Directory.CreateDirectory(remoteFileDir);
                    }
                    fileInfo.CopyTo(_remoteDirPath + fileName);
                    Console.WriteLine("拷贝文件：" + fileName);
                    Common.ModifyXmlFile(remoteXmlPath, fileName);
                }
            }

            foreach (var localFilePath in localFilePaths)
            {
                if (localFilePath == localXmlPath || localFilePath.Contains("\\Log\\")) continue;//忽略UpdateList.xml文件和Log文件夹

                var fileName = GetNamePath(localFilePath, _localDirPath);
                if (fileName != null)
                {
                    var remoteFilePath = remoteFilePaths.FirstOrDefault(q => q.EndsWith(fileName));
                    if (remoteFilePath != null)
                    {
                        var localFileInfo = new FileInfo(localFilePath);
                        var remoteFileInfo = new FileInfo(remoteFilePath);

                        if (localFileInfo.Length != remoteFileInfo.Length ||
                            localFileInfo.LastWriteTime > remoteFileInfo.LastWriteTime)
                        {
                            localFileInfo.CopyTo(_remoteDirPath + fileName, true);
                            Console.WriteLine("覆盖文件：" + fileName);
                            Common.ModifyXmlFile(remoteXmlPath, fileName);
                        }

                    }
                }
            }

            Console.WriteLine("发布完成！按回车键退出...");
            Console.ReadLine();

        }

        private bool IsLocalXmlFileExist(out string localXmlPath)
        {
            localXmlPath = _localDirPath + "\\UpdateList.xml";
            if (!File.Exists(localXmlPath))
            {
                Console.WriteLine("本地UpdateList.xml文件不存在");
                Console.ReadLine();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 远程文件是否正常连通
        /// </summary>
        /// <returns></returns>
        private bool CanRemoteFileConnected()
        {
            if (string.IsNullOrEmpty(_remoteDirPath) || string.IsNullOrEmpty(_localDirPath))
            {
                Console.WriteLine("远程目录或本地目录为空");
                Console.ReadLine();
                return true;
            }

            NetHelper.DeleteNetUse(); //先删除所有远程连接
            string ip;
            string path;
            NetHelper.GetIpAndPath(_remoteDirPath, out ip, out path);
            if (!string.IsNullOrEmpty(ip)) //说明是共享目录，否则认为就是本地目录
            {
                if (!NetHelper.NetUseDirectory(_remoteDirPath, _userName, _password))
                {
                    Console.WriteLine("无法访问远程共享目录：" + _remoteDirPath);
                    Console.ReadLine();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取除主目录以外的路径
        /// （
        /// 比如全路径是D:\temp\ftp\UpdateFiles\Resources\Template\报价参数抽查表SL.xls，
        /// 主目录路径是D:\temp\ftp\UpdateFiles，
        /// 那么返回的结果就是\Resources\Template\报价参数抽查表SL.xls
        /// ）
        /// </summary>
        /// <param name="fullPath">全路径</param>
        /// <param name="mainDirPath">主目录路径</param>
        /// <returns></returns>
        private string GetNamePath(string fullPath, string mainDirPath)
        {
            if (fullPath == null || mainDirPath == null) throw new ArgumentNullException();

            if (!fullPath.StartsWith(mainDirPath)) throw new Exception("主目录路径不与全路径匹配");

            return fullPath.Substring(mainDirPath.Length);
        }

    }
}
