#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Nullscent.Beatmap
{
    /// <summary>
    /// Parser completo de archivos .osu (formato osu!mania, Mode = 3).
    /// Implementa el formato de beatmap según especificación osu!stable/osu!lazer.
    /// Soporta todas las secciones: [General], [Metadata], [Difficulty], [TimingPoints], [HitObjects].
    /// </summary>
    public static class BeatmapParser
    {
        /// <summary>
        /// Parsea un archivo .osu completo y retorna un objeto Beatmap.
        /// </summary>
        /// <param name="filePath">Ruta completa al archivo .osu</param>
        /// <returns>Beatmap parseado con todos los datos</returns>
        /// <exception cref="FileNotFoundException">Si el archivo no existe</exception>
        /// <exception cref="InvalidDataException">Si el archivo no es válido o no es modo mania</exception>
        public static Beatmap Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Beatmap file not found: {filePath}");

            var beatmap = new Beatmap
            {
                FilePath = filePath,
                Directory = Path.GetDirectoryName(filePath) ?? string.Empty
            };

            // Leer todas las líneas con encoding UTF-8 (soporta BOM)
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);

            string currentSection = string.Empty;
            var hitObjectLines = new List<string>();
            var timingPointLines = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Ignorar líneas vacías y comentarios
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                // Detectar cambio de sección
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line[1..^1];
                    continue;
                }

                // Parsear según sección actual
                switch (currentSection)
                {
                    case "General":
                        ParseGeneralLine(line, beatmap);
                        break;

                    case "Metadata":
                        ParseMetadataLine(line, beatmap);
                        break;

                    case "Difficulty":
                        ParseDifficultyLine(line, beatmap);
                        break;

                    case "TimingPoints":
                        timingPointLines.Add(line);
                        break;

                    case "HitObjects":
                        hitObjectLines.Add(line);
                        break;
                }
            }

            // Validar que sea modo mania
            if (beatmap.Metadata.Mode != 3)
                throw new InvalidDataException($"Beatmap is not osu!mania mode (Mode = {beatmap.Metadata.Mode}). Only Mode = 3 is supported.");

            // Validar key count
            if (beatmap.KeyCount < 1 || beatmap.KeyCount > 18)
                throw new InvalidDataException($"Invalid key count: {beatmap.KeyCount}. Supported range: 1-18.");

            // Parsear timing points y ordenar
            beatmap.TimingPoints = ParseTimingPoints(timingPointLines);
            beatmap.TimingPoints.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            Console.WriteLine($"[BeatmapParser] Parsing {hitObjectLines.Count} hit object lines for {beatmap.KeyCount}K");

            // Parsear hit objects, calcular columnas y SV, luego ordenar
            beatmap.HitObjects = ParseHitObjects(hitObjectLines, beatmap.KeyCount, beatmap);
            beatmap.HitObjects.Sort((a, b) => a.Time.CompareTo(b.Time));

            Console.WriteLine($"[BeatmapParser] Parsed {beatmap.HitObjects.Count} hit objects");

            return beatmap;
        }

        /// <summary>
        /// Parsea una línea de la sección [General].
        /// </summary>
        private static void ParseGeneralLine(string line, Beatmap beatmap)
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return;

            switch (parts[0])
            {
                case "AudioFilename":
                    beatmap.Metadata.AudioFilename = parts[1].Trim();
                    break;

                case "AudioLeadIn":
                    beatmap.Metadata.AudioLeadIn = ParseInt(parts[1]);
                    break;

                case "PreviewTime":
                    beatmap.Metadata.PreviewTime = ParseInt(parts[1]);
                    break;

                case "Mode":
                    beatmap.Metadata.Mode = ParseInt(parts[1]);
                    break;
            }
        }

        /// <summary>
        /// Parsea una línea de la sección [Metadata].
        /// </summary>
        private static void ParseMetadataLine(string line, Beatmap beatmap)
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return;

            switch (parts[0])
            {
                case "Title":
                    beatmap.Metadata.Title = parts[1];
                    break;

                case "TitleUnicode":
                    beatmap.Metadata.TitleUnicode = parts[1];
                    break;

                case "Artist":
                    beatmap.Metadata.Artist = parts[1];
                    break;

                case "ArtistUnicode":
                    beatmap.Metadata.ArtistUnicode = parts[1];
                    break;

                case "Creator":
                    beatmap.Metadata.Creator = parts[1];
                    break;

                case "Version":
                    beatmap.Metadata.Version = parts[1];
                    break;

                case "BeatmapSetID":
                    beatmap.Metadata.BeatmapSetID = ParseInt(parts[1]);
                    break;

                case "BeatmapID":
                    beatmap.Metadata.BeatmapID = ParseInt(parts[1]);
                    break;

                case "Source":
                    beatmap.Metadata.Source = parts[1];
                    break;

                case "Tags":
                    beatmap.Metadata.Tags = parts[1];
                    break;
            }
        }

        /// <summary>
        /// Parsea una línea de la sección [Difficulty].
        /// </summary>
        private static void ParseDifficultyLine(string line, Beatmap beatmap)
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return;

            switch (parts[0])
            {
                case "HPDrainRate":
                    beatmap.HPDrainRate = ParseFloat(parts[1]);
                    break;

                case "CircleSize":
                    beatmap.CircleSize = ParseFloat(parts[1]);
                    break;

                case "OverallDifficulty":
                    beatmap.OverallDifficulty = ParseFloat(parts[1]);
                    break;

                case "SliderMultiplier":
                    beatmap.SliderMultiplier = ParseFloat(parts[1]);
                    break;

                case "SliderTickRate":
                    beatmap.SliderTickRate = ParseFloat(parts[1]);
                    break;
            }
        }

        /// <summary>
        /// Parsea todas las líneas de [TimingPoints].
        /// Formato: offset,beatLength,meter,sampleSet,sampleIndex,volume,uninherited,effects
        /// </summary>
        private static List<TimingPoint> ParseTimingPoints(List<string> lines)
        {
            var timingPoints = new List<TimingPoint>();

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var tp = new TimingPoint
                {
                    Offset = ParseDouble(parts[0]),
                    BeatLength = ParseDouble(parts[1]),
                    Meter = parts.Length > 2 ? ParseInt(parts[2]) : 4,
                    SampleSet = parts.Length > 3 ? ParseInt(parts[3]) : 0,
                    SampleIndex = parts.Length > 4 ? ParseInt(parts[4]) : 0,
                    Volume = parts.Length > 5 ? ParseInt(parts[5]) : 100,
                    Uninherited = parts.Length > 6 ? ParseInt(parts[6]) == 1 : true,
                    Effects = parts.Length > 7 ? ParseInt(parts[7]) : 0
                };

                timingPoints.Add(tp);
            }

            return timingPoints;
        }

        /// <summary>
        /// Parsea todas las líneas de [HitObjects].
        /// Formato: x,y,time,type,hitSound,endTime:extras (para LN) o extras (para normal note)
        /// </summary>
        private static List<HitObject> ParseHitObjects(List<string> lines, int keyCount, Beatmap beatmap)
        {
            var hitObjects = new List<HitObject>();

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                var hitObject = new HitObject
                {
                    X = ParseInt(parts[0]),
                    Y = ParseInt(parts[1]),
                    Time = ParseInt(parts[2]),
                    Type = ParseInt(parts[3]),
                    HitSound = ParseInt(parts[4])
                };

                // Calcular columna según fórmula osu!mania: column = floor(x * keyCount / 512)
                hitObject.Column = (int)Math.Floor((double)hitObject.X * keyCount / 512.0);

                // Clamp columna al rango válido (safety)
                hitObject.Column = Math.Clamp(hitObject.Column, 0, keyCount - 1);

                // Parsear end time para long notes
                if (hitObject.IsLongNote && parts.Length > 5)
                {
                    // Formato: endTime:extras
                    var endTimeParts = parts[5].Split(':');
                    hitObject.EndTime = ParseInt(endTimeParts[0]);

                    // Parsear sample file name si está presente
                    if (endTimeParts.Length > 4 && !string.IsNullOrWhiteSpace(endTimeParts[4]))
                        hitObject.SampleFileName = endTimeParts[4];
                }
                else
                {
                    hitObject.EndTime = hitObject.Time;

                    // Para notas normales, extras está directamente en parts[5]
                    if (parts.Length > 5)
                    {
                        var extras = parts[5].Split(':');
                        if (extras.Length > 4 && !string.IsNullOrWhiteSpace(extras[4]))
                            hitObject.SampleFileName = extras[4];
                    }
                }

                // Calcular scroll velocity efectiva en este objeto
                hitObject.ScrollVelocity = beatmap.GetScrollVelocityAt(hitObject.Time);

                hitObjects.Add(hitObject);
            }

            return hitObjects;
        }

        /// <summary>
        /// Parsea un int de forma segura con cultura invariante.
        /// </summary>
        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) 
                ? result 
                : 0;
        }

        /// <summary>
        /// Parsea un float de forma segura con cultura invariante.
        /// </summary>
        private static float ParseFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) 
                ? result 
                : 0f;
        }

        /// <summary>
        /// Parsea un double de forma segura con cultura invariante.
        /// </summary>
        private static double ParseDouble(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) 
                ? result 
                : 0.0;
        }

        /// <summary>
        /// Carga solo los metadatos de un beatmap sin parsear timing points ni hit objects.
        /// Útil para song select (escaneo rápido de carpetas).
        /// </summary>
        /// <param name="filePath">Ruta al archivo .osu</param>
        /// <returns>Beatmap con solo metadatos y difficulty cargados</returns>
        public static Beatmap ParseMetadataOnly(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Beatmap file not found: {filePath}");

            var beatmap = new Beatmap
            {
                FilePath = filePath,
                Directory = Path.GetDirectoryName(filePath) ?? string.Empty
            };

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            string currentSection = string.Empty;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line[1..^1];

                    // Parar early si ya pasamos las secciones que necesitamos
                    if (currentSection == "TimingPoints")
                        break;

                    continue;
                }

                switch (currentSection)
                {
                    case "General":
                        ParseGeneralLine(line, beatmap);
                        break;

                    case "Metadata":
                        ParseMetadataLine(line, beatmap);
                        break;

                    case "Difficulty":
                        ParseDifficultyLine(line, beatmap);
                        break;
                }
            }

            return beatmap;
        }
    }
}
