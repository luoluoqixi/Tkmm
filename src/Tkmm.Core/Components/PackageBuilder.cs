﻿using MalsMerger.Core;
using System.IO.Compression;
using System.Text.Json;
using Tkmm.Core.Generics;
using Tkmm.Core.Helpers;
using Tkmm.Core.Helpers.Operations;
using Tkmm.Core.Models.Mods;
using TKMM.SarcTool.Core;

namespace Tkmm.Core.Components;

public class PackageBuilder
{
    public const string METADATA = "info.json";
    public const string THUMBNAIL = ".thumbnail";
    public const string OPTIONS = "options";

    private const string CHECK_ICON = "fa-solid fa-circle-check";
    private const string METADATA_ICON = "fa-regular fa-file-code";
    private const string COPY_ICON = "fa-solid fa-object-group";

    public static void CreateMetaData<T>(T item, string outputFolder, bool useSourceFolderName = false) where T : IModItem
    {
        AppStatus.Set($"正在从 '{item.Name}' 创建元数据", METADATA_ICON);
        Directory.CreateDirectory(outputFolder);

        string metadataFile = Path.Combine(outputFolder, METADATA);
        using FileStream fs = File.Create(metadataFile);
        JsonSerializer.Serialize(fs, item);

        if (File.Exists(item.ThumbnailUri)) {
            AppStatus.Set($"正在拷贝缩略图", METADATA_ICON);
            File.Copy(item.ThumbnailUri, Path.Combine(outputFolder, THUMBNAIL), true);
            item.ThumbnailUri = THUMBNAIL;
        }

        if (item is Mod mod) {
            foreach (var group in mod.OptionGroups) {
                string groupOutputFolder = useSourceFolderName
                    ? Path.Combine(outputFolder, OPTIONS, Path.GetFileName(group.SourceFolder))
                    : Path.Combine(outputFolder, OPTIONS, group.Id.ToString());
                CreateMetaData(group, groupOutputFolder);

                foreach (var option in group.Options) {
                    string optionOutputFolder = useSourceFolderName
                        ? Path.Combine(groupOutputFolder, Path.GetFileName(option.SourceFolder))
                        : Path.Combine(groupOutputFolder, option.Id.ToString());
                    CreateMetaData(option, optionOutputFolder);
                }
            }

            AppStatus.Set($"生成模组元数据 '{item.Name}'", CHECK_ICON, isWorkingStatus: false, temporaryStatusTime: 1.5);
        }
    }

    public static async Task CopyContents<T>(T item, string sourceFolder, string outputFolder) where T : IModItem
    {
        await Task.WhenAll(CopyContentsInternal(item, sourceFolder, outputFolder));
        AppStatus.Set($"打包 '{item.Name}'", CHECK_ICON, isWorkingStatus: false, temporaryStatusTime: 1.5);
    }

    private static List<Task> CopyContentsInternal<T>(T item, string sourceFolder, string outputFolder) where T : IModItem
    {
        AppStatus.Set($"从 '{item.Name}' 生成changelogs", COPY_ICON);

        List<Task> tasks = [

            // Mals
            Task.Run(() => {
                if (Directory.Exists(Path.Combine(sourceFolder, TotkConfig.ROMFS, "Mals"))) {
                    Merger malsMerger = new([Path.Combine(sourceFolder, TotkConfig.ROMFS)], Path.Combine(outputFolder, TotkConfig.ROMFS), null);
                    malsMerger.GenerateChangelogs(format: false);
                }
            }),

            // RSDB
            ToolHelper.Call(Tool.RsdbMerger,
                    "--generate-changelog", Path.Combine(sourceFolder, "romfs", "RSDB"),
                    "--output", outputFolder
                ).WaitForExitAsync(),

            // SARC
            Task.Run(() => {
                SarcAssembler assembler = new(Path.Combine(sourceFolder, "romfs"));
                assembler.Assemble();

                SarcPackager packager = new(Path.Combine(outputFolder, "romfs"), Path.Combine(sourceFolder, "romfs"));
                packager.Package();
            }),

            // General
            Task.Run(() => {
                foreach (var folder in TotkConfig.FileSystemFolders) {
                    string inputFsFolder = Path.Combine(sourceFolder, folder);

                    if (Directory.Exists(inputFsFolder)) {
                        DirectoryOperations.CopyDirectory(
                            inputFsFolder, Path.Combine(outputFolder, folder),
                            [..ToolHelper.ExcludeFiles, ".ini", ".rsizetable"], [..ToolHelper.ExcludeFolders, "Mals"],
                            overwrite: true
                        );
                    }
                }
            })
        ];

        if (item is Mod mod) {
            AppStatus.Set("正在拷贝选项", COPY_ICON);

            foreach (var group in mod.OptionGroups) {
                string groupOutputFolder = Path.Combine(outputFolder, OPTIONS, group.Id.ToString());

                foreach (var option in group.Options) {
                    string optionOutputFolder = Path.Combine(groupOutputFolder, option.Id.ToString());
                    tasks.AddRange(CopyContentsInternal(option, option.SourceFolder, optionOutputFolder));
                }
            }
        }

        return tasks;
    }

    public static void Package(string inputFolder, string outputFile)
    {
        using FileStream fs = File.Create(outputFile);
        ZipFile.CreateFromDirectory(inputFolder, fs);
    }
}
