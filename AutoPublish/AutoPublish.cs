﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Text;
using System.Threading;
using System.Xml;

namespace AutoPublish
{
    public class AutoPublish
    {
        private const string XmlFileName = "UpdateList.xml";
        private const string TempDownloadDirName = "tempDownload";
        private readonly string _localTempDir = AppDomain.CurrentDomain.BaseDirectory + "\\" + TempDownloadDirName;
        private readonly string _tempXmlPath = AppDomain.CurrentDomain.BaseDirectory + "\\" + TempDownloadDirName + "\\" + XmlFileName;//等待+1后被上传到服务器的xml文件路径

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

            if (!Directory.Exists(_localTempDir))
            {
                Directory.CreateDirectory(_localTempDir);
            }

            var remoteXmlPath = _ftpUpdateFolder + "\\" + Path.GetFileName(_tempXmlPath);//服务器上xml文件路径

            _ftpTool.DownLoadFile(TempDownloadDirName, XmlFileName);

            var localFilePathsTemp = Directory.GetFiles(_localDirPath, "*", _needCopyDescendantDir ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            var localFilePaths = localFilePathsTemp.Where(localFilePath =>
                !IsDirPath(localFilePath) && !_excludeNames.Any(localFilePath.Contains)
                || IsDirPath(localFilePath) && IsContainedByIncludeDirPaths(localFilePath))
                .ToList();

            using (FtpClient ftpClient = new FtpClient())
            {
                ftpClient.Host = _ftpUrl.Replace("ftp://", "");
                ftpClient.Credentials = new NetworkCredential(_ftpUserName, _ftpPassword);

                CreateRemoteFileDirIfNotExist(ftpClient, localFilePaths);

                var needUpdateRemoteFilePaths = GetNeedUpdateFilePaths(ftpClient, localFilePaths);

                var excludeFilePaths = needUpdateRemoteFilePaths.Where(p => p.Contains("System.Net.FtpClient.dll")).ToList();
                UpdateXmlFile(_tempXmlPath, needUpdateRemoteFilePaths, excludeFilePaths);

                if (needUpdateRemoteFilePaths.Count == 0)
                {
                    Console.WriteLine("没有要更新的文件！");
                    return;
                }
                Console.WriteLine("开始上传文件......");

                UploadFiles(ftpClient, needUpdateRemoteFilePaths);//先上传要发布的文件
                Console.WriteLine("发布文件上传完毕......");

                UploadFiles(ftpClient, new List<string>() { remoteXmlPath });//最后上传xml文件
                Console.WriteLine("xml文件上传完毕......");

                Console.WriteLine("发布完成！");
            }

        }

        /// <summary>
        /// 根据本地目录路径判断如果远程对应子目录不存在，需要创建远程子目录
        /// </summary>
        /// <param name="ftpClient"></param>
        /// <param name="localFilePaths"></param>
        private void CreateRemoteFileDirIfNotExist(FtpClient ftpClient, List<string> localFilePaths)
        {
            var distinctDirPaths = localFilePaths.Select(p => p.Substring(0, p.LastIndexOf("\\"))).Distinct().OrderBy(p => p.Length).ToList();

            foreach (var dirPath in distinctDirPaths)
            {
                var relativeDirPath = GetRelativeFilePath(dirPath, _localDirPath);
                var ftpDirPath = _ftpUpdateFolder + relativeDirPath.Replace("\\", "/");
                if (!ftpClient.DirectoryExists(ftpDirPath))
                {
                    ftpClient.CreateDirectory(ftpDirPath, true);
                }
            }

        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="ftpClient"></param>
        /// <param name="needUpdateRemoteFilePaths">需要被更新的文件路径</param>
        private void UploadFiles(FtpClient ftpClient, List<string> needUpdateRemoteFilePaths)
        {
            List<string> toBeUploadFilePaths = needUpdateRemoteFilePaths;

            int i = 0;
            while (i++ < 5 && toBeUploadFilePaths.Count > 0)
            {
                var localFilePaths = ConvertToLoaclFilePaths(toBeUploadFilePaths);
                _ftpTool.UploadFileList(ftpClient, localFilePaths.ToArray(), toBeUploadFilePaths.ToArray());

                Thread.Sleep(3000);
                toBeUploadFilePaths = GetUploadedFailedFiles(ftpClient, toBeUploadFilePaths);
                if (toBeUploadFilePaths.Count > 0)
                {
                    Console.WriteLine(string.Format("有文件内容丢失！重试{0}次......", i));
                }
            }

            if (toBeUploadFilePaths.Count > 0)
            {
                throw new Exception(string.Format("上传完后检测到远程文件与本地文件大小不一致！重试5次后依然失败！remoteFilePath = {0}}", toBeUploadFilePaths.First()));
            }
        }

        /// <summary>
        /// 把远程路径转换成本地路径
        /// </summary>
        /// <param name="remoteFilePaths"></param>
        /// <returns></returns>
        private List<string> ConvertToLoaclFilePaths(List<string> remoteFilePaths)
        {
            var filePaths = remoteFilePaths.Select(p => _localDirPath + p.Replace(_ftpUpdateFolder, "")).ToList();

            //xml文件特殊对待
            for (int i = 0; i < remoteFilePaths.Count; i++)
            {
                if (remoteFilePaths[i].EndsWith(XmlFileName))
                {
                    filePaths[i] = _tempXmlPath;
                }
            }

            return filePaths;
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

        /// <summary>
        /// 取需要更新的文件路径
        /// </summary>
        /// <param name="ftpClient"></param>
        /// <param name="localFilePaths"></param>
        /// <returns></returns>
        private List<string> GetNeedUpdateFilePaths(FtpClient ftpClient, List<string> localFilePaths)
        {
            var needUpdateRemoteFilePaths = new List<string>();

            foreach (var localFilePath in localFilePaths)
            {
                var relativeFilePath = GetRelativeFilePath(localFilePath, _localDirPath);
                if (relativeFilePath != null)
                {
                    var tmpRemoteFilePath = _ftpUpdateFolder + relativeFilePath;
                    var remoteFilePath = (tmpRemoteFilePath).Replace("\\", "/");
                    if (!ftpClient.FileExists(remoteFilePath))
                    {
                        needUpdateRemoteFilePaths.Add(remoteFilePath);
                    }
                    else
                    if (_ftpTool.IsLocalFileNewerThanRemoteFile(tmpRemoteFilePath,
                        localFilePath, ftpClient))
                    {
                        needUpdateRemoteFilePaths.Add(remoteFilePath);
                    }
                }
            }

            return needUpdateRemoteFilePaths;
        }

        private void UpdateXmlFile(string tempXmlPath, List<string> needUpdateRemoteFilePaths, List<string> excludeFilePaths)
        {
            needUpdateRemoteFilePaths = needUpdateRemoteFilePaths.Except(excludeFilePaths).ToList();
            foreach (var needUpdateRemoteFilePath in needUpdateRemoteFilePaths)
            {
                var remoteRelativeFilePath = GetRelativeFilePath(needUpdateRemoteFilePath, _ftpUpdateFolder);
                var localRelativeFilePath = remoteRelativeFilePath.Replace("/", @"\");

                Common.ModifyXmlFile(tempXmlPath, localRelativeFilePath);
            }

        }

        /// <summary>
        /// 取上传后有内容丢失的所有文件路径
        /// </summary>
        /// <param name="ftpClient">已上传的文件路径</param>
        /// <param name="uploadedRemoteFilePaths">已上传的文件路径</param>
        /// <returns></returns>
        private List<string> GetUploadedFailedFiles(FtpClient ftpClient, List<string> uploadedRemoteFilePaths)
        {
            var list = new List<string>();
            var localFilePaths = ConvertToLoaclFilePaths(uploadedRemoteFilePaths);
            for (int i = 0; i < uploadedRemoteFilePaths.Count; i++)
            {
                var localFilePath = localFilePaths[i];
                var remoteFilePath = uploadedRemoteFilePaths[i];

                FileInfo fileInfo = new FileInfo(localFilePath);
                var localFileLength = fileInfo.Length;
                var remoteFileLength = ftpClient.GetFileSize(remoteFilePath);
                if (localFileLength != remoteFileLength)
                {
                    list.Add(remoteFilePath);
                    Console.WriteLine(string.Format("上传完后检测到远程文件与本地文件大小不一致！" +
                                                    "localFileLength = {0}, remoteFileLenth = {1}，remoteFilePath = {2}",
                        localFileLength, remoteFileLength, remoteFilePath));
                }
            }

            return list;
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
