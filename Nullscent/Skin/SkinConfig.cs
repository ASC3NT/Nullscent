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
        public bool HasSkinIni { get; set; }
        public string? ParseError { get; set; }
        public string Name { get; set; } = "Unknown";
        public string Author { get; set; } = string.Empty;
        public string ComboPrefix { get; set; } = "score";
        public float ComboOverlap { get; set; }
        // A folder without skin.ini uses the newest legacy rules. An ini without
        // Version uses 1.0, as specified by the osu! skinning contract.
        public string Version { get; set; } = "latest";
        public bool IsVersionAtLeast(double minimum) => Version.Equals("latest", StringComparison.OrdinalIgnoreCase)
            || (double.TryParse(Version, NumberStyles.Float, CultureInfo.InvariantCulture, out double version) && version >= minimum);
        public int AnimationFramerate { get; set; } = -1; // -1 significa auto-calcular o fallback
        public List<SkinConfigProperty> Properties { get; } = new();
        public List<SkinDiagnostic> Diagnostics { get; } = new();

        /// <summary>
        /// Configuraciones específicas de mania por key count.
        /// Dictionary: KeyCount → ManiaKeyConfig
        /// </summary>
        public Dictionary<int, ManiaKeyConfig> ManiaConfigs { get; set; } = new();
        public Dictionary<int, (byte R, byte G, byte B, byte A)> GlobalComboColors { get; } = new();

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

    public sealed record SkinConfigProperty(string Section, string Key, string Value, int Line,
        int? ManiaKeyCount = null);

    /// <summary>
    /// Configuración específica de mania para un key count.
    /// Basado directamente en la especificación de osu!mania skinning.
    /// </summary>
    public class ManiaKeyConfig
    {
        public float[]? ColumnWidths { get; set; }
        public float[]? ColumnSpacing { get; set; }
        public float[]? ColumnLineWidths { get; set; }
        public bool[]? ColumnLines { get; set; }
        public float[]? LightingNWidths { get; set; }
        public float[]? LightingLWidths { get; set; }
        public float? ColumnStart { get; set; }
        public float? ColumnRight { get; set; }
        public float? BarlineHeight { get; set; }
        public float? WidthForNoteHeightScale { get; set; }
        public (byte R, byte G, byte B, byte A)[]? ColumnColors { get; set; } // Obsoleto, usar ColumnColorsMap
        public int? HitPosition { get; set; }
        public int? LightPosition { get; set; }
        public int LightFramePerSecond { get; set; } = 60;
        public bool ColumnLighting { get; set; } = true;
        public bool JudgementLine { get; set; } = true;
        public int SpecialStyle { get; set; } = 0;
        public bool? UpsideDown { get; set; }
        public bool KeysUnderNotes { get; set; } = false;
        public int? ScorePosition { get; set; }
        public int? ComboPosition { get; set; }
        public int NoteBodyStyle { get; set; } = 1;
        public Dictionary<int, int> NoteBodyStyles { get; set; } = new();
        public bool? NoteFlipWhenUpsideDown { get; set; }
        public bool? KeyFlipWhenUpsideDown { get; set; }
        public Dictionary<string, bool> NoteFlips { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> KeyFlips { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // Colores por columna: ColourX y ColourLightX (0-indexed para ColumnIndex)
        public Dictionary<int, (byte R, byte G, byte B, byte A)> ColumnColorsMap { get; set; } = new();
        public Dictionary<int, (byte R, byte G, byte B, byte A)> ColourLights { get; set; } = new();

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
        public Dictionary<string, string> HitImages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parser robusto de skin.ini (formato osu!stable).
    /// </summary>
    public static class SkinConfigParser
    {
        private class ParsedSection
        {
            public string Name { get; set; } = string.Empty;
            public List<SkinConfigProperty> KeyValues { get; } = new();
        }

        public static SkinConfig Parse(string skinDirectory)
        {
            string iniPath = Path.Combine(skinDirectory, "skin.ini");
            var config = new SkinConfig { SkinDirectory = skinDirectory };

            if (!File.Exists(iniPath))
            {
                Console.WriteLine($"[SkinConfigParser] No skin.ini found in {skinDirectory}, using defaults");
                return config;
            }

            config.HasSkinIni = true;
            config.Version = "1.0";
            // osu!stable treats its reserved User skin folder as latest even if it
            // contains a skin.ini with an older Version value.
            bool isUserSkin = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(skinDirectory)))
                .Equals("User", StringComparison.OrdinalIgnoreCase);
            if (isUserSkin)
            {
                config.Version = "latest";
                config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                    SkinDiagnosticSeverity.Information,
                    "The reserved User skin folder always uses the latest skin rules.",
                    Property: "Version"));
            }

            try
            {
                var lines = File.ReadAllLines(iniPath);
                var sections = new List<ParsedSection>();
                ParsedSection? currentSection = null;

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string rawLine = lines[lineIndex];
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
                        string sectionName = line[1..^1].Trim();
                        if (sectionName.Length == 0)
                        {
                            config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.InvalidConfiguration,
                                SkinDiagnosticSeverity.Warning, $"Line {lineIndex + 1}: section name is empty.", Line: lineIndex + 1));
                            currentSection = null;
                            continue;
                        }
                        currentSection = new ParsedSection { Name = sectionName };
                        sections.Add(currentSection);
                        continue;
                    }

                    // Parsear par clave-valor
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2 && currentSection != null && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        if (string.IsNullOrWhiteSpace(parts[1]))
                        {
                            // The official Blank skin.ini template intentionally
                            // leaves most values empty. Treat these as no-op
                            // placeholders so the format default remains active.
                            config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                                SkinDiagnosticSeverity.Information,
                                $"Line {lineIndex + 1}: empty template placeholder ignored; the property's default remains active.",
                                currentSection.Name, parts[0].Trim(), lineIndex + 1));
                            continue;
                        }

                        var property = new SkinConfigProperty(currentSection.Name,
                            parts[0].Trim(), parts[1].Trim(), lineIndex + 1);
                        currentSection.KeyValues.Add(property);
                        config.Properties.Add(property);
                    }
                    else if (currentSection?.Name == "Mania"
                        && line.Equals("KeyFlipWhenUpsideDown", StringComparison.Ordinal))
                    {
                        // The published Blank template contains this one bare
                        // placeholder without a colon. Like its empty key:value
                        // entries, it is a no-op and leaves the default active.
                        config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                            SkinDiagnosticSeverity.Information,
                            $"Line {lineIndex + 1}: empty template placeholder ignored; the property's default remains active.",
                            currentSection.Name, line, lineIndex + 1));
                    }
                    else
                    {
                        config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.MalformedProperty,
                            SkinDiagnosticSeverity.Warning,
                            $"Line {lineIndex + 1}: expected a non-empty key:value entry inside a section.",
                            currentSection?.Name, Line: lineIndex + 1));
                    }
                }

                // Procesar cada sección recolectada
                foreach (var section in sections)
                {
                    if (section.Name == "General")
                    {
                        foreach (SkinConfigProperty kvp in section.KeyValues)
                        {
                            ParseGeneralSection(kvp, config, isUserSkin);
                        }
                    }
                    else if (section.Name == "Fonts")
                    {
                        foreach (SkinConfigProperty kvp in section.KeyValues)
                        {
                            if (kvp.Key == "ComboPrefix")
                                config.ComboPrefix = kvp.Value;
                            else if (kvp.Key == "ComboOverlap"
                                && TryParseFiniteFloat(kvp.Value, out float overlap))
                                config.ComboOverlap = overlap;
                            else if (kvp.Key == "ComboOverlap")
                                ReportMalformed(config, kvp, "ComboOverlap must be a finite number.");
                        }
                    }
                    else if (section.Name == "Colours")
                    {
                        foreach (SkinConfigProperty property in section.KeyValues)
                        {
                            if (!property.Key.StartsWith("Combo", StringComparison.Ordinal)
                                || !int.TryParse(property.Key[5..], NumberStyles.None,
                                    CultureInfo.InvariantCulture, out int comboIndex)
                                || comboIndex is < 1 or > 8)
                                continue;

                            var colour = ParseColor(config, property, allowAlpha: false);
                            if (colour.HasValue)
                                config.GlobalComboColors[comboIndex] = colour.Value;
                        }
                    }
                    else if (section.Name == "Mania")
                    {
                        // 1. Determinar keyCount. Primero ver si está la clave 'Keys' en la sección.
                        int? keyCount = null;
                        SkinConfigProperty? keysKvp = section.KeyValues.FirstOrDefault(k => k.Key == "Keys");
                        if (keysKvp != null && int.TryParse(keysKvp.Value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int parsedKeys) && parsedKeys is >= 1 and <= 18)
                        {
                            keyCount = parsedKeys;
                        }
                        else if (keysKvp != null)
                        {
                            ReportMalformed(config, keysKvp, "Keys must be an integer from 1 to 18.");
                        }

                        // 2. Obtener o crear configuración correspondiente
                        if (!keyCount.HasValue)
                            continue;
                        if (!config.ManiaConfigs.TryGetValue(keyCount.Value, out var maniaConfig))
                        {
                            maniaConfig = new ManiaKeyConfig();
                            config.ManiaConfigs[keyCount.Value] = maniaConfig;
                        }

                        // 3. Parsear todas las claves de esta sección
                        foreach (SkinConfigProperty kvp in section.KeyValues)
                        {
                            SkinConfigProperty contextual = kvp with { ManiaKeyCount = keyCount.Value };
                            int propertyIndex = config.Properties.FindIndex(p => p.Line == kvp.Line);
                            if (propertyIndex >= 0) config.Properties[propertyIndex] = contextual;
                            ParseManiaSection(contextual, maniaConfig, config);
                        }
                    }
                }

                Console.WriteLine($"[SkinConfigParser] Parsed skin.ini: {config.Name} by {config.Author} (Mania configs: {config.ManiaConfigs.Count})");
                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkinConfigParser] Failed to parse skin.ini: {ex.Message}");
                config.ParseError = ex.Message;
                config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.InvalidConfiguration,
                    SkinDiagnosticSeverity.Error, $"skin.ini could not be read: {ex.Message}",
                    Property: "skin.ini"));
                return config;
            }
        }

        private static void ParseGeneralSection(SkinConfigProperty property, SkinConfig config, bool isUserSkin)
        {
            switch (property.Key)
            {
                case "Name":
                    config.Name = property.Value;
                    break;
                case "Author":
                    config.Author = property.Value;
                    break;
                case "Version":
                    if (isUserSkin) break;
                    if (property.Value.Equals("latest", StringComparison.OrdinalIgnoreCase)
                        || TryParseFiniteFloat(property.Value, out float version) && version > 0f)
                        config.Version = property.Value;
                    else
                        ReportMalformed(config, property, "Version must be a positive number or latest.");
                    break;
                case "AnimationFramerate":
                    if (int.TryParse(property.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps)
                        && (fps == -1 || fps >= 1))
                        config.AnimationFramerate = fps;
                    else
                        ReportMalformed(config, property, "AnimationFramerate must be -1 or a positive integer.");
                    break;
            }
        }

        private static void ParseManiaSection(SkinConfigProperty property, ManiaKeyConfig maniaConfig,
            SkinConfig config)
        {
            string key = property.Key;
            string value = property.Value;
            if (key == "ColumnWidth")
                maniaConfig.ColumnWidths = ParseFloatArray(config, property, allowZero: false);
            else if (key == "ColumnSpacing")
                maniaConfig.ColumnSpacing = ParseFloatArray(config, property, allowZero: true);
            else if (key == "ColumnLineWidth")
                maniaConfig.ColumnLineWidths = ParseFloatArray(config, property, allowZero: true);
            else if (key == "ColumnLine")
                maniaConfig.ColumnLines = ParseBoolArray(config, property);
            else if (key == "ColumnStart")
                maniaConfig.ColumnStart = ParseNonNegativeFloat(config, property);
            else if (key == "ColumnRight")
                maniaConfig.ColumnRight = ParseNonNegativeFloat(config, property);
            else if (key == "BarlineHeight")
                maniaConfig.BarlineHeight = ParseNonNegativeFloat(config, property);
            else if (key == "WidthForNoteHeightScale")
                maniaConfig.WidthForNoteHeightScale = ParsePositiveFloat(config, property);
            else if (key == "HitPosition")
                maniaConfig.HitPosition = ParseInteger(config, property);
            else if (key == "LightPosition")
                maniaConfig.LightPosition = ParseInteger(config, property);
            else if (key == "LightFramePerSecond")
            {
                int? fps = ParseInteger(config, property);
                if (fps is >= 1) maniaConfig.LightFramePerSecond = fps.Value;
                else if (fps.HasValue) ReportMalformed(config, property, "LightFramePerSecond must be a positive integer.");
            }
            else if (key == "LightingNWidth")
                maniaConfig.LightingNWidths = ParseFloatArray(config, property, allowZero: true);
            else if (key == "LightingLWidth")
                maniaConfig.LightingLWidths = ParseFloatArray(config, property, allowZero: true);
            else if (key == "ScorePosition")
                maniaConfig.ScorePosition = ParseInteger(config, property);
            else if (key == "ComboPosition")
                maniaConfig.ComboPosition = ParseInteger(config, property);
            else if (key == "JudgementLine")
            {
                if (TryParseBoolean(value, out bool enabled)) maniaConfig.JudgementLine = enabled;
                else ReportMalformed(config, property, "JudgementLine accepts only 0 or 1.");
            }
            else if (key == "SpecialStyle")
            {
                if (TryParseSpecialStyle(value, out int specialStyle)) maniaConfig.SpecialStyle = specialStyle;
                else ReportMalformed(config, property, "SpecialStyle accepts 0, 1, or 2.");
            }
            else if (key == "UpsideDown")
            {
                if (TryParseBoolean(value, out bool upsideDown)) maniaConfig.UpsideDown = upsideDown;
                else ReportMalformed(config, property, "UpsideDown accepts only 0 or 1.");
            }
            else if (key == "KeysUnderNotes")
            {
                if (TryParseBoolean(value, out bool enabled)) maniaConfig.KeysUnderNotes = enabled;
                else ReportMalformed(config, property, "KeysUnderNotes accepts only 0 or 1.");
            }
            else if (key == "ColumnLighting")
            {
                if (TryParseBoolean(value, out bool enabled)) maniaConfig.ColumnLighting = enabled;
                else ReportMalformed(config, property, "ColumnLighting accepts only 0 or 1.");
            }
            else if (key == "LightingN") maniaConfig.LightingN = value;
            else if (key == "LightingL") maniaConfig.LightingL = value;
            else if (key == "StageLeft") maniaConfig.StageLeft = value;
            else if (key == "StageRight") maniaConfig.StageRight = value;
            else if (key == "StageBottom") maniaConfig.StageBottom = value;
            else if (key == "StageHint") maniaConfig.StageHint = value;
            else if (key == "StageLight") maniaConfig.StageLight = value;
            else if (key is "Hit0" or "Hit50" or "Hit100" or "Hit200" or "Hit300" or "Hit300g")
                maniaConfig.HitImages[key] = value;
            else if (key == "ColourBarline")
            {
                var color = ParseColor(config, property, allowAlpha: true);
                if (color.HasValue) maniaConfig.ColourBarline = color.Value;
            }
            else if (key == "ColourJudgementLine")
            {
                var color = ParseColor(config, property, allowAlpha: false);
                if (color.HasValue) maniaConfig.ColourJudgementLine = color.Value;
            }
            else if (key == "ColourColumnLine")
            {
                var color = ParseColor(config, property, allowAlpha: true);
                if (color.HasValue) maniaConfig.ColourColumnLine = color.Value;
            }
            else if (key == "ColourBreak")
            {
                var color = ParseColor(config, property, allowAlpha: false);
                if (color.HasValue) maniaConfig.ColourBreak = color.Value;
            }
            else if (key == "NoteBodyStyle" || key.StartsWith("NoteBodyStyle", StringComparison.Ordinal))
            {
                string indexString = key["NoteBodyStyle".Length..];
                if (!TryParseNoteBodyStyle(value, out int bodyStyle, out bool wasAlias))
                {
                    ReportMalformed(config, property, "NoteBodyStyle accepts 0, 1, or 2.");
                    return;
                }

                if (wasAlias)
                    config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.AdaptedProperty,
                        SkinDiagnosticSeverity.Information,
                        $"Non-standard NoteBodyStyle alias '{value}' was mapped to {bodyStyle}.",
                        property.Section, property.Key, property.Line));

                if (indexString.Length == 0)
                    maniaConfig.NoteBodyStyle = bodyStyle;
                else if (int.TryParse(indexString, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                    && index >= 0)
                    maniaConfig.NoteBodyStyles[index] = bodyStyle;
            }
            else if (key.StartsWith("NoteFlipWhenUpsideDown", StringComparison.Ordinal))
            {
                string suffix = key["NoteFlipWhenUpsideDown".Length..];
                if (!IsValidNoteFlipSuffix(suffix))
                    ReportMalformed(config, property, "The per-column note flip suffix must be a column index with an optional H, L, or T part suffix.");
                else if (!TryParseBoolean(value, out bool flip))
                    ReportMalformed(config, property, "NoteFlipWhenUpsideDown commands accept only 0 or 1.");
                else if (suffix.Length == 0) maniaConfig.NoteFlipWhenUpsideDown = flip;
                else maniaConfig.NoteFlips[suffix] = flip;
            }
            else if (key.StartsWith("KeyFlipWhenUpsideDown", StringComparison.Ordinal))
            {
                string suffix = key["KeyFlipWhenUpsideDown".Length..];
                if (!IsValidKeyFlipSuffix(suffix))
                    ReportMalformed(config, property, "The per-column key flip suffix must be a column index with an optional D state suffix.");
                else if (!TryParseBoolean(value, out bool flip))
                    ReportMalformed(config, property, "KeyFlipWhenUpsideDown commands accept only 0 or 1.");
                else if (suffix.Length == 0) maniaConfig.KeyFlipWhenUpsideDown = flip;
                else maniaConfig.KeyFlips[suffix] = flip;
            }
            else if (key.StartsWith("ColourLight", StringComparison.Ordinal))
            {
                string indexString = key["ColourLight".Length..];
                if (!int.TryParse(indexString, NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index < 1)
                {
                    ReportMalformed(config, property, "ColourLight# uses a one-based column index.");
                    return;
                }
                var color = ParseColor(config, property, allowAlpha: false);
                if (color.HasValue) maniaConfig.ColourLights[index - 1] = color.Value;
            }
            else if (key == "ColourHold")
            {
                var color = ParseColor(config, property, allowAlpha: true);
                if (color.HasValue) maniaConfig.ColourHold = color.Value;
            }
            else if (key.StartsWith("Colour", StringComparison.Ordinal))
            {
                string indexString = key["Colour".Length..];
                if (!int.TryParse(indexString, NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index < 1)
                {
                    ReportMalformed(config, property, "Colour# uses a one-based column index.");
                    return;
                }
                var color = ParseColor(config, property, allowAlpha: true);
                if (color.HasValue) maniaConfig.ColumnColorsMap[index - 1] = color.Value;
            }
            else if (key.StartsWith("NoteImage", StringComparison.Ordinal))
            {
                string suffix = string.Empty;
                string indexString = key["NoteImage".Length..];
                if (indexString.EndsWith("H", StringComparison.Ordinal)) { suffix = "H"; indexString = indexString[..^1]; }
                else if (indexString.EndsWith("L", StringComparison.Ordinal)) { suffix = "L"; indexString = indexString[..^1]; }
                else if (indexString.EndsWith("T", StringComparison.Ordinal)) { suffix = "T"; indexString = indexString[..^1]; }

                if (int.TryParse(indexString, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index >= 0)
                {
                    if (suffix == "H") maniaConfig.NoteHeadImages[index] = value;
                    else if (suffix == "L") maniaConfig.NoteBodyImages[index] = value;
                    else if (suffix == "T") maniaConfig.NoteTailImages[index] = value;
                    else maniaConfig.NoteImages[index] = value;
                }
                else ReportMalformed(config, property, "NoteImage entries require a zero-based column index and an optional H, L, or T suffix.");
            }
            else if (key.StartsWith("KeyImage", StringComparison.Ordinal))
            {
                string suffix = string.Empty;
                string indexString = key["KeyImage".Length..];
                if (indexString.EndsWith("D", StringComparison.Ordinal)) { suffix = "D"; indexString = indexString[..^1]; }

                if (int.TryParse(indexString, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index >= 0)
                {
                    if (suffix == "D") maniaConfig.KeyPressedImages[index] = value;
                    else maniaConfig.KeyImages[index] = value;
                }
                else ReportMalformed(config, property, "KeyImage entries require a zero-based column index and an optional D suffix.");
            }
        }

        private static float[] ParseFloatArray(SkinConfig config, SkinConfigProperty property, bool allowZero)
        {
            bool malformed = false;
            float[] values = property.Value.Split(',').Select(token =>
            {
                if (TryParseFiniteFloat(token.Trim(), out float value) && (allowZero ? value >= 0f : value > 0f))
                    return value;
                malformed = true;
                return float.NaN;
            }).ToArray();

            if (malformed)
                ReportMalformed(config, property, "The list contains a non-finite value or a value outside the property's range; that entry uses its format default.");
            return values;
        }

        private static bool[] ParseBoolArray(SkinConfig config, SkinConfigProperty property)
        {
            bool malformed = false;
            bool[] values = property.Value.Split(',').Select(token =>
            {
                if (TryParseBoolean(token.Trim(), out bool value)) return value;
                malformed = true;
                return true;
            }).ToArray();
            if (malformed)
                ReportMalformed(config, property, "ColumnLine accepts only comma-separated 0 or 1 values; invalid entries use the default enabled state.");
            return values;
        }

        private static int? ParseInteger(SkinConfig config, SkinConfigProperty property)
        {
            if (int.TryParse(property.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) return value;
            ReportMalformed(config, property, "The property requires an integer value.");
            return null;
        }

        private static float? ParseNonNegativeFloat(SkinConfig config, SkinConfigProperty property)
        {
            if (TryParseFiniteFloat(property.Value, out float value) && value >= 0f) return value;
            ReportMalformed(config, property, "The property requires a finite number greater than or equal to zero.");
            return null;
        }

        private static float? ParsePositiveFloat(SkinConfig config, SkinConfigProperty property)
        {
            if (TryParseFiniteFloat(property.Value, out float value) && value > 0f) return value;
            ReportMalformed(config, property, "The property requires a finite number greater than zero.");
            return null;
        }

        private static bool TryParseNoteBodyStyle(string value, out int style, out bool wasAlias)
        {
            wasAlias = false;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out style))
                return style is >= 0 and <= 2;

            // A few skins use descriptive aliases although the official contract
            // specifies numeric values. Accept them as a diagnosed adaptation.
            wasAlias = true;
            switch (value.Trim().ToLowerInvariant())
            {
                case "stretch": style = 0; return true;
                case "repeatbottom":
                case "repeat-bottom":
                case "bottom":
                case "tilefromtail":
                case "tile-from-tail": style = 1; return true;
                case "repeattop":
                case "repeat-top":
                case "top":
                case "tilefromhead":
                case "tile-from-head": style = 2; return true;
                default: style = 1; return false;
            }
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            if (value == "0") { result = false; return true; }
            if (value == "1") { result = true; return true; }
            result = false;
            return false;
        }

        private static bool TryParseSpecialStyle(string value, out int result)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                return result is >= 0 and <= 2;
            result = 0;
            return false;
        }

        private static bool IsValidNoteFlipSuffix(string suffix)
        {
            if (suffix.Length == 0) return true;
            string index = suffix.TrimEnd('H', 'L', 'T');
            string part = suffix[index.Length..];
            return int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out int column)
                && column is >= 0 and <= 17
                && (part.Length == 0 || part is "H" or "L" or "T");
        }

        private static bool IsValidKeyFlipSuffix(string suffix)
        {
            if (suffix.Length == 0) return true;
            string index = suffix.EndsWith("D", StringComparison.Ordinal) ? suffix[..^1] : suffix;
            return int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out int column)
                && column is >= 0 and <= 17;
        }

        private static bool TryParseFiniteFloat(string value, out float result) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && float.IsFinite(result);

        private static (byte R, byte G, byte B, byte A)? ParseColor(SkinConfig config,
            SkinConfigProperty property, bool allowAlpha)
        {
            string[] parts = property.Value.Split(',');
            bool hasIgnoredAlpha = !allowAlpha && parts.Length == 4;
            if (parts.Length < 3 || parts.Length > 4)
            {
                ReportMalformed(config, property, allowAlpha
                    ? "The colour requires RGB or RGBA values."
                    : "The colour requires RGB values.");
                return null;
            }

            if (!byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                || !byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                || !byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
            {
                ReportMalformed(config, property, "Colour components must be integers from 0 to 255.");
                return null;
            }

            byte alpha = 255;
            if (allowAlpha && parts.Length == 4
                && !byte.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out alpha))
            {
                ReportMalformed(config, property, "The alpha component must be an integer from 0 to 255.");
                return null;
            }
            if (hasIgnoredAlpha)
                config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                    SkinDiagnosticSeverity.Information,
                    $"The alpha component '{parts[3].Trim()}' was ignored because {property.Key} accepts RGB only.",
                    property.Section, property.Key, property.Line));
            return (r, g, b, alpha);
        }

        private static void ReportMalformed(SkinConfig config, SkinConfigProperty property, string reason) =>
            config.Diagnostics.Add(new SkinDiagnostic(SkinDiagnosticKind.MalformedProperty,
                SkinDiagnosticSeverity.Warning,
                $"Line {property.Line}: [{property.Section}] {property.Key}: {reason}",
                property.Section, property.Key, property.Line));
    }
}
