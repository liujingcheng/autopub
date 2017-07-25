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
        public void Publish(string needUpdateXmlFilePath, string filesHasToCopyPath)
        {
            if (filesHasToCopyPath == null || needUpdateXmlFilePath == null)
            {
                Console.WriteLine("配置文件里读取不到要更新的xml文件地址或要更新的文件列表容器地址！");
                return;
            }

            var rFile = new FileStream(filesHasToCopyPath, FileMode.Open);
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
                Common.ModifyXmlFile(needUpdateXmlFilePath, lineStr);
            }

            Console.WriteLine("WPF端XML文件更新完成！");
        }
    }
}
