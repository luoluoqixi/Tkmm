using Avalonia.Controls;
using Avalonia.Data;
using ConfigFactory.Avalonia.Helpers;
using ConfigFactory.Core.Attributes;
using FluentAvalonia.UI.Controls;
using Markdown.Avalonia.Full;
using System.Diagnostics;
using Tkmm.Attributes;
using Tkmm.Core;
using Tkmm.Core.Components;
using Tkmm.Core.Helpers;
using Tkmm.Core.Helpers.Operations;
using Tkmm.Core.Helpers.Win32;
using Tkmm.Core.Models.Mods;
using Tkmm.Core.Services;
using Tkmm.Helpers;

namespace Tkmm.Builders.MenuModels;

public class ShellViewMenu
{
    [Menu("导出到SD卡", "文件", "Ctrl + E", "fa-solid fa-sd-card")]
    public static async Task ExportToSdCard()
    {
        const string GAME_ID = "0100F2C0115B6000";

        DriveInfo[] disks = DriveInfo.GetDrives()
            .Where(drive => {
                try {
                    return drive.DriveType == DriveType.Removable && drive.DriveFormat == "FAT32";
                }
                catch {
                    return false;
                }
            })
            .ToArray();

        if (disks.Length == 0) {
            App.ToastError(new InvalidOperationException("""
                没有找到可移动磁盘!
                """));

            return;
        }

        ContentDialog dialog = new() {
            Title = "选择SD卡",
            Content = new ComboBox {
                ItemsSource = disks,
                SelectedIndex = 0,
                DisplayMemberBinding = new Binding("VolumeLabel")
            },
            PrimaryButtonText = "导出",
            SecondaryButtonText = "取消"
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Content is not ComboBox selector) {
            return;
        }

        if (selector.SelectedItem is DriveInfo drive) {
            await MergerOperations.Merge();

            string output = Path.Combine(drive.Name, "atmosphere", "contents", GAME_ID);
            DirectoryOperations.CopyDirectory(Config.Shared.MergeOutput, output);
        }
    }

    [Menu("退出", "文件", "Alt + F4", "fa-solid fa-right-from-bracket", IsSeparator = true)]
    public static void Exit()
    {
        ProfileManager.Shared.Apply();
        Environment.Exit(0);
    }

    [Menu("安装文件", "模组", "Ctrl + I", "fa-solid fa-file-import")]
    public static async Task ImportModFile()
    {
        BrowserDialog dialog = new(BrowserMode.OpenFile, "打开模组文件", "TKCL:*.tkcl|All Archives:*.tkcl;*.zip;*.rar;*.7z|All Files:*.*");
        string? selectedFile = await dialog.ShowDialog();

        if (string.IsNullOrEmpty(selectedFile)) {
            return;
        }

        await ModHelper.Import(selectedFile);
    }

    [Menu("安装文件夹", "模组", "Ctrl + Shift + I", "fa-regular fa-folder-open")]
    public static async Task ImportModFolder()
    {
        BrowserDialog dialog = new(BrowserMode.OpenFolder, "打开模组文件夹");
        string? selectedFolder = await dialog.ShowDialog();

        if (string.IsNullOrEmpty(selectedFolder)) {
            return;
        }

        await ModHelper.Import(selectedFolder);
    }

    [Menu("从参数安装", "模组", "Ctrl + Alt + I", "fa-regular fa-keyboard")]
    public static async Task ImportArgument()
    {
        ContentDialog dialog = new() {
            Title = "导入参数",
            Content = new TextBox {
                Watermark = "参数 (文件, 文件夹, 链接, 模组ID)"
            },
            PrimaryButtonText = "导入",
            SecondaryButtonText = "取消",
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) {
            return;
        }

        if (dialog.Content is TextBox tb && tb.Text is not null) {
            await ModHelper.Import(tb.Text.Replace("\"", string.Empty));
        }
    }

    [Menu("合并", "模组", "Ctrl + M", "fa-solid fa-code-merge", IsSeparator = true)]
    public static async Task MergeMods()
    {
        await MergerOperations.Merge();
    }

    [Menu("显示/隐藏 控制台", "视图", "Ctrl + F11", "fa-solid fa-terminal")]
    public static void ShowHideConsole()
    {
        if (OperatingSystem.IsWindows()) {
            WindowsOperations.SwapWindowMode();
            App.Focus();
        }
        else {
            AppStatus.Set("此操作仅在Win32平台上支持", "fa-brands fa-windows",
                isWorkingStatus: false, temporaryStatusTime: 1.5);
        }
    }

#if DEBUG
    [Menu("打开模组文件夹", "调试", "Alt + O", "fa-solid fa-folder-tree")]
    public static void OpenModFolder()
    {
        if (ProfileManager.Shared.Current.Selected?.Mod is not Mod target) {
            return;
        }

        string folder = ProfileManager.GetModFolder(target);
        if (OperatingSystem.IsWindows()) {
            Process.Start("explorer.exe", folder);
        }
        else {
            App.ToastError(new InvalidOperationException("""
                该操作仅支持Windows操作系统
                """));
        }
    }
#endif

    [Menu("检查更新", "帮助", "Ctrl + U", "fa-solid fa-cloud-arrow-up")]
    public static async Task CheckForUpdate()
    {
        if (!await AppManager.HasUpdate()) {
            await new ContentDialog {
                Title = "检查更新",
                Content = "最新的软件",
                PrimaryButtonText = "确定"
            }.ShowAsync();

            return;
        }

        ContentDialog dialog = new() {
            Title = "更新",
            Content = """
                存在可用更新。
                
                是否要关闭当前会话并打开启动器？
                """,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消"
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary) {
            await Task.Run(async () => {
                await AppManager.UpdateLauncher();
                AppManager.StartLauncher();
            });

            Environment.Exit(0);
        }
    }

    [Menu("下载依赖", "帮助", "Ctrl + Shift + U", "fa-solid fa-screwdriver-wrench")]
    public static async Task DownloadDependencies()
    {
        await ToolHelper.DownloadDependencies(forceRefresh: true);
        await AssetHelper.Download();
    }

    [Menu("创建桌面图标", "帮助", "Ctrl + Alt + L", "fa-solid fa-link")]
    public static Task CreateDesktopShortcuts()
    {
        AppManager.CreateDesktopShortcuts();
        return Task.CompletedTask;
    }

    [Menu("关于", "帮助", "F12", "fa-solid fa-circle-info", IsSeparator = true)]
    public static async Task About()
    {
        string aboutFile = Path.Combine(Config.Shared.StaticStorageFolder, "Readme.md");

        TaskDialog dialog = new() {
            XamlRoot = App.XamlRoot,
            Title = "关于",
            Content = new MarkdownScrollViewer {
                Markdown = File.Exists(aboutFile) ? File.ReadAllText(aboutFile) : "无效的安装文件"
            },
            Buttons = [
                new TaskDialogButton {
                    Text = "确定"
                }
            ]
        };

        await dialog.ShowAsync();
    }
}
