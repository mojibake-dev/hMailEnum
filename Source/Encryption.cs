
// File: RijndaelIvEncryption.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HMailTools.Security
{
    public static class RijndaelIvEncryption
    {
        private const string NotSecretKey = "THIS_KEY_IS_NOT_SECRET";
        private static readonly byte[] Salt = new byte[]
        { 0x49,0x76,0x61,0x6e,0x20,0x4d,0x65,0x64,0x76,0x65,0x64,0x65,0x76 };

        public static string Encrypt(string plainText)
        {
            byte[] plainBytes = Encoding.Unicode.GetBytes(plainText);
            var pdb = new PasswordDeriveBytes(NotSecretKey, Salt);
            using var rij = Rijndael.Create();
            rij.Key = pdb.GetBytes(32);
            rij.IV  = pdb.GetBytes(16);

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, rij.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.Close();
            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string encryptedText)
        {
            byte[] cipherBytes = Convert.FromBase64String(encryptedText);
            var pdb = new PasswordDeriveBytes(NotSecretKey, Salt);
            using var rij = Rijndael.Create();
            rij.Key = pdb.GetBytes(32);
            rij.IV  = pdb.GetBytes(16);

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, rij.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(cipherBytes, 0, cipherBytes.Length);
            cs.Close();
            return Encoding.Unicode.GetString(ms.ToArray());
        }
    }
}