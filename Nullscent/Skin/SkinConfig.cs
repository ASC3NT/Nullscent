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
        /// <summary>
        /// Ruta al directorio de la skin.
        /// </summary>
        public string SkinDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Nombre de la skin.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Autor de la skin.
        /// </summary>
        public string Author { get; set; } = "Unknown";

        /// <summary>
        /// Versión de la skin.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Configuraciones específicas de mania por key count.
        /// Dictionary: KeyCount → ManiaConfig
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
    /// </summary>
    public class ManiaKeyConfig
    {
        /// <summary>
        /// Anchos de columna personalizados (en píxeles). Array de longitud = key count.
        /// Si es null o vacío, se usa ancho automático.
        /// </summary>
        public float[]? ColumnWidths { get; set; }

        /// <summary>
        /// Colores personalizados por columna. Array de (R, G, B, A).
        /// </summary>
        public (byte R, byte G, byte B, byte A)[]? ColumnColors { get; set; }

        /// <summary>
        /// Posición del hit position (receptor) en píxeles desde la parte inferior.
        /// Si es null, usa el valor por defecto de GameSettings.
        /// </summary>
        public int? HitPosition { get; set; }

        /// <summary>
        /// Habilitar iluminación de columnas al presionar (glow effect).
        /// </summary>
        public bool ColumnLighting { get; set; } = true;

        /// <summary>
        /// Rutas personalizadas a archivos de note images por columna.
        /// Dictionary: columnIndex → filePath
        /// </summary>
        public Dictionary<int, string> NoteImages { get; set; } = new();

        /// <summary>
        /// Rutas personalizadas a archivos de note head images (para LN).
        /// </summary>
        public Dictionary<int, string> NoteHeadImages { get; set; } = new();

        /// <summary>
        /// Rutas personalizadas a archivos de note body images (para LN).
        /// </summary>
        public Dictionary<int, string> NoteBodyImages { get; set; } = new();

        /// <summary>
        /// Rutas personalizadas a archivos de note tail images (para LN).
        /// </summary>
        public Dictionary<int, string> NoteTailImages { get; set; } = new();

        /// <summary>
        /// Rutas personalizadas a archivos de key/receptor images.
        /// </summary>
        public Dictionary<int, string> KeyImages { get; set; } = new();

        /// <summary>
        /// Rutas personalizadas a archivos de key pressed images.
        /// </summary>
        public Dictionary<int, string> KeyPressedImages { get; set; } = new();
    }

    /// <summary>
    /// Parser de skin.ini (formato osu!stable).
    /// </summary>
    public static class SkinConfigParser
    {
        /// <summary>
        /// Parsea un archivo skin.ini y retorna SkinConfig.
        /// </summary>
        public static SkinConfig Parse(string skinDirectory)
        {
            string iniPath = Path.Combine(skinDirectory, "skin.ini");

            var config = new SkinConfig
            {
                SkinDirectory = skinDirectory
            };

            if (!File.Exists(iniPath))
            {
                Console.WriteLine($"[SkinConfigParser] No skin.ini found in {skinDirectory}, using defaults");
                return config;
            }

            try
            {
                var lines = File.ReadAllLines(iniPath);
                string currentSection = string.Empty;
                int? currentKeyCount = null;

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    // Ignorar comentarios y líneas vacías
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("#"))
                        continue;

                    // Detectar sección
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line[1..^1];

                        // Parsear key count si la sección es [ManiaXK] (ej: [Mania4K])
                        if (currentSection.StartsWith("Mania") && currentSection.EndsWith("K"))
                        {
                            string keyStr = currentSection[5..^1];
                            if (int.TryParse(keyStr, out int keyCount))
                            {
                                currentKeyCount = keyCount;
                                if (!config.ManiaConfigs.ContainsKey(keyCount))
                                    config.ManiaConfigs[keyCount] = new ManiaKeyConfig();
                            }
                        }
                        else if (currentSection == "Mania")
                        {
                            currentKeyCount = null; // Configuración general de mania
                        }

                        continue;
                    }

                    // Parsear key-value
                    var parts = line.Split(':', 2);
                    if (parts.Length != 2)
                        continue;

                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    // Parsear según sección
                    if (currentSection == "General")
                    {
                        ParseGeneralSection(key, value, config);
                    }
                    else if (currentSection == "Mania" || currentKeyCount.HasValue)
                    {
                        var maniaConfig = currentKeyCount.HasValue
                            ? config.ManiaConfigs[currentKeyCount.Value]
                            : config.DefaultConfig;

                        ParseManiaSection(key, value, maniaConfig, currentKeyCount);
                    }
                }

                Console.WriteLine($"[SkinConfigParser] Parsed skin.ini: {config.Name} by {config.Author}");
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
            }
        }

        private static void ParseManiaSection(string key, string value, ManiaKeyConfig maniaConfig, int? keyCount)
        {
            // ColumnWidth (comma-separated)
            if (key == "ColumnWidth")
            {
                var widths = value.Split(',')
                    .Select(s => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float w) ? w : 50f)
                    .ToArray();

                maniaConfig.ColumnWidths = widths;
            }

            // HitPosition
            else if (key == "HitPosition")
            {
                if (int.TryParse(value, out int hitPos))
                    maniaConfig.HitPosition = hitPos;
            }

            // ColumnLighting
            else if (key == "LightingN" || key == "ColumnLighting")
            {
                maniaConfig.ColumnLighting = value == "1" || value.ToLower() == "true";
            }

            // Note images
            else if (key.StartsWith("NoteImage"))
            {
                ParseImageKey(key, value, maniaConfig.NoteImages, "NoteImage");
            }

            // Note head images (LN)
            else if (key.StartsWith("NoteImageH"))
            {
                ParseImageKey(key, value, maniaConfig.NoteHeadImages, "NoteImageH");
            }

            // Note body images (LN)
            else if (key.StartsWith("NoteImageL"))
            {
                ParseImageKey(key, value, maniaConfig.NoteBodyImages, "NoteImageL");
            }

            // Note tail images (LN)
            else if (key.StartsWith("NoteImageT"))
            {
                ParseImageKey(key, value, maniaConfig.NoteTailImages, "NoteImageT");
            }

            // Key images
            else if (key.StartsWith("KeyImage") && !key.EndsWith("D"))
            {
                ParseImageKey(key, value, maniaConfig.KeyImages, "KeyImage");
            }

            // Key pressed images
            else if (key.StartsWith("KeyImage") && key.EndsWith("D"))
            {
                ParseImageKey(key, value, maniaConfig.KeyPressedImages, "KeyImage", stripSuffix: "D");
            }
        }

        private static void ParseImageKey(string key, string value, Dictionary<int, string> dict, string prefix, string stripSuffix = "")
        {
            string indexStr = key[prefix.Length..];

            if (!string.IsNullOrEmpty(stripSuffix) && indexStr.EndsWith(stripSuffix))
                indexStr = indexStr[..^stripSuffix.Length];

            if (int.TryParse(indexStr, out int index))
                dict[index] = value;
        }
    }
}
