#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nullscent.Skin
{
    public enum SkinProviderKind
    {
        NullscentNative,
        OsuLegacy,
        Emergency
    }

    public enum NoteBodyRenderMode
    {
        Stretch,
        TileFromTail,
        TileFromHead
    }

    public enum SpriteHeightMode
    {
        TextureAspect,
        ReferenceWidthAspect,
        LaneWidthFraction
    }

    /// <summary>
    /// A normalized presentation recipe. Asset names are resolved once when a skin is
    /// activated; layout values are in the provider's declared design-space units.
    /// </summary>
    public sealed class ManiaSkinDefinition
    {
        public int FormatVersion { get; init; } = 1;
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = "Untitled";
        public string Author { get; init; } = string.Empty;
        public SkinProviderKind Provider { get; init; }
        public bool UsesStableLegacyUnits { get; init; }
        public double? LegacyVersion { get; init; }
        public float LegacyHudScale { get; init; } = 1f;
        public int? AnimationFramerate { get; init; }
        public string ComboPrefix { get; init; } = "score";
        public float ComboOverlap { get; init; }
        public Dictionary<int, ManiaKeymodeDefinition> Keymodes { get; init; } = new();
        public Dictionary<string, string> SharedAssets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ManiaKeymodeDefinition
    {
        public int KeyCount { get; init; }
        public string Placement { get; init; } = "center";
        public List<ManiaLaneDefinition> Lanes { get; init; } = new();
        public float? ColumnStart { get; init; }
        public float? ColumnRight { get; init; }
        public float? HitPosition { get; init; }
        public float? LightPosition { get; init; }
        public float? ScorePosition { get; init; }
        public float? ComboPosition { get; init; }
        public float? NoteHeightReferenceWidth { get; init; }
        public float DefaultHitPositionRatio { get; init; } = 0.86f;
        public bool KeysUnderNotes { get; init; }
        public bool JudgementLine { get; init; } = true;
        public bool ColumnLighting { get; init; } = true;
        public bool? UpsideDown { get; init; }
        public bool SupportsFlipOverrides { get; init; }
        public bool? NoteFlipWhenUpsideDown { get; init; }
        public bool? KeyFlipWhenUpsideDown { get; init; }
        public bool DrawColumnEdgesBothSides { get; init; } = true;
        public int LightFramesPerSecond { get; init; } = 60;
        public float[] ColumnLineWidths { get; init; } = Array.Empty<float>();
        public bool[] ColumnLines { get; init; } = Array.Empty<bool>();
        public float[] LightingNoteWidths { get; init; } = Array.Empty<float>();
        public float[] LightingHoldWidths { get; init; } = Array.Empty<float>();
        public SkinColor? ColumnLineColor { get; init; }
        public SkinColor? BarlineColor { get; init; }
        public float BarlineHeight { get; init; } = 1.2f;
        public SkinColor? JudgementLineColor { get; init; }
        public SkinColor? HoldColor { get; init; }
        public SkinColor? BreakColor { get; init; }
        public NoteBodyRenderMode BodyMode { get; init; } = NoteBodyRenderMode.TileFromTail;
        public SpriteStyleDefinition NoteStyle { get; init; } = new();
        public SpriteStyleDefinition ReceptorStyle { get; init; } = new();
        public SpriteStyleDefinition LongHeadStyle { get; init; } = new();
        public SpriteStyleDefinition LongBodyStyle { get; init; } = new();
        public SpriteStyleDefinition LongTailStyle { get; init; } = new();
        public Dictionary<string, string?> StageAssets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> JudgementAssets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public string? LightingNoteAsset { get; init; }
        public string? LightingHoldAsset { get; init; }
        public Dictionary<string, bool> NoteFlips { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> KeyFlips { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ManiaLaneDefinition
    {
        public float Width { get; init; } = 48f;
        public float GapAfter { get; init; }
        public float? NoteWidthRatio { get; init; }
        public float? ReceptorWidthRatio { get; init; }
        public float? HoldBodyWidthRatio { get; init; }
        public NoteBodyRenderMode? BodyMode { get; init; }
        public string? NoteAsset { get; init; }
        public string? ReceptorAsset { get; init; }
        public string? PressedReceptorAsset { get; init; }
        public string? LongHeadAsset { get; init; }
        public string? LongBodyAsset { get; init; }
        public string? LongTailAsset { get; init; }
        public SkinColor? LaneColor { get; init; }
        public SkinColor? LightColor { get; init; }
        public SkinColor? HoldColor { get; init; }
    }

    public sealed class SpriteStyleDefinition
    {
        public string? Asset { get; init; }
        public string? PressedAsset { get; init; }
        public float LaneWidthRatio { get; init; } = 1f;
        public SpriteHeightMode HeightMode { get; init; } = SpriteHeightMode.TextureAspect;
        public float HeightValue { get; init; }
        public float OffsetXRatio { get; init; }
        public float OffsetY { get; init; }
        public int? FramesPerSecond { get; init; }
    }

    public readonly record struct SkinColor(byte R, byte G, byte B, byte A = 255)
    {
        public static SkinColor? FromLegacy((byte R, byte G, byte B, byte A)? colour) => colour is { } c
            ? new SkinColor(c.R, c.G, c.B, c.A) : null;
    }

    public readonly record struct SkinSpriteMetrics(float WidthRatio, SpriteHeightMode HeightMode, float HeightValue,
        float ReferenceWidth, float OffsetXRatio, float OffsetY, int? FramesPerSecond)
    {
        public static SkinSpriteMetrics FromStyle(SpriteStyleDefinition style, float referenceWidth = 0f) =>
            new(style.LaneWidthRatio, style.HeightMode, style.HeightValue, referenceWidth,
                style.OffsetXRatio, style.OffsetY, style.FramesPerSecond);
    }

    /// <summary>Converts skin.ini details into Nullscent's engine-independent model.</summary>
    public static class LegacyManiaSkinAdapter
    {
        public const float StableScale = 1.6f;
        public const float StableDesignHeight = 768f;

        public static ManiaSkinDefinition Decode(SkinConfig config, string? id = null)
        {
            var modes = new Dictionary<int, ManiaKeymodeDefinition>();
            bool supportsBodyStyle = config.IsVersionAtLeast(2.5);
            bool supportsFlipOverrides = config.IsVersionAtLeast(2.5);
            double? version = double.TryParse(config.Version, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedVersion)
                ? parsedVersion : null;
            // A conventional-name skin without skin.ini has a defined newest-rules
            // interpretation. Missing keymode sections use the format defaults.
            for (int keys = 1; keys <= 18; keys++)
            {
                ManiaKeyConfig source = config.ManiaConfigs.TryGetValue(keys, out ManiaKeyConfig? configured)
                    ? configured : new ManiaKeyConfig();

                var lanes = new List<ManiaLaneDefinition>(keys);
                for (int i = 0; i < keys; i++)
                {
                    float width = Read(source.ColumnWidths, i, 30f) * StableScale;
                    float gap = Read(source.ColumnSpacing, i, 0f) * StableScale;
                    lanes.Add(new ManiaLaneDefinition
                    {
                        Width = width,
                        GapAfter = gap,
                        NoteAsset = Value(source.NoteImages, i),
                        ReceptorAsset = Value(source.KeyImages, i),
                        PressedReceptorAsset = Value(source.KeyPressedImages, i),
                        LongHeadAsset = Value(source.NoteHeadImages, i),
                        LongBodyAsset = Value(source.NoteBodyImages, i),
                        LongTailAsset = Value(source.NoteTailImages, i),
                        BodyMode = supportsBodyStyle && source.NoteBodyStyles.TryGetValue(i, out int columnBodyMode)
                            ? DecodeBodyMode(columnBodyMode) : null,
                        LaneColor = SkinColor.FromLegacy(Value(source.ColumnColorsMap, i)),
                        LightColor = SkinColor.FromLegacy(Value(source.ColourLights, i)),
                        HoldColor = SkinColor.FromLegacy(Value(source.ColourHolds, i))
                    });
                }

                float? noteReference = source.WidthForNoteHeightScale is > 0 and var widthRef
                    ? widthRef * StableScale
                    : lanes.Select(l => l.Width).DefaultIfEmpty(30f * StableScale).Min();

                modes[keys] = new ManiaKeymodeDefinition
                {
                    KeyCount = keys,
                    Placement = "legacy",
                    Lanes = lanes,
                    ColumnStart = (source.ColumnStart ?? 136f) * StableScale,
                    ColumnRight = (source.ColumnRight ?? 19f) * StableScale,
                    HitPosition = (source.HitPosition ?? 402) * StableScale,
                    LightPosition = (source.LightPosition ?? 413) * StableScale,
                    ScorePosition = (source.ScorePosition ?? 300) * StableScale,
                    ComboPosition = (source.ComboPosition ?? 111) * StableScale,
                    NoteHeightReferenceWidth = noteReference,
                    KeysUnderNotes = source.KeysUnderNotes,
                    JudgementLine = source.JudgementLine,
                    ColumnLighting = source.ColumnLighting,
                    UpsideDown = source.UpsideDown,
                    SupportsFlipOverrides = supportsFlipOverrides,
                    NoteFlipWhenUpsideDown = supportsFlipOverrides ? source.NoteFlipWhenUpsideDown : null,
                    KeyFlipWhenUpsideDown = supportsFlipOverrides ? source.KeyFlipWhenUpsideDown : null,
                    DrawColumnEdgesBothSides = config.IsVersionAtLeast(2.4),
                    LightFramesPerSecond = source.LightFramePerSecond,
                    ColumnLineWidths = Expand(source.ColumnLineWidths, keys + 1, 2f).Select(v => v * StableScale).ToArray(),
                    ColumnLines = Expand(source.ColumnLines, keys + 1, true),
                    LightingNoteWidths = config.IsVersionAtLeast(2.5) ? source.LightingNWidths?.Select(v => v * StableScale).ToArray() ?? Array.Empty<float>() : Array.Empty<float>(),
                    LightingHoldWidths = config.IsVersionAtLeast(2.5) ? source.LightingLWidths?.Select(v => v * StableScale).ToArray() ?? Array.Empty<float>() : Array.Empty<float>(),
                    NoteFlips = supportsFlipOverrides
                        ? new Dictionary<string, bool>(source.NoteFlips, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
                    KeyFlips = supportsFlipOverrides
                        ? new Dictionary<string, bool>(source.KeyFlips, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
                    ColumnLineColor = SkinColor.FromLegacy(source.ColourColumnLine),
                    BarlineColor = SkinColor.FromLegacy(source.ColourBarline),
                    BarlineHeight = source.BarlineHeight ?? 1.2f,
                    JudgementLineColor = SkinColor.FromLegacy(source.ColourJudgementLine),
                    HoldColor = SkinColor.FromLegacy(source.ColourHold),
                    BreakColor = SkinColor.FromLegacy(source.ColourBreak),
                    BodyMode = DecodeBodyMode(supportsBodyStyle ? source.NoteBodyStyle : 1),
                    NoteStyle = new SpriteStyleDefinition
                    {
                        LaneWidthRatio = 1f,
                        HeightMode = SpriteHeightMode.ReferenceWidthAspect,
                        FramesPerSecond = 60
                    },
                    ReceptorStyle = new SpriteStyleDefinition { LaneWidthRatio = 1f, HeightMode = SpriteHeightMode.TextureAspect },
                    LongHeadStyle = new SpriteStyleDefinition { LaneWidthRatio = 1f, HeightMode = SpriteHeightMode.ReferenceWidthAspect, FramesPerSecond = 60 },
                    LongBodyStyle = new SpriteStyleDefinition { LaneWidthRatio = 1f, HeightMode = SpriteHeightMode.TextureAspect, FramesPerSecond = 30 },
                    LongTailStyle = new SpriteStyleDefinition { LaneWidthRatio = 1f, HeightMode = SpriteHeightMode.ReferenceWidthAspect, FramesPerSecond = 60 },
                    StageAssets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["left"] = source.StageLeft, ["right"] = source.StageRight,
                        ["bottom"] = source.StageBottom, ["hint"] = source.StageHint,
                        ["light"] = source.StageLight
                    },
                    JudgementAssets = new Dictionary<string, string>(source.HitImages, StringComparer.OrdinalIgnoreCase),
                    LightingNoteAsset = source.LightingN,
                    LightingHoldAsset = source.LightingL
                };
            }

            return new ManiaSkinDefinition
            {
                FormatVersion = 1,
                Id = id ?? config.SkinDirectory,
                Name = config.Name,
                Author = config.Author,
                Provider = SkinProviderKind.OsuLegacy,
                UsesStableLegacyUnits = true,
                LegacyVersion = version,
                LegacyHudScale = config.IsVersionAtLeast(2.4) ? 1f : StableScale,
                AnimationFramerate = config.AnimationFramerate > 0 ? config.AnimationFramerate : null,
                ComboPrefix = config.ComboPrefix,
                ComboOverlap = config.ComboOverlap,
                Keymodes = modes
            };
        }

        public static NoteBodyRenderMode DecodeBodyMode(int style) => style switch
        {
            0 => NoteBodyRenderMode.Stretch,
            2 => NoteBodyRenderMode.TileFromHead,
            _ => NoteBodyRenderMode.TileFromTail
        };

        private static T? Value<T>(Dictionary<int, T> values, int index) => values.TryGetValue(index, out T? value) ? value : default;
        private static float Read(float[]? values, int index, float fallback) =>
            values != null && index < values.Length && float.IsFinite(values[index]) && values[index] >= 0 ? values[index] : fallback;

        private static T[] Expand<T>(T[]? values, int count, T fallback)
        {
            var result = new T[count];
            for (int i = 0; i < count; i++)
                result[i] = values != null && i < values.Length ? values[i] : fallback;
            return result;
        }
    }
}
