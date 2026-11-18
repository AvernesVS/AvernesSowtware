using System;
using System.IO;
using System.IO.Compression;

internal static class ReleasePackager
{
    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(destination);
        string[] files = Directory.GetFiles(source);
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".pyo", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".build.dll", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.StartsWith("Release", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(files[i], Path.Combine(destination, name), true);
        }
        string[] folders = Directory.GetDirectories(source);
        for (int i = 0; i < folders.Length; i++)
        {
            string folderName = Path.GetFileName(folders[i]);
            if (String.Equals(folderName, "__pycache__", StringComparison.OrdinalIgnoreCase)) continue;
            CopyDirectory(folders[i], Path.Combine(destination, folderName));
        }
    }

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("Usage: ReleasePackager.exe <project-root> <output-zip> <stage-name>");
            return 2;
        }
        string root = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        string stageName = args[2];
        string stage = Path.Combine(Path.GetTempPath(), "AvernesSowtware_" + Guid.NewGuid().ToString("N"));
        try
        {
            if (!File.Exists(Path.Combine(root, "dist", "AvernesSowtware.dll")))
                throw new FileNotFoundException("The compiled trainer DLL is missing; package was not created.");
            Directory.CreateDirectory(stage);
            string archiveRootName = "AvernesSowtware_v" + stageName;
            string packageRoot = Path.Combine(stage, archiveRootName);
            Directory.CreateDirectory(packageRoot);
            string[] dirs = new string[] { "src", "build", "package", "docs", "dist", "assets" };
            for (int i = 0; i < dirs.Length; i++) CopyDirectory(Path.Combine(root, dirs[i]), Path.Combine(packageRoot, dirs[i]));
            string[] files = new string[] {
                "AvernesSowtware.csproj", "AvernesSowtware.sln", "BUILD_RELEASE.bat", "START_HERE_RU.txt",
                "Install_AvernesSowtware.bat", "Uninstall_AvernesSowtware.bat", "README_RU.md",
                "BUILD_RELEASE_REPORT_RU.txt"
            };
            for (int i = 0; i < files.Length; i++)
            {
                string source = Path.Combine(root, files[i]);
                if (File.Exists(source)) File.Copy(source, Path.Combine(packageRoot, files[i]), true);
            }
            if (File.Exists(output)) File.Delete(output);
            string parent = Path.GetDirectoryName(output);
            if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
            using (FileStream archiveStream = new FileStream(output, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
            {
                string[] stagedFiles = Directory.GetFiles(stage, "*", SearchOption.AllDirectories);
                Array.Sort(stagedFiles, StringComparer.OrdinalIgnoreCase);
                string installerFirst = Path.Combine(packageRoot, "Install_AvernesSowtware.bat");
                archive.CreateEntryFromFile(installerFirst, archiveRootName + "/Install_AvernesSowtware.bat", CompressionLevel.Optimal);
                for (int i = 0; i < stagedFiles.Length; i++)
                {
                    if (String.Equals(stagedFiles[i], installerFirst, StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = stagedFiles[i].Substring(stage.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string entryName = relative.Replace('\\', '/');
                    archive.CreateEntryFromFile(stagedFiles[i], entryName, CompressionLevel.Optimal);
                }
            }
            EnsureInstallerFirst(output, archiveRootName);
            using (ZipArchive archive = ZipFile.OpenRead(output))
            {
                if (archive.Entries.Count == 0 || !String.Equals(archive.Entries[0].FullName.Replace('\\', '/'), archiveRootName + "/Install_AvernesSowtware.bat", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The unified installer must be the first ZIP entry.");
                string[] requiredEntries = new string[] {
                    archiveRootName + "/BUILD_RELEASE.bat", archiveRootName + "/START_HERE_RU.txt",
                    archiveRootName + "/Install_AvernesSowtware.bat", archiveRootName + "/Uninstall_AvernesSowtware.bat",
                    archiveRootName + "/src/Core/AvernesSowtwareScript.cs", archiveRootName + "/src/Core/RuntimeDiagnostics.cs",
                    archiveRootName + "/src/Core/HotkeyManager.cs", archiveRootName + "/src/Core/TeleportHistory.cs",
                    archiveRootName + "/src/Managers/VehicleSystems.cs",
                    archiveRootName + "/package/AvernesSowtware/hotkeys.json",
                    archiveRootName + "/docs/CHANGELOG_1_4_3_RU.md", archiveRootName + "/docs/TEST_REPORT_1_4_3_RU.md",
                    archiveRootName + "/dist/AvernesSowtware.dll",
                    archiveRootName + "/assets/AvernesSowtware.png",
                    archiveRootName + "/assets/AvernesSowtware.ico",
                    archiveRootName + "/docs/CHANGELOG_1_3_4_RU.md",
                    archiveRootName + "/docs/TEST_REPORT_1_3_4_RU.md",
                    archiveRootName + "/BUILD_RELEASE_REPORT_RU.txt"
                };
                for (int i = 0; i < requiredEntries.Length; i++)
                {
                    bool found = false;
                    for (int j = 0; j < archive.Entries.Count; j++)
                        if (String.Equals(archive.Entries[j].FullName.Replace('\\', '/'), requiredEntries[i], StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    if (!found) throw new InvalidDataException("ZIP verification failed; required entry is missing: " + requiredEntries[i]);
                }
            }
            FileInfo info = new FileInfo(output);
            Console.WriteLine("[OK] Package created and verified: " + output + " (" + info.Length + " bytes)");
            return info.Length > 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERROR] Package creation failed: " + ex.Message);
            if (File.Exists(output)) File.Delete(output);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { }
        }
    }


    private static void EnsureInstallerFirst(string output, string archiveRootName)
    {
        string temp = output + ".ordered.tmp";
        if (File.Exists(temp)) File.Delete(temp);
        try
        {
            using (ZipArchive source = ZipFile.OpenRead(output))
            {
                using (ZipArchive destination = ZipFile.Open(temp, ZipArchiveMode.Create))
                {
                    string installerName = archiveRootName + "/Install_AvernesSowtware.bat";
                    ZipArchiveEntry installer = source.GetEntry(installerName);
                    if (installer == null)
                        throw new InvalidDataException("The unified installer is missing from the release ZIP.");
                    CopyEntryForOrderedArchive(installer, destination, installerName);
                    foreach (ZipArchiveEntry entry in source.Entries)
                    {
                        if (String.Equals(entry.FullName, installerName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        CopyEntryForOrderedArchive(entry, destination, entry.FullName);
                    }
                }
            }
            File.Delete(output);
            File.Move(temp, output);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void CopyEntryForOrderedArchive(ZipArchiveEntry sourceEntry, ZipArchive destination, string entryName)
    {
        ZipArchiveEntry destinationEntry = destination.CreateEntry(entryName, CompressionLevel.Optimal);
        using (Stream sourceStream = sourceEntry.Open())
        using (Stream destinationStream = destinationEntry.Open())
        {
            sourceStream.CopyTo(destinationStream);
        }
    }

}
