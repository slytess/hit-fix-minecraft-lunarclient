using System;
using System.IO;
using System.Text;
using System.Reflection;

[assembly: AssemblyTitle("Lunar HitFix Installer")]
[assembly: AssemblyDescription("Transparent profile installer for the unofficial Lunar 1.7.10 HitFix adaptation")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace LunarHitFix {
    public static class Program {
        static string LogPath;
        static void Log(string text) {
            if (LogPath == null) return;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, DateTime.UtcNow.ToString("o") + " " + text + Environment.NewLine, new UTF8Encoding(false));
            } catch { Console.WriteLine("WARNING: The diagnostic log could not be written."); }
        }
        static void Write(string text) { Console.WriteLine(text); Log(text); }
        public static int Main(string[] arguments) {
            Console.OutputEncoding = Encoding.UTF8;
            if (arguments.Length > 0) {
                if (arguments.Length == 1 && arguments[0] == "--version") { Console.WriteLine("Lunar HitFix Installer 0.1.0"); return 0; }
                Console.WriteLine("Usage: LunarHitFixInstaller.exe");
                Console.WriteLine("Double-click to open the interactive menu. --version prints the version without changing files.");
                return arguments.Length == 1 && arguments[0] == "--help" ? 0 : 2;
            }
            LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunarHitFixInstaller", "installer.log");
            Console.WriteLine("Lunar HitFix Installer 0.1.0 - unofficial / experimental");
            Console.WriteLine("Credits: Incognitolol/HitFix-Mod; upstream also credits dewgs.");
            Console.WriteLine("https://github.com/Incognitolol/HitFix-Mod");
            Console.WriteLine("Processes all queued inbound packets earlier, including damage and teleports.");
            Console.WriteLine("Close Lunar and Minecraft before installing or reverting. No administrator rights needed.");
            Console.WriteLine("Changes only a dedicated launcher profile, its selection, and local HitFix files.");
            Console.WriteLine("No downloads, telemetry, automatic uploads, or changes to animation settings.");
            var installer = new Installer(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lunarclient"), Installer.EnsureLunarClosed, Write, null);
            int lastResult = 0;
            while (true) {
                Console.WriteLine();
                Console.WriteLine("1 - Install / select the HitFix profile");
                Console.WriteLine("2 - Revert this tool's installation");
                Console.WriteLine("0 - Exit");
                Console.Write("Choose 1, 2 or 0: ");
                string choice = Console.ReadLine();
                if (choice == null || choice.Trim() == "0") return lastResult;
                choice = choice.Trim();
                if (choice != "1" && choice != "2") { Console.WriteLine("Invalid choice. Enter 1, 2 or 0."); continue; }
                try {
                    Log("Version=0.1.0 action=" + choice);
                    if (choice == "1") installer.Install(Installer.EmbeddedAgent()); else installer.Revert();
                    lastResult = 0;
                } catch (Exception error) {
                    lastResult = 1;
                    string code = error is InstallerError ? ((InstallerError)error).Code
                        : error is UnauthorizedAccessException ? "E_ACCESS_DENIED"
                        : error is DllNotFoundException || error is EntryPointNotFoundException ? "E_WINDOWS_SQLITE"
                        : error is PathTooLongException ? "E_PATH_TOO_LONG"
                        : error is IOException ? "E_FILE_IO" : "E_UNEXPECTED";
                    Console.WriteLine("ERROR [" + code + "]: " + error.Message);
                    Console.WriteLine("If an operation was interrupted, keep the backups and retry with Lunar closed.");
                    Log("ERROR [" + code + "] " + error);
                    Console.WriteLine("Diagnostic log: " + LogPath);
                    Console.WriteLine("Report the error code and log when asking for help. Review local paths before sharing.");
                }
            }
        }
    }
}
