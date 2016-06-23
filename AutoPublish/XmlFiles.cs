using System.Xml;

namespace AutoPublish
{
    /// <summary>
    /// 处理XML文件 
    /// </summary>
    public sealed class XmlFiles : XmlDocument
    {
        public string XmlFileName { get; set; }
        public XmlFiles(string xmlFile)
        {
            XmlFileName = xmlFile;
            Load(xmlFile);
        }

        /// <summary>
        /// 给定一个节点的xPath表达式并返回一个节点
        /// </summary>
        /// <param name="xPath"></param>
        /// <returns></returns>
        public XmlNode FindNode(string xPath)
        {
            var xmlNode = SelectSingleNode(xPath);
            return xmlNode;
        }

        /// <summary>
        /// 给定一个节点的xPath表达式返回其值
        /// </summary>
        /// <param name="xPath"></param>
        /// <returns></returns>
        public string GetNodeValue(string xPath)
        {
            var xmlNode = SelectSingleNode(xPath);
            return xmlNode != null ? xmlNode.InnerText : string.Empty;
        }

        /// <summary>
        /// 给定一个节点的表达式返回此节点下的孩子节点列表
        /// </summary>
        /// <param name="xPath"></param>
        /// <returns></returns>
        public XmlNodeList GetNodeList(string xPath)
        {
            var node = SelectSingleNode(xPath);
            if (node == null) return null;
            var nodeList = node.ChildNodes;
            return nodeList;
        }
    }
}