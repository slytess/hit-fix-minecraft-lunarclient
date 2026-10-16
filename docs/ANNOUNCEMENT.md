I've published an open-source Windows installer for the unofficial Lunar Client
1.7.10 HitFix adaptation.

It opens a console menu:

- **1:** Create and select a separate Lunar 1.7.10 profile with HitFix.
- **2:** Revert the installation and return to the previous profile selection
  if the HitFix profile is still selected.

The original profile stays intact. The tool keeps local backups, reports error
codes and writes diagnostic logs to help investigate problems. If there is no
installation record, option 2 reports that there is nothing to revert.

The repository includes the installer and agent source, build instructions,
an English usage guide and SHA-256 checksums for the release files.

This is an **experimental release**. HitFix changes inbound packet processing
timing; it is not limited to hits, and damage or teleports may feel different.

The method is based on [Incognitolol/HitFix-Mod](https://github.com/Incognitolol/HitFix-Mod),
whose README also credits [dewgs](https://github.com/dewgs). This is an unofficial
community project and is not affiliated with Lunar Client.

- [Source code and instructions](https://github.com/slytess/hit-fix-minecraft-lunarclient)
- [Download v0.1.0](https://github.com/slytess/hit-fix-minecraft-lunarclient/releases/tag/v0.1.0)
- [Report a problem](https://github.com/slytess/hit-fix-minecraft-lunarclient/issues)
