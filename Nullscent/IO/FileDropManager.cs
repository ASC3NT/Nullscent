#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace Nullscent.IO
{
    /// <summary>
    /// Gestiona la importación de archivos .osz (beatmaps) y .osk (skins).
    /// Inspirado en osu!lazer - permite integrar fácilmente contenido.
    /// Implementa drag-and-drop usando Windows Forms para MonoGame.
    /// </summary>
    public class FileDropManager
    {
        private readonly string _songsDirectory;
        private readonly string _skinsDirectory;

        public event Action<string>? OnBeatmapImported;
        public event Action<string>? OnSkinImported;
        public event Action<string>? OnImportError;

        public FileDropManager(GameWindow window, string songsDirectory, string skinsDirectory)
        {
            _songsDirectory = songsDirectory;
            _skinsDirectory = skinsDirectory;

            // Asegurar que las carpetas existan
            Directory.CreateDirectory(_songsDirectory);
            Directory.CreateDirectory(_skinsDirectory);

            Console.WriteLine($"[FileDropManager] Songs directory: {_songsDirectory}");
            Console.WriteLine($"[FileDropManager] Skins directory: {_skinsDirectory}");

            try
            {
                window.FileDrop += (sender, e) =>
                {
                    Console.WriteLine($"[FileDropManager] FileDrop event triggered via MonoGame, found {e.Files.Length} file(s)");
                    foreach (var file in e.Files)
                    {
                        Console.WriteLine($"[FileDropManager] Processing file: {file}");
                        ImportFile(file);
                    }
                };
                Console.WriteLine("[FileDropManager] ✓ Drag & drop enabled successfully via MonoGame FileDrop!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileDropManager] ERROR: Could not enable MonoGame drag & drop: {ex.Message}");
            }
        }

        /// <summary>
        /// Importa un archivo (puede ser .osz, .osk o .osu).
        /// </summary>
        public void ImportFile(string filePath)
        {
            try
            {
                ProcessFile(filePath);
            }
            catch (Exception ex)
            {
                OnImportError?.Invoke($"Error importing {Path.GetFileName(filePath)}: {ex.Message}");
                Console.WriteLine($"[FileDropManager] Import failed: {ex}");
            }
        }

        private void ProcessFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            switch (extension)
            {
                case ".osz":
                    ImportBeatmap(filePath);
                    break;

                case ".osk":
                    ImportSkin(filePath);
                    break;

                case ".osu":
                    ImportLooseOsuFile(filePath);
                    break;

                default:
                    OnImportError?.Invoke($"Unsupported file type: {extension}");
                    break;
            }
        }

        /// <summary>
        /// Importa un archivo .osz (beatmap pack) extrayéndolo a Songs/.
        /// </summary>
        private void ImportBeatmap(string oszPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(oszPath);
            string targetDirectory = Path.Combine(_songsDirectory, SanitizeFolderName(fileName));

            // Si ya existe, agregar número
            int counter = 1;
            string baseDirectory = targetDirectory;
            while (Directory.Exists(targetDirectory))
            {
                targetDirectory = $"{baseDirectory} ({counter})";
                counter++;
            }

            Directory.CreateDirectory(targetDirectory);

            try
            {
                // Extraer .osz (es un ZIP)
                ZipFile.ExtractToDirectory(oszPath, targetDirectory);

                Console.WriteLine($"[FileDropManager] Imported beatmap: {fileName} -> {targetDirectory}");
                OnBeatmapImported?.Invoke(targetDirectory);
            }
            catch (Exception ex)
            {
                // Limpiar si falla
                if (Directory.Exists(targetDirectory) && !Directory.EnumerateFiles(targetDirectory).Any())
                    Directory.Delete(targetDirectory);

                throw new Exception($"Failed to extract beatmap: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Importa un archivo .osk (skin pack) extrayéndolo a Skins/.
        /// </summary>
        private void ImportSkin(string oskPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(oskPath);
            string targetDirectory = Path.Combine(_skinsDirectory, SanitizeFolderName(fileName));

            // Si ya existe, agregar número
            int counter = 1;
            string baseDirectory = targetDirectory;
            while (Directory.Exists(targetDirectory))
            {
                targetDirectory = $"{baseDirectory} ({counter})";
                counter++;
            }

            Directory.CreateDirectory(targetDirectory);

            try
            {
                // Extraer .osk (es un ZIP)
                ZipFile.ExtractToDirectory(oskPath, targetDirectory);

                Console.WriteLine($"[FileDropManager] Imported skin: {fileName} -> {targetDirectory}");
                OnSkinImported?.Invoke(targetDirectory);
            }
            catch (Exception ex)
            {
                // Limpiar si falla
                if (Directory.Exists(targetDirectory) && !Directory.EnumerateFiles(targetDirectory).Any())
                    Directory.Delete(targetDirectory);

                throw new Exception($"Failed to extract skin: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Importa un archivo .osu suelto creando una carpeta para él.
        /// </summary>
        private void ImportLooseOsuFile(string osuFilePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(osuFilePath);
            string targetDirectory = Path.Combine(_songsDirectory, SanitizeFolderName(fileName));

            int counter = 1;
            string baseDirectory = targetDirectory;
            while (Directory.Exists(targetDirectory))
            {
                targetDirectory = $"{baseDirectory} ({counter})";
                counter++;
            }

            Directory.CreateDirectory(targetDirectory);
            string targetFile = Path.Combine(targetDirectory, Path.GetFileName(osuFilePath));
            File.Copy(osuFilePath, targetFile);

            Console.WriteLine($"[FileDropManager] Imported loose .osu file: {fileName}");
            OnBeatmapImported?.Invoke(targetDirectory);
        }

        /// <summary>
        /// Sanitiza nombres de carpeta para evitar caracteres inválidos.
        /// </summary>
        private string SanitizeFolderName(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
