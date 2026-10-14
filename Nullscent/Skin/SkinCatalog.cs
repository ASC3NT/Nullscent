#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nullscent.Skin
{
    public enum SkinValidity { Valid, Partial, Invalid }

    public sealed class SkinValidation
    {
        public string Directory { get; init; } = string.Empty;
        public SkinValidity Status { get; set; } = SkinValidity.Invalid;
        public bool HasSkinIni { get; set; }
        public bool SkinIniParsed { get; set; }
        public bool IsNative { get; set; }
        public List<int> ManiaKeyModes { get; } = new();
        public List<string> Elements { get; } = new();
        public List<string> Unsupported { get; } = new();
        public List<string> ParsedButNotApplied { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public SkinConfig? Config { get; set; }
        public ManiaSkinDefinition? Definition { get; set; }
        public string Summary => Status == SkinValidity.Partial
            ? $"Skin parcial: {Warnings.Count} aviso(s), {Unsupported.Distinct().Count()} sin soporte, {ParsedButNotApplied.Distinct().Count()} sin aplicar. {Warnings.FirstOrDefault()}"
            : Status == SkinValidity.Invalid ? $"Skin inválida: {string.Join("; ", Errors.Take(3))}" : "Skin cargada.";
    }

    public sealed class SkinCatalog
    {
        private static readonly HashSet<string> imageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        private static readonly HashSet<string> soundExtensions = new(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".ogg" };
        private static readonly HashSet<string> parsedButNotAppliedMania = new(StringComparer.OrdinalIgnoreCase)
        {
            "SpecialStyle", "BarlineHeight", "ColourBarline"
        };
        private static readonly HashSet<string> supportedMania = new(StringComparer.OrdinalIgnoreCase)
        {
            "Keys", "ColumnWidth", "ColumnSpacing", "ColumnLineWidth", "ColumnLine", "ColumnStart", "ColumnRight",
            "WidthForNoteHeightScale", "HitPosition", "LightPosition", "LightingNWidth", "LightingLWidth",
            "ScorePosition", "ComboPosition", "JudgementLine", "KeysUnderNotes", "UpsideDown", "ColumnLighting",
            "LightingN", "LightingL", "LightFramePerSecond", "StageLeft", "StageRight", "StageBottom", "StageHint",
            "StageLight", "ColourJudgementLine", "ColourColumnLine", "ColourBreak", "ColourHold", "NoteBodyStyle"
        };

        private readonly List<SkinValidation> entries = new();
        public string Root { get; }
        public string? BuiltInRoot { get; }
        public LegacySkinCompatibilityMode CompatibilityMode { get; private set; }
        public IReadOnlyList<SkinValidation> Entries => entries;
        public event Action? CatalogChanged;

        public SkinCatalog(string root, string? builtInRoot = null,
            LegacySkinCompatibilityMode compatibilityMode = LegacySkinCompatibilityMode.Stable)
        {
            Root = Path.GetFullPath(root);
            BuiltInRoot = string.IsNullOrWhiteSpace(builtInRoot) ? null : Path.GetFullPath(builtInRoot);
            CompatibilityMode = compatibilityMode;
        }

        public void SetCompatibilityMode(LegacySkinCompatibilityMode compatibilityMode)
        {
            if (CompatibilityMode == compatibilityMode) return;
            CompatibilityMode = compatibilityMode;
            Refresh();
        }

        public void Refresh()
        {
            entries.Clear();
            try
            {
                AddDirectories(Root, requireNativeManifest: false);
                if (BuiltInRoot != null)
                    AddDirectories(BuiltInRoot, requireNativeManifest: true);
            }
            catch (Exception ex) { Console.WriteLine($"[SkinCatalog] No se pudo leer la biblioteca: {ex.Message}"); }
            CatalogChanged?.Invoke();
        }

        private void AddDirectories(string root, bool requireNativeManifest)
        {
            if (!System.IO.Directory.Exists(root)) return;
            foreach (string path in System.IO.Directory.GetDirectories(root)
                         .Where(p => !Path.GetFileName(p).StartsWith(".import-", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (requireNativeManifest && !File.Exists(Path.Combine(path, NativeSkinAdapter.ManifestFileName))) continue;
                string fullPath = Path.GetFullPath(path);
                if (!entries.Any(e => string.Equals(e.Directory, fullPath, StringComparison.OrdinalIgnoreCase)))
                    entries.Add(Validate(fullPath));
            }
        }

        public SkinValidation Validate(string directory, LegacySkinCompatibilityMode? compatibilityMode = null)
        {
            var result = new SkinValidation { Directory = Path.GetFullPath(directory) };
            if (!System.IO.Directory.Exists(directory))
            {
                result.Errors.Add("La carpeta no existe.");
                return result;
            }

            try
            {
                string root = Path.GetFullPath(directory);
                string[] files = System.IO.Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                string[] images = files.Where(f => imageExtensions.Contains(Path.GetExtension(f))).ToArray();
                string[] sounds = files.Where(f => soundExtensions.Contains(Path.GetExtension(f))).ToArray();
                string iniPath = Path.Combine(root, "skin.ini");
                result.HasSkinIni = File.Exists(iniPath);
                result.IsNative = File.Exists(Path.Combine(root, NativeSkinAdapter.ManifestFileName));

                if (files.Length == 0)
                {
                    result.Errors.Add("La carpeta está vacía.");
                    return result;
                }
                if (images.Length > 0) result.Elements.Add($"{images.Length} imágenes");
                if (sounds.Length > 0) result.Elements.Add($"{sounds.Length} sonidos");

                if (result.IsNative)
                {
                    result.Definition = NativeSkinAdapter.Load(root);
                    result.ManiaKeyModes.AddRange(result.Definition.Keymodes.Keys.OrderBy(k => k));
                    foreach (string name in ReferencedImages(result.Definition))
                    {
                        if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!HasImage(root, name)) result.Warnings.Add($"Falta asset: {name}");
                    }
                }
                else
                {
                    result.Config = SkinConfigParser.Parse(root);
                    result.SkinIniParsed = result.Config.ParseError == null;
                    if (!result.SkinIniParsed)
                    {
                        result.Errors.Add(result.Config.ParseError!);
                        return result;
                    }
                    LegacySkinCompatibilityMode selectedCompatibilityMode = compatibilityMode ?? CompatibilityMode;
                    result.Definition = LegacyManiaSkinAdapter.Decode(result.Config, result.Directory,
                        selectedCompatibilityMode);

                    if (result.HasSkinIni)
                    {
                        ReadLegacyClassification(iniPath, result, result.Config, selectedCompatibilityMode);
                        if (result.ManiaKeyModes.Count == 0)
                            result.ManiaKeyModes.AddRange(result.Config.ManiaConfigs.Keys.OrderBy(k => k));
                        if (result.ManiaKeyModes.Count == 0)
                            result.Warnings.Add("No hay sección [Mania] con Keys; se usarán recursos convencionales y geometría por defecto.");

                        foreach (var mode in result.Config.ManiaConfigs)
                        foreach (string name in ReferencedImages(mode.Value))
                        {
                            if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase)) continue;
                            if (!HasImage(root, name)) result.Warnings.Add($"{mode.Key}K: falta {name}");
                        }
                    }
                    else
                    {
                        bool conventionalImage = images.Any(f => IsRecognized(Path.GetFileNameWithoutExtension(f)));
                        if (conventionalImage) result.ManiaKeyModes.AddRange(new[] { 4, 7 });
                        result.Warnings.Add("No hay skin.ini; se aplican reglas latest y nombres convencionales.");
                    }
                }

                if (images.Length == 0 && sounds.Length == 0)
                    result.Errors.Add("No hay recursos reconocibles.");
                else if (images.Length == 0)
                    result.Warnings.Add("No hay imágenes; se usarán assets visuales internos.");

                if (result.ManiaKeyModes.Count > 0 && result.Definition?.Provider == SkinProviderKind.OsuLegacy
                    && !images.Any(f => Path.GetFileNameWithoutExtension(f).StartsWith("mania-", StringComparison.OrdinalIgnoreCase))
                    && result.Config?.ManiaConfigs.Values.Any(c => c.NoteImages.Count > 0 || c.KeyImages.Count > 0) != true)
                    result.Warnings.Add("No se detectaron notas ni receptores legacy; se usarán assets visuales internos.");

                if (result.Unsupported.Count > 0)
                    result.Warnings.Add($"Propiedades mania no soportadas: {string.Join(", ", result.Unsupported.Distinct().Take(5))}");
                if (result.ParsedButNotApplied.Count > 0)
                    result.Warnings.Add($"Propiedades mania ignoradas por diseño: {string.Join(", ", result.ParsedButNotApplied.Distinct().Take(5))}");

                if (result.Errors.Count > 0) return result;
                result.Status = result.ManiaKeyModes.Count > 0 && images.Length > 0 && result.Warnings.Count == 0
                    ? SkinValidity.Valid : SkinValidity.Partial;
            }
            catch (Exception ex) { result.Errors.Add(ex.Message); }
            return result;
        }

        private static void ReadLegacyClassification(string iniPath, SkinValidation result,
            SkinConfig config, LegacySkinCompatibilityMode compatibilityMode)
        {
            string section = string.Empty;
            foreach (string raw in File.ReadAllLines(iniPath))
            {
                string line = raw.Trim();
                if (line.StartsWith("//") || line.StartsWith(";") || line.StartsWith("#") || line.Length == 0) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line[1..^1].Trim();
                    continue;
                }
                string[] parts = line.Split(':', 2);
                if (parts.Length != 2 || !section.Equals("Mania", StringComparison.OrdinalIgnoreCase)) continue;
                string key = parts[0].Trim();
                string value = parts[1].Split(';')[0].Trim();
                if (key.Equals("Keys", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out int parsed) && parsed is >= 1 and <= 18)
                    {
                        if (!result.ManiaKeyModes.Contains(parsed)) result.ManiaKeyModes.Add(parsed);
                    }
                    else result.Warnings.Add($"Keys inválido: {value}");
                }
                else if (IsFlipProperty(key)
                    && (compatibilityMode != LegacySkinCompatibilityMode.Stable || !config.IsVersionAtLeast(2.5)))
                    result.ParsedButNotApplied.Add(key);
                else if (parsedButNotAppliedMania.Contains(key))
                    result.ParsedButNotApplied.Add(key);
                else if (!IsApplied(key)) result.Unsupported.Add(key);
            }
        }

        private static bool Matches(string key, string pattern) => Regex.IsMatch(key, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static bool IsFlipProperty(string key) => key.Equals("NoteFlipWhenUpsideDown", StringComparison.OrdinalIgnoreCase)
            || key.Equals("KeyFlipWhenUpsideDown", StringComparison.OrdinalIgnoreCase)
            || Matches(key, @"^(?:NoteFlipWhenUpsideDown\d+[HLT]?|KeyFlipWhenUpsideDown\d+D?)$");
        private static bool IsApplied(string key) => supportedMania.Contains(key)
            || Matches(key, @"^NoteImage\d+[HLT]?$")
            || Matches(key, @"^KeyImage\d+D?$")
            || Matches(key, @"^Hit(?:0|50|100|200|300|300g)$")
            || Matches(key, @"^Colour(?:\d+|Light\d+)$")
            || Matches(key, @"^NoteBodyStyle\d+$")
            || (key.Equals("NoteFlipWhenUpsideDown", StringComparison.OrdinalIgnoreCase)
                || key.Equals("KeyFlipWhenUpsideDown", StringComparison.OrdinalIgnoreCase)
                || Matches(key, @"^(?:NoteFlipWhenUpsideDown\d+[HLT]?|KeyFlipWhenUpsideDown\d+D?)$"));

        private static bool IsRecognized(string name)
        {
            string n = name.Replace("@2x", "", StringComparison.OrdinalIgnoreCase);
            return n.StartsWith("mania-", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("cursor", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("score", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("hit", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("normal-", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("soft-", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("drum-", StringComparison.OrdinalIgnoreCase);
        }

        internal static IEnumerable<string> ReferencedImages(ManiaKeyConfig config)
        {
            foreach (var name in config.NoteImages.Values) yield return name;
            foreach (var name in config.NoteHeadImages.Values) yield return name;
            foreach (var name in config.NoteBodyImages.Values) yield return name;
            foreach (var name in config.NoteTailImages.Values) yield return name;
            foreach (var name in config.KeyImages.Values) yield return name;
            foreach (var name in config.KeyPressedImages.Values) yield return name;
            foreach (var name in config.HitImages.Values) yield return name;
            foreach (string? name in new[] { config.StageLeft, config.StageRight, config.StageBottom, config.StageHint, config.StageLight, config.LightingN, config.LightingL })
                if (!string.IsNullOrWhiteSpace(name)) yield return name;
        }

        internal static IEnumerable<string> ReferencedImages(ManiaSkinDefinition definition)
        {
            foreach (var mode in definition.Keymodes.Values)
            {
                foreach (var style in new[] { mode.NoteStyle, mode.ReceptorStyle, mode.LongHeadStyle, mode.LongBodyStyle, mode.LongTailStyle })
                {
                    if (!string.IsNullOrWhiteSpace(style.Asset)) yield return style.Asset;
                    if (!string.IsNullOrWhiteSpace(style.PressedAsset)) yield return style.PressedAsset;
                }
                foreach (var lane in mode.Lanes)
                    foreach (string? asset in new[] { lane.NoteAsset, lane.ReceptorAsset, lane.PressedReceptorAsset, lane.LongHeadAsset, lane.LongBodyAsset, lane.LongTailAsset })
                        if (!string.IsNullOrWhiteSpace(asset)) yield return asset;
                foreach (string? asset in mode.StageAssets.Values)
                    if (!string.IsNullOrWhiteSpace(asset)) yield return asset;
                foreach (string asset in mode.JudgementAssets.Values) yield return asset;
                if (!string.IsNullOrWhiteSpace(mode.LightingNoteAsset)) yield return mode.LightingNoteAsset;
                if (!string.IsNullOrWhiteSpace(mode.LightingHoldAsset)) yield return mode.LightingHoldAsset;
            }
            foreach (string asset in definition.SharedAssets.Values) yield return asset;
        }

        private static bool HasImage(string root, string name)
        {
            if (Path.IsPathRooted(name)) return false;
            string normalized = name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string extension = Path.GetExtension(normalized);
            string stem = imageExtensions.Contains(extension) ? normalized[..^extension.Length] : normalized;
            if (stem.EndsWith("@2x", StringComparison.OrdinalIgnoreCase)) stem = stem[..^3];
            foreach (string suffix in new[] { "", "@2x" })
            foreach (string ext in imageExtensions.Contains(extension) ? new[] { extension } : new[] { ".png", ".jpg", ".jpeg" })
            {
                string path = Path.GetFullPath(Path.Combine(root, stem + suffix + ext));
                if (path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(path)) return true;
            }
            return false;
        }
    }
}
