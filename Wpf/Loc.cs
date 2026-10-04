using System.Globalization;
using TestMahjongGame.Engine;

namespace MahjongTable;

public enum Lang
{
    En,
    Uk
}

// Window text in English or Ukrainian. The English text is the key: Loc.T("Wall {0} left", 5) returns the
// Ukrainian version when that language is selected, and falls back to the English text if none is listed.
// The game log is written by the engine and stays in English.
public static class Loc
{
    public static Lang Current { get; set; } = Lang.Uk;

    public static bool IsUkrainian => Current == Lang.Uk;

    public static string T(string english, params object[] args)
    {
        var text = Current == Lang.Uk && Ukrainian.TryGetValue(english, out var uk) ? uk : english;
        return args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
    }

    // "Same run twice (iipeikou) (1)" style text for a winning hand, with the yaku names translated.
    public static string YakuText(YakuResult yaku)
    {
        return string.Join(", ", yaku.Yaku.Select(entry =>
        {
            var name = entry.Name;
            const string dragon = "Dragon triplet";
            var shown = name.StartsWith(dragon, StringComparison.Ordinal)
                ? T(dragon) + name[dragon.Length..]
                : T(name);
            return entry.Han >= 13 ? $"{shown} ({T("yakuman")})" : $"{shown} ({entry.Han})";
        }));
    }

    private static readonly Dictionary<string, string> Ukrainian = new()
    {
        // Static window text
        ["Mahjong Table"] = "Стіл маджонгу",
        ["Riichi engine · four bots play one round"] = "Рушій ріічі-маджонгу · чотири боти грають один раунд",
        ["Dora"] = "Дора",
        ["Hint (H)"] = "Підказка (H)",
        ["Show the discard that keeps you closest to winning"] = "Показати скид, що тримає вас найближче до перемоги",
        ["CONTROLS"] = "КЕРУВАННЯ",
        ["Next turn  ▶"] = "Наступний хід  ▶",
        ["Play one full turn (Right arrow)"] = "Розіграти один повний хід (стрілка вправо)",
        ["Auto-play"] = "Автогра",
        ["Play turns automatically (A)"] = "Ходи йдуть автоматично (A)",
        ["New round"] = "Новий раунд",
        ["Deal a new round (N)"] = "Нова роздача (N)",
        ["YOUR SEAT"] = "ВАШЕ МІСЦЕ",
        ["Watch"] = "Глядач",
        ["Spectate: four bots play"] = "Спостерігати: грають чотири боти",
        ["Picking a seat starts a new round."] = "Вибір місця починає новий раунд.",
        ["Language"] = "Мова",
        ["SEED (OPTIONAL)"] = "SEED (НЕОБОВ'ЯЗКОВО)",
        ["Seed"] = "Seed",
        ["A whole number replays the same round"] = "Ціле число повторює той самий раунд",
        ["Seed must be a whole number, for example 7."] = "Seed має бути цілим числом, наприклад 7.",
        ["Empty = random round."] = "Порожньо = випадковий раунд.",
        ["Show all hands"] = "Показати всі руки",
        ["GAME LOG"] = "ЖУРНАЛ ГРИ",
        ["Game log"] = "Журнал гри",
        ["Auto-play speed"] = "Швидкість автогри",
        ["Keys: → next · A auto · N new"] = "Клавіші: → далі · A авто · N новий",
        ["Press New round (N) to deal again, or enter a seed to replay a round."] = "Натисніть «Новий раунд» (N), щоб роздати знову, або введіть seed, щоб повторити раунд.",

        // Header and panels
        ["Turn {0}"] = "Хід {0}",
        ["Wall {0} left"] = "У стіні {0}",
        ["Seed {0}"] = "Seed {0}",
        ["Player {0} · {1}"] = "Гравець {0} · {1}",
        [" · You"] = " · Ви",
        ["Discards ({0})"] = "Скиди ({0})",
        ["WINNER · Tsumo"] = "ПЕРЕМОЖЕЦЬ · Цумо",
        ["WINNER · Ron"] = "ПЕРЕМОЖЕЦЬ · Рон",
        ["Tenpai · Furiten"] = "Тенпай · Фурітен",
        ["Tenpai"] = "Тенпай",
        ["Shanten {0}"] = "Шантен {0}",
        ["Hand hidden"] = "Рука прихована",
        ["▶ Your move"] = "▶ Ваш хід",
        ["▶ Draws next"] = "▶ Наступний добір",
        ["▶ Discards next"] = "▶ Наступний скид",
        ["Speed: {0} ms per turn"] = "Швидкість: {0} мс на хід",
        ["Speed: {0} ms per bot turn"] = "Швидкість: {0} мс на хід бота",
        ["Dora indicators"] = "Індикатори дора",

        // Seats and tiles
        ["East"] = "Схід",
        ["South"] = "Південь",
        ["West"] = "Захід",
        ["North"] = "Північ",
        ["White"] = "Білий дракон",
        ["Green"] = "Зелений дракон",
        ["Red"] = "Червоний дракон",
        ["characters"] = "символи",
        ["circles"] = "кола",
        ["bamboo"] = "бамбук",
        ["{0} of {1} ({2})"] = "{0} · {1} ({2})",
        ["Hidden tile"] = "Прихована плитка",
        ["Player {0} ({1}{2})"] = "Гравець {0} ({1}{2})",
        [" - you"] = " - ви",

        // Melds
        ["Pon"] = "Пон",
        ["Chi"] = "Чі",
        ["Closed kan"] = "Закритий кан",
        ["Added kan"] = "Доданий кан",
        ["Open kan"] = "Відкритий кан",
        ["{0} of {1}"] = "{0} з {1}",
        ["{0} of {1}, called {2} from Player {3}"] = "{0} з {1}, закликано {2} від гравця {3}",

        // Call prompt
        ["{0} discarded {1}. Call it?"] = "{0} скинув(ла) {1}. Закликати?",
        ["pon (three of a kind)"] = "пон (трійка однакових)",
        ["kan (four of a kind; you then draw a replacement tile)"] = "кан (чотири однакові; потім ви берете запасну плитку)",
        ["chi (a run)"] = "чі (послідовність)",
        [" or "] = " або ",
        ["Pon (P)"] = "Пон (P)",
        ["Kan (K)"] = "Кан (K)",
        ["Chi {0}"] = "Чі {0}",
        ["Pass (S)"] = "Пас (S)",
        ["You can {0}. Calling takes the tile and you discard next; the called tile stays locked. Pass is the focused default."] =
            "Ви можете: {0}. Заклик забирає плитку, і ви скидаєте далі; закликана плитка лишається заблокованою. Пас стоїть у фокусі за замовчуванням.",

        // Your turn
        ["completes your hand"] = "завершує вашу руку",
        ["tenpai"] = "тенпай",
        ["shanten {0}"] = "шантен {0}",
        ["Discard {0}: {1}"] = "Скинути {0}: {1}",
        ["Discard {0}"] = "Скинути {0}",
        ["{0} is locked: you can't discard it right after calling"] = "{0} заблокована: її не можна скидати одразу після заклику",
        ["{0}, locked this turn"] = "{0}, заблокована цього ходу",
        ["Closed kan {0}"] = "Закритий кан {0}",
        ["Add {0} to pon"] = "Додати {0} до пона",
        ["You called: now discard a tile."] = "Ви закликали: тепер скиньте плитку.",
        ["Your move: click a tile to discard it."] = "Ваш хід: клацніть плитку, щоб скинути її.",
        ["Suggested: {0} (keeps you at {1}). Green outline marks them."] = "Радимо: {0} (лишає вас на рівні: {1}). Їх позначено зеленою рамкою.",
        ["You can't discard the {0} you just called, and the dimmed tiles are locked this turn."] =
            "Не можна скинути щойно закликану {0}; затемнені плитки заблоковані цього ходу.",
        ["Dimmed tiles are locked this turn: you can't discard the tile you just called, or the other end of the same run."] =
            "Затемнені плитки заблоковані цього ходу: не можна скинути щойно закликану плитку або протилежний кінець тієї самої послідовності.",
        ["Blue outline = the tile you just drew. Hover a tile to see where it leaves you. Tab and Enter also work."] =
            "Синя рамка = щойно витягнута плитка. Наведіть на плитку, щоб побачити результат. Tab і Enter теж працюють.",

        // Result
        ["{0} wins by tsumo (self-drawn) on turn {1}."] = "{0} перемагає цумо (власний добір) на ході {1}.",
        ["{0} wins by ron on {1}, discarded by {2}, on turn {3}."] = "{0} перемагає роном на {1}, скинуту гравцем {2}, на ході {3}.",
        ["Exhaustive draw: the wall ran out after {0} turns and nobody won."] = "Нічия: стіна закінчилася після {0} ходів, ніхто не виграв.",
        ["{0} = {1} han"] = "{0} = {1} хан",
        [" · dora {0}"] = " · дора {0}",
        ["yakuman"] = "якуман",

        // Yaku
        ["Self-draw (menzen tsumo)"] = "Власний добір (менцен цумо)",
        ["Last tile draw (haitei)"] = "Остання плитка зі стіни (хайтей)",
        ["Last discard (houtei)"] = "Останній скид (хотей)",
        ["After a kan (rinshan)"] = "Після кана (рінсян)",
        ["Robbing a kan (chankan)"] = "Пограбування кана (чанкан)",
        ["All simples (tanyao)"] = "Усі прості (тан'яо)",
        ["All terminals and honours"] = "Усі крайні й почесні (хонрото)",
        ["Half flush (honitsu)"] = "Половинний флеш (хонніцу)",
        ["Full flush (chinitsu)"] = "Повний флеш (чиніцу)",
        ["Dragon triplet"] = "Трійка драконів",
        ["Seat wind"] = "Вітер місця",
        ["Round wind"] = "Вітер раунду",
        ["No-points hand (pinfu)"] = "Рука без очок (пінфу)",
        ["Twice the same run (ryanpeikou)"] = "Двічі по дві однакові послідовності (рянпейко)",
        ["Same run twice (iipeikou)"] = "Дві однакові послідовності (іпейко)",
        ["Same run in three suits (sanshoku)"] = "Та сама послідовність у трьох мастях (сансьоку)",
        ["Straight (ittsu)"] = "Стрейт (іццу)",
        ["Terminal or honour in every set (chanta)"] = "Крайня чи почесна в кожному сеті (чанта)",
        ["Terminal in every set (junchan)"] = "Крайня в кожному сеті (дзюнчан)",
        ["All triplets (toitoi)"] = "Усі трійки (тойтой)",
        ["Three concealed triplets (sanankou)"] = "Три закриті трійки (сананко)",
        ["Same triplet in three suits (sanshoku doukou)"] = "Та сама трійка в трьох мастях (сансьоку доко)",
        ["Three kans (sankantsu)"] = "Три кани (санканцу)",
        ["Little three dragons (shousangen)"] = "Малі три дракони (сьосанген)",
        ["Seven pairs"] = "Сім пар (чийтойцу)",
        ["Thirteen orphans"] = "Тринадцять сиріт (кокусі)",
        ["All honours"] = "Усі почесні (цуїсо)",
        ["All terminals"] = "Усі крайні (чінрото)",
        ["All green"] = "Усе зелене (рюїсо)",
        ["Nine gates"] = "Дев'ять брам (чурен пото)",
        ["Big three dragons"] = "Великі три дракони (дайсанген)",
        ["Big four winds"] = "Великі чотири вітри (дайсусі)",
        ["Little four winds"] = "Малі чотири вітри (сьосусі)",
        ["Four kans"] = "Чотири кани (суканцу)",
        ["Four concealed triplets"] = "Чотири закриті трійки (суанко)",
    };
}
