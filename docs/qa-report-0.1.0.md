# QA report: Mahjong Table 0.1.0

Date: 2026-10-16. Scope: the new bot levels (Junior, Middle, Senior), the desktop app changes, the installer and portable build, and the release process.

**Final status: PASS WITH FINDINGS.** The engine, the app, the packaging and the live release were verified with evidence, and the defects found are fixed. The remaining findings are the missing code signature (S-1), the bundled runtime needing a new release for each .NET security update (S-2), and repository security features that are switched off (S-3). Only Windows 11 was available, so other Windows versions and assistive technology are untested; see [What could not be tested](#what-could-not-be-tested). The first version of this report, written before the release, was PARTIALLY VERIFIED because the release workflow had not run yet; [Post-release verification](#post-release-verification) closes that gap.

## 1. Executive summary

- **Verified:** 198 automated tests pass; the Middle bot plays exactly as the original (200 seeds byte-identical); the three levels form a measurable ladder; the real app was driven end to end through UI Automation (20 checks, 20 pass); the installer installs, runs and uninstalls cleanly without administrator rights; the app opens no network connections; the history contains no secrets.
- **Found and fixed during this QA:** four defects (D-1 to D-4 below), the most serious being a packaging script that could not even start in Windows PowerShell 5.1.
- **Open:** the build is not code-signed (S-1, Medium), so Windows SmartScreen will warn on first run; checksums and signed build provenance are the mitigation. The bundled .NET runtime is only as current as the last release (S-2, Low). Repository secret scanning, push protection, Dependabot security updates and code scanning are off (S-3, Low).
- **Vulnerabilities:** GitHub reports 0 open Dependabot alerts; the NuGet audit finds no vulnerable packages; the release was built on .NET runtime 10.0.13, the latest patch in Microsoft's release data on the day of release.

## 2. Environment

| | |
| --- | --- |
| Mode | C: source code and the running app (local). No web UI or HTTP API exists, so web and API areas are not applicable. |
| OS | Windows 11 Pro 10.0.26200 (x64) |
| Toolchain | .NET SDK 10.0.402, runtime 10.0.13 (bundled in the self-contained exe), Inno Setup 6.5 locally, 6.7.1 on the CI runner image |
| Shells | Windows PowerShell 5.1.26100 and PowerShell 7 |
| Build under test | `main` up to the `v0.1.0` tag. The installer and UI checks ran on local builds made with `tools/package.ps1` from a commit before the last small changes (the D-1 parser fix, documentation); the released files are built from the tagged commit. |
| Evidence rules | Findings are **Confirmed** (reproduced), **Suspected** or **Not tested**. |

## 3. Test coverage

| Area | Status | Evidence |
| --- | --- | --- |
| Functional: engine, calls, kan, yaku, furiten | Passed | 100 original tests, unchanged and green after the refactor |
| Functional: bot levels | Passed | New bot tests (98 new tests in total, bringing the suite from 100 to 198): every level finishes 120 rounds with no tile lost, repeatable rounds, mixed tables, human seat against Senior bots |
| Regression: Middle bot unchanged | Passed | Full console logs of seeds 1-200 compared with a build of the previous commit: 0 differences. Seeds 1-30 pinned by a test (SHA-256 of the logs). |
| Information hiding (bots only see public information) | Passed | Views never show a tile more than 4 times; a view adds up to the seat's own tiles plus what lies face-up (60 rounds, every turn, every seat) |
| Boundary and negative: console arguments | Passed after fix | 27 cases (seed `int.MinValue` to `int.MaxValue + 1`, empty, two seeds, unknown options, levels, arena rounds 0 and negative, 3-seat tables, bad levels) all give the right exit code and no stack trace. One case failed (D-1) and was fixed. |
| Boundary and negative: settings file | Passed | 13 bad inputs (empty, `null`, `{}`, missing fields, unknown language or level, seat 7 and -2, numeric enum, wrong types), oversized file, truncated file, failing save, missing file |
| UI: end to end | Passed | 20 UI Automation checks (below) |
| UI: visual | Passed | Screenshots: start, mid-game in Ukrainian and English, finished round, smallest window (700x460), installed copy |
| i18n | Passed | Every text the window can show, and every yaku name, has a Ukrainian translation (test; also proven able to fail by removing one entry); placeholders match; Cyrillic renders and fits |
| Compatibility | Partly tested | Windows 11 only. Scripts run under PowerShell 5.1 and 7. Engine and tests build on Ubuntu in CI (to be confirmed by the first CI run). |
| Security | Passed with finding S-1 | See section 5 |
| Performance | Measured | Section 6 |
| Reliability | Passed | Damaged settings file, failing save, missing file all fall back safely |
| Concurrency | Passed | The arena plays rounds in parallel; a test repeats a run and gets identical results |
| Accessibility | Partly tested | Controls expose automation names (UI Automation found and drove them); keyboard focus is visible (screenshots). **Not tested:** screen reader, high contrast, contrast ratios with a tool, high-DPI scaling. |
| Release process | Passed | `actionlint` clean; scripts run locally under 5.1 and 7; CI-strict build passes; then the real `v0.1.0` run succeeded and its output was verified (see Post-release verification) |

### The end-to-end checks (UI Automation against the real exe)

Fresh start defaults to Ukrainian, Junior and East; no settings file appears until the user changes something; Next turn is hidden while playing; bot panels say "Junior bot". Choosing Senior saves it and starts a Senior round; choosing English saves it and relabels the window; choosing Watch saves seat -1, shows Next turn, and pressing it advances to Turn 1. After a restart Senior, Watch and English are all still selected. With a truncated `settings.json` the app still starts, in Ukrainian and Junior.

## 4. Defects

| ID | Severity | Status | Summary |
| --- | --- | --- | --- |
| D-1 | Low | Fixed, regression tests added | `--level 1` was accepted as Middle because `Enum.TryParse` accepts numbers. The help promises `junior|middle|senior`. Same pattern in the window's `--level` and `--lang`. Now rejected; tests cover `"1"`, `"99"`, `" senior"` and more. |
| D-2 | Medium | Fixed, verified | `tools/package.ps1` used `?.`, which only PowerShell 7 can parse. In Windows PowerShell 5.1 (the one the Start menu opens, and the one the README tells people to use) the script failed with a parse error. Found through an editor diagnostic. Verified: parses with no errors and builds all files under 5.1. |
| D-3 | Low | Fixed, verified | The window footer said v1.0.0, the SDK default, because no version was set. Version now comes from `Directory.Build.props`; the installed app shows v0.1.0. |
| D-4 | Low | Fixed | Test packages used floating versions (`17.*`, `2.*`) with no lock file (checklist item 10). Pinned to the resolved versions plus `packages.lock.json`; CI restores with `--locked-mode`. |

Every defect has a regression check: D-1 by the parser tests, D-2 by running the script under 5.1 (and CI), D-3 by the installed-app screenshot, D-4 by the locked restore in CI.

## 5. Security findings

Mapped to `security-checklist.md`.

| Item | Result | Evidence |
| --- | --- | --- |
| 1 Patching | Passed, with S-2 | NuGet audit: no vulnerable packages (direct or transitive). GitHub: 0 open Dependabot alerts. The release job installed .NET runtime 10.0.13 and SDK 10.0.402, which Microsoft's release data lists as the latest patch (released 2026-10-13, a security release). Whether that runtime has vulnerabilities not yet published cannot be known. xunit 2.9.3 is marked "Legacy" (v3 exists); test-only, not shipped (Info). |
| 2 Network exposure | Confirmed none | Running app: 0 TCP and 0 UDP endpoints, no networking DLLs loaded. Source has no `System.Net`, sockets or HTTP. |
| 3 Authentication | Not applicable | No accounts or secrets. |
| 4 Privilege | Confirmed | The installer runs with `PrivilegesRequired=lowest` and installed without elevation; the app registers no service. |
| 5 Persistence | Confirmed | Before, during and after install and uninstall, the startup Run keys, scheduled-task count, service count and Startup folder were identical. The only addition was the per-user uninstall entry, which uninstall removed. |
| 6 Integrity | **S-1, below** | Files are unsigned. SHA-256 checksums are published and match the downloaded files; signed build provenance verifies with `gh attestation verify`. |
| 7 Protection controls | Not applicable to the app | Observed: Windows needs about 7 s to scan the first launch of the new exe. |
| 8 Secrets | Confirmed none | Pattern scan over all 12 commits (12,092 lines): 0 matches, and the scan detects planted tokens (positive control). No key, certificate or `.env` file is tracked. Workflows use only the built-in `github.token`. |
| 9 Logging | Not applicable | The game log is gameplay text only. |
| 10 Supply chain | Confirmed | Test packages pinned with a lock file; all three GitHub Actions pinned to commit SHAs; Dependabot configured; no `curl | sh`; first-party packages only. The Inno Setup version on the CI runner is set by the runner image, not pinned by us (Info). |
| 11 Privacy permissions | Not applicable | macOS item. |
| 12 Backups | Not applicable | |

| ID | Severity | Confidence | Finding | Fix |
| --- | --- | --- | --- | --- |
| S-1 | Medium | Confirmed | The exe and installer are not code-signed, so Windows SmartScreen may show "Windows protected your PC" and users cannot tell who published the file from a signature. | Needs a code-signing certificate. Mitigated by SHA-256 checksums, signed build provenance (`gh attestation verify`) and clear instructions in the release notes and README. |

| S-2 | Low | Confirmed | The exe is self-contained, so it carries its own .NET runtime. That runtime never updates itself: a user keeps the runtime of the release they installed until they install a newer release. | Release a patch version after each .NET security update (they ship monthly); the workflow builds on the latest 10.0.x. Now stated in RELEASING.md and SECURITY.md. |
| S-3 | Low | Confirmed | On the GitHub repository, secret scanning, push protection, Dependabot security updates and code scanning are disabled (a GitGuardian check does run on pull requests). Private vulnerability reporting is on. | Enable them under Settings > Code security; they are free for public repositories. Not changed by this review because they are repository settings. |

### Language checks (`language-pitfalls.md`, C# and PowerShell)

Checked by source search and review. No `BinaryFormatter` or other unsafe deserialization (settings use `System.Text.Json` into a fixed type); no async code, so no `.Result` deadlocks; no crypto other than SHA-256 for checksums; no `Process.Start` or `Invoke-Expression`; only specific exceptions are caught (`IOException`, `UnauthorizedAccessException`, `JsonException`); the settings file is capped at 4 KB; external input (command line, settings) is validated. PowerShell scripts use `$ErrorActionPreference = 'Stop'` and strict mode, and contain only ASCII (so 5.1 reads them correctly).

## 6. Performance (measured)

| Measure | Result |
| --- | --- |
| Time to window, single-file exe | 1.4-1.5 s warm; about 6.9 s on the very first run (Windows scanning a new unsigned file) |
| Time to window, the 405-file folder alternative | 1.2-1.3 s warm; 7.7 s first run (so the single file costs about 0.2 s) |
| Size | Single exe 62 MB; installer 57 MB; portable zip 56.5 MB |
| Bot speed | Four Senior bots: about 62 ms per full round on one core, under 1 ms per discard |

Memory and CPU use of the window were not measured.

## 7. Informational

- **I-1.** Seeds `n` and `-n` (and 0, `int.MaxValue`, `int.MinValue`) play the same round. This is how .NET's `Random(int)` behaves; checked directly. A seed still replays the same round, so nothing is broken; the README notes it.
- **I-2.** Senior's defence is neutral on "wins minus deal-ins" (30.2% win, 8.6% deal-in with it; 31.1% and 9.5% without). It stays because it is how a strong player behaves and should pay off once riichi and scoring exist. The README says so.
- **I-3.** `Arena.Run` takes any positive round count; a very large number runs long and allocates one result per round. It is a developer tool, so this is left as is.

## 8. Recommendations

| Recommendation | Trade-off |
| --- | --- |
| Get a code-signing certificate and sign the exe and installer in the release workflow | Costs money and key handling; removes the SmartScreen warning and proves the publisher |
| Add a UI test project (for example with UI Automation) built from the script used here | More maintenance; turns this manual end-to-end check into a regression test |
| Test on Windows 10 and with a screen reader and high-DPI scaling before claiming support | Needs other machines |
| Move tests to xunit v3 | Migration work for little gain right now |

## What could not be tested

- Windows 10 or any Windows version other than 11; ARM.
- Screen readers, high-contrast themes, high-DPI scaling, colour-contrast ratios with a tool.
- Whether the bundled .NET runtime has vulnerabilities that are not yet published.
- Memory use and very long play sessions.

## What was verified, what failed, what is uncertain

1. **Verified:** the items in sections 3, 5 and 6 marked Passed or Confirmed.
2. **Failed (and fixed):** D-1 to D-4.
3. **Uncertain:** behaviour on other Windows versions; accessibility with assistive technology; unpublished runtime vulnerabilities.
4. **Could not be tested:** see the list above.
5. **Evidence:** the test run (198 passed), the log comparison, the UI Automation transcript, the install and uninstall footprint, the network check, the history scan and the screenshots, all produced during this review.
6. **To check next:** install on a clean Windows 10 machine and with a screen reader; enable the repository security features (S-3); decide on a code-signing certificate (S-1).

## Post-release verification

Done on 2026-10-16, after pushing the `v0.1.0` tag, from a fresh download of the published release.

| Check | Result |
| --- | --- |
| CI on `main` (commit `11428c5`) | Both jobs green: Ubuntu build, tests and smoke runs (the pinned Middle-bot hash also holds on Linux), and the Windows job that ran the packaging script |
| Release workflow | Every step passed, including the tag check, locked restore, tests, packaging, build provenance and publishing (2 min 35 s) |
| Release page | `Mahjong Table 0.1.0`, not a draft or pre-release, with `MahjongTable-0.1.0-Setup.exe` (59.8 MB), `MahjongTable-0.1.0-win-x64-portable.zip` (59.5 MB) and `SHA256SUMS.txt`; notes show the changelog, the file guide, the SmartScreen warning and verification steps |
| Checksums | Both files match `SHA256SUMS.txt` |
| Provenance | `gh attestation verify` succeeds for both files: issuer GitHub Actions, repository `Aleks108-heaven/mahjong-game`, ref `refs/tags/v0.1.0`, commit `11428c5`, workflow `release.yml`, GitHub-hosted runner |
| Downloaded installer | Silent install exit 0; installed exe reports 0.1.0; window shows v0.1.0; uninstall exit 0 and leaves no files or uninstall entry |
| Portable zip | Contains `MahjongTable.exe` and `README.txt`; the exe shows its window after 2.8 s |
| Dependabot PR #1 (test-package bump) | Its CI is green. It is a routine version bump, not a security fix. Not merged; left for review. |
