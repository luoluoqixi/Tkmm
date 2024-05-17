using CommunityToolkit.Mvvm.ComponentModel;
using ConfigFactory.Core;
using ConfigFactory.Core.Models;
using System.Text.Json.Serialization;
using TotkCommon;

namespace Tkmm.Core;

public partial class TotkConfig : ConfigModule<TotkConfig>
{
    public const string ROMFS = "romfs";
    public const string EXEFS = "exefs";

    public static readonly string[] FileSystemFolders = [
        ROMFS,
        EXEFS
    ];

    [JsonIgnore]
    public override string Name => "totk";

    [ObservableProperty]
    [property: ConfigFactory.Core.Attributes.Config(
        Header = "游戏路径",
        Description = """
            你的王国之泪 RomFS 游戏转储的绝对路径
            (例如: F:\Games\Totk\RomFS)
    
            *合并需要!
            """,
        Category = "TotK",
        Group = "通用")]
    [property: ConfigFactory.Core.Attributes.BrowserConfig(
        BrowserMode = ConfigFactory.Core.Attributes.BrowserMode.OpenFolder,
        InstanceBrowserKey = "totk-config-game-path",
        Title = "王国之泪 RomFS 游戏路径")]
    private string _gamePath = string.Empty;
    public static int GetVersion(string romfsFolder, int @default = 100)
    {
        string regionLangMask = Path.Combine(romfsFolder, "System", "RegionLangMask.txt");
        if (File.Exists(regionLangMask)) {
            string[] lines = File.ReadAllLines(regionLangMask);
            if (lines.Length >= 3 && int.TryParse(lines[2], out int value)) {
                return value;
            }
        }

        return @default;
    }

    [JsonIgnore]
    public string ZsDicPath => Path.Combine(GamePath, "Pack", "ZsDic.pack.zs");

    [JsonIgnore]
    public int Version => GetVersion(GamePath);

    public TotkConfig()
    {
        OnSaving += () => {
            if (Validate(out string? message, out ConfigProperty? target) == false) {
                AppStatus.Set($"无效设置, {target.Property.Name} 是无效的",
                    "fa-solid fa-triangle-exclamation", isWorkingStatus: false);
                return false;
            }

            AppStatus.Reset();
            return true;
        };
    }

    partial void OnGamePathChanged(string value)
    {
        Validate(() => GamePath, value => {
            if (value is not null && File.Exists(Path.Combine(value, "Pack", "ZsDic.pack.zs"))) {
                Totk.Zstd.LoadDictionaries(ZsDicPath);
                return true;
            }

            return false;
        });
    }
}