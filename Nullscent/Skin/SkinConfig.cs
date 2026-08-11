#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Nullscent.Skin
{
    /// <summary>
    /// Configuración de una skin parseada desde skin.ini.
    /// Soporta el formato de skin de osu!stable (sección [Mania]).
    /// </summary>
    public class SkinConfig
    {
        public string SkinDirectory { get; set; } = string.Empty;
        public string Name { get; set; } = "Default";
        public string Author { get; set; } = "Unknown";
        public string Version { get; set; } = "1.0";
        public int AnimationFramerate { get; set; } = -1; // -1 significa auto-calcular o fallback

        /// <summary>
        /// Configuraciones específicas de mania por key count.
        /// Dictionary: KeyCount → ManiaKeyConfig
        /// </summary>
        public Dictionary<int, ManiaKeyConfig> ManiaConfigs { get; set; } = new();

        /// <summary>
        /// Configuración por defecto para todos los key counts no especificados.
        /// </summary>
        public ManiaKeyConfig DefaultConfig { get; set; } = new();

        /// <summary>
        /// Obtiene la configuración para un key count específico.
        /// Si no existe configuración específica, retorna DefaultConfig.
        /// </summary>
        public ManiaKeyConfig GetConfigForKeyCount(int keyCount)
        {
            return ManiaConfigs.TryGetValue(keyCount, out var config) ? config : DefaultConfig;
        }
    }

    /// <summary>
    /// Configuración específica de mania para un key count.
    /// Basado directamente en la especificación de osu!mania skinning.
    /// </summary>
    public class ManiaKeyConfig
    {
        public float[]? ColumnWidths { get; set; }
        public float[]? ColumnSpacing { get; set; }
        public bool[]? ColumnLines { get; set; }
        public float[]? LightingNWidths { get; set; }
        public float[]? LightingLWidths { get; set; }
        public float? ColumnStart { get; set; }
        public float? WidthForNoteHeightScale { get; set; }
        public (byte R, byte G, byte B, byte A)[]? ColumnColors { get; set; } // Obsoleto, usar ColumnColorsMap
        public int? HitPosition { get; set; }
        public int? LightPosition { get; set; }
        public bool ColumnLighting { get; set; } = true;
        public bool JudgementLine { get; set; } = true;
        public int SpecialStyle { get; set; } = 0;
        public bool UpsideDown { get; set; } = false;
        public bool KeysUnderNotes { get; set; } = false;
        public int? ScorePosition { get; set; }
        public int? ComboPosition { get; set; }
        public int NoteBodyStyle { get; set; } = 1;
        public Dictionary<int, int> NoteBodyStyles { get; set; } = new();

        // Colores ColourX, ColourLightX, ColourHoldX (0-indexed para ColumnIndex)
        public Dictionary<int, (byte R, byte G, byte B, byte A)> ColumnColorsMap { get; set; } = new();
        public Dictionary<int, (byte R, byte G, byte B, byte A)> ColourLights { get; set; } = new();
        public Dictionary<int, (byte R, byte G, byte B, byte A)> ColourHolds { get; set; } = new();

        public (byte R, byte G, byte B, byte A)? ColourBarline { get; set; }
        public (byte R, byte G, byte B, byte A)? ColourJudgementLine { get; set; }
        public (byte R, byte G, byte B, byte A)? ColourColumnLine { get; set; }
        public (byte R, byte G, byte B, byte A)? ColourBreak { get; set; }
        public (byte R, byte G, byte B, byte A)? ColourHold { get; set; }

        // Recursos gráficos específicos para el stage
        public string? StageLeft { get; set; }
        public string? StageRight { get; set; }
        public string? StageBottom { get; set; }
        public string? StageHint { get; set; }
        public string? StageLight { get; set; }
        public string? LightingN { get; set; }
        public string? LightingL { get; set; }

        public Dictionary<int, string> NoteImages { get; set; } = new();
        public Dictionary<int, string> NoteHeadImages { get; set; } = new();
        public Dictionary<int, string> NoteBodyImages { get; set; } = new();
        public Dictionary<int, string> NoteTailImages { get; set; } = new();
        public Dictionary<int, string> KeyImages { get; set; } = new();
        public Dictionary<int, string> KeyPressedImages { get; set; } = new();
    }

    /// <summary>
    /// Parser robusto de skin.ini (formato osu!stable).
    /// </summary>
    public static class SkinConfigParser
    {
        private class ParsedSection
        {
            public string Name { get; set; } = string.Empty;
            public List<KeyValuePair<string, string>> KeyValues { get; } = new();
        }

        public static SkinConfig Parse(string skinDirectory)
        {
            string iniPath = Path.Combine(skinDirectory, "skin.ini");
            string resolvedSkinDir = skinDirectory;

            if (!File.Exists(iniPath))
            {
                try
                {
                    if (Directory.Exists(skinDirectory))
                    {
                        var subDirs = Directory.GetDirectories(skinDirectory);
                        foreach (var subDir in subDirs)
                        {
                            if (File.Exists(Path.Combine(subDir, "skin.ini")))
                            {
                                resolvedSkinDir = subDir;
                                iniPath = Path.Combine(resolvedSkinDir, "skin.ini");
                                Console.WriteLine($"[SkinConfigParser] Found skin.ini in subdirectory: {resolvedSkinDir}");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SkinConfigParser] Error searching subdirectories: {ex.Message}");
                }
            }

            var config = new SkinConfig { SkinDirectory = resolvedSkinDir };

            if (!File.Exists(iniPath))
            {
                Console.WriteLine($"[SkinConfigParser] No skin.ini found in {skinDirectory}, using defaults");
                return config;
            }

            try
            {
                var lines = File.ReadAllLines(iniPath);
                var sections = new List<ParsedSection>();
                ParsedSection? currentSection = null;

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    // Ignorar líneas vacías y comentarios que ocupan toda la línea (empezando con //, # o ;)
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    // Eliminar comentarios en línea
                    int commentIdx = line.IndexOf("//");
                    if (commentIdx >= 0)
                        line = line[..commentIdx].Trim();

                    commentIdx = line.IndexOf(";");
                    if (commentIdx >= 0)
                        line = line[..commentIdx].Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Detectar cabecera de sección
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = new ParsedSection { Name = line[1..^1].Trim() };
                        sections.Add(currentSection);
                        continue;
                    }

                    // Parsear par clave-valor
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2 && currentSection != null)
                    {
                        currentSection.KeyValues.Add(new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()));
                    }
                }

                // Procesar cada sección recolectada
                foreach (var section in sections)
                {
                    if (section.Name == "General")
                    {
                        foreach (var kvp in section.KeyValues)
                        {
                            ParseGeneralSection(kvp.Key, kvp.Value, config);
                        }
                    }
                    else if (section.Name == "Mania" || (section.Name.StartsWith("Mania") && section.Name.EndsWith("K")))
                    {
                        // 1. Determinar keyCount. Primero ver si está la clave 'Keys' en la sección.
                        int? keyCount = null;
                        var keysKvp = section.KeyValues.FirstOrDefault(k => k.Key.Equals("Keys", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(keysKvp.Key) && int.TryParse(keysKvp.Value, out int parsedKeys))
                        {
                            keyCount = parsedKeys;
                        }
                        // Si no, intentar obtenerlo del nombre de la sección (ej: [Mania4K] -> 4)
                        else if (section.Name.StartsWith("Mania") && section.Name.EndsWith("K"))
                        {
                            string keyStr = section.Name[5..^1];
                            if (int.TryParse(keyStr, out int parsedKeysSec))
                            {
                                keyCount = parsedKeysSec;
                            }
                        }

                        // 2. Obtener o crear configuración correspondiente
                        ManiaKeyConfig maniaConfig;
                        if (keyCount.HasValue)
                        {
                            if (!config.ManiaConfigs.TryGetValue(keyCount.Value, out var specificConfig))
                            {
                                specificConfig = new ManiaKeyConfig();
                                config.ManiaConfigs[keyCount.Value] = specificConfig;
                            }
                            maniaConfig = specificConfig;
                        }
                        else
                        {
                            maniaConfig = config.DefaultConfig;
                        }

                        // 3. Parsear todas las claves de esta sección
                        foreach (var kvp in section.KeyValues)
                        {
                            ParseManiaSection(kvp.Key, kvp.Value, maniaConfig);
                        }
                    }
                }

                Console.WriteLine($"[SkinConfigParser] Parsed skin.ini: {config.Name} by {config.Author} (Mania configs: {config.ManiaConfigs.Count})");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkinConfigParser] Failed to parse skin.ini: {ex.Message}");
                return config;
            }
        }

        private static void ParseGeneralSection(string key, string value, SkinConfig config)
        {
            switch (key)
            {
                case "Name":
                    config.Name = value;
                    break;
                case "Author":
                    config.Author = value;
                    break;
                case "Version":
                    config.Version = value;
                    break;
                case "AnimationFramerate":
                    if (int.TryParse(value, out int fps))
                        config.AnimationFramerate = fps;
                    break;
            }
        }

        private static void ParseManiaSection(string key, string value, ManiaKeyConfig maniaConfig)
        {
            if (key.Equals("ColumnWidth", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.ColumnWidths = ParseFloatArray(value);
            }
            else if (key.Equals("ColumnSpacing", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.ColumnSpacing = ParseFloatArray(value);
            }
            else if (key.Equals("ColumnLine", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.ColumnLines = ParseBoolArray(value);
            }
            else if (key.Equals("ColumnStart", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float start))
                    maniaConfig.ColumnStart = start;
            }
            else if (key.Equals("WidthForNoteHeightScale", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                    maniaConfig.WidthForNoteHeightScale = val;
            }
            else if (key.Equals("HitPosition", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out int hitPos))
                    maniaConfig.HitPosition = hitPos;
            }
            else if (key.Equals("LightPosition", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out int lightPos))
                    maniaConfig.LightPosition = lightPos;
            }
            else if (key.Equals("LightingNWidth", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.LightingNWidths = ParseFloatArray(value);
            }
            else if (key.Equals("LightingLWidth", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.LightingLWidths = ParseFloatArray(value);
            }
            else if (key.Equals("ScorePosition", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out int scorePos))
                    maniaConfig.ScorePosition = scorePos;
            }
            else if (key.Equals("ComboPosition", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out int comboPos))
                    maniaConfig.ComboPosition = comboPos;
            }
            else if (key.Equals("JudgementLine", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.JudgementLine = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("SpecialStyle", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out int style))
                    maniaConfig.SpecialStyle = style;
            }
            else if (key.Equals("UpsideDown", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.UpsideDown = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("KeysUnderNotes", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.KeysUnderNotes = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("ColumnLighting", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.ColumnLighting = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("LightingN", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.LightingN = value;
            }
            else if (key.Equals("LightingL", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.LightingL = value;
            }
            else if (key.Equals("StageLeft", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.StageLeft = value;
            }
            else if (key.Equals("StageRight", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.StageRight = value;
            }
            else if (key.Equals("StageBottom", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.StageBottom = value;
            }
            else if (key.Equals("StageHint", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.StageHint = value;
            }
            else if (key.Equals("StageLight", StringComparison.OrdinalIgnoreCase))
            {
                maniaConfig.StageLight = value;
            }
            else if (key.Equals("ColourBarline", StringComparison.OrdinalIgnoreCase))
            {
                var col = ParseColor(value);
                if (col.HasValue) maniaConfig.ColourBarline = col.Value;
            }
            else if (key.Equals("ColourJudgementLine", StringComparison.OrdinalIgnoreCase))
            {
                var col = ParseColor(value);
                if (col.HasValue) maniaConfig.ColourJudgementLine = col.Value;
            }
            else if (key.Equals("ColourColumnLine", StringComparison.OrdinalIgnoreCase))
            {
                var col = ParseColor(value);
                if (col.HasValue) maniaConfig.ColourColumnLine = col.Value;
            }
            else if (key.Equals("ColourBreak", StringComparison.OrdinalIgnoreCase))
            {
                var col = ParseColor(value);
                if (col.HasValue) maniaConfig.ColourBreak = col.Value;
            }
            else if (key.StartsWith("NoteBodyStyle", StringComparison.OrdinalIgnoreCase))
            {
                string idxStr = key["NoteBodyStyle".Length..];
                if (int.TryParse(idxStr, out int idx))
                {
                    if (idx >= 0 && int.TryParse(value, out int bodyStyle))
                    {
                        if (bodyStyle >= 0 && bodyStyle <= 2)
                            maniaConfig.NoteBodyStyles[idx] = bodyStyle;
                    }
                }
                else if (string.IsNullOrEmpty(idxStr))
                {
                    if (int.TryParse(value, out int bodyStyle))
                    {
                        if (bodyStyle >= 0 && bodyStyle <= 2)
                            maniaConfig.NoteBodyStyle = bodyStyle;
                    }
                }
            }
            else if (key.StartsWith("ColourLight", StringComparison.OrdinalIgnoreCase))
            {
                string idxStr = key["ColourLight".Length..];
                if (int.TryParse(idxStr, out int idx))
                {
                    if (idx >= 1)
                    {
                        var col = ParseColor(value);
                        if (col.HasValue)
                            maniaConfig.ColourLights[idx - 1] = col.Value;
                    }
                }
            }
            else if (key.StartsWith("ColourHold", StringComparison.OrdinalIgnoreCase))
            {
                string idxStr = key["ColourHold".Length..];
                if (int.TryParse(idxStr, out int idx))
                {
                    if (idx >= 1)
                    {
                        var col = ParseColor(value);
                        if (col.HasValue)
                            maniaConfig.ColourHolds[idx - 1] = col.Value;
                    }
                }
                else if (string.IsNullOrEmpty(idxStr))
                {
                    var col = ParseColor(value);
                    if (col.HasValue)
                        maniaConfig.ColourHold = col.Value;
                }
            }
            else if (key.StartsWith("Colour", StringComparison.OrdinalIgnoreCase))
            {
                string idxStr = key["Colour".Length..];
                if (int.TryParse(idxStr, out int idx))
                {
                    if (idx >= 1)
                    {
                        var col = ParseColor(value);
                        if (col.HasValue)
                            maniaConfig.ColumnColorsMap[idx - 1] = col.Value;
                    }
                }
            }
            else if (key.StartsWith("NoteImage", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = "";
                string indexStr = key["NoteImage".Length..];
                if (indexStr.EndsWith("H", StringComparison.OrdinalIgnoreCase)) { suffix = "H"; indexStr = indexStr[..^1]; }
                else if (indexStr.EndsWith("L", StringComparison.OrdinalIgnoreCase)) { suffix = "L"; indexStr = indexStr[..^1]; }
                else if (indexStr.EndsWith("T", StringComparison.OrdinalIgnoreCase)) { suffix = "T"; indexStr = indexStr[..^1]; }

                if (int.TryParse(indexStr, out int idx) && idx >= 0)
                {
                    if (suffix == "H") maniaConfig.NoteHeadImages[idx] = value;
                    else if (suffix == "L") maniaConfig.NoteBodyImages[idx] = value;
                    else if (suffix == "T") maniaConfig.NoteTailImages[idx] = value;
                    else maniaConfig.NoteImages[idx] = value;
                }
            }
            else if (key.StartsWith("KeyImage", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = "";
                string indexStr = key["KeyImage".Length..];
                if (indexStr.EndsWith("D", StringComparison.OrdinalIgnoreCase)) { suffix = "D"; indexStr = indexStr[..^1]; }

                if (int.TryParse(indexStr, out int idx) && idx >= 0)
                {
                    if (suffix == "D") maniaConfig.KeyPressedImages[idx] = value;
                    else maniaConfig.KeyImages[idx] = value;
                }
            }
        }

        private static float[] ParseFloatArray(string value)
        {
            return value.Split(',')
                .Select(s => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float w) ? w : 0f)
                .ToArray();
        }

        private static bool[] ParseBoolArray(string value)
        {
            return value.Split(',')
                .Select(s => s.Trim() == "1" || s.Trim().ToLower() == "true")
                .ToArray();
        }

        private static (byte R, byte G, byte B, byte A)? ParseColor(string value)
        {
            var parts = value.Split(',');
            if (parts.Length < 3) return null;
            byte r = byte.TryParse(parts[0].Trim(), out byte vr) ? vr : (byte)255;
            byte g = byte.TryParse(parts[1].Trim(), out byte vg) ? vg : (byte)255;
            byte b = byte.TryParse(parts[2].Trim(), out byte vb) ? vb : (byte)255;
            byte a = parts.Length > 3 && byte.TryParse(parts[3].Trim(), out byte va) ? va : (byte)255;
            return (r, g, b, a);
        }
    }
}
