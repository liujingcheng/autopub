using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Text;
using System.Threading;
using CanYou.WPF.Common.Controls.FileUploadUC.FileOperations;

namespace AutoPublish
{
    public class FtpTool
    {
        private string _ftpurl;
        private string _ftpUserName;
        private string _ftpPasswd;
        private string _ftpUpdateFolder;

        public FtpTool(string ftpurl, string ftpUserName, string ftpPasswd, string ftpUpdateFolder)
        {
            _ftpurl = ftpurl;
            _ftpUserName = ftpUserName;
            _ftpPasswd = ftpPasswd;
            _ftpUpdateFolder = ftpUpdateFolder;
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="localTempDirPath">本地临时文件夹路径</param>
        /// <param name="fileName">要下载的文件名</param>
        public void DownLoadFile(string localTempDirPath, string fileName)
        {
            var serverPath = _ftpurl + "/" + _ftpUpdateFolder;
            var serverFilePath = serverPath + "/" + fileName;

            var localTempFilePath = localTempDirPath + "\\" + fileName;
            var outputStream = new FileStream(localTempFilePath, FileMode.Create);
            var reqFtp = (FtpWebRequest)WebRequest.Create(new Uri(serverFilePath));
            reqFtp.Method = WebRequestMethods.Ftp.DownloadFile;
            reqFtp.UseBinary = true;
            reqFtp.Credentials = new NetworkCredential(_ftpUserName, _ftpPasswd);
            var response = (FtpWebResponse)reqFtp.GetResponse();
            Stream ftpStream = response.GetResponseStream();
            try
            {
                const int bufferSize = 2048;
                var buffer = new byte[bufferSize];
                if (ftpStream != null)
                {
                    int readCount = ftpStream.Read(buffer, 0, bufferSize);
                    while (readCount > 0)
                    {
                        outputStream.Write(buffer, 0, readCount);
                        readCount = ftpStream.Read(buffer, 0, bufferSize);
                    }
                }
            }
            finally
            {
                if (ftpStream != null) ftpStream.Close();
                outputStream.Close();
                response.Close();
            }

        }

        /// <summary>
        /// 列出远程目录下所有文件路径到指定的临时文件里
        /// </summary>
        /// <param name="remoteDirName">远程ftp根目录下的子目录（为空则视为根目录）</param>
        /// <param name="localTempDirPath">本地临时文件夹路径</param>
        /// <param name="tempFileName">存放指定远程目录下所有文件名的临时文件</param>
        public void ListFtpFiles(string remoteDirName, string localTempDirPath, string tempFileName)
        {
            var serverPath = _ftpurl + "/" + _ftpUpdateFolder + (string.IsNullOrEmpty(remoteDirName) ? "" : "/" + remoteDirName);

            var localTempFilePath = localTempDirPath + "\\" + tempFileName;
            var outputStream = new FileStream(localTempFilePath, FileMode.Create);
            var reqFtp = (FtpWebRequest)WebRequest.Create(new Uri(serverPath));
            reqFtp.Method = WebRequestMethods.Ftp.ListDirectory;
            reqFtp.UseBinary = true;
            reqFtp.Credentials = new NetworkCredential(_ftpUserName, _ftpPasswd);
            var response = (FtpWebResponse)reqFtp.GetResponse();
            Stream ftpStream = response.GetResponseStream();
            try
            {
                const int bufferSize = 2048;
                var buffer = new byte[bufferSize];
                if (ftpStream != null)
                {
                    int readCount = ftpStream.Read(buffer, 0, bufferSize);
                    while (readCount > 0)
                    {
                        outputStream.Write(buffer, 0, readCount);
                        readCount = ftpStream.Read(buffer, 0, bufferSize);
                    }
                }
            }
            finally
            {
                if (ftpStream != null) ftpStream.Close();
                outputStream.Close();
                response.Close();
            }

        }


        /// <summary>
        /// 上传多文件
        /// </summary>
        /// <param name="filePaths">文件路径集合</param>
        /// <param name="targetDir">目标文件夹</param>
        /// <returns></returns>
        public List<FileResult> UploadFileList(string[] filePaths, string targetDir)
        {
            List<FileResult> list = new List<FileResult>();
            if (filePaths != null && filePaths.Length > 0)
            {
                using (FtpClient ftpClient = new FtpClient())
                {
                    SetCredentials(ftpClient);
                    foreach (string filePath in filePaths)
                    {
                        list.Add(UploadByFtpClient(filePath, targetDir, ftpClient));
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 上传文件,指定上传目标文件名
        /// </summary>
        /// modified by wyq on 2017-3-27
        /// <param name="filePath">待上传的文件路径</param>
        /// <param name="targetDir">目标文件夹</param>
        /// <param name="ftpClient">ftp连接池</param>
        public FileResult UploadByFtpClient(string filePath, string targetDir, FtpClient ftpClient)
        {
            if (string.IsNullOrEmpty(filePath))
                return new FileResult(false, "上传失败，文件路径为空。", string.Empty);

            var targetName = Path.GetFileName(filePath);//目标文件名

            //拼接上传目标文件路径
            string uploadPath = targetDir + "/" + targetName;

            //默认上传数据格式为二进制数据
            FtpDataType type = FtpDataType.Binary;
            //当文件类型为cs或txt文件时，用ASCII格式作为数据传输格式
            if (Path.GetExtension(filePath).ToLower() == ".cs" || Path.GetExtension(filePath).ToLower() == ".txt")
                type = FtpDataType.ASCII;
            //判断目标文件夹是否存在
            if (!ftpClient.DirectoryExists(targetDir.Trim()))
                ftpClient.CreateDirectory(targetDir, true);
            //定义上传文件为数据流
            Stream istream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            //定义目标文件数据流
            Stream ostream = ftpClient.OpenWrite(uploadPath, type);
            //定义每次读取的字节流长度
            const int buffLength = 2048;
            //定义读取的数据容器
            byte[] buf = new byte[buffLength];
            //获取本次读取的数据长度
            int contentLen = istream.Read(buf, 0, buffLength);
            long hasUpLoad = contentLen;
            try
            {
                //循环写入目标数据，直到读取到的数据流长度为0
                while (contentLen != 0)
                {
                    //将已读取的数据写入目标文件
                    ostream.Write(buf, 0, contentLen);
                    //从上次读取结束的位置开始重新读取数据
                    contentLen = istream.Read(buf, 0, buffLength);
                    //记录已经读取的数据长度
                    hasUpLoad += contentLen;
                }
            }
            catch (Exception)
            {
                return new FileResult(false, string.Format("上传{0}时连接不上远程服务器!\n", filePath), string.Empty);
            }
            finally
            {
                ostream.Close();
                istream.Close();
            }

            return new FileResult(true, string.Empty, uploadPath, uploadPath);
        }

        /// <summary>
        /// 判断本地文件是不是比服务器文件新
        /// add by ljc 2016-04-20
        /// </summary>
        /// <param name="serverFile">服务器文件</param>
        /// <param name="localFile">本地文件</param>
        /// <returns></returns>
        public bool IsLocalFileNewerThanRemoteFile(string serverFile, string localFile, FtpClient conn)
        {
            serverFile = serverFile.Replace(_ftpurl, "/");

            var serverFileModifiedTime = conn.GetModifiedTime(serverFile);
            var localFileModifiedTime = (new FileInfo(localFile)).LastWriteTime;
            if (serverFileModifiedTime < localFileModifiedTime)
            {
                return true;
            }
            return false;
        }

        public void SetCredentials(FtpClient conn)
        {
            string tempUrl = _ftpurl.Split(':')[1];
            //ftpClient的Host格式为“192.168.10.214”这种格式，默认端口是21
            conn.Host = tempUrl.Substring(2);
            conn.Credentials = new NetworkCredential(_ftpUserName, _ftpPasswd);
        }

    }
}
