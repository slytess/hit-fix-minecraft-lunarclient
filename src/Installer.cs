using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace LunarHitFix {
    public sealed class InstallerError : Exception {
        public string Code { get; private set; }
        public InstallerError(string code, string message) : base(message) { Code = code; }
    }
    public sealed class InstallState {
        public int Version { get; set; }
        public string Root { get; set; }
        public string Phase { get; set; }
        public string ProfileId { get; set; }
        public string SourceId { get; set; }
        public string PreviousSelection { get; set; }
        public string Arguments { get; set; }
        public string BackupDirectory { get; set; }
    }
    public sealed class Installer {
        public const string Slug = "hitfix-installer-1.7.10";
        public const string ProfileName = "Lunar 1.7.10 + HitFix (Installer)";
        public readonly string Root;
        public readonly string DataDirectory;
        public string StatePath { get { return Path.Combine(DataDirectory, "state.json"); } }
        public string DatabasePath { get { return Path.Combine(Root, "db", "profiles.db"); } }
        public string LauncherPath { get { return Path.Combine(Root, "settings", "launcher.json"); } }
        readonly Action ensureClosed;
        readonly Action<string> output;
        readonly Action<string> checkpoint;
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16777216 };
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern uint GetShortPathName(string path, StringBuilder buffer, uint length);

        public Installer(string root, Action checkClosed, Action<string> write, Action<string> testCheckpoint) {
            Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            DataDirectory = Path.Combine(Root, "hitfix-installer");
            ensureClosed = checkClosed;
            output = write;
            checkpoint = testCheckpoint ?? delegate { };
        }
        static void Fail(string code, string message) { throw new InstallerError(code, message); }
        public static string Hash(byte[] bytes) {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static byte[] EmbeddedAgent() {
            using (Stream stream = typeof(Installer).Assembly.GetManifestResourceStream("hitfix-agent.jar")) {
                if (stream == null) throw new InstallerError("E_AGENT_MISSING", "The embedded agent is missing. Download a complete release.");
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); return memory.ToArray(); }
            }
        }
        public static void AtomicWrite(string path, byte[] bytes) {
            string temporary = Path.Combine(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        static byte[] Serialize(object value) { return new UTF8Encoding(false).GetBytes(Json.Serialize(value)); }
        void Save(InstallState state) { AtomicWrite(StatePath, Serialize(state)); }
        InstallState Load() {
            if (!File.Exists(StatePath)) return null;
            InstallState state;
            try { state = Json.Deserialize<InstallState>(File.ReadAllText(StatePath)); }
            catch { throw new InstallerError("E_STATE_INVALID", "The installation record cannot be read. Keep it and the backups for troubleshooting."); }
            if (state == null || state.Version != 1 || !String.Equals(state.Root, Root, StringComparison.OrdinalIgnoreCase)
                || String.IsNullOrEmpty(state.ProfileId) || String.IsNullOrEmpty(state.PreviousSelection)
                || !new[] { "prepared", "installed", "removing", "reverted" }.Contains(state.Phase))
                Fail("E_STATE_INVALID", "The installation record does not match this Lunar installation. No profiles were changed.");
            return state;
        }
        Dictionary<string, object> ReadLauncher(out Dictionary<string, object> settings) {
            try {
                var launcher = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(LauncherPath));
                settings = (Dictionary<string, object>)launcher["settings"];
                string selected = (string)settings["gameProfile"];
                if (String.IsNullOrEmpty(selected)) throw new Exception();
                return launcher;
            } catch { throw new InstallerError("E_LAUNCHER_FORMAT", "launcher.json has an unsupported format. No profile changes were started."); }
        }
        void Preflight() {
            ensureClosed();
            if (!File.Exists(DatabasePath) || !File.Exists(LauncherPath))
                Fail("E_LUNAR_NOT_FOUND", "Lunar profiles were not found. Open Lunar, create/select a Lunar 1.7.10 profile, then close Lunar and try again.");
        }
        Sqlite OpenDatabase() {
            var database = new Sqlite(DatabasePath, false);
            try {
                var columns = database.Query("PRAGMA table_info(profiles)").Select(row => Convert.ToString(row["name"])).ToArray();
                foreach (string required in new[] { "id", "name", "description", "path", "type", "game_version", "jvm_arguments", "created_at" })
                    if (!columns.Contains(required)) Fail("E_PROFILE_SCHEMA", "Lunar's profiles database format is unsupported (missing " + required + ").");
                return database;
            } catch { database.Dispose(); throw; }
        }
        FileStream Lock() {
            Directory.CreateDirectory(DataDirectory);
            try { return new FileStream(Path.Combine(DataDirectory, "installer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new InstallerError("E_INSTALLER_BUSY", "Another installer is running. Close the other installer and retry."); }
        }
        static bool NormalProfile(Dictionary<string, object> row) {
            string arguments = Convert.ToString(row["jvm_arguments"]);
            return Convert.ToString(row["type"]) == "lunar" && Convert.ToString(row["game_version"]) == "1.7.10"
                && Convert.ToString(row["path"]) != Slug
                && Convert.ToString(row["path"]) != "hitfix-lunar-1.7.10"
                && arguments.IndexOf("hitfix", StringComparison.OrdinalIgnoreCase) < 0;
        }
        static string Identifier(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }
        static string ArgumentDirectory(string path) {
            if (!path.Any(Char.IsWhiteSpace)) return path;
            var buffer = new StringBuilder(32768);
            uint length = GetShortPathName(path, buffer, (uint)buffer.Capacity);
            string result = buffer.ToString();
            if (length == 0 || length >= buffer.Capacity || result.Any(Char.IsWhiteSpace))
                Fail("E_ARGUMENT_PATH", "Lunar's JVM argument parser needs a path without spaces. Windows could not provide a short path for the installation directory. No profiles were changed.");
            return result;
        }
        public void Install(byte[] agent) {
            Preflight();
            using (Lock()) using (Sqlite database = OpenDatabase()) {
                InstallState state = Load();
                if (state != null && (state.Phase == "prepared" || state.Phase == "removing")) {
                    output("Recovering an interrupted operation...");
                    Remove(database, state);
                    state = Load();
                }
                var existing = database.Query("SELECT * FROM profiles WHERE path = ?", Slug);
                if (state != null && state.Phase == "installed") {
                    if (existing.Count != 1 || Convert.ToString(existing[0]["id"]) != state.ProfileId
                        || Convert.ToString(existing[0]["jvm_arguments"]) != state.Arguments)
                        Fail("E_PROFILE_CHANGED", "The managed profile was changed or removed. Use option 2 to revert before installing again.");
                    string agentPath = Path.Combine(DataDirectory, "hitfix-agent.jar");
                    if (!File.Exists(agentPath) || Hash(File.ReadAllBytes(agentPath)) != Hash(agent))
                        Fail("E_AGENT_CHANGED", "The installed agent differs from this release or is missing. Use option 2, then option 1 to reinstall.");
                    Dictionary<string, object> settings;
                    var launcher = ReadLauncher(out settings);
                    settings["gameProfile"] = state.ProfileId;
                    AtomicWrite(LauncherPath, Serialize(launcher));
                    output("HitFix is already installed. Selected: " + ProfileName);
                    return;
                }
                if (existing.Count != 0) Fail("E_PROFILE_CONFLICT", "An existing profile uses the installer path, but this tool has no matching installation record. Nothing was overwritten.");
                Dictionary<string, object> launcherSettings;
                var launcherData = ReadLauncher(out launcherSettings);
                string previous = Convert.ToString(launcherSettings["gameProfile"]);
                var profiles = database.Query("SELECT * FROM profiles ORDER BY id");
                if (!profiles.Any(row => Convert.ToString(row["id"]) == previous))
                    Fail("E_PREVIOUS_PROFILE_MISSING", "The selected original profile no longer exists. Select a normal Lunar profile first.");
                var previousProfile = profiles.First(row => Convert.ToString(row["id"]) == previous);
                if (Convert.ToString(previousProfile["jvm_arguments"]).IndexOf("hitfix", StringComparison.OrdinalIgnoreCase) >= 0
                    || Convert.ToString(previousProfile["path"]) == "hitfix-lunar-1.7.10")
                    Fail("E_EXISTING_HITFIX_PROFILE", "An older HitFix profile is selected. Select your normal Lunar profile, close Lunar, then install. This keeps option 2 able to restore a normal selection.");
                var source = profiles.FirstOrDefault(row => Convert.ToString(row["id"]) == previous && NormalProfile(row))
                    ?? profiles.FirstOrDefault(NormalProfile);
                if (source == null) Fail("E_NO_17_PROFILE", "No normal Lunar 1.7.10 profile was found. Create one in Lunar, close Lunar and retry.");
                output("Source profile: " + Convert.ToString(source["name"]));
                output("Creates a separate profile and selects it. Your source profile and visual settings are preserved.");
                output("HitFix processes all queued inbound packets earlier, including damage and teleports.");
                string argumentDirectory = ArgumentDirectory(DataDirectory);
                string agentFile = Path.Combine(DataDirectory, "hitfix-agent.jar");
                // Never replace a previously installed payload while it might still be referenced.
                if (File.Exists(agentFile) && Hash(File.ReadAllBytes(agentFile)) != Hash(agent))
                    AtomicWrite(agentFile, agent);
                else if (!File.Exists(agentFile)) AtomicWrite(agentFile, agent);
                string arguments = (Convert.ToString(source["jvm_arguments"]) + " -javaagent:" + Path.Combine(argumentDirectory, "hitfix-agent.jar")
                    + " -Dhitfix.log=" + Path.Combine(argumentDirectory, "hitfix-runtime.log")).Trim();
                string backup = Path.Combine(DataDirectory, "backups", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backup);
                database.Backup(Path.Combine(backup, "profiles.db"));
                byte[] oldLauncher = File.ReadAllBytes(LauncherPath);
                AtomicWrite(Path.Combine(backup, "launcher.json"), oldLauncher);
                state = new InstallState { Version=1, Root=Root, Phase="prepared", ProfileId=Guid.NewGuid().ToString(),
                    SourceId=Convert.ToString(source["id"]), PreviousSelection=previous, Arguments=arguments, BackupDirectory=backup };
                Save(state);
                checkpoint("prepared");
                var values = new Dictionary<string, object>(source);
                values["id"] = state.ProfileId; values["name"] = ProfileName; values["path"] = Slug;
                values["description"] = "Unofficial HitFix adaptation: earlier inbound packet processing for Lunar 1.7.10.";
                values["jvm_arguments"] = arguments;
                values["created_at"] = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                bool launcherWritten = false;
                database.Query("BEGIN IMMEDIATE");
                try {
                    database.Query("INSERT INTO profiles (" + String.Join(",", values.Keys.Select(Identifier)) + ") VALUES ("
                        + String.Join(",", values.Keys.Select(key => "?")) + ")", values.Values.ToArray());
                    checkpoint("profile-inserted");
                    launcherSettings["gameProfile"] = state.ProfileId;
                    AtomicWrite(LauncherPath, Serialize(launcherData)); launcherWritten = true;
                    checkpoint("launcher-written");
                    database.Query("COMMIT");
                } catch {
                    try { database.Query("ROLLBACK"); } catch { }
                    if (launcherWritten) AtomicWrite(LauncherPath, oldLauncher);
                    throw;
                }
                checkpoint("committed");
                state.Phase = "installed"; Save(state);
                output("SUCCESS: Created and selected " + ProfileName);
                output("Agent SHA-256: " + Hash(agent));
                output("Backups: " + backup);
                output("Open Lunar and launch the new profile. Use option 2 with Lunar closed to revert.");
            }
        }
        public void Revert() {
            // Absence of the install record must never trigger changes to unrelated profiles.
            if (!File.Exists(StatePath)) {
                output("No previous installation or backed-up profile was found. Nothing to revert."); return;
            }
            Preflight();
            using (Lock()) using (Sqlite database = OpenDatabase()) {
                InstallState state = Load();
                if (state == null || state.Phase == "reverted") {
                    output("No active HitFix installation was found. Nothing to revert."); return;
                }
                Remove(database, state);
                output("SUCCESS: HitFix profile removed. Open Lunar to play normally.");
                output("Backups and local logs were kept in: " + DataDirectory);
            }
        }
        void Remove(Sqlite database, InstallState state) {
            var rows = database.Query("SELECT * FROM profiles WHERE id = ?", state.ProfileId);
            if (rows.Count > 0 && Convert.ToString(rows[0]["path"]) != Slug)
                Fail("E_PROFILE_CHANGED", "The managed profile's identity changed. No profiles were removed. Keep state.json and backups for troubleshooting.");
            Dictionary<string, object> settings;
            var launcher = ReadLauncher(out settings);
            bool selected = Convert.ToString(settings["gameProfile"]) == state.ProfileId;
            if (selected && database.Query("SELECT id FROM profiles WHERE id = ?", state.PreviousSelection).Count == 0)
                Fail("E_ORIGINAL_PROFILE_MISSING", "Your previous profile no longer exists. Select another normal profile in Lunar, close Lunar, then run option 2 again.");
            // Persist the intent so the next invocation can finish an interrupted uninstall.
            state.Phase = "removing"; Save(state);
            byte[] oldLauncher = File.ReadAllBytes(LauncherPath);
            bool launcherWritten = false;
            database.Query("BEGIN IMMEDIATE");
            try {
                database.Query("DELETE FROM profiles WHERE id = ? AND path = ?", state.ProfileId, Slug);
                checkpoint("profile-deleted");
                if (selected) {
                    settings["gameProfile"] = state.PreviousSelection;
                    AtomicWrite(LauncherPath, Serialize(launcher)); launcherWritten = true;
                }
                checkpoint("revert-launcher-written");
                database.Query("COMMIT");
            } catch {
                try { database.Query("ROLLBACK"); } catch { }
                if (launcherWritten) AtomicWrite(LauncherPath, oldLauncher);
                throw;
            }
            checkpoint("revert-committed");
            state.Phase = "reverted"; Save(state);
        }
        public static void EnsureLunarClosed() {
            foreach (Process process in Process.GetProcesses()) {
                using (process) {
                    string name;
                    try { name = process.ProcessName; } catch { continue; }
                    // Conservative: Java may be the running game. Do not inspect command lines.
                    if (IsBlockingProcess(name))
                        Fail("E_LUNAR_RUNNING", "Close Lunar Client and Minecraft before continuing. A Lunar or Java process is still running (" + name + "). This tool never closes processes for you.");
                }
            }
        }
        public static bool IsBlockingProcess(string name) {
            return name.StartsWith("Lunar Client", StringComparison.OrdinalIgnoreCase)
                || name.Equals("LunarClient", StringComparison.OrdinalIgnoreCase)
                || name.Equals("javaw", StringComparison.OrdinalIgnoreCase)
                || name.Equals("java", StringComparison.OrdinalIgnoreCase);
        }
    }
}
