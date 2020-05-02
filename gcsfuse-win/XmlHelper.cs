/**
 * @copyright wesley wu 
 * @email jie1975.wu@gmail.com
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace gcsfuse_win
{
    /// <summary>
    /// 读取xml的配置
    /// </summary>
    class XmlHelper
    {
        private string _filePath;
        public XmlDocument Xmldoc;
        public XmlHelper(String filePath)
        {
            this._filePath = AppDomain.CurrentDomain.BaseDirectory + filePath;
            Xmldoc = processFile(this._filePath);
        }


        /// <summary>
        /// Reads file and returns a Xml Document
        /// </summary>
        /// <param name="xmlFilePath"></param>
        /// <returns></returns>
        public XmlDocument processFile(String xmlFilePath)
        {
            if (xmlFilePath != null && File.Exists(xmlFilePath))
            {
                try
                {
                    XmlDocument tempdoc = new XmlDocument();
                    tempdoc.Load(xmlFilePath);
                    return tempdoc;
                }

                catch (Exception)
                {
                    // TODO: Publish an event.
                    throw (new System.Exception($"Error Loading Xml File: {xmlFilePath}."));
                }
            }

            else
            {
                throw (new System.Exception($"File: {xmlFilePath} not found!"));
            }
        }

        /// <summary>
        /// Get the root node of a XML document
        /// </summary>
        /// <returns>XmlNode : Root node</returns>
        public XmlNode getRootNode()
        {
            if (Xmldoc.HasChildNodes)
            {
                return Xmldoc.FirstChild;
            }

            else return Xmldoc.ParentNode;
        }

        /// <summary>
        /// Counts the number of nodes matching the specified xPath
        /// </summary>
        /// <param name="xPath"></param>
        /// <returns>No. of nodes found</returns>
        public int getNumberOfNodes(String xPath)
        {
            int nodeCount = Xmldoc.SelectNodes(xPath).Count;
            return nodeCount;
        }

        /// <summary>
        /// Gets the no. of child nodes of a specified node whose xPath is mentioned. NOTE: Only the first child
        /// node that is found is considered for the count.
        /// </summary>
        /// <param name="xPath"></param>
        /// <returns>No. of child nodes for a certain node</returns>
        public int getNumberOfChildNodes(String xPath)
        {
            XmlNode xNode = Xmldoc.SelectSingleNode(xPath);
            int childNodeCount = xNode.ChildNodes.Count;
            return childNodeCount;
        }

        /// <summary>
        /// Retrieves XMLNodeList snippet from XML document matching the xPath
        /// </summary>
        /// <param name="xPath"></param>
        /// <returns>A list of XML Nodes matching the xPath</returns>
        public XmlNodeList retrieveXMLNodes(String xPath)
        {
            if (xPath != null || xPath != "")
            {
                XmlNodeList xnList = Xmldoc.SelectNodes(xPath);
                return xnList;
            }

            else
            {
                throw (new System.Exception("xPath is Empty!"));
            }
        }

        /// <summary>
        /// read xml child node into Dictionary
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeName"></param>
        /// <returns></returns>
        public Dictionary<string, string> retrieveXMLChildNodeByName(XmlNode node, string nodeName)
        {
            Dictionary<string, string> result = new Dictionary<string, string>() { };
            XmlNodeList childNodes = node.SelectNodes(nodeName);
            if (childNodes.Count == 0) { return result; }
            var children = childNodes[0].Cast<XmlNode>().Where(n => n.NodeType != XmlNodeType.Comment);
            foreach (XmlElement item in children)
            {
                string cleanTxt = item.InnerText.Trim().Replace("\t", string.Empty).
                    Replace("\r", string.Empty).Replace("\n", string.Empty);
                result.Add(item.Name, cleanTxt);
            }
            return result;
        }

        /// <summary>
        /// Retrieves a list of values from an XML Document based on the xPath
        /// </summary>
        /// <param name="xPath">The path of the node in the XML file for which the details are to be retrieved</param>
        /// <param name="result">A String list reference that is populated based on the retrieved values</param>
        /// <returns>True if retrieval is successful else returns false</returns>
        public bool retrieveValueListFromXML(String xPath, out List<String> result)
        {
            result = new List<String>();

            if (xPath != null || xPath != "")
            {
                try
                {
                    XmlNodeList xnList = Xmldoc.SelectNodes(xPath);
                    foreach (XmlNode xn in xnList)
                    {
                        result.Add(xn.InnerText);
                    }
                    return true;
                }

                catch (Exception e)
                {
                    throw (new System.Exception("Failed to load nodes from Xml Document! \n " + e.ToString()));
                }
            }

            else
            {
                throw (new System.Exception("xPath is Empty!"));
            }
        }

        /// <summary>
        /// Retrieves inner text from the first node encountered
        /// </summary>
        /// <param name="xPath">Xml node tree traversal path</param>
        /// <returns>String value retrieved from first encountered node</returns>
        public String retrieveValueForSingleNode(String xPath)
        {
            XmlNode xNode = null;
            String innerText = "";
            try
            {
                xNode = Xmldoc.SelectSingleNode(xPath);
                innerText = xNode.InnerText;
            }

            catch (Exception e)
            {
                throw (new Exception("Failed to load node from Xml Document! \n" + e.ToString()));
            }

            return innerText;
        }

        /// <summary>
        /// Given an XMLNode, it returns a dictionary
        /// containing the name value pairs of 
        /// the attributes of that node.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public Dictionary<String, String> getAttributes(XmlNode node)
        {
            XmlAttributeCollection xa = node.Attributes;
            Dictionary<String, String> result = new Dictionary<string, string>();

            foreach (XmlAttribute x in xa)
            {
                result.Add(x.Name, x.Value);
            }

            return result;
        }

    }
}
