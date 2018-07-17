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
        private FtpTool _ftpTool;

        /// <summary>
        /// 本地目录（生成文件的地方）
        /// </summary>
        private readonly string _localDirPath;

        private readonly string _ftpUrl;
        private readonly string _ftpUserName;
        private readonly string _ftpPassword;

        /// <summary>
        /// ftp发布文件更新目录
        /// </summary>
        string _ftpUpdateFolder;
        /// <summary>
        /// 是否包括子孙文件夹内的文件
        /// </summary>
        string _needCopyDescendantDirStr = ConfigurationManager.AppSettings["NeedCopyDescendantDir"];
        /// <summary>
        /// 要排除的名称（目录或文件名都可，逗号分割）
        /// </summary>
        string _excludeNamesStr = ConfigurationManager.AppSettings["ExcludeNames"];
        /// <summary>
        /// 要包含的目录路径（逗号分割）
        /// </summary>
        string _includeDirPathsStr = ConfigurationManager.AppSettings["IncludeDirPaths"];
        /// <summary>
        /// 要排除的名称（目录或文件名都可）
        /// </summary>
        private string[] _excludeNames = { };
        /// <summary>
        /// 要包含的目录路径
        /// </summary>
        private string[] _includeDirPaths = { };
        /// <summary>
        /// 是否包括子孙文件夹内的文件
        /// </summary>
        private bool _needCopyDescendantDir;
        /// <summary>
        /// 需要更新的文件路径
        /// </summary>
        private readonly List<string> _needUpdateFilePaths = new List<string>();

        public AutoPublish(string localDirPath, string ftpUrl, string ftpUserName, string ftpPassword, string ftpUpdateFolder)
        {
            _localDirPath = localDirPath;
            _ftpUrl = ftpUrl;
            _ftpUserName = ftpUserName;
            _ftpPassword = ftpPassword;
            _ftpUpdateFolder = ftpUpdateFolder;
            Init();
        }

        public AutoPublish()
        {
            _localDirPath = ConfigurationManager.AppSettings["LocalDirPath"];
            _ftpUrl = ConfigurationManager.AppSettings["FtpUrl"];
            _ftpUserName = ConfigurationManager.AppSettings["FtpUserName"];
            _ftpPassword = ConfigurationManager.AppSettings["FtpPassword"];
            _ftpUpdateFolder = ConfigurationManager.AppSettings["FtpUpdateFolder"];

            Init();
        }

        public void Init()
        {
            bool.TryParse(_needCopyDescendantDirStr, out _needCopyDescendantDir);
            _excludeNames = _excludeNamesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _includeDirPaths = _includeDirPathsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
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

            UpdateXmlWhileRemoteFileNotExist(localFilePaths, remoteFilePaths, tempRemoteXmlPath);

            UpdateXmlWhileRemoteFileExist(localFilePaths, remoteFilePaths, tempRemoteXmlPath);

            UploadFiles(tempRemoteXmlPath);

            Console.WriteLine("发布完成！");

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
                    if (string.IsNullOrWhiteSpace(str))
                    {
                        continue;
                    }
                    if (IsDirPath(str) && !_includeDirPaths.Contains(str))
                    //不是指定要被包含的目录不要
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
        /// 给定路径是否是目录
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool IsDirPath(string path)
        {
            if (path.Contains("."))
            //.代表有后缀名的文件
            {
                return false;
            }
            return true;
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
            //TODO:待实现
            //var remoteFileDir = Path.GetDirectoryName(remoteFilePath);
            //if (remoteFileDir != null && !Directory.Exists(remoteFileDir))
            //{
            //    Directory.CreateDirectory(remoteFileDir);
            //}
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
