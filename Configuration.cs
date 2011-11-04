using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Web;

namespace AngiesList.Redis
{
    internal class KeyValueStoreConfiguration
    {
        #region Constants

        protected const string CACHE_KEY = "KeyValueStoreConfiguration";
        protected const string SETTINGS_SECTION = "KeyValueStore/Master";
        protected const string CONFIG_FILE = @"KeyValueStore.config";
        #endregion Constants

        private KeyValueStoreConfiguration(XmlDocument xmlDoc)
        {
            var node = xmlDoc.SelectSingleNode(SETTINGS_SECTION);
            Host = node.Attributes["host"].Value;
            Port = Int32.Parse( node.Attributes["port"].Value );
        }

        #region Config Properties

        public string Host { get; internal set; }
        public int Port { get; internal set; }
        #endregion Config Properties

        private static string _path = CONFIG_FILE;
        private static KeyValueStoreConfiguration _config = null;

        public static KeyValueStoreConfiguration GetConfig(string path = CONFIG_FILE)
        {
            if (_config == null || _path != path)
            {
                _path = path;

                string fullPath;
                if (HttpContext.Current == null)
                {
                    fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
                }
                else
                {
                    fullPath = Path.Combine(HttpContext.Current.Server.MapPath("~/"), path);
                }
                
                var xmlDoc = LoadXmlDocFromPath(fullPath);
                SetUpFileWatcher(fullPath);
                _config = new KeyValueStoreConfiguration(xmlDoc);
            }
            return _config;
        }

        public static void SetConfigPath(string path)
        {
            _path = path;
        }

        private static XmlDocument LoadXmlDocFromPath(string path)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(path);
            return xmlDoc;
        }

        #region File Watcher logic

        private static void SetUpFileWatcher(string fullPath)
        {
            string dir = Path.GetDirectoryName(fullPath),
                   file = Path.GetFileName(fullPath);
            var watcher = new FileSystemWatcher(dir, file);
            watcher.EnableRaisingEvents = true;
            watcher.Changed += new FileSystemEventHandler((obj, args) => {
                _config = null;
                //KeyValueStoreConfiguration.OnConfigChange();
            });
        }

        //public delegate void ConfigChange();
        //public static event ConfigChange OnConfigChange;

        #endregion File Watcher logic
    }
}
