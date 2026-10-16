# Troubleshooting

| Code | Meaning / next step |
| --- | --- |
| `E_LUNAR_RUNNING` | Close Lunar and Minecraft completely. The conservative process check also blocks other Java applications. The tool does not kill them. |
| `E_LUNAR_NOT_FOUND` | Launch Lunar normally, create a 1.7.10 profile, then close it. Custom root locations are not supported. |
| `E_NO_17_PROFILE` | No normal Lunar 1.7.10 profile exists. Create one first; the tool will not create a vanilla profile from scratch. |
| `E_EXISTING_HITFIX_PROFILE` | An older HitFix profile is selected. Select a normal profile in Lunar, close it, then install. The installer does not adopt or remove older installations. |
| `E_PREVIOUS_PROFILE_MISSING` | The launcher's selected profile does not exist in its database. Select a normal profile in Lunar first. |
| `E_ORIGINAL_PROFILE_MISSING` | The previously selected profile was removed after installation. Select another normal profile in Lunar, close it, then choose 2 again. |
| `E_PROFILE_SCHEMA` / `E_LAUNCHER_FORMAT` | An unsupported Lunar data format was detected. Keep your files and report the version; do not edit the database blindly. |
| `E_PROFILE_CONFLICT` | The reserved profile path exists without a matching record. The tool will not overwrite or adopt it. |
| `E_PROFILE_CHANGED` | A managed profile was changed or removed. Option 2 can remove a profile that still has its recorded ID and reserved path; a changed identity requires investigation. |
| `E_AGENT_CHANGED` | Agent is missing or differs from the EXE's embedded copy. Choose 2, then 1 to reinstall. |
| `E_ARGUMENT_PATH` | A path contains spaces and a Windows short path is unavailable. No profile was changed. Report this limitation. |
| `E_STATE_INVALID` | The recovery record is malformed or belongs to a different root. Keep the record and backups for investigation. |
| `E_INSTALLER_BUSY` | Another instance holds the installation lock. Close it first. |
| `E_WINDOWS_SQLITE` | A required Windows SQLite API is unavailable. Use a supported Windows 10/11 installation. |
| `E_PATH_TOO_LONG` | A path exceeds the .NET Framework/Windows path limit. Keep the log and report the failing path length; custom Lunar roots are not supported by this release. |
| `E_ACCESS_DENIED` / `E_FILE_IO` | Check file permissions, disk space and locks. Use your normal account. Running as administrator is not the intended fix. |
| `E_UNEXPECTED` | Keep the log and error details. SQLite lock errors may also appear under this code. |

If Windows or the installer closes mid-operation, reopen the tool with Lunar
closed. Option 1 recovers an incomplete operation before installing; option 2
finishes reverting. Recovery keeps backups and never automatically restores a
whole database snapshot over later profile edits.

If recovery cannot complete, keep `state.json`, the backups and the logs. Do not
delete the journal to force installation. Do not post database or account files
publicly. Logs have no automatic redaction of local filesystem paths.
