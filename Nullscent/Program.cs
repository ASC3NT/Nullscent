#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Nullscent
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            string logPath;
            FileStream fileStream;
            try
            {
                logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nullscent_log.txt");
                fileStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            }
            catch
            {
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nullscent");
                Directory.CreateDirectory(appDataFolder);
                logPath = Path.Combine(appDataFolder, "nullscent_log.txt");
                fileStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            }

            var logWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
            
            // Guardar salida original de consola
            var originalOut = Console.Out;
            var dualWriter = new DualTextWriter(originalOut, logWriter);
            Console.SetOut(dualWriter);

            Console.WriteLine("==============================================");
            Console.WriteLine($"=== Nullscent Practice Client Log Started ===");
            Console.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Log file path: {logPath}");
            Console.WriteLine("==============================================");

            try
            {
#if DEBUG
                if (args.Length == 1 && args[0] == "--skin-tests")
                {
                    Skin.SkinCoreSelfTests.RunAll();
                    return;
                }
                if (args.Length == 9 && args[0] == "--skin-visual-resize-smoke" && int.TryParse(args[2], out int resizeKeyCount)
                    && int.TryParse(args[5], out int initialWidth) && int.TryParse(args[6], out int initialHeight)
                    && int.TryParse(args[7], out int resizedWidth) && int.TryParse(args[8], out int resizedHeight))
                {
                    using var resizeSmoke = new Skin.SkinVisualSmokeGame(args[1], resizeKeyCount, args[3], true,
                        viewportWidth: initialWidth, viewportHeight: initialHeight,
                        resizeOutputPath: args[4], resizedViewportWidth: resizedWidth, resizedViewportHeight: resizedHeight);
                    resizeSmoke.Run();
                    return;
                }
                if (args.Length is 6 or 7 && args[0] == "--skin-visual-switch-smoke" && int.TryParse(args[3], out int switchKeyCount))
                {
                    bool switchDownScroll = args.Length == 6 || !args[6].Equals("up", StringComparison.OrdinalIgnoreCase);
                    using var switchSmoke = new Skin.SkinVisualSmokeGame(
                        args[1], switchKeyCount, args[4], switchDownScroll, args[2], args[5]);
                    switchSmoke.Run();
                    return;
                }
                if (args.Length >= 5 && args[0] == "--skin-visual-cycle-smoke" && int.TryParse(args[2], out int cycleKeyCount))
                {
                    string cycleOutputDirectory = Path.GetFullPath(args[3]);
                    Directory.CreateDirectory(cycleOutputDirectory);
                    using var cycleSmoke = new Skin.SkinVisualSmokeGame(
                        args[1], cycleKeyCount, Path.Combine(cycleOutputDirectory, "00-initial.png"), true,
                        viewportWidth: 1920, viewportHeight: 1080,
                        switchSequence: args.Skip(4).ToArray(), switchSequenceOutputDirectory: cycleOutputDirectory);
                    cycleSmoke.Run();
                    return;
                }
                if (args.Length >= 5 && args[0] == "--skin-visual-hd-cycle-smoke"
                    && int.TryParse(args[2], out int hdCycleKeyCount))
                {
                    var highResolutionSequence = new bool[args.Length - 4];
                    for (int i = 4; i < args.Length; i++)
                    {
                        string value = args[i];
                        if (value.Equals("on", StringComparison.OrdinalIgnoreCase)
                            || value.Equals("hd", StringComparison.OrdinalIgnoreCase))
                            highResolutionSequence[i - 4] = true;
                        else if (value.Equals("off", StringComparison.OrdinalIgnoreCase)
                            || value.Equals("sd", StringComparison.OrdinalIgnoreCase))
                            highResolutionSequence[i - 4] = false;
                        else
                            throw new ArgumentException($"Modo HD invÃ¡lido en el paso {i - 3}: {value}; usa on/off.");
                    }

                    string hdCycleOutputDirectory = Path.GetFullPath(args[3]);
                    Directory.CreateDirectory(hdCycleOutputDirectory);
                    using var hdCycleSmoke = new Skin.SkinVisualSmokeGame(
                        args[1], hdCycleKeyCount, Path.Combine(hdCycleOutputDirectory, "00-initial.png"), true,
                        viewportWidth: 1920, viewportHeight: 1080,
                        highResolutionSequence: highResolutionSequence,
                        highResolutionSequenceOutputDirectory: hdCycleOutputDirectory);
                    hdCycleSmoke.Run();
                    return;
                }
                if (args.Length is >= 7 and <= 10 && args[0] == "--skin-visual-profile-smoke"
                    && int.TryParse(args[2], out int profileKeyCount)
                    && TryParseLegacySkinMode(args[3], out Skin.LegacySkinCompatibilityMode initialMode)
                    && TryParseLegacySkinMode(args[4], out Skin.LegacySkinCompatibilityMode targetMode))
                {
                    bool profileDownScroll = args.Length < 8 || !args[7].Equals("up", StringComparison.OrdinalIgnoreCase);
                    int profileWidth = args.Length >= 9 && int.TryParse(args[8], out int parsedProfileWidth) ? parsedProfileWidth : 1280;
                    int profileHeight = args.Length >= 10 && int.TryParse(args[9], out int parsedProfileHeight) ? parsedProfileHeight : 720;
                    using var profileSmoke = new Skin.SkinVisualSmokeGame(
                        args[1], profileKeyCount, args[5], profileDownScroll,
                        viewportWidth: profileWidth, viewportHeight: profileHeight,
                        legacyCompatibilityMode: initialMode, switchCompatibilityMode: targetMode,
                        compatibilityOutputPath: args[6]);
                    profileSmoke.Run();
                    return;
                }
                if (args.Length > 0 && args[0] == "--skin-visual-profile-cycle-smoke")
                {
                    if (args.Length < 6)
                        throw new ArgumentException("Uso: --skin-visual-profile-cycle-smoke <skinPath> <keyCount> <initialMode> <outputDir> <targetMode...>");
                    if (!int.TryParse(args[2], out int profileCycleKeyCount) || profileCycleKeyCount < 1 || profileCycleKeyCount > 18)
                        throw new ArgumentException($"Keycount inválido para el ciclo de perfil: {args[2]}");
                    if (!TryParseLegacySkinMode(args[3], out Skin.LegacySkinCompatibilityMode initialProfileMode))
                        throw new ArgumentException($"Perfil legacy inicial inválido: {args[3]}");

                    bool stageOnly = args.Skip(5).Any(argument => argument.Equals("--stage-only", StringComparison.OrdinalIgnoreCase));
                    var profileSequence = new Skin.LegacySkinCompatibilityMode[args.Length - 5 - (stageOnly ? 1 : 0)];
                    for (int i = 5, targetIndex = 0; i < args.Length; i++)
                    {
                        if (args[i].Equals("--stage-only", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!TryParseLegacySkinMode(args[i], out profileSequence[targetIndex++]))
                            throw new ArgumentException($"Perfil legacy inválido en el paso {i - 4}: {args[i]}");
                    }

                    string profileCycleOutputDirectory = Path.GetFullPath(args[4]);
                    Directory.CreateDirectory(profileCycleOutputDirectory);
                    using var profileCycleSmoke = new Skin.SkinVisualSmokeGame(
                        args[1], profileCycleKeyCount, Path.Combine(profileCycleOutputDirectory, "00-initial.png"), true,
                        viewportWidth: 1920, viewportHeight: 1080,
                        legacyCompatibilityMode: initialProfileMode,
                        compatibilitySequence: profileSequence,
                        compatibilitySequenceOutputDirectory: profileCycleOutputDirectory,
                        stageOnly: stageOnly);
                    profileCycleSmoke.Run();
                    return;
                }
                if (args.Length is >= 4 and <= 7 && args[0] == "--skin-visual-timeline-smoke"
                    && int.TryParse(args[2], out int timelineKeyCount))
                {
                    bool timelineDownScroll = args.Length < 5 || !args[4].Equals("up", StringComparison.OrdinalIgnoreCase);
                    int timelineWidth = args.Length >= 6 && int.TryParse(args[5], out int parsedTimelineWidth) ? parsedTimelineWidth : 1280;
                    int timelineHeight = args.Length >= 7 && int.TryParse(args[6], out int parsedTimelineHeight) ? parsedTimelineHeight : 720;
                    string outputDirectory = Path.GetFullPath(args[3]);
                    using var timelineSmoke = new Skin.SkinVisualSmokeGame(args[1], timelineKeyCount,
                        Path.Combine(outputDirectory, "00-approach-0700ms.png"), timelineDownScroll,
                        viewportWidth: timelineWidth, viewportHeight: timelineHeight,
                        timelineOutputDirectory: outputDirectory);
                    timelineSmoke.Run();
                    return;
                }
                if (args.Length is >= 5 and <= 8
                    && args[0] is "--skin-visual-at-ln-smoke" or "--skin-visual-at-ln-autocomplete-smoke"
                    && int.TryParse(args[2], out int atLnKeyCount)
                    && double.TryParse(args[4], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double atLnDuration))
                {
                    bool atLnDownScroll = args.Length < 6 || !args[5].Equals("up", StringComparison.OrdinalIgnoreCase);
                    int atLnWidth = args.Length >= 7 && int.TryParse(args[6], out int parsedAtLnWidth) ? parsedAtLnWidth : 1280;
                    int atLnHeight = args.Length >= 8 && int.TryParse(args[7], out int parsedAtLnHeight) ? parsedAtLnHeight : 720;
                    string atLnOutputDirectory = Path.GetFullPath(args[3]);
                    Directory.CreateDirectory(atLnOutputDirectory);
                    using var atLnSmoke = new Skin.SkinVisualSmokeGame(args[1], atLnKeyCount,
                        Path.Combine(atLnOutputDirectory, "00-at-ln-release.png"), atLnDownScroll,
                        viewportWidth: atLnWidth, viewportHeight: atLnHeight,
                        atReleaseTimelineOutputDirectory: atLnOutputDirectory,
                        atReleaseTimelineDurationMs: atLnDuration,
                        holdThroughAutoCompletion: args[0] == "--skin-visual-at-ln-autocomplete-smoke");
                    atLnSmoke.Run();
                    return;
                }
                if (args.Length is >= 4 and <= 7 && args[0] == "--skin-visual-smoke" && int.TryParse(args[2], out int keyCount))
                {
                    bool downScroll = args.Length == 4 || !args[4].Equals("up", StringComparison.OrdinalIgnoreCase);
                    int width = args.Length >= 6 && int.TryParse(args[5], out int parsedWidth) ? parsedWidth : 1280;
                    int height = args.Length >= 7 && int.TryParse(args[6], out int parsedHeight) ? parsedHeight : 720;
                    using var smoke = new Skin.SkinVisualSmokeGame(args[1], keyCount, args[3], downScroll,
                        viewportWidth: width, viewportHeight: height);
                    smoke.Run();
                    return;
                }
#endif
                using var game = new Nullscent.Game1();
                game.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] Unhandled Crash: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
#if DEBUG
                if (args.Length > 0 && (args[0] == "--skin-tests" || args[0] == "--skin-visual-smoke" || args[0] == "--skin-visual-switch-smoke"
                    || args[0] == "--skin-visual-profile-smoke" || args[0] == "--skin-visual-resize-smoke"
                    || args[0] == "--skin-visual-timeline-smoke" || args[0] == "--skin-visual-cycle-smoke"
                    || args[0] == "--skin-visual-at-ln-smoke" || args[0] == "--skin-visual-at-ln-autocomplete-smoke"
                    || args[0] == "--skin-visual-profile-cycle-smoke"
                    || args[0] == "--skin-visual-hd-cycle-smoke")) Environment.ExitCode = 1;
#endif
            }
            finally
            {
                logWriter.Close();
                fileStream.Close();
            }
        }

#if DEBUG
        private static bool TryParseLegacySkinMode(string value, out Skin.LegacySkinCompatibilityMode mode)
        {
            if (value.Equals("stable", StringComparison.OrdinalIgnoreCase))
            {
                mode = Skin.LegacySkinCompatibilityMode.Stable;
                return true;
            }
            if (value.Equals("lazer", StringComparison.OrdinalIgnoreCase))
            {
                mode = Skin.LegacySkinCompatibilityMode.Lazer;
                return true;
            }

            mode = default;
            return false;
        }
#endif
    }

    /// <summary>
    /// Escribe simultáneamente en la Consola estándar y en el archivo log con timestamps.
    /// </summary>
    public class DualTextWriter : TextWriter
    {
        private readonly TextWriter _consoleWriter;
        private readonly TextWriter _fileWriter;

        public DualTextWriter(TextWriter consoleWriter, TextWriter fileWriter)
        {
            _consoleWriter = consoleWriter;
            _fileWriter = fileWriter;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _consoleWriter.Write(value);
            _fileWriter.Write(value);
        }

        public override void Write(string? value)
        {
            if (value == null) return;
            _consoleWriter.Write(value);
            _fileWriter.Write(value);
        }

        public override void WriteLine(string? value)
        {
            if (value == null) return;
            
            // Evitar duplicar marcas de tiempo si ya vienen formateadas
            string formatted = value.StartsWith("[") && value.Contains("]") && value.Length > 12 && char.IsDigit(value[1])
                ? value
                : $"[{DateTime.Now:HH:mm:ss.fff}] {value}";

            _consoleWriter.WriteLine(formatted);
            _fileWriter.WriteLine(formatted);
        }
    }
}
