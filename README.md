# Mahjong Table

[![CI](https://github.com/Aleks108-heaven/mahjong-game/actions/workflows/ci.yml/badge.svg)](https://github.com/Aleks108-heaven/mahjong-game/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/Aleks108-heaven/mahjong-game)](https://github.com/Aleks108-heaven/mahjong-game/releases/latest)

Riichi mahjong for Windows, with computer opponents from **Junior** to **Senior**. The game runs on a riichi engine written in C# (.NET 10); there is also a console simulation where four bots play a round against each other.

> **Status: playable prototype (v0.1.0).** You can take a seat in the Windows app and play against three bots, or watch four bots play. The wall, dealing, turn flow, shanten maths, tsumo, ron, calls (pon, chi, kan), dora indicators, yaku and furiten work. Riichi and scoring are not built (see [Roadmap](#roadmap)).

## Download and install

1. Open the [latest release](https://github.com/Aleks108-heaven/mahjong-game/releases/latest) and download **`MahjongTable-<version>-Setup.exe`**.
2. Run it. No administrator rights are needed; it installs for your user and adds a Start menu entry (and a desktop icon if you tick the box). The installer speaks English and Ukrainian.
3. Start **Mahjong Table** from the Start menu.

Prefer not to install? Download the **`...-portable.zip`**, unzip it anywhere and run `MahjongTable.exe`.

Needs Windows 10 or 11 (64-bit). Nothing else has to be installed.

**Windows may warn you.** The files are not code-signed, so Windows SmartScreen can say "Windows protected your PC" the first time. Check the download (below), then choose **More info**, then **Run anyway**. The first start can also take several seconds while Windows scans the new file.

**Check your download.** Each release lists SHA-256 checksums in `SHA256SUMS.txt`:

```powershell
Get-FileHash .\MahjongTable-0.1.0-Setup.exe -Algorithm SHA256
```

The releases are built by GitHub Actions from this repository, with signed build provenance. With the [GitHub CLI](https://cli.github.com/) you can confirm that:

```powershell
gh attestation verify .\MahjongTable-0.1.0-Setup.exe --repo Aleks108-heaven/mahjong-game
```

There is no automatic update: to update, install the newest release over the old one. To uninstall, use Windows **Settings > Apps**. That also removes the saved settings; the portable build keeps them in `%LOCALAPPDATA%\MahjongTable`, so delete that folder to forget them.

## Choose your opponents

Pick the level in the sidebar under **Opponents**. Changing it starts a new round. New installs start on Junior.

| Level | Plays like | What it does |
| --- | --- | --- |
| **Junior** | Someone learning the game | Plays for a fast hand but about one discard in five is a random tile. Never chis or kans, only pons dragons and the winds that score, and never thinks about what the others hold. |
| **Middle** | A solid club player (the original bot) | Discards for the lowest shanten, calls only when it makes the hand faster and a yaku stays possible, and declares a kan when it doesn't hurt. |
| **Senior** | A strong player | Among equally fast discards it keeps the tiles with the most unseen improving tiles. Once tenpai it counts only the waits that can really win (a yaku, no furiten). It never takes an open kan, and when an opponent has opened three sets (or two, late) and its own hand is still far from winning it folds: it throws the safest tile, starting with tiles that opponent already discarded (those can't be ron'd: furiten). |

A bot only ever uses what a player could see: its own hand, every river and meld, and the dora indicators. It never reads another hand or the wall. This is built in, not just promised: bots receive a `TableView` of public information instead of the engine, and the tests check that what a seat can see adds up to its own tiles plus what lies face-up.

### How strong is each level?

Measured with the built-in arena, which plays the same seeded rounds and rotates the levels through every seat so none keeps the East advantage (rates are per seat played; draws are exhaustive draws):

| Table (4000 rounds) | Win rate | Deal-in rate |
| --- | --- | --- |
| Junior, Middle, Senior, Senior | Junior 4.5%, Middle 15.3%, Senior 29.4% | 17.6%, 14.4%, 10.0% |
| Three Middle, one Senior | Middle 15.5%, **Senior 30.2%** | 13.9%, **8.6%** |
| Three Junior, one Middle | Junior 6.2%, **Middle 22.1%** | 7.5%, **2.9%** |
| Three Junior, one Senior | Junior 5.4%, **Senior 40.6%** | 11.5%, **3.0%** |

What a human sits down to is a table of one level (2000 rounds each):

| All bots | Rounds that end in an exhaustive draw | Win rate per bot |
| --- | --- | --- |
| Junior | 74% | 6.5% |
| Middle | 30% | 17.4% |
| Senior | 11% | 22.2% |

Rerun any of it: `dotnet run -- arena 4000 --table junior,middle,senior,senior` (see [Console](#console)). Runs are repeatable; the same arguments give the same numbers.

What these numbers do and don't say:

- Senior's edge comes mostly from choosing better among equally fast discards. With Senior's other behaviours switched off, ukeire counting alone took it from 17.2% (the same as Middle) to 26.2%, and the keep-order alone (lone honours and terminals go first) to 27.6%; together with yaku-aware waits it reached 28.8%, and with everything 30.2%. These ablations were run with temporary switches that are not in the shipped code.
- **Defence is a trade, not a win.** Folding lowered Senior's deal-in rate from 9.5% to 8.6% and its win rate from 31.1% to 30.2%, so on a "wins minus deal-ins" count it is level. There is no riichi to signal a ready hand, so the only warning sign a bot can read is an open hand. It is in because it is how a strong player behaves; it should pay more once riichi and scoring exist.
- With no scoring, "strength" here means winning and not dealing in. A real score would weigh hand value too.
- Bots have no idea how the human plays, and the arena has no human in it.

## Run it from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The desktop app needs Windows.

### Step 1: open a terminal in the project folder

Open PowerShell (press the Windows key, type `PowerShell`, press Enter). A new terminal starts in your user folder, not in the game, so first move into the game folder with `cd` (change directory):

```powershell
cd "C:\path\to\Mahjong Game"
```

Replace the path with the folder where you saved the project. Keep the quotes because the folder name has a space. Every command below must be run from this folder, one command at a time. Don't paste the `#` comments.

### Step 2: start the game

Desktop app (a window opens; the first start builds it, so it can take a few seconds):

```powershell
.\Play.cmd
```

PowerShell only runs a program in the current folder if you write `.\` in front of it. In Command Prompt (`cmd`) you can type `Play.cmd` without it, and in File Explorer you can just double-click `Play.cmd`.

If you prefer not to use the batch file, this does the same:

```powershell
dotnet run --project Wpf\MahjongTable.csproj -c Release
```

Run the test suite:

```powershell
dotnet test Tests
```

### Console

The console version plays one round as text, no window:

```powershell
dotnet run
```

Same round every time, using seed 7 (any whole number works; note that .NET treats seeds `n` and `-n` as the same):

```powershell
dotnet run -- 7
```

Choose the bots' level (`junior`, `middle` or `senior`; the default is `middle`, the original bot):

```powershell
dotnet run -- 7 --level senior
```

Seat levels against each other and print win and deal-in rates. The table lists the level in each seat, which rotates every round; `--first-seed` picks where the seeds start:

```powershell
dotnet run -- arena 400 --table junior,middle,senior,senior --first-seed 1
```

A bad seed, level or table prints a usage message and exits with code 1.

## Desktop app

A WPF window in [Wpf/](Wpf/) that draws the round on a green table: four player panels with each hand, shanten and discard river, plus a sidebar for setup and the game log. The window remembers your language, opponent level and seat between runs.

### Playing a seat

Pick **East, South, West or North** under *Your seat* (East is the default) and you play that seat against three bots of the level you chose. Picking a seat starts a new round. Choose **Watch** to spectate four bots instead.

1. When it is your move, your panel says "Your move" and the drawn tile has a **blue outline**.
2. **Click a tile to discard it.** Tab to a tile and press Enter works too. Hovering a tile shows where discarding it leaves you, for example "Discard 5p: shanten 2".
3. **Hint** (`H`) outlines the discards, in green, that keep you closest to winning, and says what shanten they keep.
4. The bots then play their turns at the chosen speed, and the game stops again at your next move.
5. **Calls.** When a bot discards a tile you could use, the game pauses and asks. Choose **Pon** (P) to take it for a triplet, **Chi** (C, or the button for each possible run) to take it for a run, or **Pass** (S). Pass is the focused default, so a stray Enter never calls by accident. After a call you take the tile, it joins an open meld next to your hand, and you discard without drawing.
6. **Kan.** When a bot discards a fourth tile you hold three of, the call prompt also offers **Kan** (K). On your own turn, four of a kind in hand or the fourth tile of a pon you made shows a **Closed kan** or **Add to pon** button next to Hint (K takes the first). After a kan you draw a replacement tile and discard as usual.
7. A win is declared for you automatically: tsumo when your draw completes your hand and it has a yaku, ron when a bot discards your winning tile, you are not furiten and the hand has a yaku. A complete hand with no yaku is not a win; the log says so and play continues. At the end of the round every hand is revealed.

Other players' hands start face-down while you play (tick *Show all hands* to peek).

### Controls

| Control | What it does |
| --- | --- |
| **Opponents** | Junior, Middle or Senior bots. Changing it starts a new round. |
| **Your seat** | Seat to play, or Watch to spectate. Changing it starts a new round. |
| **Seed** | Empty means a random round. A whole number replays the same round. |
| **New round** (`N`) | Deals again |
| **Hint** (H) | Shows the best discards for your current hand (locked tiles excluded) |
| **Pon** (P) / **Chi** (C) / **Kan** (K) / **Pass** (S) | Answer a call prompt (K also declares your own kan when offered) |
| **Next turn** (`→`) | Watching only: plays one full turn (draw, tsumo check, discard, ron check) |
| **Auto-play** (`A`) | Watching only: plays turns by itself |
| **Speed slider** | 100-1500 ms per turn (watching) or per bot turn (playing) |
| **Show all hands** | Face-up or face-down for the other players |
| **Language** | English or Українська (Ukrainian, the default). Translates the window; the game log stays English. |
| **Game log** | The same text the console version prints |

On screen:

- The player whose draw is next gets a white outline and a "Draws next" label; your own panel has a blue outline. Bot panels say which level they play.
- The last discard has an orange outline.
- A win turns the panel gold, marks the winning tile on a ron, and shows a result banner ("You win!" or "Round over", or "Draw") with the yaku, han and dora count. Next turn and Auto-play are then disabled.
- The header shows the dora indicators; a new one appears after each kan. A tenpai player who is furiten shows "Tenpai · Furiten".
- A closed kan shows its two outer tiles face-down.
- Tiles show the suit as a glyph (萬 characters, 筒 circles, 索 bamboo) as well as by colour, and each tile has a tooltip such as "1 of bamboo (1s)".
- The layout is drawn at a fixed size and scaled, so it fits small and large windows.
- Keyboard focus is shown with a thick white border.

Developer shortcut: `MahjongTable.exe [seed] [turns] [--level junior|middle|senior] [--lang en|uk]`. A seed starts that round with you in East. Adding a turn count starts as a spectator and plays that many turns first. These options are not saved as settings.

### Example output (seed 7)

```text
P0(E) starting hand: 1m 1m 5m 1p 2p 5p 8p 4s 7s 8s 9s 5z 6z
P1(S) starting hand: 3m 3m 4m 5m 8m 4p 5p 2s 3s 3s 5s 5z 7z
...

T1   P0(E) draws 6p  | 1m 1m 5m 1p 2p 5p 6p 8p 4s 7s 8s 9s 5z 6z | shanten 3 | wall 69
     P0(E) discards 6z | discards so far 1
T2   P1(S) draws 4s  | 3m 3m 4m 5m 8m 4p 5p 2s 3s 3s 4s 5s 5z 7z | shanten 2 | wall 68
...
Wall exhausted after 70 turns. Exhaustive draw.
```

Each draw line shows the turn number, seat and wind, the tile drawn, the full 14-tile hand, its shanten, and the live-wall count. A round ends in one of three ways: tsumo (win on own draw), ron (win on another player's discard) or an exhaustive draw.

## Tile notation

Standard riichi notation:

| Tiles | Notation |
| --- | --- |
| Characters (manzu) | `1m`-`9m` |
| Circles (pinzu) | `1p`-`9p` |
| Bamboo (souzu) | `1s`-`9s` |
| Honors | `1z` East, `2z` South, `3z` West, `4z` North, `5z` White, `6z` Green, `7z` Red |

## How it works

| File | Role |
| --- | --- |
| [Engine/Tile.cs](Engine/Tile.cs) | Immutable tile with validation, value equality and a 0-33 index |
| [Engine/Meld.cs](Engine/Meld.cs) | Melds (chi, pon, three kinds of kan), chi and kan options, call options for the human, and the swap-calling lock |
| [Engine/Wall.cs](Engine/Wall.cs) | 136 tiles, Fisher-Yates shuffle, 122 live + 14 dead (4 kan replacement tiles, 5 dora indicators, 5 spare). Each kan tops the dead wall up from the live wall. `Wall.Stacked` builds a chosen deal for tests. |
| [Engine/Dora.cs](Engine/Dora.cs) | Indicator to dora tile, and dora counting |
| [Engine/Yaku.cs](Engine/Yaku.cs) | Finds the yaku of a complete hand by trying every way to split it into sets |
| [Engine/PlayerHand.cs](Engine/PlayerHand.cs) | Hand storage, shanten, win checks, waits, kan options, shanten-minimising discard |
| [Engine/ShantenCalculator.cs](Engine/ShantenCalculator.cs) | Shanten for standard hands, seven pairs and thirteen orphans. `-1` means complete. |
| [Engine/TurnState.cs](Engine/TurnState.cs) | `Draw` > `ActionPhase` > `Discard` > `WaitPhase` > `NextPlayer` |
| [Engine/GameEngine.cs](Engine/GameEngine.cs) | Round state machine with `StartRound()`, `PlayTurn()` and `RunSimulation()`. Raises a `Logged` event and writes to a `TextWriter`. Asks each bot's brain for its decisions. |
| [Engine/Bots/](Engine/Bots/) | The bot levels: `IBotBrain` (the decisions), `JuniorBrain`, `MiddleBrain`, `SeniorBrain`, `TableView` (what a bot may see), `TileEfficiency` (unseen improving tiles) and `Arena` (the benchmark) |
| [Program.cs](Program.cs) | Console entry point: one round, or the arena |
| [Wpf/MainWindow.xaml(.cs)](Wpf/MainWindow.xaml) | Table layout, controls and the refresh logic |
| [Wpf/TileView.cs](Wpf/TileView.cs) | Draws one tile, face-up or face-down, with an optional highlight |
| [Wpf/UserSettings.cs](Wpf/UserSettings.cs) | The saved language, level and seat |
| [Wpf/Loc.cs](Wpf/Loc.cs) | English and Ukrainian window text |
| [installer/](installer/), [tools/](tools/) | The Inno Setup script and the scripts that build the release files |

Notes:

- **Bots:** each seat has a level (`new GameEngine(seed, writer, BotLevel.Senior)`, or one level per seat). Middle is the original bot: 141 of seeds 1-200 ended in a win (47 tsumo, 94 ron), about 70%, far more often than real players because it never defends. Its play is pinned: the logs of seeds 1-200 matched the pre-levels engine byte for byte, and a test keeps seeds 1-30 from drifting.
- **Reproducibility:** `new GameEngine(seed, writer)` makes a round repeatable and lets tests capture output.
- **Turn flow:** the wall is checked only when a turn starts, so every turn that begins finishes. A full exhaustive round is 70 draws (fewer with kans), and the round ends as soon as the last live tile has been drawn and discarded.
- **Stepping:** `PlayTurn()` plays one turn and `RunSimulation()` plays the whole round; both give the same result for the same seed.
- **Human seat:** `StartRound(seat)` hands one seat to a person. `PlayTurn()` then stops with `AwaitingHumanDiscard` true on that seat's 14-tile hand, and `DiscardHuman(tile)` resumes the round. `PlayerHand.SuggestDiscards()` and `ShantenAfterDiscard()` back the hint.
- **Ron** is checked in turn order from the discarder's left.
- **Calls:** after a discard, ron is checked first, then pon (any other seat), then chi (only the seat to the discarder's left). A bot calls when its brain wants to (Middle: only when the call lowers its best reachable shanten and a yaku stays possible). The last discard of the round can't be called. A called tile leaves the discarder's river and joins the caller's meld, and the caller discards without drawing.
- **Swap-calling rule:** right after a call you can't discard the called tile, and after a chi you also can't discard the tile that would complete the same run from the other end (for example, after chi 3-4-5 on the 3, the 6 is locked). The window dims locked tiles and the engine rejects them.
- **Open hands:** a meld counts as three tiles (a kan too), so a hand is complete at 14 tiles counting melds. Seven pairs and thirteen orphans only count for closed hands.
- **Kan:** an open kan (three in hand plus another player's discard), a closed kan (four in hand, hand stays closed) and an added kan (fourth tile on a pon). The kan player draws a replacement tile from the dead wall and then discards; the live wall shrinks by one per kan, so a round has `70 - kans` draws. Closed kans flip a dora indicator at once, open and added kans after the kan player's discard. An added kan can be robbed by a player waiting on that tile (chankan). At most four kans a round, none on the last live tile; the four-kan abortive draw is not implemented.
- **Yaku:** a hand needs at least one to win, by tsumo or ron. Implemented: menzen tsumo, pinfu, tanyao (open allowed), iipeikou, ryanpeikou, dragon / seat wind / round wind triplets, sanshoku (run and triplet), ittsu, chanta, junchan, toitoi, sanankou, sankantsu, shousangen, honroutou, honitsu, chinitsu, seven pairs, haitei, houtei, rinshan, chankan, and the yakuman thirteen orphans, big three dragons, four concealed triplets, little and big four winds, all honours, all terminals, all green, nine gates and four kans. Not implemented: riichi, ippatsu, double riichi, tenhou, chiihou. The round wind is always East. Dora are counted and shown but do not count as a yaku, and there is no fu or point scoring.
- **Furiten:** a tenpai player whose winning tile is among their own discards (called tiles included) can't ron, only tsumo. Temporary furiten (after passing a ron) doesn't exist because ron is automatic.
- **Bots and yaku:** Middle and Senior only open their hand when a yaku stays possible: a value-tile set, or an all-simples plan with at most three terminals and honours left, and then they shed terminals and honours first.
- **Human calls:** PlayTurn() also stops with AwaitingHumanCall true and a PendingCall describing what is possible; answer with HumanPon(), HumanChi(option), HumanKan() or HumanPass(). On your own discard, `HumanKanOptions` lists closed and added kans and `HumanDeclareKan(option)` declares one.

## Tests

`dotnet test Tests` runs 198 xunit tests in [Tests/](Tests/). They cover:

- tile validation, equality and notation
- shanten for known hands in all three shapes
- wall composition (136 tiles, 4 of each, 122/14 split)
- tsumo/ron detection and seed reproducibility
- turn completion over 200 seeds
- the step API: one turn at a time, round end after turn 70, reset on a new round, the `Logged` event, and stepping matching a full run
- the human seat: the engine pauses on your 14-tile hand, `PlayTurn` does nothing while it waits, bad or out-of-turn discards throw and leave the round waiting, and 100 full rounds with a human playing hint discards always finish
- hints: the suggested discards agree with the shanten after each discard, and never include locked tiles
- calls: which pons and chis a hand can make (suit, edges of the suit, honours), meld contents, how melds count towards shanten, tenpai and winning, and the swap-calling lock for pon and chi at every position in the run
- bots and rounds: calls happen regularly, melds are well formed (chi only from the player on the left), no tile is ever lost or duplicated over 300 rounds, nobody calls the last discard, and a human who takes every call still finishes every round
- the human call prompt: only legal options are offered, the round pauses while it waits, pass leaves the hand alone, pon and chi take the tile and lock the discard, and illegal calls throw
- dora: indicator to dora tile (including the wraps), counting through melds and kans
- yaku: tanyao, pinfu and its wait rule, a closed ron with no yaku being rejected while the tsumo wins, open hands with and without yaku, value winds, seven pairs, thirteen orphans, flush plus straight, a closed kan keeping the hand closed
- kan and furiten: a prepared wall where the human holds four of a tile, closed kan taking the replacement tile, flipping a dora and keeping the tile count at 136, unavailable kans throwing, waits, and 400 full rounds checking that every win has a yaku, no ron winner was furiten and no tile is lost
- bot levels: every level finishes every round without losing a tile, rounds are repeatable, mixed tables work, a human seat works against Senior bots, and the Middle bot's logs for seeds 1-30 match a stored hash (it still plays the original game exactly)
- what a bot can see: no tile is ever seen more than four times, and a view adds up to the seat's own tiles plus what lies face-up
- Junior: about the stated share of mistakes, never a forbidden tile, pons only tiles that score, never chis or kans
- Senior: picks the discard with the most unseen improving tiles (checked against an independent count on 300 random hands), stays tenpai instead of folding, folds to a safe tile against an open hand, respects the swap-calling rule, skips open kans, and won't pon, chi or add a kan while folding
- tile efficiency: the pruned search gives the same answer as trying every tile (1500 random hands, including seven-pairs and thirteen-orphans shapes), and tiles already seen aren't counted
- the arena: levels rotate through every seat, results are repeatable, bad arguments throw, and the levels form a ladder (Junior < Middle < Senior in win rate)
- settings: every choice survives a write and a read, damaged, foreign, out-of-range or oversized files give the defaults, a failed save never interrupts the game
- translations: every text the window can show has a Ukrainian translation, every yaku name has one, translations keep their `{0}` placeholders, and the level and language parsers reject numbers and unknown names

The window itself has no automated tests. It was checked by running it: driving the real app through Windows UI Automation (choose a level, switch language and mode, restart and confirm the choices came back, start with a damaged settings file), keyboard play, and screenshots of the start, mid-game, a finished round and the smallest window size. See the [QA report](docs/qa-report-0.1.0.md).

CI runs on every push and pull request ([.github/workflows/ci.yml](.github/workflows/ci.yml)): restore from the lock file, build, tests and smoke runs on Ubuntu, and on Windows a build of the desktop app plus the full packaging script, so a broken installer shows up before release day.

## Building a release

`tools\package.ps1` builds the exe, the portable zip, the installer and the checksums (it needs Inno Setup 6). Pushing a tag such as `v0.1.0` makes GitHub Actions do the same from a clean checkout and publish the release. See [RELEASING.md](RELEASING.md). Changes are listed in [CHANGELOG.md](CHANGELOG.md).

## QA and design review

An initial review (source review plus runtime checks) found these issues, all now fixed:

| # | Severity | Issue | Fix |
| --- | --- | --- | --- |
| 1 | High | No win detection; bots threw away complete hands | Tsumo and ron added; discards minimise shanten |
| 2 | Medium | Last player never discarded | Wall checked only at turn start |
| 3 | Medium | `Tile` accepted invalid ranks (`Tile(Manzu,10)` collided with 1p, rank 0 gave index -1) | Constructor validation |
| 4 | Medium | `PlayerHand.Remove` used reference equality | Value equality on `Tile` |
| 5 | Low | Seven pairs and thirteen orphans not counted | Added to `CalculateShanten` |
| 6 | Low | No seed, no tests, no project file | Seed parameter, test project, csproj |
| 7 | Info | Unreachable early return in setup | Removed. Dead wall kept for kans and dora. |

Design fixes: white/green/red dragons were `P`/`F`/`C`, easy to confuse with pinzu, and are now `5z`-`7z`. Output gained turn numbers, seat winds, the full hand and the wall count. Rendering goes through a `TextWriter`, so a UI can replace the console.

The release review ([docs/qa-report-0.1.0.md](docs/qa-report-0.1.0.md)) covers the bot levels, the app, the installer and the release process, with what was tested, what was found and what could not be tested.

Known limits: shanten was checked on a handful of hands, not against a full reference table. The yaku list has no riichi, ippatsu, tenhou or chiihou, and there is no scoring. The window has no automated tests, and screen readers, high-DPI scaling and other Windows versions than Windows 11 were not tested.

## Roadmap

- Riichi (with ura dora and ippatsu), tenhou and chiihou, the four-kan abortive draw. Senior could then read riichi, which is the signal its defence is missing.
- Scoring: fu, han to points, payments
- Rounds, dealer rotation and winds
- Let the human choose whether to call tsumo or ron (both are automatic for now); that would also allow temporary furiten
- Show who called what in a short on-screen history, not only in the log
- Animations and sound in the desktop app
- Code-sign the installer so Windows SmartScreen stops warning
