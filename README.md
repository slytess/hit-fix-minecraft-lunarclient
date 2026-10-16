# Lunar HitFix Installer

An unofficial Windows console tool that creates a separate Lunar Client 1.7.10
profile with a HitFix Java agent. Run it again to revert its installation.
The installer and agent source code are included. Version: **0.1.1 (experimental)**.

**Antivirus notice:** Some engines flag v0.1.1. These may be false positives,
but vendor confirmation is still outstanding. The EXE is unchanged and the source
is public. [Read the short notice](docs/SECURITY-REVIEW.md).

## Download and run

1. Download `LunarHitFixInstaller.exe` from [Releases](https://github.com/slytess/hit-fix-minecraft-lunarclient/releases).
   The ZIP release also includes this README, third-party notices and checksums.
2. If you already use an older HitFix setup, first select your normal Lunar
   profile. Close Minecraft and the Lunar launcher completely, including background launcher
   processes. The tool also refuses to proceed while a `java` or `javaw` process
   is running; close other Java applications temporarily if necessary.
3. Double-click the EXE. A console window opens. **Do not run as administrator.**
4. Enter `1` and press Enter to install. The tool displays the source profile,
   creates **Lunar 1.7.10 + HitFix (Installer)** and selects it.
5. Open Lunar and launch that profile. Keep the installed agent in its recorded
   location under `.lunarclient\hitfix-installer`.

```text
  [1] Install / select HitFix
  [2] Revert installation
  [3] Credits
  [0] Exit

  Choose 1, 2, 3 or 0:
```

The EXE contains the agent; users do not need Python, a separate JAR download,
a JDK, or a separate .NET SDK. Requires **Windows 10/11**, the Windows .NET
Framework 4.x runtime and Windows `winsqlite3.dll`, plus an existing Lunar
**1.7.10** profile. Lunar supplies the Java runtime used by the game.
This is for the normal Windows Lunar installation at `%USERPROFILE%\.lunarclient`.
Custom Lunar installation roots are not supported by the menu.
Option `3` displays the project and upstream credits, then returns to the menu
without changing files. The compact startup screen keeps the detailed technical
explanation in this README.

## Revert

Close Minecraft and Lunar, reopen the EXE, enter `2`, and press Enter.
The tool removes only the profile it created. If that profile is still selected,
it restores the selection recorded before installation. If you have already
selected another profile, your current selection is preserved.

If no installation record exists, it prints:

```text
No previous installation or backed-up profile was found. Nothing to revert.
```

An existing profile using the same path without a matching installation record
is treated as a conflict and is never overwritten. This tool does not adopt or
remove profiles created by older scripts. The original source profile is never
edited. Repeated installs do not create duplicate profiles; repeated reverts
are harmless.

Backups, logs and the inactive agent remain on disk for troubleshooting. The
tool never deletes game files, resource packs, screenshots, worlds, accounts,
or mod settings. A dedicated launcher profile shares the original profile's
game/mod directories and Lunar's existing visual settings; it does not copy or
reset those files.

## What HitFix actually does

This is **not a hits-only mode**. The agent runs extra inbound queue processing
at frame start and tick start. It can change when damage, movement, velocity
and teleport information takes effect. It does not replace the animation code
with Minecraft 1.8's, but the changed timing may feel different.

This does not establish improved server-side hit registration, reduced network
ping, a different tick rate, or permission to use it on every server. Check the
rules of the server you intend to play on.

The adaptation uses a JVM `-javaagent` argument and hooks Minecraft's loop in
memory. It preserves the upstream `HitFix/1.0` plugin-channel announcement on
channel `1337`. This is sent to the Minecraft server you connect to; it is not
installer telemetry. No Lunar/Minecraft binaries are redistributed or patched
on disk. Lunar updates may break compatibility.

## Exact file changes

| Location | Purpose |
| --- | --- |
| `.lunarclient\db\profiles.db` | Insert/remove this tool's dedicated profile |
| `.lunarclient\settings\launcher.json` | Select the new profile; restore the old selection on revert if appropriate |
| `.lunarclient\hitfix-installer\hitfix-agent.jar` | Embedded agent extracted by the installer |
| `.lunarclient\hitfix-installer\state.json` | Ownership and recovery journal |
| `.lunarclient\hitfix-installer\backups\...` | SQLite-consistent database snapshot and pre-install launcher JSON |
| `.lunarclient\hitfix-installer\hitfix-runtime.log` | Agent startup/compatibility diagnostics |
| `%LOCALAPPDATA%\LunarHitFixInstaller\installer.log` | Installer messages, error codes and stack traces |

Existing custom JVM arguments are retained in the new profile. No downloads,
registry edits, scheduled tasks, startup entries, telemetry or automatic uploads
are performed by the installer. No processes are terminated by it.

Database changes use SQLite transactions; launcher JSON and the recovery journal
use atomic file replacement. A journal written before installation lets the next
run recover an interrupted operation. Revert removes the managed row instead of
restoring a whole old database, so later changes to other profiles are retained.
Do not delete `state.json` while the tool's profile is installed.

Paths containing spaces use a Windows short path when available. If Windows
cannot provide one, installation stops with `E_ARGUMENT_PATH`, before profile
changes, because this Lunar JVM argument parser needs a path without spaces.

## Errors and bug reports

The console stays open after an error and returns to the menu. It shows an
error code and diagnostic log path. Open an issue in this repository with:

- Installer version and the SHA-256 of the EXE you ran.
- The option you selected and complete error code/message.
- Windows version and Lunar/game version.
- Relevant installer log lines; include the agent log for game startup problems.
- Whether retrying with Minecraft and Lunar fully closed changes the result.

Logs are local and are never uploaded automatically. They may contain Windows
usernames and filesystem paths; review them before posting. Never upload your
whole Lunar folder, database, launcher JSON or account files to an issue.

Common errors and recovery steps are in [docs/ERRORS.md](docs/ERRORS.md).
A successful install means the profile was configured. It does **not** prove
that a particular Lunar build successfully loaded the agent or that a server
allows it. Look for `Per-frame packet drain is active` and the queued-packets
verification in the runtime log. `DISABLED`, `INCOMPATIBLE` or `TRANSFORM FAILED`
means the agent is not operating correctly in that session.

## Build from source

On Windows, install a JDK (8 or later) with `java`, `javac` and `jar` on PATH.
The C# compiler included with Windows .NET Framework is used; no NuGet packages
or Python packages are required. In PowerShell, from the repository directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The command-scoped execution policy allows this local build script to run; it
does not change the computer's persistent PowerShell policy. Read the script
before running it. The build verifies the vendored ASM checksum, compiles the
Java sources, embeds the resulting JAR in the console EXE and runs tests.

Outputs are in `dist/`. `SHA256SUMS.txt` identifies the exact binary/JAR built;
build timestamps may produce different binary hashes across rebuilds.

```powershell
Get-FileHash .\dist\LunarHitFixInstaller.exe -Algorithm SHA256
```

Installer tests use temporary fixture databases and launcher settings under
`build/`. They do not edit your real Lunar profiles. Agent tests run synthetic
Minecraft fixtures and isolated classloaders. The release still needs a manual
test against the current Lunar build on a supported machine.

## Credits and license

The packet-timing method comes from
[Incognitolol/HitFix-Mod](https://github.com/Incognitolol/HitFix-Mod), based on
commit [`09df1156a015f765cdeadbb2bf63a22dadaa3ae6`](https://github.com/Incognitolol/HitFix-Mod/tree/09df1156a015f765cdeadbb2bf63a22dadaa3ae6).
The upstream project also credits [dewgs](https://github.com/dewgs).
This repository contains a separate Lunar adaptation and installer, rather
than the original Forge mod. It is not affiliated with Lunar Client or Mojang.

Original repository code: [MIT](LICENSE). Bundled ASM 9.9.1: its BSD-style
license and pinned dependency hash are in [THIRD_PARTY.txt](THIRD_PARTY.txt).
