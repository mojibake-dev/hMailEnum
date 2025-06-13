// File: ConfigProcessor.cs
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HMailTools.Security;  // RijndaelIvEncryption

namespace HMailTools.Utilities
{
    public static class ConfigProcessor
    {
        public static void ProcessConfig(string configPath)
        {
            var doc = XDocument.Load(configPath);

            var nodes = doc
                .Descendants()
                .Where(e => string.Equals(
                    e.Name.LocalName,
                    "encryptedPassword",
                    StringComparison.OrdinalIgnoreCase));

            foreach (var elem in nodes)
            {
                var cipher = elem.Value.Trim();
                if (!string.IsNullOrEmpty(cipher))
                {
                    var plain = RijndaelIvEncryption.Decrypt(cipher);
                    elem.Value = plain;
                }
            }

            doc.Save(configPath);
        }
    }
}
