﻿using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipes;
using System.Text;
using Tkmm.Core.Helpers.Models;
using Tkmm.Core.Helpers.Operations;

namespace Tkmm.Core.Components;

public static class AppManager
{
    private const string APP_NAME = "TKMM";
    private const string PROC_NAME = "tkmm";
    private const string LAUNCHER_NAME = "TKMM Launcher";

    private static readonly string _appFolder = Path.Combine(Config.Shared.StaticStorageFolder, "bin");
    private static readonly string _appPath = Path.Combine(_appFolder, "Tkmm.Desktop.exe");
    private static readonly string _appVersionFile = Path.Combine(Config.Shared.StaticStorageFolder, "version");

    private static readonly string _launcherFolder = Path.Combine(Config.Shared.StaticStorageFolder, "launcher");
    private static readonly string _launcherPath = Path.Combine(_launcherFolder, "Tkmm.Launcher.exe");

    private const string ID = "Tkmm-[9fcf39df-ec9a-4510-8f56-88b52e85ae01]";
    private static Func<string[], Task>? _attach;

    public static bool Start(string[] args, Func<string[], Task> attach)
    {
        _attach = attach;

        using NamedPipeClientStream client = new(ID);
        try {
            client.Connect(0);
        }
        catch {
            Task.Run(StartServerListener);
            return true;
        }

        using BinaryWriter writer = new(client, Encoding.UTF8);

        writer.Write(args.Length);
        for (int i = 0; i < args.Length; i++) {
            writer.Write(args[i]);
        }

        Console.WriteLine($"[Info] Waiting for '{ID}'...");
        client.ReadByte();
        return false;
    }

    public static async Task StartServerListener()
    {
        using NamedPipeServerStream server = new(ID);
        server.WaitForConnection();

        using (var reader = new BinaryReader(server, Encoding.UTF8)) {
            int argc = reader.ReadInt32();
            string[] args = new string[argc];
            for (int i = 0; i < argc; i++) {
                args[i] = reader.ReadString();
            }

            if (_attach?.Invoke(args) is Task task) {
                await task;
            }

            server.WriteByte(0);
        }

        await StartServerListener();
    }

    public static void Start()
    {
        Process.Start(_appPath);
    }

    public static void StartLauncher()
    {
        Process.Start(_launcherPath);
    }

    public static bool IsInstalled()
    {
        return File.Exists(_appVersionFile);
    }

    public static async Task<bool> HasUpdate()
    {
        if (!File.Exists(_appVersionFile)) {
            return true;
        }

        string currentVersion = File.ReadAllText(_appVersionFile);
        return await GitHubOperations.HasUpdate("TKMM-Team", "Tkmm", currentVersion);
    }

    public static async Task Update()
    {
        AppStatus.Set("正在关闭打开的应用实例", "fa-solid fa-download");
        Kill();

        AppStatus.Set("正在下载应用", "fa-solid fa-download");

        (Stream stream, string tag) = await GitHubOperations
            .GetLatestRelease("TKMM-Team", "Tkmm", $"TKMM-{Dependency.GetOSName()}.zip");

        AppStatus.Set("正在提取释放", "fa-solid fa-file-zipper");
        using ZipArchive archive = new(stream);
        archive.ExtractToDirectory(_appFolder, true);

        AppStatus.Set("正在更新版本", "fa-solid fa-code-commit");
        File.WriteAllText(_appVersionFile, tag);

        AppStatus.Set("应用程序安装完成!", "fa-solid fa-circle-check", isWorkingStatus: false, temporaryStatusTime: 1.5);
    }

    public static async Task UpdateLauncher()
    {
        AppStatus.Set("下载启动器", "fa-solid fa-download");

        (Stream stream, _) = await GitHubOperations
            .GetLatestRelease("TKMM-Team", "Tkmm", $"TKMM-Launcher-{Dependency.GetOSName()}.zip");

        AppStatus.Set("正在提取释放", "fa-solid fa-file-zipper");
        using ZipArchive archive = new(stream);
        archive.ExtractToDirectory(_launcherFolder, true);

        AppStatus.Set("启动器更新完成!", "fa-solid fa-circle-check", isWorkingStatus: false, temporaryStatusTime: 1.5);
    }

    public static void Uninstall()
    {
        Kill();

        if (Directory.Exists(_appFolder)) {
            Directory.Delete(_appFolder, true);
        }

        DeleteDesktopShortcuts();
    }

    public static void CreateDesktopShortcuts()
    {
        Shortcut.Create(APP_NAME, Location.Application, _appPath, "nxe");
        Shortcut.Create(LAUNCHER_NAME, Location.Application, _launcherPath, "nxe");
        Shortcut.Create(APP_NAME, Location.Desktop, _appPath, "nxe");
    }

    public static void DeleteDesktopShortcuts()
    {
        Shortcut.Remove(APP_NAME, Location.Application);
        Shortcut.Remove(LAUNCHER_NAME, Location.Application);
        Shortcut.Remove(APP_NAME, Location.Desktop);
    }

    private static void Kill()
    {
        foreach (var process in Process.GetProcessesByName(PROC_NAME)) {
            process.Kill();
        }
    }
}