#nullable enable

using System;
using System.IO;
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
                using var game = new Nullscent.Game1();
                game.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] Unhandled Crash: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                logWriter.Close();
                fileStream.Close();
            }
        }
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
