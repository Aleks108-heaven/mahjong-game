# Changelog

All notable changes to Mahjong Table. Versions follow [Semantic Versioning](https://semver.org/); the game is
still `0.x`, so anything may change between releases.

## [0.1.0] - 2026-10-16

The first packaged release: a Windows installer and a portable build of the riichi mahjong table.

### Added

- **Opponent levels, Junior to Senior.** Pick who you play against; new installs start on Junior.
  - **Junior** slips up on about one discard in five, rarely calls and never defends.
  - **Middle** is the original bot: fastest hand, calls when it helps, keeps a yaku in reach.
  - **Senior** counts the tiles it can see, aims for hands that can really win (a yaku, no furiten) and folds
    against open hands.
  - Measured over 4000 seeded rounds at a table of one Junior, one Middle and two Senior bots, the win rates were
    Junior 4.5%, Middle 15.3% and Senior 29.4%, and the deal-in rates 17.6%, 14.4% and 10.0%. The README has the
    other matchups and what drives the gap.
- `TestMahjongGame arena` seats levels against each other and prints win and deal-in rates, and
  `TestMahjongGame --level <junior|middle|senior>` plays one round at a chosen level.
- A **Windows installer** (`MahjongTable-0.1.0-Setup.exe`, English and Ukrainian, no administrator rights needed)
  and a **portable zip**, with SHA-256 checksums and signed build provenance.
- The window remembers your language, opponent level and seat between runs.
- Bot panels show their level, the result banner has a headline ("You win!", "Draw"), and the footer shows the
  version.

### Changed

- The sidebar is regrouped: opponents, seat and deal first, playback controls only while watching.
- Test packages are pinned to exact versions with a lock file; GitHub Actions are pinned to commits.

### Included from earlier development

- Riichi engine: wall and dead wall, dealing, turn flow, shanten (standard, seven pairs, thirteen orphans),
  tsumo and ron, pon, chi and kan (closed, open, added), dora indicators, yaku, furiten.
- Desktop table with click-to-discard, hints, call prompts, keyboard control, English and Ukrainian.
- Console simulation and 198 automated tests.

### Known limits

- No riichi, scoring or multi-round games yet (see the README roadmap).
- The installer and exe are not code-signed, so Windows SmartScreen may warn on first run.
