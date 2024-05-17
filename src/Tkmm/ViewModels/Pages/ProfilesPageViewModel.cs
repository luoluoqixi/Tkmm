using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Tkmm.Core.Components;
using Tkmm.Core.Components.Models;
using Tkmm.Core.Models.Mods;

namespace Tkmm.ViewModels.Pages;

public partial class ProfilesPageViewModel : ObservableObject
{
    public ProfileMod? Selected {
        get => ProfileManager.Shared.Current.Selected;
        set {
            OnPropertyChanging(nameof(Selected));
            ProfileManager.Shared.Current.Selected = value;
            OnPropertyChanged(nameof(Selected));
        }
    }

    [ObservableProperty]
    private Mod? _masterSelected;

    [RelayCommand]
    private void Remove()
    {
        if (Selected is not null) {
            ProfileManager.Shared.Current.Mods.Remove(Selected);
        }
    }

    [RelayCommand]
    private Task MoveUp()
    {
        if (Selected is not null) {
            Selected = ProfileManager.Shared.Current.Move(Selected, -1);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task MoveDown()
    {
        if (Selected is not null) {
            Selected = ProfileManager.Shared.Current.Move(Selected, 1);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private static async Task Uninstall(Mod? target)
    {
        if (target is null) {
            return;
        }

        ContentDialog dialog = new() {
            Content = $"""
            您确定要卸载 '{target.Name}' 吗?

            这是无法挽回的
            """,
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = "卸载模组",
            SecondaryButtonText = "取消",
            Title = "警告"
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary) {
            target.Uninstall();
        }
    }

    [RelayCommand]
    private static async Task DeleteCurrentProfile()
    {
        if (ProfileManager.Shared.Profiles.Count < 2) {
            App.Toast("不能删除最后一个配置文件!", "错误", NotificationType.Error);
            return;
        }

        ContentDialog dialog = new() {
            Title = "删除配置文件",
            Content = $"""
            您确定要删除配置文件 '{ProfileManager.Shared.Current.Name}' 吗?

            这是无法挽回的
            """,
            PrimaryButtonText = "删除",
            SecondaryButtonText = "取消",
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) {
            return;
        }

        int currentIndex = ProfileManager.Shared.Profiles.IndexOf(ProfileManager.Shared.Current);
        ProfileManager.Shared.Profiles.RemoveAt(currentIndex);
        ProfileManager.Shared.Current = ProfileManager.Shared.Profiles[currentIndex == ProfileManager.Shared.Profiles.Count
            ? --currentIndex : currentIndex
        ];
    }
}
