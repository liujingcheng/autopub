namespace CanYou.WPF.Common.Controls.FileUploadUC.FileOperations
{
    /// <summary>
    /// 文件上传下载返回信息
    /// </summary>
    public class FileResult
    {
        /// <summary>
        /// 文件上传返回信息
        /// </summary>
        /// <param name="state">上传状态 true：上传成功；false：上传失败</param>
        /// <param name="failureMessage">失败原因</param>
        /// <param name="url">上传成功后返回有路径</param>
        /// <param name="path">上传成功后返回相对路径</param>
        public FileResult(bool state, string failureMessage, string url,string path = "")
        {
            State = state;
            FailureMessage = failureMessage;
            Url = url ;
            Path = path;
        }

        public FileResult(){ }

        /// <summary>
        /// 上传状态 true：上传成功；false：上传失败
        /// </summary>
        public bool State { get; set; }

        /// <summary>
        /// 失败原因
        /// </summary>
        public string FailureMessage { get; set; }

        /// <summary>
        /// 上传或下载成功后返回有路径
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 上传或下载成功后返回相对路径
        /// </summary>
        public string Path { get; set; }
    }
}
