using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TestMahjongGame.Engine;

namespace MahjongTable;

// What the app remembers between runs: language, opponent level and seat (-1 = watch). Stored as a small JSON
// file in the user's local app data. Nothing here is secret, and a missing or damaged file just means defaults.
public sealed record UserSettings(Lang Language, BotLevel Level, int Seat)
{
    // Far larger than a real settings file; anything bigger is not ours and is not read.
    private const long MaxFileBytes = 4096;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // A new install starts against the gentlest opponents, in Ukrainian, sitting East.
    public static UserSettings Default { get; } = new(Lang.Uk, BotLevel.Junior, 0);

    // MAHJONGTABLE_DATA moves the folder (used for tests and screenshots, so they never touch real settings).
    public static string FolderPath =>
        Environment.GetEnvironmentVariable("MAHJONGTABLE_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MahjongTable");

    private static string FilePath => Path.Combine(FolderPath, "settings.json");

    public static UserSettings Load()
    {
        try
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return Default;
            }

            return Parse(File.ReadAllText(info.FullName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Default;
        }
    }

    // Reads settings text; anything that isn't a valid, complete settings object gives the defaults.
    public static UserSettings Parse(string json)
    {
        try
        {
            var loaded = JsonSerializer.Deserialize<UserSettings>(json, Json);
            return loaded is not null && loaded.IsValid ? loaded : Default;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Default;
        }
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, Json);
    }

    public bool IsValid => Enum.IsDefined(Language) && Enum.IsDefined(Level) && Seat is >= -1 and <= 3;

    // Writes to a temporary file first, so a crash mid-write can't leave a half-written settings file.
    // Failing to save is not worth interrupting a game for.
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, ToJson());
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
    }
}

public static class AppInfo
{
    // "0.1.0" from the assembly's version, without any "+commit" build metadata.
    public static string Version
    {
        get
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var version = informational ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
            var plus = version.IndexOf('+');
            return plus >= 0 ? version[..plus] : version;
        }
    }
}
