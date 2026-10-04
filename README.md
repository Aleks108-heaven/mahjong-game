# Mahjong Game

A riichi mahjong engine in C# (.NET 10) with a console simulation: four bot players play one round against each other.

> **Status: playable prototype.** You can take a seat in the Windows app and discard against three bots, or just watch four bots play. The wall, dealing, turn flow, shanten maths, tsumo, ron, calls (pon, chi, kan), dora indicators, yaku and furiten work. Riichi and scoring are not built (see [Roadmap](#roadmap)).

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The desktop app needs Windows.

### Step 1: open a terminal in the project folder

Open PowerShell (press the Windows key, type `PowerShell`, press Enter). A new terminal starts in your user folder, not in the game, so first move into the game folder with `cd` (change directory):

```powershell
cd "C:\path\to\Mahjong Game"
```

Change the path if you keep the project somewhere else. Keep the quotes because the folder name has a space. Every command below must be run from this folder, one command at a time. Don't paste the `#` comments.

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

Console version (four bots play one round as text, no window):

```powershell
dotnet run
```

Same console round every time, using seed 7 (any whole number works):

```powershell
dotnet run -- 7
```

Run the test suite:

```powershell
dotnet test Tests
```

A non-integer seed in the console version prints a usage message and exits with code 1.

## Desktop app (Mahjong Table)

A WPF window in [Wpf/](Wpf/) that draws the round on a green table: four player panels with each hand, shanten and discard river, plus a control sidebar and the game log.

### Playing a seat

Pick **East, South, West or North** under *Your seat* (East is the default) and you play that seat against three bots. Picking a seat starts a new round. Choose **Watch** to spectate four bots instead.

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
| **Your seat** | Seat to play, or Watch to spectate. Changing it starts a new round. |
| **Hint** (H) | Shows the best discards for your current hand (locked tiles excluded) |
| **Pon** (P) / **Chi** (C) / **Kan** (K) / **Pass** (S) | Answer a call prompt (K also declares your own kan when offered) |
| **Next turn** (`→`) | Spectator only: plays one full turn (draw, tsumo check, discard, ron check) |
| **Auto-play** (`A`) | Spectator only: plays turns by itself |
| **Speed slider** | 100-1500 ms per turn (spectating) or per bot turn (playing) |
| **New round** (`N`) | Deals again. A whole-number seed replays the same round; empty means random. |
| **Show all hands** | Face-up or face-down for the other players |
| **Language** | English or Українська (Ukrainian, the default). Translates the window; the game log stays English. |
| **Game log** | The same text the console version prints |

On screen:

- The player whose draw is next gets a white outline and a "Draws next" label; your own panel has a blue outline.
- The last discard has an orange outline.
- A win turns the panel gold, marks the winning tile on a ron, and shows a result banner with the yaku, han and dora count. Next turn and Auto-play are then disabled.
- The header shows the dora indicators; a new one appears after each kan. A tenpai player who is furiten shows "Tenpai · Furiten".
- A closed kan shows its two outer tiles face-down.
- Tiles show the suit as a glyph (萬 characters, 筒 circles, 索 bamboo) as well as by colour, and each tile has a tooltip such as "1 of bamboo (1s)".
- The layout is drawn at a fixed size and scaled, so it fits small and large windows.
- Keyboard focus is shown with a thick white border.

Developer shortcut: `MahjongTable.exe <seed>` starts that round with you in East. Adding a turn count (`MahjongTable.exe <seed> <turns>`) starts as a spectator and plays that many turns first.

### Example output (seed 7)

```
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
| [Engine/GameEngine.cs](Engine/GameEngine.cs) | Round state machine with `StartRound()`, `PlayTurn()` and `RunSimulation()`. Raises a `Logged` event and writes to a `TextWriter`. |
| [Program.cs](Program.cs) | Console entry point |
| [Wpf/MainWindow.xaml(.cs)](Wpf/MainWindow.xaml) | Table layout, controls and the refresh logic |
| [Wpf/TileView.cs](Wpf/TileView.cs) | Draws one tile, face-up or face-down, with an optional highlight |

Notes:

- **Bots** discard the tile that leaves the lowest shanten (ties random). They win far more often than real players: 141 of seeds 1-200 ended in a win (47 tsumo, 94 ron), about 70%.
- **Reproducibility:** `new GameEngine(seed, writer)` makes a round repeatable and lets tests capture output.
- **Turn flow:** the wall is checked only when a turn starts, so every turn that begins finishes. A full exhaustive round is 70 draws (fewer with kans), and the round ends as soon as the last live tile has been drawn and discarded.
- **Stepping:** `PlayTurn()` plays one turn and `RunSimulation()` plays the whole round; both give the same result for the same seed.
- **Human seat:** `StartRound(seat)` hands one seat to a person. `PlayTurn()` then stops with `AwaitingHumanDiscard` true on that seat's 14-tile hand, and `DiscardHuman(tile)` resumes the round. `PlayerHand.SuggestDiscards()` and `ShantenAfterDiscard()` back the hint.
- **Ron** is checked in turn order from the discarder's left.
- **Calls:** after a discard, ron is checked first, then pon (any other seat), then chi (only the seat to the discarder's left). Bots call only when the call lowers their best reachable shanten. The last discard of the round can't be called. A called tile leaves the discarder's river and joins the caller's meld, and the caller discards without drawing.
- **Swap-calling rule:** right after a call you can't discard the called tile, and after a chi you also can't discard the tile that would complete the same run from the other end (for example, after chi 3-4-5 on the 3, the 6 is locked). The window dims locked tiles and the engine rejects them.
- **Open hands:** a meld counts as three tiles (a kan too), so a hand is complete at 14 tiles counting melds. Seven pairs and thirteen orphans only count for closed hands.
- **Kan:** an open kan (three in hand plus another player's discard), a closed kan (four in hand, hand stays closed) and an added kan (fourth tile on a pon). The kan player draws a replacement tile from the dead wall and then discards; the live wall shrinks by one per kan, so a round has `70 - kans` draws. Closed kans flip a dora indicator at once, open and added kans after the kan player's discard. An added kan can be robbed by a player waiting on that tile (chankan). At most four kans a round, none on the last live tile; the four-kan abortive draw is not implemented.
- **Yaku:** a hand needs at least one to win, by tsumo or ron. Implemented: menzen tsumo, pinfu, tanyao (open allowed), iipeikou, ryanpeikou, dragon / seat wind / round wind triplets, sanshoku (run and triplet), ittsu, chanta, junchan, toitoi, sanankou, sankantsu, shousangen, honroutou, honitsu, chinitsu, seven pairs, haitei, houtei, rinshan, chankan, and the yakuman thirteen orphans, big three dragons, four concealed triplets, little and big four winds, all honours, all terminals, all green, nine gates and four kans. Not implemented: riichi, ippatsu, double riichi, tenhou, chiihou. The round wind is always East. Dora are counted and shown but do not count as a yaku, and there is no fu or point scoring.
- **Furiten:** a tenpai player whose winning tile is among their own discards (called tiles included) can't ron, only tsumo. Temporary furiten (after passing a ron) doesn't exist because ron is automatic.
- **Bots and yaku:** bots only open their hand when a yaku stays possible: a value-tile set, or an all-simples plan with at most three terminals and honours left, and then they shed terminals and honours first. They declare a kan when it doesn't make the hand worse.
- **Human calls:** PlayTurn() also stops with AwaitingHumanCall true and a PendingCall describing what is possible; answer with HumanPon(), HumanChi(option), HumanKan() or HumanPass(). On your own discard, `HumanKanOptions` lists closed and added kans and `HumanDeclareKan(option)` declares one.

## Tests

100 xunit tests in [Tests/](Tests/) cover:

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

The window itself has no automated tests. It was checked by running it, driving it with the keyboard (Enter to discard, `H` for a hint) and looking at screenshots of the start, mid-game, a ron win and an exhaustive draw.

CI runs on every push and pull request ([.github/workflows/ci.yml](.github/workflows/ci.yml)): build, tests and a smoke run on Ubuntu, and a build of the desktop app on Windows.

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

Known limits: shanten was checked on a handful of hands, not against a full reference table. The yaku list has no riichi, ippatsu, tenhou or chiihou, and there is no scoring. The window changes for kan, dora and furiten were checked only by launching the app, not by driving a kan with the keyboard or comparing screenshots.

## Roadmap

- Riichi (with ura dora and ippatsu), tenhou and chiihou, the four-kan abortive draw
- Scoring: fu, han to points, payments
- Rounds, dealer rotation and winds
- Let the human choose whether to call tsumo or ron (both are automatic for now); that would also allow temporary furiten
- Show who called what in a short on-screen history, not only in the log
- Animations and sound in the desktop app
