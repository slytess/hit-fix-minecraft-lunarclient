# Antivirus reports: v0.1.1

Status on **2026-10-16**: investigation documented; no detecting vendor has yet
confirmed a false positive or removal. This page is not antivirus clearance.
Do not disable antivirus or add exclusions to use this tool.

## Reported file

- File: `LunarHitFixInstaller.exe`, version `0.1.1`, **170,496 bytes**.
- SHA-256: `a4abf4760f7729460d7db9d7226ba227e5ef11fb76e3d020e20a81a3cc9d1eac`.
- [Published source commit](https://github.com/slytess/hit-fix-minecraft-lunarclient/tree/6bf7314b3dac5c1f2af1937eb06859afdcd09d66).
- [Release](https://github.com/slytess/hit-fix-minecraft-lunarclient/releases/tag/v0.1.1).
- [VirusTotal report for that hash](https://www.virustotal.com/gui/file/a4abf4760f7729460d7db9d7226ba227e5ef11fb76e3d020e20a81a3cc9d1eac).

A user-supplied screenshot shows **7/71 detections**. The visible hash prefix and
file size match this release. The live VirusTotal results were not independently
retrieved, so this is a record of the screenshot, not a current scan result.

| Vendor shown in screenshot | Detection shown |
| --- | --- |
| Arctic Wolf | Unsafe |
| Elastic | Malicious (high Confidence) |
| MaxSecure | Trojan.Malware.300983.susgen |
| McAfee Scanner | Ti!A4ABF4760F77 |
| SecureAge | Malicious |
| SentinelOne (Static ML) | Static AI - Suspicious PE |
| Trapmine | Malicious.moderate.ml.score |

The detection names and behavior tags do not identify the exact rule or code
responsible. No causal claim about signing, embedding, process enumeration or
Java instrumentation has been established from that screenshot.

## Checks completed locally

1. The local EXE hash matches the published GitHub asset digest, and the release
   tag points at source commit `6bf7314b3dac5c1f2af1937eb06859afdcd09d66`.
2. The EXE has **no Authenticode signature**. This is a publisher/reputation
   limitation; it does not establish why these vendors flagged it.
3. Microsoft Defender custom scan of this exact EXE returned **"found no
   threats"**, exit code `0`, with signature version **1.461.60.0** on 2026-10-16.
   This result applies to that engine/signature set only. It does not override
   other vendors' verdicts. No exclusions or antivirus configuration changes
   were used.
4. An independent C# compilation matches the original EXE **byte for byte except
   for its PE build timestamp and CLR module version ID**. The comparison ignores
   only those two metadata fields in memory; it does not write a modified EXE.
   This is source correspondence evidence, not a fully bit-identical build claim.
5. The embedded JAR exactly matches the separately published JAR, SHA-256
   `a85635649daf6e86ad82d580d8d18fda51c2f24697f2e07e698b21819b47578b`.
6. All **8** agent class files exactly match fresh Java compilation of the
   published source. All **38** bundled ASM entries match the pinned vendored
   dependency. Its full SHA-256 is checked before compilation.
7. The reviewed source contains no anti-debug checks, deliberate long sleeps,
   credential collection, installer network communication, process injection via
   Windows APIs, or security-setting changes. Process enumeration is used to
   refuse profile changes while Lunar/Java is running. The Java agent does modify
   Minecraft bytecode in memory at startup, as documented in the README.

These checks cannot establish that the program has no bugs or harmful side
effects, and are not an independent security audit. They give vendors concrete
evidence to investigate the reported detection.

## Reproduce the correspondence check

Requires Windows, the .NET Framework C# compiler, a JDK 8, and Python 3.10+.
Download the original v0.1.1 EXE and JAR into a local directory. The audit script
refuses a different EXE hash. Check out the source commit above in a second
directory, then run the audit tools from the current repository:

The historical source must be checked out with Git `core.autocrlf=false` and
`core.eol=lf`. In particular, the manifest's original LF bytes are embedded in the
EXE; Git converting them to CRLF changes the binary even though the XML is
semantically identical. The public workflow explicitly preserves those original
bytes. The checker does not exclude or rewrite manifest bytes to make them pass.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\audit-release.ps1 `
  -SourceDirectory C:\path\to\v0.1.1-source `
  -ReleaseDirectory C:\path\to\v0.1.1-files `
  -OutputPath .\build\security-evidence\v0.1.1-verification.json
```

The script compiles the inspected source and uses a static PE/CLR reader. It does
not load or run the downloaded EXE. The existing Java and installer tests are a
separate check. Different compiler versions may produce legitimate byte changes;
any mismatch must be investigated, never ignored to force a passing result.

[Public verification workflow](https://github.com/slytess/hit-fix-minecraft-lunarclient/actions/workflows/security-verification.yml)
and [local verification output](security/v0.1.1-verification.json).
The workflow builds/tests current source and separately audits the original
release against its fixed source commit. A green run is not an antivirus verdict.

## How detections should be resolved

[VirusTotal's instructions](https://docs.virustotal.com/docs/false-positive)
say to request review from the detecting vendors; VirusTotal does not control
their verdicts. After a vendor confirms a correction, request a new analysis of
the same hash to check the updated result.

The [vendor review request draft](VENDOR-REVIEW-REQUEST.md) includes the exact hash,
source and evidence. It has **not been submitted**. Elastic's
[official submission instructions](https://discuss.elastic.co/t/submitting-false-positives/232322)
link its community review form. For the other engines, use their official
channels listed via [VirusTotal's contributors page](https://docs.virustotal.com/docs/contributors).
Some vendors require the maintainer to complete a form or sign in.

For future releases, use a legitimate code-signing identity and retain provenance.
[Microsoft's documentation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
explains that signing establishes publisher identity/reputation, and that even a
valid signature is not guaranteed to remove warnings. SmartScreen reputation is
separate from the seven antivirus detections reported here.

The v0.1.1 EXE is being kept unchanged for investigation. Creating a new repository,
renaming, repeatedly rebuilding, obfuscating or repacking a file would not resolve
the underlying review. No "0 detections" claim or malware-free guarantee is made.
