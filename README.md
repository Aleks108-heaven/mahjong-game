# Mahjong Game

A riichi mahjong engine in C# (.NET 10) with a console simulation: four bot players play one round against each other.

> **Status: engine prototype with a desktop viewer.** The wall, dealing, turn flow, shanten maths, tsumo and ron work, and a Windows app shows four bots playing a round. You can't play a tile yourself yet, and calls, riichi, yaku and scoring are not built (see [Roadmap](#roadmap)).

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The desktop app needs Windows.

```powershell
Play.cmd                                     # desktop app (double-click it, or run it)
dotnet run --project Wpf\MahjongTable.csproj # same, without the batch file
dotnet run                                   # console version, random round
dotnet run -- 7                              # console version, reproducible round (seed 7)
dotnet test Tests                            # run the test suite
```

A non-integer seed in the console version prints a usage message and exits with code 1.

## Desktop app (Mahjong Table)

A WPF window in [Wpf/](Wpf/) that draws the round on a green table: four player panels with each hand, shanten and discard river, plus a control sidebar and the game log.

| Control | What it does |
|---|---|
| **Next turn** (`→`) | Plays one full turn: draw, tsumo check, discard, ron check |
| **Auto-play** (`A`) | Plays turns by itself; the slider sets 100-1500 ms per turn |
| **New round** (`N`) | Deals again. A whole-number seed replays the same round; empty means random. |
| **Show all hands** | Untick to turn every hand face-down |
| **Game log** | The same text the console version prints |

On screen:

- The player whose draw is next gets a white outline and a "Draws next" label.
- The last discard has an orange outline.
- A win turns the panel gold, marks the winning tile on a ron, and shows a result banner. Next turn and Auto-play are then disabled.
- Tiles show the suit as a glyph (萬 characters, 筒 circles, 索 bamboo) as well as by colour, and each tile has a tooltip such as "1 of bamboo (1s)".
- The layout is drawn at a fixed size and scaled, so it fits small and large windows.
- Keyboard focus is shown with a thick white border.

Developer shortcut: `MahjongTable.exe <seed> <turns>` opens the app with a seed and plays that many turns first.

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
|---|---|
| Characters (manzu) | `1m`-`9m` |
| Circles (pinzu) | `1p`-`9p` |
| Bamboo (souzu) | `1s`-`9s` |
| Honors | `1z` East, `2z` South, `3z` West, `4z` North, `5z` White, `6z` Green, `7z` Red |

## How it works

| File | Role |
|---|---|
| [Engine/Tile.cs](Engine/Tile.cs) | Immutable tile with validation, value equality and a 0-33 index |
| [Engine/Wall.cs](Engine/Wall.cs) | 136 tiles, Fisher-Yates shuffle, 122 live + 14 dead. The dead wall is reserved for kans and dora and not used yet. |
| [Engine/PlayerHand.cs](Engine/PlayerHand.cs) | Hand storage, shanten, win checks, shanten-minimising discard |
| [Engine/ShantenCalculator.cs](Engine/ShantenCalculator.cs) | Shanten for standard hands, seven pairs and thirteen orphans. `-1` means complete. |
| [Engine/TurnState.cs](Engine/TurnState.cs) | `Draw` > `ActionPhase` > `Discard` > `WaitPhase` > `NextPlayer` |
| [Engine/GameEngine.cs](Engine/GameEngine.cs) | Round state machine with `StartRound()`, `PlayTurn()` and `RunSimulation()`. Raises a `Logged` event and writes to a `TextWriter`. |
| [Program.cs](Program.cs) | Console entry point |
| [Wpf/MainWindow.xaml(.cs)](Wpf/MainWindow.xaml) | Table layout, controls and the refresh logic |
| [Wpf/TileView.cs](Wpf/TileView.cs) | Draws one tile, face-up or face-down, with an optional highlight |

Notes:

- **Bots** discard the tile that leaves the lowest shanten (ties random). They win far more often than real players, about 70% of seeded rounds in testing.
- **Reproducibility:** `new GameEngine(seed, writer)` makes a round repeatable and lets tests capture output.
- **Turn flow:** the wall is checked only when a turn starts, so every turn that begins finishes. A full exhaustive round is 70 draws and 70 discards, and the round ends as soon as turn 70 finishes.
- **Stepping:** `PlayTurn()` plays one turn and `RunSimulation()` plays the whole round; both give the same result for the same seed.
- **Ron** is checked in turn order from the discarder's left.

## Tests

33 xunit tests in [Tests/](Tests/) cover:

- tile validation, equality and notation
- shanten for known hands in all three shapes
- wall composition (136 tiles, 4 of each, 122/14 split)
- tsumo/ron detection and seed reproducibility
- turn completion over 200 seeds
- the step API: one turn at a time, round end after turn 70, reset on a new round, the `Logged` event, and stepping matching a full run

The window itself has no automated tests; it was checked by running it and looking at screenshots of the start, middle, ron win and exhaustive draw.

CI runs on every push and pull request ([.github/workflows/ci.yml](.github/workflows/ci.yml)): build, tests and a smoke run on Ubuntu, and a build of the desktop app on Windows.

## QA and design review

An initial review (source review plus runtime checks) found these issues, all now fixed:

| # | Severity | Issue | Fix |
|---|---|---|---|
| 1 | High | No win detection; bots threw away complete hands | Tsumo and ron added; discards minimise shanten |
| 2 | Medium | Last player never discarded | Wall checked only at turn start |
| 3 | Medium | `Tile` accepted invalid ranks (`Tile(Manzu,10)` collided with 1p, rank 0 gave index -1) | Constructor validation |
| 4 | Medium | `PlayerHand.Remove` used reference equality | Value equality on `Tile` |
| 5 | Low | Seven pairs and thirteen orphans not counted | Added to `CalculateShanten` |
| 6 | Low | No seed, no tests, no project file | Seed parameter, test project, csproj |
| 7 | Info | Unreachable early return in setup | Removed. Dead wall kept for kans and dora. |

Design fixes: white/green/red dragons were `P`/`F`/`C`, easy to confuse with pinzu, and are now `5z`-`7z`. Output gained turn numbers, seat winds, the full hand and the wall count. Rendering goes through a `TextWriter`, so a UI can replace the console.

Known limits: shanten was checked on a handful of hands, not against a full reference table. Ron ignores furiten, yaku and calls.

## Roadmap

- Discard pile display and furiten
- Calls: chi, pon, kan (uses the dead wall)
- Riichi, yaku, dora and scoring
- Rounds, dealer rotation and winds
- Let a human take a seat: the engine must wait for a discard choice, then the window needs click-to-discard
- Animations and sound in the desktop app
