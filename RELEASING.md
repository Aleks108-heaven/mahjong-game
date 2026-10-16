# Releasing

A release is a version tag on `main`. Pushing the tag makes GitHub Actions build, check, sign and publish everything from a clean checkout; nothing is uploaded from a developer's machine.

## Cut a release

1. Set the new version in [Directory.Build.props](Directory.Build.props) (`<Version>1.2.3</Version>`).
2. Add a section to [CHANGELOG.md](CHANGELOG.md) headed `## [1.2.3] - YYYY-MM-DD`. Its text becomes the top of the release notes.
3. Commit to `main` and wait for the **CI** workflow to go green. Its Windows job runs the same packaging script as the release, so a broken installer shows up here.
4. Tag that commit and push the tag:

   ```powershell
   git tag -a v1.2.3 -m "Mahjong Table 1.2.3"
   git push origin v1.2.3
   ```

5. Watch the **Release** workflow. When it finishes, the release page has the installer, the portable zip and `SHA256SUMS.txt`.
6. Check the result: download the installer, compare `Get-FileHash` with `SHA256SUMS.txt`, run `gh attestation verify <file> --repo Aleks108-heaven/mahjong-game`, install it and start the game.

## What the workflow checks before it publishes

- The tagged commit is on `main`.
- The tag (`v1.2.3`), `<Version>` in `Directory.Build.props` and a `## [1.2.3]` section in the changelog all agree.
- Packages restore exactly as pinned in `Tests/packages.lock.json` (`--locked-mode`), and the build treats warnings as errors.
- All tests pass.
- The installer, zip and checksums build.

Only after that does it sign build provenance for the installer and the zip (so anyone can confirm GitHub Actions built them from this repository) and create the release. Every action it uses is pinned to a commit SHA, and only the release job has write access.

## Build the files locally

```powershell
.\tools\package.ps1
```

This needs the .NET 10 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php). Files appear in `artifacts\` (not committed). It works in Windows PowerShell 5.1 and PowerShell 7. A local build is for testing; the published files always come from the workflow.

`.\tools\release-notes.ps1 -Version 1.2.3 -Repository Aleks108-heaven/mahjong-game` previews the release notes.

## If something goes wrong

- **The workflow fails before "Publish the release".** Nothing was published. Fix the problem on `main`, then move the tag: `git push origin :refs/tags/v1.2.3`, `git tag -d v1.2.3`, tag again, push.
- **A release is out with a defect.** Don't move or delete its tag, because people may already have the files. Fix the problem and release the next patch version.
- **Updating pinned actions.** Dependabot opens pull requests for the commit SHAs in the workflows and for the test packages; review them like any other change.

## Not done yet

The exe and installer are not code-signed, so Windows SmartScreen warns on first run. Signing needs a code-signing certificate; the release notes say so and explain how to verify a download instead.
