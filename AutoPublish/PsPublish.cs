using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

namespace AutoPublish
{
    public class PsPublish
    {
        private readonly string _needUpdateFilesContainerPath = ConfigurationManager.AppSettings["NeedUpdateFiles"];
        private readonly string _needUpdateXmlFilePath = ConfigurationManager.AppSettings["NeedUpdateXmlFilePath"];
        public void Publish()
        {
            if (_needUpdateFilesContainerPath == null || _needUpdateXmlFilePath == null)
            {
                Console.WriteLine("配置文件里读取不到要更新的xml文件地址或要更新的文件列表容器地址！");
                return;
            }

            var rFile = new FileStream(_needUpdateFilesContainerPath, FileMode.Open);
            var sr = new StreamReader(rFile, Encoding.GetEncoding("utf-8"));

            var listStr = new List<string>();
            while (!sr.EndOfStream)
            {
                var str = sr.ReadLine();
                listStr.Add(str);
            }
            sr.Close();

            foreach (var lineStr in listStr)
            {
                Common.ModifyXmlFile(_needUpdateXmlFilePath, lineStr);
            }

            Console.WriteLine("WPF端XML文件更新完成！");
        }
    }
}
