using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace AutoPublish
{
    public static class Common
    {
        public static void ModifyXmlFile(string xmlPath, string fileName)
        {
            if (string.IsNullOrEmpty(xmlPath) || string.IsNullOrEmpty(fileName))
            {
                throw new Exception("xmlPath或fileName为空");
            }

            if (fileName.ToLower().Contains("autoupdate"))
            {
                return;//autoupdate不能更新自身，所以也不能更新xml节点
            }

            var fileNodeName = fileName.Replace("\\", "/");
            var remoteXmlFile = new XmlFiles(xmlPath);
            var remoteNodeList = remoteXmlFile.GetNodeList("AutoUpdater/Files").Cast<XmlNode>().ToList();
            var lastNode = remoteNodeList.LastOrDefault();
            var fileNode =
                remoteNodeList.FirstOrDefault(
                    p =>
                        p.Attributes != null && p.Attributes["Name"] != null &&
                        p.Attributes["Name"].Value.Trim().EndsWith(fileNodeName));

            if (fileNode != null)//更新节点
            {
                var versionStr = fileNode.Attributes["Ver"].Value;
                var versionTempStr = versionStr.Replace(".", "");
                int tempInt = 0;
                if (int.TryParse(versionTempStr, out tempInt))
                {
                    tempInt++;
                    var tempChars = tempInt.ToString().ToCharArray();
                    string convertedStr = tempChars.Aggregate("", (current, tempChar) => current + (tempChar + "."));
                    fileNode.Attributes["Ver"].Value = convertedStr.TrimEnd('.');
                    remoteXmlFile.Save(xmlPath);
                    Console.WriteLine("更新xml节点：" + fileName);
                }
            }
            else//新增节点
            {
                if (lastNode != null && lastNode.Attributes != null)
                {
                    fileNode = lastNode.Clone();
                    fileNode.Attributes["Name"].Value = fileNodeName;
                    fileNode.Attributes["Ver"].Value = "1.0.0.0";
                    if (lastNode.ParentNode != null)
                    {
                        lastNode.ParentNode.AppendChild(fileNode);
                        remoteXmlFile.Save(xmlPath);
                        Console.WriteLine("新增xml节点：" + fileName);
                    }
                }
                else
                {
                    Console.WriteLine("取不到样例xml文件里的File节点");
                    Console.ReadLine();
                }
            }
        }
    }
}
