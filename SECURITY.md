# Security

## Reporting a problem

Please report security problems privately, using **Report a vulnerability** on the [Security tab](https://github.com/Aleks108-heaven/mahjong-game/security/advisories/new) of this repository, rather than in a public issue. Include the version (shown in the window's footer), what you did and what happened.

Only the latest release is supported.

## What the app does and doesn't touch

- It makes no network connections and opens no ports. (Checked on the running app: no TCP or UDP endpoints.)
- It runs no other programs, uses no registry keys of its own and registers nothing to start with Windows. The installer adds a Start menu entry, an optional desktop icon and an uninstall entry for your user, and nothing else.
- It stores one small file, `%LOCALAPPDATA%\MahjongTable\settings.json`, holding the language, opponent level and seat. The file is size-limited and validated when read; a damaged file is ignored.
- Releases are built by GitHub Actions from this repository, with SHA-256 checksums and signed build provenance (`gh attestation verify`). They are **not** code-signed, so Windows SmartScreen may warn on first run.
