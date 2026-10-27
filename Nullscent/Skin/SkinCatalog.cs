#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        public List<SkinDiagnostic> Diagnostics { get; } = new();
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
        private static readonly HashSet<string> ignoredMania = new(StringComparer.Ordinal)
        {
            // These values are parsed for diagnostics and normalized, but this
            // client has no barline renderer or legacy-combo-burst consumer yet.
            "BarlineHeight", "ColourBarline"
        };
        private static readonly HashSet<string> supportedMania = new(StringComparer.Ordinal)
        {
            "Keys", "ColumnWidth", "ColumnSpacing", "ColumnLineWidth", "ColumnLine", "ColumnStart", "ColumnRight",
            "WidthForNoteHeightScale", "HitPosition", "LightPosition", "LightingNWidth", "LightingLWidth",
            "ScorePosition", "ComboPosition", "JudgementLine", "KeysUnderNotes", "UpsideDown", "ColumnLighting",
            "LightingN", "LightingL", "LightFramePerSecond", "StageLeft", "StageRight", "StageBottom", "StageHint",
            "StageLight", "SpecialStyle", "ColourJudgementLine", "ColourColumnLine", "ColourBreak", "ColourHold", "NoteBodyStyle"
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
                AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.InvalidConfiguration,
                    SkinDiagnosticSeverity.Error, "The skin directory does not exist."));
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
                    AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.InvalidConfiguration,
                        SkinDiagnosticSeverity.Error, "The skin directory is empty."));
                    return result;
                }
                if (images.Length > 0) result.Elements.Add($"{images.Length} imágenes");
                if (sounds.Length > 0) result.Elements.Add($"{sounds.Length} sonidos");

                if (result.IsNative)
                {
                    result.Definition = NativeSkinAdapter.Load(root);
                    result.ManiaKeyModes.AddRange(result.Definition.Keymodes.Keys.OrderBy(k => k));
                    foreach (string name in ReferencedImages(result.Definition).Distinct(StringComparer.OrdinalIgnoreCase))
                        AddAssetDiagnostic(result, root, name);
                }
                else
                {
                    result.Config = SkinConfigParser.Parse(root);
                    result.SkinIniParsed = result.Config.ParseError == null;
                    result.Diagnostics.AddRange(result.Config.Diagnostics);
                    foreach (SkinDiagnostic diagnostic in result.Config.Diagnostics.Where(d => d.Severity != SkinDiagnosticSeverity.Information))
                        if (!result.Warnings.Contains(diagnostic.Message, StringComparer.OrdinalIgnoreCase))
                            result.Warnings.Add(diagnostic.Message);
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
                        ReadLegacyClassification(result, result.Config, selectedCompatibilityMode);
                        if (result.ManiaKeyModes.Count == 0)
                            result.Warnings.Add("No hay sección [Mania] con Keys; se usará un perfil interno coherente.");

                        foreach (int keyCount in result.ManiaKeyModes)
                            if (result.Definition.Keymodes.TryGetValue(keyCount, out ManiaKeymodeDefinition? mode))
                                foreach (string name in ReferencedImages(mode).Distinct(StringComparer.OrdinalIgnoreCase)) AddAssetDiagnostic(result, root, name);
                    }
                    else
                    {
                        bool conventionalImage = images.Any(f => IsRecognized(Path.GetFileNameWithoutExtension(f)));
                        if (conventionalImage) result.ManiaKeyModes.AddRange(new[] { 4, 7 });
                        result.Warnings.Add("No hay skin.ini; se aplican reglas latest y nombres convencionales.");
                        foreach (int keyCount in result.ManiaKeyModes)
                            if (result.Definition.Keymodes.TryGetValue(keyCount, out ManiaKeymodeDefinition? mode))
                                foreach (string name in ReferencedImages(mode).Distinct(StringComparer.OrdinalIgnoreCase)) AddAssetDiagnostic(result, root, name);
                    }
                }

                if (images.Length == 0)
                    result.Warnings.Add("No hay imágenes; se usarán assets visuales internos.");

                if (result.ManiaKeyModes.Count > 0 && result.Definition?.Provider == SkinProviderKind.OsuLegacy
                    && !images.Any(f => Path.GetFileNameWithoutExtension(f).StartsWith("mania-", StringComparison.OrdinalIgnoreCase))
                    && result.Config?.ManiaConfigs.Values.Any(c => c.NoteImages.Count > 0 || c.KeyImages.Count > 0) != true)
                    result.Warnings.Add("No se detectaron notas ni receptores legacy; se usarán assets visuales internos.");

                if (result.Unsupported.Count > 0)
                    result.Warnings.Add($"Propiedades fuera del subconjunto: {string.Join(", ", result.Unsupported.Distinct().Take(5))}");
                if (result.ParsedButNotApplied.Count > 0)
                    result.Warnings.Add($"Propiedades mania ignoradas por diseño: {string.Join(", ", result.ParsedButNotApplied.Distinct().Take(5))}");

                if (result.Errors.Count > 0) return result;
                result.Status = result.ManiaKeyModes.Count > 0 && images.Length > 0 && result.Warnings.Count == 0
                    ? SkinValidity.Valid : SkinValidity.Partial;
            }
            catch (Exception ex)
            {
                result.Errors.Add(ex.Message);
                AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.InvalidConfiguration,
                    SkinDiagnosticSeverity.Error, ex.Message));
            }
            return result;
        }

        private static void ReadLegacyClassification(SkinValidation result,
            SkinConfig config, LegacySkinCompatibilityMode compatibilityMode)
        {
            foreach (SkinConfigProperty property in config.Properties)
            {
                string key = property.Key;
                if (property.Section == "General")
                {
                    if (key is "Name" or "Author" or "Version" or "AnimationFramerate") continue;
                    AddUnsupported(result, property);
                    continue;
                }

                if (property.Section == "Fonts")
                {
                    if (key is "ComboPrefix" or "ComboOverlap") continue;
                    AddUnsupported(result, property);
                    continue;
                }

                if (property.Section == "Colours")
                {
                    if (key.StartsWith("Combo", StringComparison.Ordinal)
                        && int.TryParse(key.AsSpan(5), out int combo) && combo is >= 1 and <= 8)
                        AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                            SkinDiagnosticSeverity.Information,
                            $"[{property.Section}] {key} is retained as metadata but is not used by the current Mania HUD.",
                            property.Section, key, property.Line));
                    else AddUnsupported(result, property);
                    continue;
                }

                if (property.Section != "Mania") continue;
                if (key == "Keys")
                {
                    if (int.TryParse(property.Value, out int parsed) && parsed is >= 1 and <= 18)
                    {
                        if (!result.ManiaKeyModes.Contains(parsed)) result.ManiaKeyModes.Add(parsed);
                    }
                    continue;
                }
                if (property.ManiaKeyCount is null)
                {
                    AddUnsupported(result, property);
                    continue;
                }

                bool versioned = key == "NoteBodyStyle" || key.StartsWith("NoteBodyStyle", StringComparison.Ordinal)
                    || key is "LightingNWidth" or "LightingLWidth"
                    || IsFlipProperty(key);
                if (versioned && !config.IsVersionAtLeast(2.5))
                {
                    AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.VersionInapplicable,
                        SkinDiagnosticSeverity.Information,
                        $"[{property.Section}] {key} requires skin version 2.5 or later and was ignored.",
                        property.Section, key, property.Line));
                    result.ParsedButNotApplied.Add(key);
                    continue;
                }
                if (IsFlipProperty(key) && compatibilityMode == LegacySkinCompatibilityMode.Lazer)
                {
                    AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                        SkinDiagnosticSeverity.Information,
                        $"[{property.Section}] {key} is not consumed by the experimental Lazer compatibility profile.",
                        property.Section, key, property.Line));
                    result.ParsedButNotApplied.Add(key);
                    continue;
                }
                if (key == "SpecialStyle")
                {
                    if (compatibilityMode == LegacySkinCompatibilityMode.Lazer)
                    {
                        AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                            SkinDiagnosticSeverity.Information,
                            $"[{property.Section}] {key} is not consumed by the experimental Lazer compatibility profile.",
                            property.Section, key, property.Line));
                        result.ParsedButNotApplied.Add(key);
                    }
                    else if (property.ManiaKeyCount is int configuredKeys
                        && configuredKeys > 4 && configuredKeys % 2 == 0)
                        AddUnsupported(result, property);
                    else
                        AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.AdaptedProperty,
                            SkinDiagnosticSeverity.Information,
                            $"[{property.Section}] {key} maps the configured single-stage lane to conventional S assets.",
                            property.Section, key, property.Line));
                    continue;
                }
                if (ignoredMania.Contains(key))
                {
                    AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.IgnoredByDesign,
                        SkinDiagnosticSeverity.Information,
                        $"[{property.Section}] {key} is parsed but has no active renderer consumer.",
                        property.Section, key, property.Line));
                    result.ParsedButNotApplied.Add(key);
                }
                else if (!IsApplied(key)) AddUnsupported(result, property);
            }

            string[] recognizedSections = { "General", "Fonts", "Colours", "Mania" };
            foreach (SkinConfigProperty property in config.Properties.Where(p =>
                         recognizedSections.Any(section => p.Section.Equals(section, StringComparison.OrdinalIgnoreCase)
                             && p.Section != section)))
                AddUnsupported(result, property);
        }

        private static bool IsFlipProperty(string key) => key is "NoteFlipWhenUpsideDown" or "KeyFlipWhenUpsideDown"
            || System.Text.RegularExpressions.Regex.IsMatch(key,
                @"^(?:NoteFlipWhenUpsideDown\d+[HLT]?|KeyFlipWhenUpsideDown\d+D?)$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        private static bool IsApplied(string key) => supportedMania.Contains(key)
            || System.Text.RegularExpressions.Regex.IsMatch(key, @"^NoteImage\d+[HLT]?$")
            || System.Text.RegularExpressions.Regex.IsMatch(key, @"^KeyImage\d+D?$")
            || System.Text.RegularExpressions.Regex.IsMatch(key, @"^Hit(?:0|50|100|200|300|300g)$")
            || System.Text.RegularExpressions.Regex.IsMatch(key, @"^Colour(?:\d+|Light\d+)$")
            || System.Text.RegularExpressions.Regex.IsMatch(key, @"^NoteBodyStyle\d+$")
            || IsFlipProperty(key);

        private static void AddUnsupported(SkinValidation result, SkinConfigProperty property)
        {
            result.Unsupported.Add(property.Key);
            AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.UnsupportedProperty,
                SkinDiagnosticSeverity.Warning,
                $"Line {property.Line}: [{property.Section}] {property.Key} is not supported by the current Mania skin subset.",
                property.Section, property.Key, property.Line));
        }

        private static void AddAssetDiagnostic(SkinValidation result, string root, string name)
        {
            if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.IntentionalBlank,
                    SkinDiagnosticSeverity.Information, $"{name} intentionally resolves to a transparent component.", Asset: name));
                return;
            }
            if (HasImage(root, name)) return;
            result.Warnings.Add($"Falta asset: {name}");
            AddDiagnostic(result, new SkinDiagnostic(SkinDiagnosticKind.MissingAsset,
                SkinDiagnosticSeverity.Warning,
                $"Missing image asset '{name}'; the matching internal component will be used.", Asset: name));
        }

        private static void AddDiagnostic(SkinValidation result, SkinDiagnostic diagnostic)
        {
            if (!result.Diagnostics.Contains(diagnostic)) result.Diagnostics.Add(diagnostic);
        }

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

        internal static IEnumerable<string> ReferencedImages(ManiaKeymodeDefinition mode)
        {
            foreach (var style in new[] { mode.NoteStyle, mode.ReceptorStyle, mode.LongHeadStyle, mode.LongBodyStyle, mode.LongTailStyle })
            {
                if (!string.IsNullOrWhiteSpace(style.Asset)) yield return style.Asset;
                if (!string.IsNullOrWhiteSpace(style.PressedAsset)) yield return style.PressedAsset;
            }
            foreach (ManiaLaneDefinition lane in mode.Lanes)
                foreach (string? asset in new[] { lane.NoteAsset, lane.ReceptorAsset, lane.PressedReceptorAsset,
                             lane.LongHeadAsset, lane.LongBodyAsset, lane.LongTailAsset })
                    if (!string.IsNullOrWhiteSpace(asset)) yield return asset;
            foreach (string? asset in mode.StageAssets.Values)
                if (!string.IsNullOrWhiteSpace(asset)) yield return asset;
            foreach (string asset in mode.JudgementAssets.Values) yield return asset;
            if (!string.IsNullOrWhiteSpace(mode.LightingNoteAsset)) yield return mode.LightingNoteAsset;
            if (!string.IsNullOrWhiteSpace(mode.LightingHoldAsset)) yield return mode.LightingHoldAsset;
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
