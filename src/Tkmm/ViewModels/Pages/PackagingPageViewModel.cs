using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConfigFactory.Avalonia.Helpers;
using ConfigFactory.Core.Attributes;
using FluentAvalonia.UI.Controls;
using System.IO.Compression;
using System.Text.Json;
using Tkmm.Core;
using Tkmm.Core.Components;
using Tkmm.Core.Generics;
using Tkmm.Core.Helpers.Operations;
using Tkmm.Core.Models.Mods;

namespace Tkmm.ViewModels.Pages;

public partial class PackagingPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _exportPath = string.Empty;

    [ObservableProperty]
    private Mod _mod = new();

    [ObservableProperty]
    private string _sourceFolder = string.Empty;

    [RelayCommand]
    private async Task EditContributors(ContentControl content)
    {
        content.DataContext = Mod;

        ContentDialog dialog = new() {
            Title = "贡献者",
            Content = content,
            IsSecondaryButtonEnabled = false,
            PrimaryButtonText = "确定"
        };

        await dialog.ShowAsync();
        dialog.Content = null;
    }

    [RelayCommand]
    private async Task BrowseExportPath()
    {
        BrowserDialog dialog = new(BrowserMode.SaveFile, "导出位置", "TKCL文件:*.tkcl");
        if (await dialog.ShowDialog() is string result) {
            ExportPath = result;
        }
    }

    [RelayCommand]
    private async Task BrowseSourceFolder()
    {
        BrowserDialog dialog = new(BrowserMode.OpenFolder, "来源文件夹");
        if (await dialog.ShowDialog() is string result) {
            SourceFolder = result;
        }
    }

    [RelayCommand]
    private static async Task BrowseThumbnail(IModItem item)
    {
        BrowserDialog dialog = new(BrowserMode.OpenFile, "缩略图", "图片文件:*.bmp;*.gif;*.jpg;*.jpeg;*.png;*.tif");
        if (await dialog.ShowDialog() is string result) {
            item.ThumbnailUri = result;
        }
    }

    [RelayCommand]
    private async Task Create()
    {
        if (string.IsNullOrEmpty(SourceFolder)) {
            App.Toast("打包需要一个模组文件夹，请提供一个模组文件夹");
            return;
        }

        if (string.IsNullOrEmpty(Mod.Name)) {
            Mod.Name = Path.GetFileName(SourceFolder);
        }

        if (string.IsNullOrEmpty(ExportPath)) {
            ExportPath = Path.Combine(Path.GetDirectoryName(SourceFolder) ?? string.Empty, $"{Path.GetFileName(SourceFolder)}.tkcl");
        }

        if (!string.IsNullOrEmpty(Mod.ThumbnailUri)) {
            string relativeThumbnailUri = Path.Combine(SourceFolder, Mod.ThumbnailUri);
            if (File.Exists(relativeThumbnailUri)) {
                Mod.ThumbnailUri = relativeThumbnailUri;
            }
        }

        string tmpOutput = Path.Combine(Path.GetTempPath(), "tkmm", Mod.Id.ToString());

        await Task.Run(async () => {
            PackageBuilder.CreateMetaData(Mod, tmpOutput);
            await PackageBuilder.CopyContents(Mod, SourceFolder, tmpOutput);
            PackageBuilder.Package(tmpOutput, ExportPath);

            Directory.Delete(tmpOutput, true);
        });
    }

    [RelayCommand]
    private async Task ImportInfo()
    {
        BrowserDialog dialog = new(BrowserMode.OpenFile, "导入模组信息", "模组文件:info.json;*.tkcl|JSON 元数据文件:info.json|Mod Archive:*.tkcl");
        if (await dialog.ShowDialog() is string result) {
            Stream? stream;
            if (result.EndsWith("info.json")) {
                stream = File.OpenRead(result);
            }
            else {
                ZipArchive archive = ZipFile.OpenRead(result);
                stream = archive.Entries.FirstOrDefault(x => x.Name == "info.json")?.Open();
            }

            if (stream is null) {
                AppStatus.Set("无法读取模组元数据!", "fa-solid fa-triangle-exclamation", temporaryStatusTime: 1.5, isWorkingStatus: false);
                return;
            }

            Mod = JsonSerializer.Deserialize<Mod>(stream)
                ?? throw new InvalidOperationException("""
                    解析元数据错误: JSON反序列化返回null
                    """);

            await stream.DisposeAsync();
        }
    }

    [RelayCommand]
    private async Task ExportMetadata()
    {
        BrowserDialog dialog = new(BrowserMode.OpenFolder, "导出模组元数据");
        if (await dialog.ShowDialog() is string result) {
            PackageBuilder.CreateMetaData(Mod, result);
            AppStatus.Set("导出元数据完成!", "fa-solid fa-circle-check", temporaryStatusTime: 1.5, isWorkingStatus: false);
        }
    }

    [RelayCommand]
    private Task WriteMetadata()
    {
        if (!string.IsNullOrEmpty(SourceFolder) && Directory.Exists(SourceFolder)) {
            PackageBuilder.CreateMetaData(Mod, SourceFolder, useSourceFolderName: true);
            AppStatus.Set("导出元数据完成!", "fa-solid fa-circle-check", temporaryStatusTime: 1.5, isWorkingStatus: false);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task Refresh()
    {
        string store = SourceFolder;
        SourceFolder = string.Empty;
        SourceFolder = store;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ImportOptionGroup()
    {
        BrowserDialog dialog = new(BrowserMode.OpenFolder, "导入模组选项组");
        if (await dialog.ShowDialog() is string result) {
            string output = Path.Combine(SourceFolder, PackageBuilder.OPTIONS, Path.GetFileName(result));
            DirectoryOperations.CopyDirectory(result, output);
            Mod.OptionGroups.Add(ModOptionGroup.FromFolder(output));
        }
    }

    [RelayCommand]
    private static async Task ImportOption(ModOptionGroup group)
    {
        BrowserDialog dialog = new(BrowserMode.OpenFolder, "导入模组选项");
        if (await dialog.ShowDialog() is string result) {
            string output = Path.Combine(group.SourceFolder, Path.GetFileName(result));
            DirectoryOperations.CopyDirectory(result, output);
            group.Options.Add(ModOption.FromFolder(output));
        }
    }

    [RelayCommand]
    private async Task RemoveOptionGroup(ModOptionGroup target)
    {
        if (await WarnRemove(target) == false) {
            return;
        }

        if (Mod.OptionGroups.Remove(target)) {
            Directory.Delete(target.SourceFolder, true);
        }
    }

    [RelayCommand]
    private async Task RemoveOption(ModOption target)
    {
        if (Mod.OptionGroups.FirstOrDefault(x => x.Options.Contains(target)) is not ModOptionGroup group) {
            return;
        }

        if (await WarnRemove(target) == false) {
            return;
        }

        if (group.Options.Remove(target)) {
            Directory.Delete(target.SourceFolder, true);
        }
    }

    private static async Task<bool> WarnRemove(IModItem target)
    {
        ContentDialog dialog = new() {
            Title = "警告",
            Content = $"""
            此操作将永久删除文件夹 '{target.SourceFolder}' 并且无法撤销

            您确定要删除 '{target.Name}' 吗?
            """,
            PrimaryButtonText = "永久删除",
            SecondaryButtonText = "取消"
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    partial void OnSourceFolderChanged(string value)
    {
        string metadataPath = Path.Combine(value, PackageBuilder.METADATA);
        if (File.Exists(metadataPath)) {
            using FileStream fs = File.OpenRead(metadataPath);
            Mod = JsonSerializer.Deserialize<Mod>(fs) ?? Mod;
        }
        else {
            Mod.Id = Guid.NewGuid();
        }

        Mod.RefreshOptions(value);
    }
}