# Mahjong Game

A riichi mahjong engine in C# (.NET 10) with a console simulation: four bot players play one round against each other.

> **Status: playable prototype.** You can take a seat in the Windows app and discard against three bots, or just watch four bots play. The wall, dealing, turn flow, shanten maths, tsumo and ron work. Calls (chi, pon, kan), riichi, yaku and scoring are not built (see [Roadmap](#roadmap)).

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

### Playing a seat

Pick **East, South, West or North** under *Your seat* (East is the default) and you play that seat against three bots. Picking a seat starts a new round. Choose **Watch** to spectate four bots instead.

1. When it is your move, your panel says "Your move" and the drawn tile has a **blue outline**.
2. **Click a tile to discard it.** Tab to a tile and press Enter works too. Hovering a tile shows where discarding it leaves you, for example "Discard 5p: shanten 2".
3. **Hint** (`H`) outlines the discards, in green, that keep you closest to winning, and says what shanten they keep.
4. The bots then play their turns at the chosen speed, and the game stops again at your next move.
5. A win is declared for you automatically: tsumo when your draw completes your hand, ron when a bot discards your winning tile. At the end of the round every hand is revealed.

Other players' hands start face-down while you play (tick *Show all hands* to peek).

### Controls

| Control | What it does |
|---|---|
| **Your seat** | Seat to play, or Watch to spectate. Changing it starts a new round. |
| **Hint** (`H`) | Shows the best discards for your current hand |
| **Next turn** (`→`) | Spectator only: plays one full turn (draw, tsumo check, discard, ron check) |
| **Auto-play** (`A`) | Spectator only: plays turns by itself |
| **Speed slider** | 100-1500 ms per turn (spectating) or per bot turn (playing) |
| **New round** (`N`) | Deals again. A whole-number seed replays the same round; empty means random. |
| **Show all hands** | Face-up or face-down for the other players |
| **Game log** | The same text the console version prints |

On screen:

- The player whose draw is next gets a white outline and a "Draws next" label; your own panel has a blue outline.
- The last discard has an orange outline.
- A win turns the panel gold, marks the winning tile on a ron, and shows a result banner. Next turn and Auto-play are then disabled.
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
- **Human seat:** `StartRound(seat)` hands one seat to a person. `PlayTurn()` then stops with `AwaitingHumanDiscard` true on that seat's 14-tile hand, and `DiscardHuman(tile)` resumes the round. `PlayerHand.SuggestDiscards()` and `ShantenAfterDiscard()` back the hint.
- **Ron** is checked in turn order from the discarder's left.

## Tests

46 xunit tests in [Tests/](Tests/) cover:

- tile validation, equality and notation
- shanten for known hands in all three shapes
- wall composition (136 tiles, 4 of each, 122/14 split)
- tsumo/ron detection and seed reproducibility
- turn completion over 200 seeds
- the step API: one turn at a time, round end after turn 70, reset on a new round, the `Logged` event, and stepping matching a full run
- the human seat: the engine pauses on your 14-tile hand, `PlayTurn` does nothing while it waits, bad or out-of-turn discards throw and leave the round waiting, and 100 full rounds with a human playing hint discards always finish
- hints: the suggested discards agree with the shanten after each discard

The window itself has no automated tests. It was checked by running it, driving it with the keyboard (Enter to discard, `H` for a hint) and looking at screenshots of the start, mid-game, a ron win and an exhaustive draw.

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
- Let the human choose whether to call tsumo or ron (both are automatic for now)
- Animations and sound in the desktop app
