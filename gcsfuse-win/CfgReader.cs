/**
 * @copyright wesley wu 
 * @email jie1975.wu@gmail.com
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace gcsfuse_win
{
    class CfgReader
    {
        //xml operator
        private XmlHelper XmlHelper;

        public CfgReader(string filePath)
        {
            XmlHelper = new XmlHelper(filePath);

        }

        /// <summary>
        /// read the configuration
        /// </summary>
        /// <returns></returns>
        public Config readCfg()
        {
            Config cfg = new Config();
            //get class type
            Type type = typeof(Config);
            Dictionary<string, string> rawSettings = new Dictionary<string, string>();
            //read configuration
            XmlNodeList nodes = XmlHelper.retrieveXMLNodes("GcsFuse/Common/*");
            foreach (XmlNode node in nodes)
            {
                string cleanTxt = node.InnerText.Trim().Replace("\t", string.Empty).
                                        Replace("\r", string.Empty).Replace("\n", string.Empty);
                rawSettings.Add(node.Name, cleanTxt);
            }
            //set value
            foreach (var pair in rawSettings)
            {
                string value = pair.Value;
                PropertyInfo propertyInfo = type.GetProperty(pair.Key);
                if (propertyInfo.PropertyType == typeof(int))
                {
                    propertyInfo.SetValue(cfg, int.Parse(value), null);
                }
                else
                {
                    propertyInfo.SetValue(cfg, value, null);
                }
            }
            //return
            return cfg;
        }
    }
}
