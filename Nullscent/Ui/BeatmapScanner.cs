#nullable enable

using Nullscent.Beatmap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nullscent.UI
{
    /// <summary>
    /// Escanea directorios de beatmaps de forma asíncrona.
    /// Agrupa beatmaps por BeatmapSetID y carga metadatos rápidamente.
    /// </summary>
    public static class BeatmapScanner
    {
        /// <summary>
        /// Escanea un directorio de forma asíncrona y retorna lista de beatmap sets.
        /// </summary>
        /// <param name="songsDirectory">Directorio raíz donde buscar beatmaps</param>
        /// <param name="progressCallback">Callback opcional para reportar progreso (current, total)</param>
        /// <returns>Lista de beatmap sets encontrados</returns>
        public static async Task<List<BeatmapSet>> ScanDirectoryAsync(
            string songsDirectory,
            Action<int, int>? progressCallback = null)
        {
            return await Task.Run(() => ScanDirectory(songsDirectory, progressCallback));
        }

        /// <summary>
        /// Escanea un directorio sincrónicamente.
        /// </summary>
        private static List<BeatmapSet> ScanDirectory(
            string songsDirectory,
            Action<int, int>? progressCallback = null)
        {
            Console.WriteLine($"[BeatmapScanner] Scanning: {songsDirectory}");

            if (!Directory.Exists(songsDirectory))
            {
                Console.WriteLine($"[BeatmapScanner] Directory not found: {songsDirectory}");
                return new List<BeatmapSet>();
            }

            // Buscar todos los archivos .osu recursivamente
            var osuFiles = Directory.GetFiles(songsDirectory, "*.osu", SearchOption.AllDirectories);
            Console.WriteLine($"[BeatmapScanner] Found {osuFiles.Length} .osu files");

            if (osuFiles.Length == 0)
                return new List<BeatmapSet>();

            // Parsear metadatos de cada archivo
            var beatmaps = new List<(string FilePath, Beatmap.Beatmap Metadata)>();
            int processed = 0;

            foreach (var filePath in osuFiles)
            {
                try
                {
                    var beatmap = BeatmapParser.ParseMetadataOnly(filePath);

                    // Solo aceptar beatmaps de mania (Mode = 3)
                    if (beatmap.Metadata.Mode == 3)
                    {
                        beatmaps.Add((filePath, beatmap));
                    }

                    processed++;
                    progressCallback?.Invoke(processed, osuFiles.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BeatmapScanner] Failed to parse {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            Console.WriteLine($"[BeatmapScanner] Parsed {beatmaps.Count} mania beatmaps");

            // Agrupar por BeatmapSetID
            var groupedBeatmaps = beatmaps
                .GroupBy(b => b.Metadata.Metadata.BeatmapSetID != 0
                    ? b.Metadata.Metadata.BeatmapSetID
                    : GetHashCode(b.Metadata.Directory)) // Fallback para beatmaps sin SetID
                .ToList();

            Console.WriteLine($"[BeatmapScanner] Found {groupedBeatmaps.Count} beatmap sets");

            // Crear BeatmapSet objects
            var beatmapSets = new List<BeatmapSet>();

            foreach (var group in groupedBeatmaps)
            {
                var firstBeatmap = group.First().Metadata;

                var set = new BeatmapSet
                {
                    BeatmapSetID = group.Key,
                    Directory = firstBeatmap.Directory,
                    Title = firstBeatmap.Metadata.Title,
                    TitleUnicode = firstBeatmap.Metadata.TitleUnicode,
                    Artist = firstBeatmap.Metadata.Artist,
                    ArtistUnicode = firstBeatmap.Metadata.ArtistUnicode,
                    Creator = firstBeatmap.Metadata.Creator
                };

                // Crear difficulties
                foreach (var (filePath, beatmap) in group)
                {
                    var difficulty = new BeatmapDifficulty
                    {
                        FilePath = filePath,
                        DifficultyName = beatmap.Metadata.Version,
                        KeyCount = beatmap.KeyCount,
                        OverallDifficulty = beatmap.OverallDifficulty,
                        HPDrainRate = beatmap.HPDrainRate,
                        BPM = beatmap.MostCommonBPM,
                        Length = beatmap.TotalLength,
                        ObjectCount = beatmap.HitObjects.Count,
                        BeatmapID = beatmap.Metadata.BeatmapID
                    };

                    set.Difficulties.Add(difficulty);
                }

                // Ordenar dificultades por OD (fácil a difícil)
                set.Difficulties.Sort((a, b) => a.OverallDifficulty.CompareTo(b.OverallDifficulty));

                beatmapSets.Add(set);
            }

            // Ordenar sets por artista + título
            beatmapSets.Sort((a, b) =>
            {
                int artistCompare = string.Compare(a.DisplayArtist, b.DisplayArtist, StringComparison.OrdinalIgnoreCase);
                return artistCompare != 0 ? artistCompare : string.Compare(a.DisplayTitle, b.DisplayTitle, StringComparison.OrdinalIgnoreCase);
            });

            Console.WriteLine($"[BeatmapScanner] Scan complete: {beatmapSets.Count} sets ready");
            return beatmapSets;
        }

        /// <summary>
        /// Genera un hash code simple para un string (usado como fallback para BeatmapSetID).
        /// </summary>
        private static int GetHashCode(string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;

            unchecked
            {
                int hash = 17;
                foreach (char c in str)
                    hash = hash * 31 + c;
                return hash;
            }
        }
    }
}
