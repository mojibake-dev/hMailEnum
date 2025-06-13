// File: IniProcessor.cs
using System;
using System.IO;
using HMailTools.Crypto;       // for BlowFishEncryptor
using HMailTools.Security;     // for MD5OnlineCracker
using HMailTools.Security;     // for RijndaelIvEncryption (if still needed elsewhere)

namespace HMailTools.Utilities
{
    public static class IniProcessor
    {
        /// <summary>
        /// In‐place processes the INI:
        ///  - Decrypts DatabasePassword (hex→plaintext via BlowFish).
        ///  - Cracks AdministratorPassword (MD5 hash→plaintext via MD5OnlineCracker).
        /// </summary>
        public static void ProcessIni(string iniPath)
        {
            var lines = File.ReadAllLines(iniPath);
            for (int i = 0; i < lines.Length; i++)
            {
                // 1) BlowFish decrypt the DB password
                if (lines[i].StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                {
                    var hex = lines[i].Substring("Password=".Length);
                    var dbPlain = BlowFishEncryptor.DecryptFromHex(hex);
                    lines[i] = "Password=" + dbPlain;
                }
                // 2) Crack the AdminPassword from its MD5 hash
                else if (lines[i].StartsWith("AdministratorPassword=", StringComparison.OrdinalIgnoreCase))
                {
                    var md5Hash = lines[i].Substring("AdministratorPassword=".Length);
                    // Use the online cracker to recover the original password
                    var adminPlain = MD5OnlineCracker.CrackHash(md5Hash);
                    lines[i] = "AdministratorPassword=" + (adminPlain ?? string.Empty);
                }
            }
            File.WriteAllLines(iniPath, lines);
        }

        /// <summary>
        /// Reads the (already‐decrypted) DatabasePassword from a processed INI.
        /// </summary>
        public static string GetDatabasePassword(string iniPath)
        {
            foreach (var line in File.ReadLines(iniPath))
            {
                if (line.StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                    return line.Substring("Password=".Length);
            }
            throw new InvalidDataException("Password not found in INI.");
        }
    }
}
