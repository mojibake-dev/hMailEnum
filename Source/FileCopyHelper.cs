// File: FileCopyHelper.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace HMailTools.Utilities
{
    public static class FileCopyHelper
    {
        public static Dictionary<string, string> CopyRegistryFilesToWorkingDirectory(
            IDictionary<string, string> registryPaths,
            string workingDirectory = null)
        {
            if (registryPaths == null)
                throw new ArgumentNullException(nameof(registryPaths));

            workingDirectory ??= Environment.CurrentDirectory;
            Directory.CreateDirectory(workingDirectory);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in registryPaths)
            {
                string key        = kv.Key;
                string sourcePath = kv.Value;
                string fileName   = Path.GetFileName(sourcePath);
                string destPath   = Path.Combine(workingDirectory, fileName);

                Console.WriteLine($"[Copy] Key '{key}'");
                Console.WriteLine($"       Source:      {sourcePath}");
                Console.WriteLine($"       Destination: {destPath}");

                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    Console.Error.WriteLine($"[Warning] Source not found for '{key}': {sourcePath}");
                    continue;
                }

                try
                {
                    if (key.Equals("hMailServer.sdf", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[Copy] Attempting Robocopy for SDF...");
                        var psi = new ProcessStartInfo
                        {
                            FileName               = "robocopy",
                            Arguments              = $"\"{Path.GetDirectoryName(sourcePath)}\" \"{workingDirectory}\" \"{fileName}\" /B /NFL /NDL /NJH /NJS",
                            UseShellExecute        = false,
                            CreateNoWindow         = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true
                        };
                        using var proc = Process.Start(psi);
                        if (proc == null) throw new Exception("Could not start robocopy.");
                        proc.WaitForExit();
                        int exit = proc.ExitCode;

                        if (exit < 8 && File.Exists(destPath))
                        {
                            Console.WriteLine("[Copy] SDF copied via Robocopy.");
                        }
                        else
                        {
                            Console.Error.WriteLine($"[Warning] Robocopy failed (code {exit}), falling back to File.Copy");
                            File.Copy(sourcePath, destPath, overwrite: true);
                            Console.WriteLine("[Copy] SDF copied via File.Copy fallback.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Copy] Copying '{key}' via File.Copy");
                        File.Copy(sourcePath, destPath, overwrite: true);
                        Console.WriteLine($"[Copy] '{key}' copied successfully.");
                    }

                    if (File.Exists(destPath))
                        result[key] = destPath;
                    else
                        Console.Error.WriteLine($"[Error] After copy, destination missing: {destPath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Error] Copy failed for '{key}': {ex.Message}");
                }
            }

            return result;
        }
    }
}
