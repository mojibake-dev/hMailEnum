// File: SdfToSqliteConverter.cs
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace HMailTools.Utilities
{
    public static class SdfToSqliteConverter
    {
        /// <summary>
        /// Converts a local .sdf to a SQLite .db by:
        /// 1) extracting ExportSqlCe.exe (if zipped),
        /// 2) dumping to .sql via ExportSqlCe.exe,
        /// 3) executing that .sql into a new SQLite DB (Microsoft.Data.Sqlite).
        /// </summary>
        public static void ConvertLocalSdfToSqlite(
            string sdfPath,
            string sqliteDbPath,
            string password,
            string converterExePath = "ExportSqlCe.exe")
        {
            // 0) Initialize the bundled e_sqlite3 provider
            Batteries_V2.Init();

            // 1) Unzip & locate ExportSqlCe.exe
            const string zipName = "ExportSql.zip", extractDir = "ExportSql";
            if (File.Exists(zipName))
            {
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipName, extractDir);

                // recursive search for the real exe
                var exe = Directory.GetFiles(extractDir, "ExportSqlCe.exe", SearchOption.AllDirectories);
                if (exe.Length == 0)
                    throw new FileNotFoundException("ExportSqlCe.exe not found in ExportSql.zip");
                converterExePath = exe[0];
                Console.WriteLine($"[Converter] Using {converterExePath}");
            }

            // 2) Validate the SDF
            if (string.IsNullOrEmpty(sdfPath) || !File.Exists(sdfPath))
                throw new FileNotFoundException("Source .sdf not found.", sdfPath);

            // 3) Ensure output folder
            var dbDir = Path.GetDirectoryName(sqliteDbPath);
            if (!string.IsNullOrEmpty(dbDir)) Directory.CreateDirectory(dbDir);

            // 4) Dump to .sql
            string scriptPath = Path.ChangeExtension(sqliteDbPath, ".sql");
            string connStr = $"\"Data Source={sdfPath};Password={password};\"";
            var psi = new ProcessStartInfo(converterExePath,
                                           $"{connStr} \"{scriptPath}\" sqlite")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardError  = true
            };
            using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException($"Cannot start {converterExePath}");
            var err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new Exception($"ExportSqlCe.exe failed ({proc.ExitCode}): {err}");

            // 5) Execute the SQL into a fresh SQLite DB
            if (File.Exists(sqliteDbPath)) File.Delete(sqliteDbPath);

            using var conn = new SqliteConnection($"Data Source={sqliteDbPath}");
            conn.Open();
            string sql = File.ReadAllText(scriptPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            conn.Close();
        }
    }
}
