**Lunar 1.7.10 HitFix — source code available**

The HitFix installer is now public, with the full source code so you can inspect
what it does or build it yourself.

It uses your existing Lunar Client installation and adds a separate profile with
the HitFix agent. You do not need to download a replacement Lunar build or launcher.

**How to install:**
1. Download [LunarHitFixInstaller.exe](https://github.com/slytess/hit-fix-minecraft-lunarclient/releases/download/v0.1.1/LunarHitFixInstaller.exe).
2. Close Minecraft and Lunar completely. If you already use an older HitFix
   profile, select your normal profile before closing Lunar.
3. Run the EXE, enter **1**, then open Lunar and launch **Lunar 1.7.10 + HitFix (Installer)**.

To undo it, close the game and Lunar, run the EXE again and enter **2**.
Option **3** shows the credits. Your original profile is preserved.

Experimental release: this changes inbound packet timing beyond hits, so damage
and teleports may feel different.

[Source code and English guide](https://github.com/slytess/hit-fix-minecraft-lunarclient)

Based on the method from [Incognitolol/HitFix-Mod](https://github.com/Incognitolol/HitFix-Mod),
which also credits [dewgs](https://github.com/dewgs). Unofficial community project.
