using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.FtpClient;
using System.Text;
using System.Xml;

namespace AutoPublish
{
    public class AutoPublish
    {
        private readonly string _ftpUrl = ConfigurationManager.AppSettings["FtpUrl"];
        private readonly string _ftpUserName = ConfigurationManager.AppSettings["FtpUserName"];
        private readonly string _ftpPassword = ConfigurationManager.AppSettings["FtpPassword"];

        private readonly string _ftpUpdateFolder = ConfigurationManager.AppSettings["FtpUpdateFolder"];
        private readonly string _localDirPath = ConfigurationManager.AppSettings["LocalDirPath"];

        private readonly string _needCopyDescendantDirStr = ConfigurationManager.AppSettings["NeedCopyDescendantDir"];
        private readonly string _excludeNamesStr = ConfigurationManager.AppSettings["ExcludeNames"];

        private readonly bool _needCopyDescendantDir;//是否包括子文件夹内的文件

        private readonly string[] _excludeNames = { };

        private readonly List<string> _needUpdateFilePaths = new List<string>();//需要更新的文件路径

        private FtpTool _ftpTool;

        public AutoPublish()
        {
            bool.TryParse(_needCopyDescendantDirStr, out _needCopyDescendantDir);
            _excludeNames = _excludeNamesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _ftpTool = new FtpTool(_ftpUrl, _ftpUserName, _ftpPassword, _ftpUpdateFolder);
        }

        public void Publish()
        {
            Console.WriteLine("开始发布...");

            var xmlFileName = "UpdateList.xml";
            var tempDownloadDirName = "tempDownload";
            // 存放远程目录下所有文件路径的临时文件名
            var remoteDirFilePathsFileName = "remoteDirFilePaths.txt";

            var localTempDir = AppDomain.CurrentDomain.BaseDirectory + "\\" + tempDownloadDirName;
            if (!Directory.Exists(localTempDir))
            {
                Directory.CreateDirectory(localTempDir);
            }

            var tempRemoteXmlPath = localTempDir + "\\" + xmlFileName;

            _ftpTool.DownLoadFile(tempDownloadDirName, xmlFileName);

            _ftpTool.ListFtpFiles(null, tempDownloadDirName, remoteDirFilePathsFileName);
            var remoteFilePaths = GetRemoteFilePaths(localTempDir + "\\" + remoteDirFilePathsFileName);

            var localFilePathsTemp = Directory.GetFiles(_localDirPath, "*", _needCopyDescendantDir ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            var localFilePaths = localFilePathsTemp.Where(localFilePath => !_excludeNames.Any(localFilePath.Contains)).ToList();

            var localXmlPath = _localDirPath + "\\" + xmlFileName;

            ThrowExceptionWhileXmlNotExist(localXmlPath);

            UpdateXmlWhileRemoteFileNotExist(localFilePaths, remoteFilePaths, tempRemoteXmlPath);

            UpdateXmlWhileRemoteFileExist(localFilePaths, remoteFilePaths, tempRemoteXmlPath);

            UploadFiles(tempRemoteXmlPath);

            Console.WriteLine("发布完成！");
            Console.ReadKey();

        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="tempRemoteXmlPath">待上传到服务器上的xml文件（覆盖服务器上的xml）</param>
        private void UploadFiles(string tempRemoteXmlPath)
        {
            if (_needUpdateFilePaths.Count == 0)
            {
                Console.WriteLine("没有要更新的文件！");
                return;
            }
            Console.WriteLine("开始上传文件......");
            var filePaths = _needUpdateFilePaths.Select(p => _localDirPath + p.Replace(_ftpUpdateFolder, "")).ToList();
            filePaths.Add(tempRemoteXmlPath);//把更新好的xml文件一起上传

            var uploadResults = _ftpTool.UploadFileList(filePaths.ToArray(), _ftpUpdateFolder);
            foreach (var uploadResult in uploadResults)
            {
                if (uploadResult.State == false)
                {
                    throw new Exception(uploadResult.Url + uploadResult.Path + "上传失败！" + uploadResult.FailureMessage);
                }
            }
        }

        /// <summary>
        /// 取远程目录所有文件相对路径集合
        /// </summary>
        /// <param name="localFilePathStoreAllRemoteFilePaths">存放远程目录下所有文件路径的临时文件路径</param>
        /// <returns></returns>
        private string[] GetRemoteFilePaths(string localFilePathStoreAllRemoteFilePaths)
        {
            var listStr = new List<string>();
            var rFile = new FileStream(localFilePathStoreAllRemoteFilePaths, FileMode.Open);

            var sr = new StreamReader(rFile, Encoding.GetEncoding("utf-8"));
            try
            {
                while (!sr.EndOfStream)
                {
                    var str = sr.ReadLine();
                    if (str == null || !str.Contains("."))
                    //暂不把目录包含进来
                    {
                        continue;
                    }
                    listStr.Add(str.Replace("/", "\\"));
                }
            }
            finally
            {
                sr.Close();
            }

            return listStr.ToArray();
        }

        /// <summary>
        /// 本地没有xml文件时抛出异常
        /// </summary>
        /// <param name="localXmlPath"></param>
        /// <param name="remoteXmlPath"></param>
        private static void ThrowExceptionWhileXmlNotExist(string localXmlPath)
        {
            if (!File.Exists(localXmlPath))
            {
                throw new Exception("本地UpdateList.xml文件不存在");
            }

        }

        private void UpdateXmlWhileRemoteFileNotExist(List<string> localFilePaths, string[] remoteFilePaths,
            string remoteXmlPath)
        {
            foreach (var localFilePath in localFilePaths)
            {
                var fileName = GetNamePath(localFilePath, _localDirPath);
                if (fileName != null && !remoteFilePaths.Any(q => q.EndsWith(fileName)))
                {
                    var remoteFilePath = _ftpUpdateFolder + fileName;
                    CreateRemoteFileDirIfNeed(remoteFilePath);
                    _needUpdateFilePaths.Add(remoteFilePath);
                    Common.ModifyXmlFile(remoteXmlPath, fileName);
                }
            }
        }

        private void UpdateXmlWhileRemoteFileExist(List<string> localFilePaths, string[] remoteFilePaths,
            string remoteXmlPath)
        {
            using (FtpClient conn = new FtpClient())
            {
                _ftpTool.SetCredentials(conn);

                foreach (var localFilePath in localFilePaths)
                {
                    var fileName = GetNamePath(localFilePath, _localDirPath);
                    if (fileName != null)
                    {
                        var remoteFilePath = remoteFilePaths.FirstOrDefault(q => q.EndsWith(fileName));
                        if (remoteFilePath != null)
                        {
                            if (_ftpTool.IsLocalFileNewerThanRemoteFile(_ftpUpdateFolder + fileName,
                                localFilePath, conn))
                            {
                                _needUpdateFilePaths.Add(remoteFilePath);
                                Common.ModifyXmlFile(remoteXmlPath, fileName);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 如果远程子目录不存在，需要创建子目录（暂未实现）
        /// </summary>
        /// <param name="remoteFilePath"></param>
        private static void CreateRemoteFileDirIfNeed(string remoteFilePath)
        {
            //var remoteFileDir = Path.GetDirectoryName(remoteFilePath);
            //if (remoteFileDir != null && !Directory.Exists(remoteFileDir))
            //{
            //    Directory.CreateDirectory(remoteFileDir);
            //}
        }

        /// <summary>
        /// 远程文件是否正常连通
        /// </summary>
        /// <returns></returns>
        private bool CanRemoteFileConnected()
        {
            if (string.IsNullOrEmpty(_ftpUpdateFolder) || string.IsNullOrEmpty(_localDirPath))
            {
                throw new Exception("远程目录或本地目录为空");
            }

            NetHelper.DeleteNetUse(); //先删除所有远程连接
            string ip;
            string path;
            NetHelper.GetIpAndPath(_ftpUpdateFolder, out ip, out path);
            if (!string.IsNullOrEmpty(ip)) //说明是共享目录，否则认为就是本地目录
            {
                if (!NetHelper.NetUseDirectory(_ftpUpdateFolder, _ftpUserName, _ftpPassword))
                {
                    throw new Exception("无法访问远程共享目录：" + _ftpUpdateFolder);
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
