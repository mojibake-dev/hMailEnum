// File: MD5OnlineCracker.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HMailTools.Security
{
    public static class MD5OnlineCracker
    {
        static readonly string[] LookupUrls = new[]
        {
            "https://api.hashify.net/hash/md5/hex?value=",
            "https://www.md5-hash.com/?hash=",
            "https://md5.gromweb.com/?md5=",
            "https://hashes.com/en/decrypt/hash?hash=",
            "https://hashkiller.co.uk/md5-decrypter.aspx?md5=",
            "https://md5decrypt.net/en/HeavyHashDecryptor?hash=",
            "https://md5hashing.net/hash/md5/",
            "https://cmd5.com/?md5=",
            "https://hash.online-convert.com/md5-decrypt?value=",
            "https://www.nitrxgen.net/md5db/"
        };

        public static string CrackHash(string targetHash, int maxParallel = 5, TimeSpan? timeout = null)
            => CrackHashAsync(targetHash, maxParallel, timeout).GetAwaiter().GetResult();

        public static async Task<string> CrackHashAsync(
            string targetHash,
            int maxParallel = 5,
            TimeSpan? timeout = null)
        {
            targetHash = targetHash.Trim().ToLowerInvariant();
            using var http = new HttpClient
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(3)
            };
            // spoof a real browser
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/115.0 Safari/537.36");

            var regexSplit = new Regex(@"[^\w]+", RegexOptions.Compiled);
            var sem = new SemaphoreSlim(maxParallel);
            var tasks = new List<Task<string>>();

            foreach (var prefix in LookupUrls)
            {
                await sem.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var url = prefix + targetHash;
                        var resp = await http.GetAsync(url);
                        if (!resp.IsSuccessStatusCode) return null;

                        var body = await resp.Content.ReadAsStringAsync();

                        // Special-case JSON from api.hashify.net
                        if (prefix.StartsWith("https://api.hashify"))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(body);
                                if (doc.RootElement.TryGetProperty("Digest", out var dig))
                                {
                                    var candidate = dig.GetString();
                                    if (candidate is not null &&
                                        candidate.Equals(targetHash, StringComparison.OrdinalIgnoreCase))
                                        return candidate;
                                }
                            }
                            catch { }
                        }

                        // Direct plaintext responses (nitrxgen, etc.)
                        var trimmed = body.Trim();
                        if (trimmed.Length <= 32 && IsAscii(trimmed))
                        {
                            if (ComputeMD5(trimmed) == targetHash)
                                return trimmed;
                        }

                        // Otherwise split into tokens
                        var tokens = regexSplit.Split(body)
                                               .Where(t => t.Length > 0 && t.Length <= 32)
                                               .Distinct();

                        foreach (var tok in tokens)
                        {
                            if (!IsAscii(tok)) continue;
                            if (ComputeMD5(tok) == targetHash)
                                return tok;
                        }
                    }
                    catch { /* ignore */ }
                    finally { sem.Release(); }
                    return null;
                }));
            }

            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks);
                tasks.Remove(done);
                var result = await done;
                if (result != null) return result;
            }

            return null;
        }

        static bool IsAscii(string s) => Encoding.UTF8.GetByteCount(s) == s.Length;

        static string ComputeMD5(string input)
        {
            using var md5 = MD5.Create();
            var bytes = Encoding.ASCII.GetBytes(input);
            var hash  = md5.ComputeHash(bytes);
            var sb    = new StringBuilder(32);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
