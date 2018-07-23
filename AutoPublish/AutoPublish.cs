using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
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

            var localTempDir = AppDomain.CurrentDomain.BaseDirectory + "\\" + tempDownloadDirName;
            if (!Directory.Exists(localTempDir))
            {
                Directory.CreateDirectory(localTempDir);
            }

            var tempRemoteXmlPath = localTempDir + "\\" + xmlFileName;

            _ftpTool.DownLoadFile(tempDownloadDirName, xmlFileName);

            var localFilePathsTemp = Directory.GetFiles(_localDirPath, "*", _needCopyDescendantDir ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            var localFilePaths = localFilePathsTemp.Where(localFilePath =>
                !IsDirPath(localFilePath) && !_excludeNames.Any(localFilePath.Contains)
                || IsDirPath(localFilePath) && IsContainedByIncludeDirPaths(localFilePath))
                .ToList();

            CreateRemoteFileDirIfNotExist(localFilePaths);

            UpdateXmlFile(localFilePaths, tempRemoteXmlPath);

            UploadFiles(tempRemoteXmlPath);

            Console.WriteLine("发布完成！");

        }

        /// <summary>
        /// 根据本地目录路径判断如果远程对应子目录不存在，需要创建远程子目录
        /// </summary>
        /// <param name="localFilePaths"></param>
        private void CreateRemoteFileDirIfNotExist(List<string> localFilePaths)
        {
            var distinctDirPaths = localFilePaths.Select(p => p.Substring(0, p.LastIndexOf("\\"))).Distinct().OrderBy(p => p.Length).ToList();

            using (FtpClient conn = new FtpClient())
            {
                conn.Host = _ftpUrl.Replace("ftp://", "");
                conn.Credentials = new NetworkCredential(_ftpUserName, _ftpPassword);

                foreach (var dirPath in distinctDirPaths)
                {
                    var relativeDirPath = GetRelativeFilePath(dirPath, _localDirPath);
                    var ftpDirPath = _ftpUpdateFolder + relativeDirPath.Replace("\\", "/");
                    if (!conn.DirectoryExists(ftpDirPath))
                    {
                        conn.CreateDirectory(ftpDirPath, true);
                    }
                }
            }

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
            _needUpdateFilePaths.Add(_ftpUpdateFolder + "\\" + Path.GetFileName(tempRemoteXmlPath));

            var uploadResults = _ftpTool.UploadFileList(filePaths.ToArray(), _needUpdateFilePaths.ToArray());
            foreach (var uploadResult in uploadResults)
            {
                if (uploadResult.State == false)
                {
                    throw new Exception(uploadResult.Url + uploadResult.Path + "上传失败！" + uploadResult.FailureMessage);
                }
            }
        }

        /// <summary>
        /// 指定目录路径是否是想被包含（用于文件对比）的目录路径
        /// </summary>
        /// <param name="dirPath"></param>
        /// <returns></returns>
        private bool IsContainedByIncludeDirPaths(string dirPath)
        {
            if (!dirPath.StartsWith("\\"))
            {
                dirPath = "\\" + dirPath;
            }
            if (!dirPath.EndsWith("\\"))
            {
                dirPath = dirPath + "\\";
            }
            foreach (var includeDirPath in _includeDirPaths)
            {
                if (dirPath.Contains(includeDirPath))
                {
                    return true;
                }
            }

            return false;
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

        private void UpdateXmlFile(List<string> localFilePaths,
            string remoteXmlPath)
        {
            using (FtpClient conn = new FtpClient())
            {
                _ftpTool.SetCredentials(conn);

                foreach (var localFilePath in localFilePaths)
                {
                    var relativeFilePath = GetRelativeFilePath(localFilePath, _localDirPath);
                    if (relativeFilePath != null)
                    {
                        var remoteFilePath = (_ftpUpdateFolder + relativeFilePath).Replace("\\", "/");
                        if (!conn.FileExists(remoteFilePath))
                        {
                            _needUpdateFilePaths.Add(remoteFilePath);
                            Common.ModifyXmlFile(remoteXmlPath, relativeFilePath);
                        }
                        else
                        if (_ftpTool.IsLocalFileNewerThanRemoteFile(_ftpUpdateFolder + relativeFilePath,
                            localFilePath, conn))
                        {
                            _needUpdateFilePaths.Add(remoteFilePath);
                            Common.ModifyXmlFile(remoteXmlPath, relativeFilePath);
                        }
                    }
                }
            }
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
        private string GetRelativeFilePath(string fullPath, string mainDirPath)
        {
            if (fullPath == null || mainDirPath == null) throw new ArgumentNullException();

            if (!fullPath.StartsWith(mainDirPath)) throw new Exception("主目录路径不与全路径匹配");

            return fullPath.Substring(mainDirPath.Length);
        }

    }
}
