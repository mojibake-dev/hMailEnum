// File: BlowFishEncryptor.cs
using System;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;

namespace HMailTools.Crypto
{
    public static class BlowFishEncryptor
    {
        // ANSI bytes of the ASCII key "THIS_KEY_IS_NOT_SECRET"
        static readonly byte[] KeyBytes = Encoding.ASCII.GetBytes("THIS_KEY_IS_NOT_SECRET");

        // Reverse byte order within each 4-byte word to match C++ little-endian DWORD handling
        private static byte[] ReverseBytesInWords(byte[] block)
        {
            var swapped = new byte[block.Length];
            for (int i = 0; i < block.Length; i += 4)
            {
                swapped[i + 0] = block[i + 3];
                swapped[i + 1] = block[i + 2];
                swapped[i + 2] = block[i + 1];
                swapped[i + 3] = block[i + 0];
            }
            return swapped;
        }

        /// <summary>
        /// Encrypts an ASCII plaintext string to a lowercase hex string.
        /// Zero-pads to 8-byte blocks and uses pure ECB mode.
        /// </summary>
        public static string EncryptToHex(string plainText)
        {
            // 1) Get ASCII bytes and pad to 8-byte boundary
            byte[] data = Encoding.ASCII.GetBytes(plainText);
            int paddedLen = ((data.Length + 7) / 8) * 8;
            var buffer = new byte[paddedLen];
            Array.Copy(data, buffer, data.Length);

            // 2) Initialize Blowfish engine in ECB mode
            var engine = new BlowfishEngine();
            engine.Init(true, new KeyParameter(KeyBytes));

            // 3) Process each 8-byte block with byte-order reversal
            var output = new byte[paddedLen];
            for (int off = 0; off < paddedLen; off += 8)
            {
                var block = new byte[8];
                Array.Copy(buffer, off, block, 0, 8);

                // Convert little-endian DWORDs → big-endian for BouncyCastle
                var beBlock = ReverseBytesInWords(block);

                // Encrypt one block
                var encBlock = new byte[8];
                engine.ProcessBlock(beBlock, 0, encBlock, 0);

                // Convert back to little-endian DWORD byte order
                var leBlock = ReverseBytesInWords(encBlock);
                Array.Copy(leBlock, 0, output, off, 8);
            }

            // 4) Hex-encode (lowercase)
            return Hex.ToHexString(output).ToLowerInvariant();
        }

        /// <summary>
        /// Decrypts a lowercase hex string (from EncryptToHex) back to the ASCII plaintext.
        /// </summary>
        public static string DecryptFromHex(string hexCipher)
        {
            // 1) Hex → bytes
            byte[] input = Hex.Decode(hexCipher);

            // 2) Initialize Blowfish engine for decryption
            var engine = new BlowfishEngine();
            engine.Init(false, new KeyParameter(KeyBytes));

            // 3) Process each 8-byte block with byte-order reversal
            var output = new byte[input.Length];
            for (int off = 0; off < input.Length; off += 8)
            {
                var block = new byte[8];
                Array.Copy(input, off, block, 0, 8);

                // Bring into big-endian DWORD order
                var beBlock = ReverseBytesInWords(block);

                // Decrypt one block
                var decBlock = new byte[8];
                engine.ProcessBlock(beBlock, 0, decBlock, 0);

                // Convert back to little-endian DWORD byte order
                var leBlock = ReverseBytesInWords(decBlock);
                Array.Copy(leBlock, 0, output, off, 8);
            }

            // 4) Trim trailing zero bytes (padding)
            int actualLen = output.Length;
            while (actualLen > 0 && output[actualLen - 1] == 0) actualLen--;

            // 5) ASCII-decode
            return Encoding.ASCII.GetString(output, 0, actualLen);
        }
    }
}
