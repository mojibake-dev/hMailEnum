// File: RegistryPaths.cs
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace HMailTools.Registry
{
    public static class RegistryPaths
    {
        /// <summary>
        /// Reads the InstallLocation from HKLM\SOFTWARE\hMailServer (64- and 32-bit views).
        /// Logs every step of the lookup.
        /// </summary>
        public static string GetInstallLocation()
        {
            Console.WriteLine("[Registry] Starting InstallLocation lookup...");

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                Console.WriteLine($"[Registry] Checking {view} view...");
                using var lm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = lm.OpenSubKey("SOFTWARE\\hMailServer", writable: false);
                if (key == null)
                {
                    Console.WriteLine($"[Registry] Key HKLM\\SOFTWARE\\hMailServer not found under {view}.");
                    continue;
                }

                Console.WriteLine($"[Registry] Found HKLM\\SOFTWARE\\hMailServer under {view}. Looking for InstallLocation value...");
                var val = key.GetValue("InstallLocation") as string;
                if (string.IsNullOrEmpty(val))
                {
                    Console.WriteLine($"[Registry] InstallLocation value missing or empty under {view}.");
                    continue;
                }

                Console.WriteLine($"[Registry] InstallLocation = \"{val}\" (from {view}).");
                return val;
            }

            Console.Error.WriteLine("[Registry] ERROR: hMailServer InstallLocation not found in any registry view.");
            return null;
        }

        /// <summary>
        /// Builds a dictionary of essential file paths based on the installDir.
        /// Logs each decision, existence check, and fallback.
        /// </summary>
        public static Dictionary<string, string> GetPaths(string installDir)
        {
            if (string.IsNullOrEmpty(installDir))
                throw new ArgumentException("installDir must be provided", nameof(installDir));

            Console.WriteLine($"[Paths] Using installDir: {installDir}");
            string binDir = Path.Combine(installDir, "Bin");
            Console.WriteLine($"[Paths] Computed binDir = {binDir}");

            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["hMailServer.ini"]       = Path.Combine(binDir, "hMailServer.ini"),
                ["hMailServer.sdf"]       = Path.Combine(binDir, "hMailServer.sdf"),
                ["hMailAdmin.exe.config"] = Path.Combine(binDir, "hMailAdmin.exe.config")
            };

            // Log the initial registry-based paths
            Console.WriteLine($"[Paths] Registry path for INI:    {paths["hMailServer.ini"]}");
            Console.WriteLine($"[Paths] Registry path for SDF:    {paths["hMailServer.sdf"]}");
            Console.WriteLine($"[Paths] Registry path for CONFIG: {paths["hMailAdmin.exe.config"]}");

            // INI fallback
            if (!File.Exists(paths["hMailServer.ini"]))
            {
                string iniFallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "hMailServer", "Bin", "hMailServer.ini");
                Console.WriteLine($"[Fallback] INI not found at registry location. Checking fallback: {iniFallback}");
                if (File.Exists(iniFallback))
                {
                    Console.WriteLine($"[Fallback] Found INI at fallback path.");
                    paths["hMailServer.ini"] = iniFallback;
                }
                else
                {
                    Console.Error.WriteLine($"[Fallback] INI fallback also missing: {iniFallback}");
                }
            }

            // SDF fallback
            if (!File.Exists(paths["hMailServer.sdf"]))
            {
                string sdfFallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "hMailServer", "Database", "hMailServer.sdf");
                Console.WriteLine($"[Fallback] SDF not found at registry location. Checking fallback: {sdfFallback}");
                if (File.Exists(sdfFallback))
                {
                    Console.WriteLine($"[Fallback] Found SDF at fallback path.");
                    paths["hMailServer.sdf"] = sdfFallback;
                }
                else
                {
                    Console.Error.WriteLine($"[Fallback] SDF fallback also missing: {sdfFallback}");
                }
            }

            // CONFIG fallback
            string cfgKey = "hMailAdmin.exe.config";
            if (!File.Exists(paths[cfgKey]))
            {
                string primary = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Halvar Information", "hMailServer", "hMailAdmin.exe.config");
                Console.WriteLine($"[Fallback] Config not found at registry location. Checking current user AppData: {primary}");
                if (File.Exists(primary))
                {
                    Console.WriteLine($"[Fallback] Found Config in current user AppData.");
                    paths[cfgKey] = primary;
                }
                else
                {
                    Console.WriteLine($"[Fallback] Not found in current user AppData. Scanning other user profiles...");
                    string usersRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Users");
                    if (Directory.Exists(usersRoot))
                    {
                        foreach (var userDir in Directory.GetDirectories(usersRoot))
                        {
                            string attempt = Path.Combine(userDir, "AppData", "Local",
                                "Halvar Information", "hMailServer", "hMailAdmin.exe.config");
                            Console.WriteLine($"[Fallback] Checking: {attempt}");
                            if (File.Exists(attempt))
                            {
                                Console.WriteLine($"[Fallback] Found Config under user profile: {attempt}");
                                paths[cfgKey] = attempt;
                                break;
                            }
                        }
                    }
                }
            }

            // Final resolved paths
            Console.WriteLine($"[Paths] Final INI path:    {paths["hMailServer.ini"]}");
            Console.WriteLine($"[Paths] Final SDF path:    {paths["hMailServer.sdf"]}");
            Console.WriteLine($"[Paths] Final CONFIG path: {paths["hMailAdmin.exe.config"]}");

            return paths;
        }
    }
}
