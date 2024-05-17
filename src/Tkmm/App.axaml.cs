using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using ConfigFactory;
using ConfigFactory.Avalonia;
using ConfigFactory.Avalonia.Helpers;
using ConfigFactory.Core;
using ConfigFactory.Core.Models;
using ConfigFactory.Models;
using FluentAvalonia.UI.Controls;
using System.Reflection;
using Tkmm.Builders;
using Tkmm.Builders.MenuModels;
using Tkmm.Core;
using Tkmm.Core.Components;
using Tkmm.Core.Helpers;
using Tkmm.Helpers;
using Tkmm.ViewModels;
using Tkmm.Views;
using Tkmm.Views.Pages;

namespace Tkmm;

public partial class App : Application
{
    private static WindowNotificationManager? _notificationManager;

    public static string? Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
    public static string Title { get; } = $"王国之泪模组管理器";
    public static string ShortTitle { get; } = $"TKMM v{Version}";
    public static string ReleaseUrl { get; } = $"https://github.com/TKMM-Team/Tkmm/releases/{Version}";
    public static TopLevel? XamlRoot { get; private set; }
    public static Exception? SettingsException { get; private set; }

    /// <summary>
    /// Application <see cref="IMenuFactory"/> (used for extending the main menu at runtime)
    /// </summary>
    public static IMenuFactory MenuFactory { get; private set; } = null!;

    public App()
    {
        TaskScheduler.UnobservedTaskException += (s, e) => {
            ToastError(e.Exception);
        };
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            ShellView shellView = new() {
                DataContext = new ShellViewModel()
            };

            shellView.Closed += (s, e) => {
                ProfileManager.Shared.Apply();
                Config.Shared.Save();
            };

            XamlRoot = shellView;
            shellView.Loaded += (s, e) => {
                _notificationManager = new(XamlRoot) {
                    Position = NotificationPosition.BottomRight,
                    MaxItems = 3,
                    Margin = new(0, 0, 4, 0)
                };
            };

            MenuFactory = new MenuFactory(XamlRoot);
            MenuFactory.Append<ShellViewMenu>();

            shellView.MainMenu.ItemsSource = MenuFactory.Items;
            desktop.MainWindow = shellView;

            // ConfigFactory Configuration
            BrowserDialog.StorageProvider = desktop.MainWindow.StorageProvider;
            Config.SetTheme = (theme) => {
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            };

            ConfigPage settingsPage = new();
            bool isValid = false;
            string? message = string.Empty;

            if (settingsPage.DataContext is ConfigPageModel settingsModel) {
                settingsModel.SecondaryButtonIsEnabled = false;

                isValid = ConfigModule<Config>.Shared.Validate(out message, out ConfigProperty? target);
                settingsModel.Append<Config>();

                isValid = isValid && ConfigModule<TotkConfig>.Shared.Validate(out message, out target);
                settingsModel.Append<TotkConfig>();

                settingsModel.PrimaryButtonContent = "保存";
                settingsModel.SecondaryButtonContent = "取消";

                if (!isValid && target?.Attribute is not null) {
                    settingsModel.SelectedGroup = settingsModel.Categories
                        .Where(x => x.Header == target.Attribute.Category)
                        .SelectMany(x => x.Groups)
                        .FirstOrDefault(x => x.Header == target.Attribute.Group);

                    AppStatus.Set($"无效的设置, {target.Property.Name} 是无效的.",
                        "fa-solid fa-triangle-exclamation", isWorkingStatus: false);
                }
            }

            PageManager.Shared.Register(Page.Home, "主页", new HomePageView(), Symbol.Home, "主页", isDefault: true);
            PageManager.Shared.Register(Page.Profiles, "配置文件", new ProfilesPageView(), Symbol.OtherUser, "管理模组配置文件");
            PageManager.Shared.Register(Page.Tools, "TKCL打包", new PackagingPageView(), Symbol.CodeHTML, "模组开发工具");
            PageManager.Shared.Register(Page.ShopParam, "ShopParam Overflow 编辑器", new ShopParamPageView(), Symbol.Sort, "ShopParam overflow 订购工具");
            PageManager.Shared.Register(Page.Mods, "模组浏览器(GameBanana)", new GameBananaPageView(), Symbol.Globe, "王国之泪模组的浏览器客户端(GameBanana)");

            PageManager.Shared.Register(Page.About, "关于", new AboutPageView(), Symbol.Bookmark, "关于这个项目", isFooter: true);
            PageManager.Shared.Register(Page.Logs, "日志", new LogsPageView(), Symbol.AllApps, "系统日志", isFooter: true);
            PageManager.Shared.Register(Page.Settings, "设置", settingsPage, Symbol.Settings, "设置", isFooter: true, isDefault: isValid == false);

            Config.SetTheme(Config.Shared.Theme);

            await ToolHelper.LoadDeps();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void Focus()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null) {
            desktop.MainWindow.WindowState = WindowState.Normal;
            desktop.MainWindow.Activate();
        }
    }

    public static void Toast(string message, string title = "通知", NotificationType type = NotificationType.Information, TimeSpan? expiration = null, Action? action = null)
    {
        Dispatcher.UIThread.Invoke(() => {
            _notificationManager?.Show(
                new Notification(title, message, type, expiration, action));
        });
    }

    public static void ToastError(Exception ex)
    {
        AppLog.Log(ex);

        Dispatcher.UIThread.Invoke(() => {
            _notificationManager?.Show(new Notification(
                ex.GetType().Name, ex.Message, NotificationType.Error, onClick: () => {
                    PageManager.Shared.Focus(Page.Logs);
                }));
        });
    }
}
