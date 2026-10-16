# v0.1.1 - experimental

**Antivirus notice:** Some engines flag this release; these may be false positives,
but the vendors have not confirmed that assessment. **The EXE has not changed.**
[Short notice and public source verification](https://github.com/slytess/hit-fix-minecraft-lunarclient/blob/main/docs/SECURITY-REVIEW.md).

Console layout update for the unofficial Lunar 1.7.10 HitFix installer.

- Compact startup screen with spacing and cyan title/menu keys.
- New `3 - Credits` screen, available on demand and returning to the menu.
- Startup no longer prints the full credits and technical description.
- Updated English guide and short Telegram announcement with installation steps.

Profile installation, recovery and the Java-agent behavior are unchanged.

- `1` creates/selects a dedicated profile while retaining the source profile's
  JVM arguments and game/mod directories.
- `2` removes the managed profile and restores the previous selection when
  appropriate. With no installation record, it reports that there is nothing
  to revert.
- `3` displays credits without changing files.
- Local backups, recovery journal, error codes and logs are included.
- The Java agent is embedded in the EXE. No runtime downloads are required.
- English README, source, build scripts, third-party notices and hashes are included.

**Requirements:** Windows 10/11, Windows .NET Framework 4.x, and an existing normal
Lunar 1.7.10 profile. Close Lunar/Minecraft before changing profiles. No admin
rights, Python, separate .NET SDK or JDK are required to run the EXE.

**Validation:** 33 installer scenarios using fixture databases, including actual
process termination at transaction boundaries, WAL-consistent backups, repeated
install/revert, preservation of later profile edits, and absent/conflicting
installation records. Agent integration and classloader isolation tests passed
with Java 8, 17, 21 and 25. Automated console checks cover invalid choices,
credits only after selection, return to the menu, and exit.

The new installer has not been run against the author's live Lunar profile, and
this release has not had a new end-to-end playtest. Test on the current Lunar
build before relying on it. Future Lunar updates may need a compatibility fix.

**Scope:** This uses the existing full inbound-queue HitFix behavior. It is not a
hits-only implementation and may affect the timing of damage, movement, velocity
and teleports. It does not establish improved server-side hit registration.

Credits: [Incognitolol/HitFix-Mod](https://github.com/Incognitolol/HitFix-Mod),
which also credits [dewgs](https://github.com/dewgs). Unofficial; not affiliated
with Lunar Client or Mojang.

Download the EXE on its own, or the ZIP with documentation/notices/checksums.
`hitfix-agent.jar` is also provided for inspection; the EXE already embeds it.
The binaries are unsigned. No independent live VirusTotal reanalysis or antivirus
clearance is claimed; see the short antivirus notice above.
