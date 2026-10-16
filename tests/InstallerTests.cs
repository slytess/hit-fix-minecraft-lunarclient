using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using LunarHitFix;

public static class InstallerTests {
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static string fixtures;
    static int passed;
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS: " + name); }
    static void Error(string code, Action action) {
        try { action(); } catch (InstallerError error) { Require(error.Code == code, "Expected " + code + ", got " + error.Code); return; }
        throw new Exception("Expected error " + code);
    }
    static string Make(string name, bool source) {
        string root = Path.Combine(fixtures, name);
        Directory.CreateDirectory(Path.Combine(root, "db"));
        Directory.CreateDirectory(Path.Combine(root, "settings"));
        using (var db = new Sqlite(Path.Combine(root, "db", "profiles.db"), true)) {
            db.Query("CREATE TABLE profiles (id TEXT PRIMARY KEY, name TEXT, description TEXT, path TEXT UNIQUE, type TEXT, game_version TEXT, jvm_arguments TEXT, created_at INTEGER, allocated_memory INTEGER, extension REAL, future_data BLOB)");
            db.Query("INSERT INTO profiles VALUES (?,?,?,?,?,?,?,?,?,?,?)", "other", "Other 1.8", "other", "1.8", "lunar", "1.8.9", null, 100L, 2048L, 1.5, new byte[] { 1, 2 });
            if (source) db.Query("INSERT INTO profiles VALUES (?,?,?,?,?,?,?,?,?,?,?)", "original", "Player's perfil ñ", "original", "1.7", "lunar", "1.7.10", "-Xmx2G -Dcustom=yes", 101L, 4096L, 2.5, new byte[] { 3, 4 });
        }
        File.WriteAllText(Path.Combine(root, "settings", "launcher.json"), "{\"settings\":{\"gameProfile\":\"" + (source ? "original" : "other") + "\",\"preserved\":true},\"unknown\":123}");
        return root;
    }
    static Installer Engine(string root, Action<string> checkpoint) { return new Installer(root, delegate { }, delegate { }, checkpoint); }
    static Installer Engine(string root) { return Engine(root, null); }
    static Dictionary<string, object> Launcher(string root) { return Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(root, "settings", "launcher.json"))); }
    static string Selection(string root) { return Convert.ToString(((Dictionary<string, object>)Launcher(root)["settings"])["gameProfile"]); }
    static void Select(string root, string id) {
        var launcher = Launcher(root); ((Dictionary<string, object>)launcher["settings"])["gameProfile"] = id;
        File.WriteAllText(Path.Combine(root, "settings", "launcher.json"), Json.Serialize(launcher));
    }
    static List<Dictionary<string, object>> Rows(string root, string sql, params object[] args) {
        using (var db = new Sqlite(Path.Combine(root, "db", "profiles.db"), false)) return db.Query(sql, args);
    }
    static InstallState State(string root) { return Json.Deserialize<InstallState>(File.ReadAllText(Engine(root).StatePath)); }
    static void Install(string root) { Engine(root).Install(Installer.EmbeddedAgent()); }
    static void SnapshotUnchanged(string root, string before) { Require(Selection(root) == before, "Selection changed unexpectedly"); Require(Rows(root, "SELECT id FROM profiles WHERE path = ?", Installer.Slug).Count == 0, "Managed row remains"); }
    static Action<string> ThrowAt(string stage) { return value => { if (value == stage) throw new IOException("Injected " + stage); }; }
    static void Crash(string root, string stage, bool revert) {
        var start = new ProcessStartInfo(typeof(InstallerTests).Assembly.Location,
            "--crash \"" + root + "\" " + stage + " " + (revert ? "revert" : "install"));
        start.UseShellExecute = false; start.CreateNoWindow = true;
        using (Process process = Process.Start(start)) {
            Require(process.WaitForExit(15000), "Crash fixture timed out");
            Require(process.ExitCode == 86, "Crash checkpoint was not reached: " + process.ExitCode);
        }
    }
    public static int Main(string[] args) {
        try {
            if (args.Length == 4 && args[0] == "--crash") {
                var engine = Engine(args[1], stage => { if (stage == args[2]) Environment.Exit(86); });
                if (args[3] == "revert") engine.Revert(); else engine.Install(Installer.EmbeddedAgent());
                return 2;
            }
            fixtures = Path.GetFullPath(args[0]); Directory.CreateDirectory(fixtures);
            Test("revert without installation does not create a directory", delegate {
                string root = Path.Combine(fixtures, "absent"); Engine(root).Revert(); Require(!Directory.Exists(root), "Unexpected filesystem change");
            });
            Test("missing Lunar files rejected without creating a database", delegate {
                string root = Path.Combine(fixtures, "missing"); Error("E_LUNAR_NOT_FOUND", () => Install(root)); Require(!Directory.Exists(root), "Created missing database");
            });
            Test("process guard runs before filesystem changes", delegate {
                string root = Path.Combine(fixtures, "running"); var engine = new Installer(root, () => { throw new InstallerError("E_LUNAR_RUNNING", "fixture"); }, delegate { }, null);
                Error("E_LUNAR_RUNNING", () => engine.Install(Installer.EmbeddedAgent())); Require(!Directory.Exists(root), "Files created with running process");
            });
            Test("process guard recognizes Lunar and Java without blocking this installer", delegate {
                Require(!Installer.IsBlockingProcess("LunarHitFixInstaller"), "Installer would block itself");
                Require(!Installer.IsBlockingProcess("InstallerTests"), "Test process is not the game");
                Require(Installer.IsBlockingProcess("Lunar Client") && Installer.IsBlockingProcess("javaw") && Installer.IsBlockingProcess("java"), "Game processes not blocked");
            });
            Test("no original 1.7 profile", delegate { string root = Make("no-source", false); Error("E_NO_17_PROFILE", () => Install(root)); SnapshotUnchanged(root, "other"); });
            Test("older HitFix selection requires normal selection before installation", delegate {
                string root = Make("old-hitfix", true);
                Rows(root, "INSERT INTO profiles (id,path,type,game_version,jvm_arguments) VALUES (?,?,?,?,?)", "legacy", "hitfix-lunar-1.7.10", "lunar", "1.7.10", "-javaagent:hitfix.jar");
                Select(root, "legacy"); Error("E_EXISTING_HITFIX_PROFILE", () => Install(root)); SnapshotUnchanged(root, "legacy");
            });
            Test("unsupported database schema", delegate {
                string root = Make("schema", true); Rows(root, "ALTER TABLE profiles RENAME COLUMN jvm_arguments TO changed");
                Error("E_PROFILE_SCHEMA", () => Install(root)); Require(Selection(root) == "original", "Selection changed");
            });
            Test("malformed launcher JSON", delegate {
                string root = Make("json", true); File.WriteAllText(Engine(root).LauncherPath, "{");
                Error("E_LAUNCHER_FORMAT", () => Install(root)); Require(Rows(root, "SELECT id FROM profiles").Count == 2, "Changed database");
            });
            Test("clones profile fields, Unicode, blobs and JVM arguments", delegate {
                string root = Make("clone", true); Install(root);
                var managed = Rows(root, "SELECT * FROM profiles WHERE path = ?", Installer.Slug).Single();
                Require(Selection(root) == Convert.ToString(managed["id"]), "Not selected");
                Require(Convert.ToString(managed["jvm_arguments"]).StartsWith("-Xmx2G -Dcustom=yes -javaagent:"), "Custom arguments lost");
                Require(Convert.ToInt64(managed["allocated_memory"]) == 4096, "Memory lost");
                Require(((byte[])managed["future_data"]).SequenceEqual(new byte[] {3,4}), "Blob lost");
                Require(Convert.ToDouble(managed["extension"]) == 2.5, "Float lost");
                Require(Convert.ToString(Rows(root, "SELECT name FROM profiles WHERE id = ?", "original")[0]["name"]) == "Player's perfil ñ", "Source modified");
                Require((bool)((Dictionary<string, object>)Launcher(root)["settings"])["preserved"], "Other JSON setting lost");
                var state = State(root); Require(File.Exists(Path.Combine(state.BackupDirectory, "profiles.db")), "No DB backup");
                using (var backup = new Sqlite(Path.Combine(state.BackupDirectory, "profiles.db"), false)) Require(backup.Query("SELECT id FROM profiles").Count == 2, "Backup is not pre-install");
                Require(Installer.Hash(File.ReadAllBytes(Path.Combine(Engine(root).DataDirectory, "hitfix-agent.jar"))) == Installer.Hash(Installer.EmbeddedAgent()), "Payload differs");
                Install(root); Require(Rows(root, "SELECT id FROM profiles").Count == 3, "Duplicate install");
                Require(State(root).PreviousSelection == "original", "Original selection overwritten");
                Engine(root).Revert(); SnapshotUnchanged(root, "original"); Engine(root).Revert(); SnapshotUnchanged(root, "original");
            });
            Test("fallback to 1.7 source restores actual previous selection", delegate {
                string root = Make("fallback", true); Select(root, "other"); Install(root); Engine(root).Revert(); SnapshotUnchanged(root, "other");
            });
            Test("revert preserves other profiles and later source edits", delegate {
                string root = Make("later-edits", true); Install(root);
                Rows(root, "UPDATE profiles SET jvm_arguments = ? WHERE id = ?", "-Dnew=keep", "original");
                Rows(root, "INSERT INTO profiles (id,name,path,type,game_version) VALUES (?,?,?,?,?)", "new", "New", "new", "lunar", "1.21");
                Select(root, "new"); Engine(root).Revert(); SnapshotUnchanged(root, "new");
                Require(Convert.ToString(Rows(root, "SELECT jvm_arguments FROM profiles WHERE id = ?", "original")[0]["jvm_arguments"]) == "-Dnew=keep", "Restored stale DB over later edits");
            });
            Test("missing previous profile blocks destructive revert", delegate {
                string root = Make("deleted-original", true); Install(root); Rows(root, "DELETE FROM profiles WHERE id = ?", "original");
                Error("E_ORIGINAL_PROFILE_MISSING", () => Engine(root).Revert());
                Require(Rows(root, "SELECT id FROM profiles WHERE path = ?", Installer.Slug).Count == 1, "Removed still-selected profile");
                Select(root, "other"); Engine(root).Revert(); SnapshotUnchanged(root, "other");
            });
            Test("unowned profile conflict is never overwritten or adopted", delegate {
                string root = Make("conflict", true); Rows(root, "INSERT INTO profiles (id,path) VALUES (?,?)", "foreign", Installer.Slug);
                Error("E_PROFILE_CONFLICT", () => Install(root)); Engine(root).Revert();
                Require(Rows(root, "SELECT id FROM profiles WHERE id = ?", "foreign").Count == 1, "Deleted unowned profile");
            });
            Test("malformed installation record rejected", delegate {
                string root = Make("bad-state", true); Directory.CreateDirectory(Engine(root).DataDirectory); File.WriteAllText(Engine(root).StatePath, "{}");
                Error("E_STATE_INVALID", () => Install(root)); SnapshotUnchanged(root, "original");
            });
            Test("changed agent rejected and explicit reinstall works", delegate {
                string root = Make("agent-changed", true); Install(root); File.WriteAllText(Path.Combine(Engine(root).DataDirectory, "hitfix-agent.jar"), "changed");
                Error("E_AGENT_CHANGED", () => Install(root)); Engine(root).Revert(); Install(root); Engine(root).Revert(); SnapshotUnchanged(root, "original");
            });
            Test("changed profile path is never deleted", delegate {
                string root = Make("identity-changed", true); Install(root); Rows(root, "UPDATE profiles SET path = ? WHERE id = ?", "custom", State(root).ProfileId);
                Error("E_PROFILE_CHANGED", () => Engine(root).Revert()); Require(Rows(root, "SELECT id FROM profiles WHERE path = ?", "custom").Count == 1, "Wrong profile deleted");
            });
            foreach (string failureStage in new[] { "prepared", "profile-inserted", "launcher-written", "committed" }) {
                string stage = failureStage;
                Test("install recovery after injected error at " + stage, delegate {
                    string root = Make("failure-" + stage, true);
                    try { Engine(root, ThrowAt(stage)).Install(Installer.EmbeddedAgent()); throw new Exception("No injected error"); } catch (IOException) { }
                    Engine(root).Revert(); SnapshotUnchanged(root, "original");
                    Install(root); Engine(root).Revert(); SnapshotUnchanged(root, "original");
                });
            }
            foreach (string failureStage in new[] { "profile-deleted", "revert-launcher-written", "revert-committed" }) {
                string stage = failureStage;
                Test("revert recovery after injected error at " + stage, delegate {
                    string root = Make("failure-" + stage, true); Install(root);
                    try { Engine(root, ThrowAt(stage)).Revert(); throw new Exception("No injected error"); } catch (IOException) { }
                    Engine(root).Revert(); SnapshotUnchanged(root, "original");
                });
            }
            foreach (string crashStage in new[] { "prepared", "profile-inserted", "launcher-written", "committed" }) {
                string stage = crashStage;
                Test("real process termination recovery at " + stage, delegate {
                    string root = Make("crash-" + stage, true); Crash(root, stage, false);
                    Engine(root).Revert(); SnapshotUnchanged(root, "original");
                });
            }
            foreach (string crashStage in new[] { "profile-deleted", "revert-launcher-written", "revert-committed" }) {
                string stage = crashStage;
                Test("real uninstall termination recovery at " + stage, delegate {
                    string root = Make("crash-" + stage, true); Install(root); Crash(root, stage, true);
                    Engine(root).Revert(); SnapshotUnchanged(root, "original");
                });
            }
            Test("install automatically recovers interrupted prior install", delegate {
                string root = Make("auto-recovery", true); Crash(root, "launcher-written", false);
                Install(root); Require(State(root).Phase == "installed", "Recovery not completed"); Engine(root).Revert(); SnapshotUnchanged(root, "original");
            });
            Test("SQLite backup includes committed WAL records", delegate {
                string root = Make("wal", true);
                using (var live = new Sqlite(Engine(root).DatabasePath, false)) {
                    live.Query("PRAGMA journal_mode=WAL"); live.Query("UPDATE profiles SET name = ? WHERE id = ?", "In WAL", "original");
                    Install(root);
                    using (var backup = new Sqlite(Path.Combine(State(root).BackupDirectory, "profiles.db"), false))
                        Require(Convert.ToString(backup.Query("SELECT name FROM profiles WHERE id = ?", "original")[0]["name"]) == "In WAL", "WAL contents not backed up");
                    Engine(root).Revert();
                }
            });
            Test("paths containing spaces work or fail before profile changes", delegate {
                string root = Make("path with spaces", true);
                try { Install(root); Engine(root).Revert(); } catch (InstallerError error) { Require(error.Code == "E_ARGUMENT_PATH", "Unexpected path error"); }
                SnapshotUnchanged(root, "original");
            });
            Console.WriteLine("PASS: " + passed + " installer scenarios. All files stayed under " + fixtures);
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
