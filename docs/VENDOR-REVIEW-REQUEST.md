# Vendor review request draft — not yet submitted

Subject: Possible false positive review — LunarHitFixInstaller.exe v0.1.1

Hello,

I maintain an open-source Windows console installer for an unofficial Lunar
Client 1.7.10 networking-timing adaptation. A user has reported your engine
flagging our published executable on VirusTotal. Please review the detection
and advise whether it is a false positive or identify the behavior of concern.

File: LunarHitFixInstaller.exe, 170,496 bytes, version 0.1.1.

SHA-256:
`a4abf4760f7729460d7db9d7226ba227e5ef11fb76e3d020e20a81a3cc9d1eac`

- [Source commit](https://github.com/slytess/hit-fix-minecraft-lunarclient/tree/6bf7314b3dac5c1f2af1937eb06859afdcd09d66)
- [Exact binary](https://github.com/slytess/hit-fix-minecraft-lunarclient/releases/download/v0.1.1/LunarHitFixInstaller.exe)
- [VirusTotal hash report](https://www.virustotal.com/gui/file/a4abf4760f7729460d7db9d7226ba227e5ef11fb76e3d020e20a81a3cc9d1eac)
- [Investigation and source correspondence evidence](https://github.com/slytess/hit-fix-minecraft-lunarclient/blob/main/docs/SECURITY-REVIEW.md)

The executable is an unsigned, unobfuscated .NET Framework console application.
It embeds a Java agent JAR and extracts it under the user's `.lunarclient` folder.
On user request it creates a dedicated launcher profile, appends a `-javaagent`
argument, keeps backups, and can revert the profile changes. It enumerates
process names only to refuse edits while Lunar or Java is running.

The Java agent transforms Minecraft's loop in memory at Java startup and uses
reflection to call its packet-processing method. It also sends the documented
HitFix plugin-channel announcement to the Minecraft server the user connects to.
The installer does not communicate over the network, collect credentials, alter
security settings, establish persistence or inject into a running Windows process.

We verified the EXE against independently compiled source, allowing only the PE
timestamp and CLR module version ID to differ. The embedded JAR, all 8 agent
classes and 38 bundled ASM entries were also checked against the published
JAR, fresh agent compilation and pinned ASM dependency respectively.

Microsoft Defender with signatures 1.461.60.0 returned no threats on 2026-10-16;
we recognize this does not invalidate your engine's verdict. We have not changed
the reported binary to alter its hash or obscure its behavior.

Please let us know the outcome of your review and any further information needed.

Thank you,
slytess

---

Before submitting: select the appropriate vendor, attach the exact detection
name/report timestamp, fill in any required maintainer contact details and attach
the sample only through the vendor's official submission channel. No submission
or vendor confirmation is implied by this draft.
