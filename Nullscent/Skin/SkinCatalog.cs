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
        public List<int> ManiaKeyModes { get; } = new();
        public List<string> Elements { get; } = new();
        public List<string> Unsupported { get; } = new();
        public List<string> ParsedButNotApplied { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public SkinConfig? Config { get; set; }
        public string Summary => Status == SkinValidity.Partial
            ? $"Skin parcial: {Warnings.Count} aviso(s), {Unsupported.Distinct().Count()} desconocida(s), {ParsedButNotApplied.Distinct().Count()} pendiente(s). {Warnings.FirstOrDefault()}"
            : Status == SkinValidity.Invalid ? $"Skin inválida: {string.Join("; ", Errors.Take(3))}" : "Skin cargada.";
    }

    public sealed class SkinCatalog
    {
        private static readonly HashSet<string> imageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        private static readonly HashSet<string> soundExtensions = new(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".ogg" };
        private static readonly HashSet<string> supportedMania = new(StringComparer.OrdinalIgnoreCase)
        {
            "Keys", "ColumnWidth", "ColumnSpacing", "ColumnLineWidth", "ColumnStart", "WidthForNoteHeightScale",
            "HitPosition", "LightPosition", "LightingNWidth", "LightingLWidth", "ScorePosition", "ComboPosition",
            "JudgementLine", "KeysUnderNotes", "ColumnLighting", "LightingN", "LightingL", "LightFramePerSecond",
            "StageLeft", "StageRight", "StageBottom", "StageHint", "StageLight",
            "ColourJudgementLine", "ColourColumnLine", "ColourBreak", "ColourHold", "NoteBodyStyle"
        };
        private static readonly HashSet<string> parsedButNotAppliedMania = new(StringComparer.OrdinalIgnoreCase)
        {
            "ColumnLine", "ColumnRight", "BarlineHeight", "SpecialStyle", "UpsideDown",
            "ColourBarline"
        };
        private readonly List<SkinValidation> entries = new();
        public string Root { get; }
        public IReadOnlyList<SkinValidation> Entries => entries;
        public event Action? CatalogChanged;

        public SkinCatalog(string root) => Root = Path.GetFullPath(root);

        public void Refresh()
        {
            entries.Clear();
            try
            {
                if (System.IO.Directory.Exists(Root))
                {
                    foreach (string path in System.IO.Directory.GetDirectories(Root).Where(p => !Path.GetFileName(p).StartsWith(".import-", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                        entries.Add(Validate(path));
                }
            }
            catch (Exception ex) { Console.WriteLine($"[SkinCatalog] No se pudo leer la biblioteca: {ex.Message}"); }
            CatalogChanged?.Invoke();
        }

        public SkinValidation Validate(string directory)
        {
            var result = new SkinValidation { Directory = Path.GetFullPath(directory) };
            if (!System.IO.Directory.Exists(directory))
            {
                result.Errors.Add("La carpeta no existe.");
                return result;
            }

            try
            {
                var files = System.IO.Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                var images = files.Where(f => imageExtensions.Contains(Path.GetExtension(f))).ToArray();
                var sounds = files.Where(f => soundExtensions.Contains(Path.GetExtension(f))).ToArray();
                bool recognizable = images.Any(f => IsRecognized(Path.GetFileNameWithoutExtension(f)))
                    || sounds.Any(f => IsRecognized(Path.GetFileNameWithoutExtension(f)));
                string ini = Path.Combine(directory, "skin.ini");
                result.HasSkinIni = File.Exists(ini);
                if (files.Length == 0)
                {
                    result.Errors.Add("La carpeta está vacía.");
                    return result;
                }

                if (images.Length > 0) result.Elements.Add($"{images.Length} imágenes");
                if (sounds.Length > 0) result.Elements.Add($"{sounds.Length} sonidos");
                if (result.HasSkinIni)
                {
                    var lines = File.ReadAllLines(ini);
                    string section = "";
                    foreach (string raw in lines)
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("//") || line.StartsWith(";") || line.StartsWith("#") || line.Length == 0) continue;
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            section = line[1..^1].Trim();
                            continue;
                        }
                        var parts = line.Split(':', 2);
                        if (parts.Length != 2) continue;
                        string key = parts[0].Trim();
                        string value = parts[1].Split(';')[0].Trim();
                        if (section.Equals("Mania", StringComparison.OrdinalIgnoreCase))
                        {
                            if (key.Equals("Keys", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(value, out int parsed) && parsed >= 1 && parsed <= 18)
                                {
                                    if (!result.ManiaKeyModes.Contains(parsed)) result.ManiaKeyModes.Add(parsed);
                                }
                                else result.Warnings.Add($"Keys inválido: {value}");
                            }
                            else if (IsParsedButNotApplied(key))
                                result.ParsedButNotApplied.Add(key);
                            else if (!IsApplied(key))
                                result.Unsupported.Add(key);
                        }
                    }
                    result.Config = SkinConfigParser.Parse(directory);
                    result.SkinIniParsed = result.Config.ParseError == null;
                    if (!result.SkinIniParsed) result.Errors.Add(result.Config.ParseError!);
                    if (result.ManiaKeyModes.Count == 0) result.Warnings.Add("Sin sección [Mania] con Keys reconocible.");
                    foreach (var config in result.Config.ManiaConfigs)
                    foreach (string name in ReferencedImages(config.Value))
                    {
                        if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!HasImage(directory, name)) result.Warnings.Add($"{config.Key}K: falta {name}");
                    }
                }
                else result.Warnings.Add("No hay skin.ini; solo se reconocerán recursos por nombre convencional.");

                if ((images.Length == 0 && sounds.Length == 0) || (!result.HasSkinIni && !recognizable))
                {
                    result.Errors.Add("No hay recursos de skin reconocibles.");
                    return result;
                }
                if (result.Errors.Count > 0) return result;
                if (images.Length == 0) result.Warnings.Add("No hay imágenes; gameplay usa fallback interno.");
                if (result.ManiaKeyModes.Count > 0 && !images.Any(f => Path.GetFileNameWithoutExtension(f).StartsWith("mania-", StringComparison.OrdinalIgnoreCase))
                    && result.Config?.ManiaConfigs.Values.Any(c => c.NoteImages.Count > 0 || c.KeyImages.Count > 0) != true)
                    result.Warnings.Add("Sin notas ni receptores mania reconocibles; gameplay usa fallback interno.");
                if (result.Unsupported.Count > 0) result.Warnings.Add($"Propiedades mania no soportadas: {string.Join(", ", result.Unsupported.Distinct().Take(5))}");
                if (result.ParsedButNotApplied.Count > 0)
                    result.Warnings.Add($"Propiedades mania leídas pero aún no aplicadas: {string.Join(", ", result.ParsedButNotApplied.Distinct().Take(5))}");
                result.Status = result.HasSkinIni && result.ManiaKeyModes.Count > 0 && images.Length > 0 && result.Warnings.Count == 0
                    ? SkinValidity.Valid : SkinValidity.Partial;
            }
            catch (Exception ex) { result.Errors.Add(ex.Message); }
            return result;
        }

        private static bool Matches(string key, string pattern) =>
            Regex.IsMatch(key, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static bool IsParsedButNotApplied(string key) => parsedButNotAppliedMania.Contains(key)
            || Matches(key, @"^ColourHold\d+$");

        private static bool IsApplied(string key) => supportedMania.Contains(key)
            || key.Equals("NoteFlipWhenUpsideDown", StringComparison.OrdinalIgnoreCase)
            || key.Equals("KeyFlipWhenUpsideDown", StringComparison.OrdinalIgnoreCase)
            || Matches(key, @"^NoteImage\d+[HLT]?$")
            || Matches(key, @"^KeyImage\d+D?$")
            || Matches(key, @"^Hit(?:0|50|100|200|300|300g)$")
            || Matches(key, @"^Colour(?:\d+|Light\d+)$")
            || Matches(key, @"^NoteBodyStyle\d+$")
            || Matches(key, @"^NoteFlipWhenUpsideDown\d+[HLT]?$")
            || Matches(key, @"^KeyFlipWhenUpsideDown\d+D?$");

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

        private static bool HasImage(string root, string name)
        {
            if (Path.IsPathRooted(name)) return false;
            string normalized = name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string ext = Path.GetExtension(normalized);
            string stem = imageExtensions.Contains(ext) ? normalized[..^ext.Length] : normalized;
            if (stem.EndsWith("@2x", StringComparison.OrdinalIgnoreCase)) stem = stem[..^3];
            foreach (string suffix in new[] { "", "@2x" })
            foreach (string extension in imageExtensions.Contains(ext) ? new[] { ext } : new[] { ".png", ".jpg", ".jpeg" })
            {
                string path = Path.GetFullPath(Path.Combine(root, stem + suffix + extension));
                if (path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(path)) return true;
            }
            return false;
        }
    }
}
