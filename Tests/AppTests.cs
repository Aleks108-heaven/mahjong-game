using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MahjongTable;
using TestMahjongGame.Engine;
using Xunit;

namespace TestMahjongGame.Tests;

// UserSettings and Loc keep static or on-disk state, so tests that touch them run one at a time.
[CollectionDefinition("App state", DisableParallelization = true)]
public class AppStateCollection
{
}

[Collection("App state")]
public sealed class UserSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mahjong-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string? _previous = Environment.GetEnvironmentVariable("MAHJONGTABLE_DATA");

    public UserSettingsTests()
    {
        Environment.SetEnvironmentVariable("MAHJONGTABLE_DATA", _folder);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("MAHJONGTABLE_DATA", _previous);
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ANewInstallStartsGentlyInUkrainianSittingEast()
    {
        Assert.Equal(new UserSettings(Lang.Uk, BotLevel.Junior, 0), UserSettings.Default);
        Assert.True(UserSettings.Default.IsValid);
    }

    [Fact]
    public void EveryChoiceSurvivesAWriteAndARead()
    {
        foreach (var language in Enum.GetValues<Lang>())
        {
            foreach (var level in Enum.GetValues<BotLevel>())
            {
                for (var seat = -1; seat <= 3; seat++)
                {
                    var settings = new UserSettings(language, level, seat);
                    Assert.Equal(settings, UserSettings.Parse(settings.ToJson()));
                }
            }
        }
    }

    [Fact]
    public void SavingThenLoadingGivesTheSameSettingsAndLeavesNoTempFile()
    {
        var settings = new UserSettings(Lang.En, BotLevel.Senior, -1);
        settings.Save();

        Assert.Equal(settings, UserSettings.Load());
        Assert.Equal(new[] { "settings.json" }, Directory.GetFiles(_folder).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void AMissingFileGivesTheDefaults()
    {
        Assert.Equal(UserSettings.Default, UserSettings.Load());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Language\":\"En\"}")]
    [InlineData("{\"Language\":\"Klingon\",\"Level\":\"Junior\",\"Seat\":0}")]
    [InlineData("{\"Language\":\"En\",\"Level\":\"Expert\",\"Seat\":0}")]
    [InlineData("{\"Language\":\"En\",\"Level\":\"Junior\",\"Seat\":7}")]
    [InlineData("{\"Language\":\"En\",\"Level\":\"Junior\",\"Seat\":-2}")]
    [InlineData("{\"Language\":99,\"Level\":\"Junior\",\"Seat\":0}")]
    [InlineData("{\"Language\":\"En\",\"Level\":\"Junior\",\"Seat\":\"north\"}")]
    public void DamagedOrForeignContentGivesTheDefaults(string json)
    {
        Assert.Equal(UserSettings.Default, UserSettings.Parse(json));
    }

    [Fact]
    public void UnknownExtraFieldsAreIgnored()
    {
        var parsed = UserSettings.Parse("{\"Language\":\"En\",\"Level\":\"Senior\",\"Seat\":2,\"FutureOption\":true}");
        Assert.Equal(new UserSettings(Lang.En, BotLevel.Senior, 2), parsed);
    }

    [Fact]
    public void AFileFarLargerThanSettingsIsNotRead()
    {
        Directory.CreateDirectory(_folder);
        var padding = new string('x', 6000);
        File.WriteAllText(
            Path.Combine(_folder, "settings.json"),
            "{\"Language\":\"En\",\"Level\":\"Senior\",\"Seat\":2,\"Note\":\"" + padding + "\"}");

        Assert.Equal(UserSettings.Default, UserSettings.Load());
    }

    [Fact]
    public void AGarbledFileOnDiskGivesTheDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{\"Language\":\"En\",\"Lev");
        Assert.Equal(UserSettings.Default, UserSettings.Load());
    }

    [Fact]
    public void FailingToSaveNeverInterruptsTheGame()
    {
        // The "folder" is really a file, so it can't be created.
        var blocker = Path.Combine(Path.GetTempPath(), "mahjong-blocker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "in the way");
        try
        {
            Environment.SetEnvironmentVariable("MAHJONGTABLE_DATA", blocker);
            var exception = Record.Exception(() => new UserSettings(Lang.En, BotLevel.Middle, 1).Save());
            Assert.Null(exception);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAHJONGTABLE_DATA", _folder);
            File.Delete(blocker);
        }
    }
}

[Collection("App state")]
public sealed class LocalizationTests : IDisposable
{
    private static readonly Lang Original = Loc.Current;

    public void Dispose()
    {
        Loc.Current = Original;
    }

    [Fact]
    public void EveryFixedTextInTheWindowHasAUkrainianTranslation()
    {
        var translations = Dictionary();
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var checkedFiles = 0;
        var literalsFound = 0;

        foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "Wpf"), "*.cs"))
        {
            if (Path.GetFileName(file) == "Loc.cs")
            {
                continue;
            }

            checkedFiles++;
            foreach (var key in LiteralsPassedToT(File.ReadAllText(file)))
            {
                literalsFound++;
                if (!translations.ContainsKey(key))
                {
                    missing.Add(file.Split(Path.DirectorySeparatorChar)[^1] + ": " + key);
                }
            }
        }

        Assert.True(checkedFiles >= 3, "The window's source files were not found.");
        Assert.True(literalsFound >= 80, $"Only {literalsFound} texts were found; the scan is not reading the window code.");
        Assert.True(missing.Count == 0, "No Ukrainian translation for:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryYakuTheEngineCanNameHasAUkrainianTranslation()
    {
        var translations = Dictionary();
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "Engine", "Yaku.cs"));
        var names = Regex.Matches(source, "(?:Man\\(|YakuEntry\\(|new\\()\\s*\"([A-Z][^\"]*)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.True(names.Count >= 30, $"Expected to find the yaku names, found {names.Count}.");
        var missing = names.Where(name => !translations.ContainsKey(name)).ToList();
        Assert.True(missing.Count == 0, "No Ukrainian translation for yaku: " + string.Join(", ", missing));
        Assert.True(translations.ContainsKey("Dragon triplet"));
    }

    [Fact]
    public void BothLanguagesFormatTheirPlaceholders()
    {
        foreach (var language in Enum.GetValues<Lang>())
        {
            Loc.Current = language;
            var text = Loc.T("Wall {0} left", 42);
            Assert.Contains("42", text);
            Assert.DoesNotContain("{0}", text);
        }
    }

    [Fact]
    public void TheLevelNamesAreTranslated()
    {
        Loc.Current = Lang.Uk;
        Assert.Equal("Джуніор", Loc.T("Junior"));
        Assert.Equal("Мідл", Loc.T("Middle"));
        Assert.Equal("Сеніор", Loc.T("Senior"));

        Loc.Current = Lang.En;
        Assert.Equal("Senior", Loc.T("Senior"));
    }

    [Fact]
    public void TranslationsKeepTheirPlaceholders()
    {
        // A translation that drops or invents a {0} would crash string.Format or hide a value.
        foreach (var (english, ukrainian) in Dictionary())
        {
            Assert.True(
                Placeholders(english).SetEquals(Placeholders(ukrainian)),
                $"Placeholders differ: \"{english}\" -> \"{ukrainian}\"");
        }
    }

    private static HashSet<string> Placeholders(string text)
    {
        return Regex.Matches(text, "\\{\\d+\\}").Select(m => m.Value).ToHashSet();
    }

    private static IReadOnlyDictionary<string, string> Dictionary()
    {
        var field = typeof(Loc).GetField("Ukrainian", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IReadOnlyDictionary<string, string>)field.GetValue(null)!;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TestMahjongGame.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    // Every string literal in the first argument of a Loc.T( call: the text passed in, whether it is written
    // directly or chosen by a ?: or a switch.
    private static IEnumerable<string> LiteralsPassedToT(string source)
    {
        const string marker = "Loc.T(";
        var index = 0;
        while ((index = source.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            index += marker.Length;
            var depth = 0;
            for (var i = index; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '"')
                {
                    var literal = new StringBuilder();
                    for (i++; i < source.Length && source[i] != '"'; i++)
                    {
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            i++;
                        }

                        literal.Append(source[i]);
                    }

                    yield return literal.ToString();
                }
                else if (c is '(' or '{')
                {
                    depth++;
                }
                else if (c is ')' or '}')
                {
                    if (depth == 0)
                    {
                        break;
                    }

                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    break;
                }
            }
        }
    }
}
