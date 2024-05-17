using CommunityToolkit.Mvvm.ComponentModel;
using ConfigFactory.Core;
using ConfigFactory.Core.Attributes;

namespace Tkmm.Core;

public partial class Config : ConfigModule<Config>
{
    static Config()
    {
        Directory.CreateDirectory(DocumentsFolder);
    }

    public static string DocumentsFolder { get; }
        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TotK Mod Manager");

    private static readonly string _defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tkmm");
    private static readonly string _defaultMergedPath = Path.Combine(DocumentsFolder, "TotK Mod Manager", "Merged Output");

    public override string Name { get; } = "tkmm";

    public string StaticStorageFolder { get; }
        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tkmm");

    public static Action<string>? SetTheme { get; set; }

    [ObservableProperty]
    [property: Config(
        Header = "主题",
        Description = "",
        Group = "应用程序")]
    [property: DropdownConfig("Dark", "Light")]
    private string _theme = "Dark";

    [ObservableProperty]
    [property: Config(
        Header = "显示控制台",
        Description = "显示控制台窗口以获取其他信息 (需要重新启动)",
        Group = "应用程序")]
    private bool _showConsole = false;

    [ObservableProperty]
    [property: Config(
        Header = "系统文件夹",
        Description = "用于存储TKMM系统文件的文件夹。",
        Group = "应用程序")]
    [property: BrowserConfig(
        BrowserMode = BrowserMode.OpenFolder,
        InstanceBrowserKey = "config-storage-folder",
        Title = "Storage Folder")]
    private string _storageFolder = _defaultPath;

    [ObservableProperty]
    [property: Config(
        Header = "默认的作者",
        Description = "封装TKCL模组时使用的默认作者。",
        Group = "打包")]
    private string _defaultAuthor = Path.GetFileName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    [ObservableProperty]
    [property: Config(
        Header = "合并完成模组的输出文件夹",
        Description = "将最终合并模组写入的输出文件夹。",
        Group = "合并")]
    [property: BrowserConfig(
        BrowserMode = BrowserMode.OpenFolder,
        InstanceBrowserKey = "config-mrged-output-folder",
        Title = "合并完成模组的输出文件夹")]
    private string _mergeOutput = _defaultMergedPath;

    [ObservableProperty]
    [property: Config(
        Header = "目标语言",
        Description = "使用 MalsMerger 创建存档的目标语言",
        Group = "合并")]
    [property: DropdownConfig("USen", "EUen", "JPja", "EUfr", "USfr", "USes", "EUes", "EUde", "EUnl", "EUit", "KRko", "CNzh", "TWzh")]
    private string _gameLanguage = "USen";

    [ObservableProperty]
    [property: Config(
        Header = "使用Ryujinx",
        Description = "自动导出到你的 Ryujinx 模组文件夹",
        Group = "合并")]
    private bool _useRyujinx = false;

    [ObservableProperty]
    [property: Config(
        Header = "使用Japanese Citrus Fruit",
        Description = "自动导出到你的 Japanese Citrus Fruit 模组文件夹",
        Group = "合并")]
    private bool _useJapaneseCitrusFruit = false;

    partial void OnThemeChanged(string value)
    {
        SetTheme?.Invoke(value);
    }

    private static readonly string _ryujinxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ryujinx", "sdcard", "atmosphere", "contents", "0100f2c0115b6000");
    partial void OnUseRyujinxChanged(bool value)
    {
        if (Directory.Exists(_ryujinxPath)) {
            Directory.Delete(_ryujinxPath, true);
        }

        if (value == true) {
            if (Path.GetDirectoryName(_ryujinxPath) is string folder) {
                Directory.CreateDirectory(folder);
            }

            Directory.CreateSymbolicLink(_ryujinxPath, MergeOutput);
        }
    }

    private static readonly string _japaneseCitrusFruitPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yuzu", "load", "0100F2C0115B6000", "TKMM");
    partial void OnUseJapaneseCitrusFruitChanged(bool value)
    {
        if (Directory.Exists(_japaneseCitrusFruitPath)) {
            Directory.Delete(_japaneseCitrusFruitPath, true);
        }

        if (value == true) {
            if (Path.GetDirectoryName(_japaneseCitrusFruitPath) is string folder) {
                Directory.CreateDirectory(folder);
            }

            Directory.CreateSymbolicLink(_japaneseCitrusFruitPath, MergeOutput);
        }
    }
}
