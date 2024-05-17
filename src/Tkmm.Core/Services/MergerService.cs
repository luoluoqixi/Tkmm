﻿using Tkmm.Core.Components;
using Tkmm.Core.Components.Mergers;
using Tkmm.Core.Components.Mergers.Special;
using Tkmm.Core.Components.Models;
using Tkmm.Core.Models.Mods;

namespace Tkmm.Core.Services;

public class MergerService
{
    private static readonly IMerger[] _mergers = [
        new ContentMerger(),
        new MalsMergerShell(),
        new RsdbMergerShell(),
        new SarcMergerShell()
    ];

    public static async Task Merge() => await Merge(ProfileManager.Shared.Current, Config.Shared.MergeOutput);
    public static async Task Merge(string output) => await Merge(ProfileManager.Shared.Current, output);
    public static async Task Merge(Profile profile) => await Merge(profile, Config.Shared.MergeOutput);
    public static async Task Merge(Profile profile, string output)
    {
        Mod[] mods = profile.Mods
            .Where(x => x.IsEnabled && x.Mod is not null)
            .Select(x => x.Mod!)
            .Reverse()
            .ToArray();

        if (mods.Length <= 0) {
            AppStatus.Set("没有什么要合并的", "fa-solid fa-code-merge",
                isWorkingStatus: false, temporaryStatusTime: 1.5,
                logLevel: LogLevel.Info);

            return;
        }

        if (Directory.Exists(output)) {
            AppStatus.Set($"清除输出", "fa-solid fa-code-merge");
            Directory.Delete(output, true);
        }

        TriviaService.Start();
        await Task.Run(async () => {
            Directory.CreateDirectory(output);
            await MergeAsync(mods, output);
        });

        TriviaService.Stop();
        AppStatus.Set("合并成功完成", "fa-solid fa-list-check",
            isWorkingStatus: false, temporaryStatusTime: 1.5,
            logLevel: LogLevel.Info);
    }



    private static async Task MergeAsync(Mod[] mods, string output)
    {
        Task[] tasks = new Task[_mergers.Length];
        for (int i = 0; i < tasks.Length; i++) {
            tasks[i] = _mergers[i].Merge(mods, output);
        }

        await Task.WhenAll(tasks);
        await RstbMergerShell.Shared.Merge(mods, output);
    }
}
