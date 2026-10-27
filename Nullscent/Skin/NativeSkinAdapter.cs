#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nullscent.Skin
{
    public static class NativeSkinAdapter
    {
        public const string ManifestFileName = "NullscentSkin.json";
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        static NativeSkinAdapter() => jsonOptions.Converters.Add(new JsonStringEnumConverter());

        public static ManiaSkinDefinition Load(string directory)
        {
            string root = Path.GetFullPath(directory);
            string manifestPath = Path.Combine(root, ManifestFileName);
            ManiaSkinDefinition loaded = JsonSerializer.Deserialize<ManiaSkinDefinition>(File.ReadAllText(manifestPath), jsonOptions)
                ?? throw new InvalidDataException("El manifiesto nativo no contiene una skin.");
            if (loaded.FormatVersion != 1)
                throw new InvalidDataException($"Versión de NullscentSkin no compatible: {loaded.FormatVersion}.");
            ValidateDefinition(loaded);

            return new ManiaSkinDefinition
            {
                FormatVersion = loaded.FormatVersion,
                Id = string.IsNullOrWhiteSpace(loaded.Id) ? root : loaded.Id,
                Name = string.IsNullOrWhiteSpace(loaded.Name) ? Path.GetFileName(root) : loaded.Name,
                Author = loaded.Author,
                Provider = SkinProviderKind.NullscentNative,
                CompatibilityProfile = ManiaSkinCompatibilityProfile.NullscentNative,
                UsesStableLegacyUnits = false,
                LegacyHudScale = loaded.LegacyHudScale,
                AnimationFramerate = loaded.AnimationFramerate,
                ComboPrefix = string.IsNullOrWhiteSpace(loaded.ComboPrefix) ? "score" : loaded.ComboPrefix,
                ComboOverlap = loaded.ComboOverlap,
                Keymodes = loaded.Keymodes,
                SharedAssets = loaded.SharedAssets
            };
        }

        private static void ValidateDefinition(ManiaSkinDefinition definition)
        {
            if (definition.Keymodes == null)
                throw new InvalidDataException("El manifiesto nativo debe contener un objeto keymodes.");
            if (definition.SharedAssets == null)
                throw new InvalidDataException("El manifiesto nativo no puede asignar null a sharedAssets.");
            if (!IsPositiveFinite(definition.LegacyHudScale))
                throw new InvalidDataException("legacyHudScale debe ser un número finito mayor que cero.");
            if (!float.IsFinite(definition.ComboOverlap))
                throw new InvalidDataException("comboOverlap debe ser finito.");
            if (definition.AnimationFramerate is int animationRate && !IsValidFrameRate(animationRate))
                throw new InvalidDataException("animationFramerate debe ser -1 o estar entre 1 y 1000.");

            foreach (var pair in definition.Keymodes)
            {
                int keyCount = pair.Key;
                ManiaKeymodeDefinition? mode = pair.Value;
                if (keyCount is < 1 or > 18 || mode == null || mode.KeyCount != keyCount)
                    throw new InvalidDataException($"Keymode inválido en el manifiesto: {keyCount}.");
                if (mode.Lanes == null || mode.Lanes.Count != keyCount)
                    throw new InvalidDataException($"El keymode {keyCount}K requiere una definición de lane por columna.");
                if (mode.Placement == null || !(mode.Placement.Equals("center", StringComparison.OrdinalIgnoreCase)
                    || mode.Placement.Equals("left", StringComparison.OrdinalIgnoreCase)
                    || mode.Placement.Equals("right", StringComparison.OrdinalIgnoreCase)
                    || mode.Placement.Equals("legacy", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"El keymode {keyCount}K usa una placement desconocida.");
                if (!Enum.IsDefined(mode.ReceptorAnchor) || !Enum.IsDefined(mode.BodyAnimationReleaseMode)
                    || !Enum.IsDefined(mode.BodyMode))
                    throw new InvalidDataException($"El keymode {keyCount}K contiene un enum de presentación inválido.");
                if (!IsFinite(mode.ColumnStart) || !IsFinite(mode.ColumnRight)
                    || !IsFinite(mode.HitPosition) || !IsFinite(mode.LightPosition)
                    || !IsFinite(mode.ScorePosition) || !IsFinite(mode.ComboPosition))
                    throw new InvalidDataException($"El keymode {keyCount}K contiene posiciones no finitas.");
                if (mode.NoteHeightReferenceWidth is float referenceWidth && !IsPositiveFinite(referenceWidth))
                    throw new InvalidDataException($"El keymode {keyCount}K requiere noteHeightReferenceWidth mayor que cero.");
                if (!float.IsFinite(mode.DefaultHitPositionRatio)
                    || mode.DefaultHitPositionRatio < 0f || mode.DefaultHitPositionRatio > 1f)
                    throw new InvalidDataException($"El keymode {keyCount}K requiere defaultHitPositionRatio entre 0 y 1.");
                if (!IsValidFrameRate(mode.LightFramesPerSecond) || !IsValidFrameRate(mode.JudgementFramesPerSecond))
                    throw new InvalidDataException($"El keymode {keyCount}K contiene un FPS fuera del rango -1 o 1..1000.");
                ValidateTiming(mode.JudgementTiming, keyCount, "judgementTiming");
                ValidateTiming(mode.NoteLightingTiming, keyCount, "noteLightingTiming");
                ValidateTiming(mode.HoldLightingTiming, keyCount, "holdLightingTiming");
                if (!float.IsFinite(mode.BarlineHeight) || mode.BarlineHeight < 0f)
                    throw new InvalidDataException($"El keymode {keyCount}K contiene una barlineHeight inválida.");
                if (mode.StageStyle is not { } stageStyle
                    || !IsPositiveFinite(stageStyle.SideHeightScale)
                    || !IsPositiveFinite(stageStyle.HintHeightScale)
                    || !IsPositiveFinite(stageStyle.BottomScale))
                    throw new InvalidDataException($"El keymode {keyCount}K contiene escalas de stage inválidas.");
                if (mode.HoldAssembly is not { } holdAssembly
                    || !IsValidHoldAnchor(holdAssembly.HeadConnectionRatio)
                    || !IsValidHoldAnchor(holdAssembly.TailConnectionRatio))
                    throw new InvalidDataException($"El keymode {keyCount}K contiene anchors de LN inválidos (rango permitido: 0 a 0.5).");
                if (mode.ColumnLineWidths == null || mode.ColumnLines == null
                    || mode.LightingNoteWidths == null || mode.LightingHoldWidths == null
                    || mode.StageAssets == null || mode.StageAssetAnimations == null || mode.JudgementAssets == null
                    || mode.NoteFlips == null || mode.KeyFlips == null)
                    throw new InvalidDataException($"El keymode {keyCount}K contiene una colección nula.");

                ValidateNonNegativeFinite(mode.ColumnLineWidths, keyCount, "columnLineWidths");
                ValidateNonNegativeFinite(mode.LightingNoteWidths, keyCount, "lightingNoteWidths");
                ValidateNonNegativeFinite(mode.LightingHoldWidths, keyCount, "lightingHoldWidths");
                ValidateStyle(mode.NoteStyle, keyCount, "noteStyle");
                ValidateStyle(mode.ReceptorStyle, keyCount, "receptorStyle");
                ValidateStyle(mode.LongHeadStyle, keyCount, "longHeadStyle");
                ValidateStyle(mode.LongBodyStyle, keyCount, "longBodyStyle");
                ValidateStyle(mode.LongTailStyle, keyCount, "longTailStyle");

                double totalLaneWidth = 0d;
                for (int laneIndex = 0; laneIndex < mode.Lanes.Count; laneIndex++)
                {
                    ManiaLaneDefinition? lane = mode.Lanes[laneIndex];
                    if (lane == null || !IsPositiveFinite(lane.Width)
                        || !float.IsFinite(lane.GapAfter) || lane.GapAfter < 0f
                        || !IsOptionalPositiveFinite(lane.NoteWidthRatio)
                        || !IsOptionalPositiveFinite(lane.ReceptorWidthRatio)
                        || !IsOptionalPositiveFinite(lane.HoldBodyWidthRatio)
                        || lane.BodyMode is NoteBodyRenderMode bodyMode && !Enum.IsDefined(bodyMode))
                        throw new InvalidDataException($"El keymode {keyCount}K contiene métricas de lane inválidas.");
                    totalLaneWidth += (double)lane.Width + (laneIndex == mode.Lanes.Count - 1 ? 0d : lane.GapAfter);
                }
                if (!double.IsFinite(totalLaneWidth) || totalLaneWidth > float.MaxValue)
                    throw new InvalidDataException($"El keymode {keyCount}K excede las métricas de layout representables.");
            }
        }

        private static void ValidateStyle(SpriteStyleDefinition? style, int keyCount, string role)
        {
            if (style == null || !IsPositiveFinite(style.LaneWidthRatio)
                || !Enum.IsDefined(style.HeightMode)
                || !float.IsFinite(style.HeightValue)
                || !float.IsFinite(style.OffsetXRatio) || !float.IsFinite(style.OffsetY)
                || style.HeightMode == SpriteHeightMode.LaneWidthFraction && style.HeightValue <= 0f
                || style.FramesPerSecond is int fps && !IsValidFrameRate(fps))
                throw new InvalidDataException($"El keymode {keyCount}K contiene un {role} inválido.");
            ValidateTiming(style.Timing, keyCount, $"{role}.timing");
        }

        private static void ValidateTiming(AnimationTimingDefinition? timing, int keyCount, string role)
        {
            if (timing == null) return;
            if (!double.IsFinite(timing.MinimumFrameDurationMs) || timing.MinimumFrameDurationMs <= 0d
                || timing.TargetAnimationDurationMs is double targetDuration
                    && (!double.IsFinite(targetDuration) || targetDuration <= 0d))
                throw new InvalidDataException($"Keymode {keyCount} contains invalid animation timing for {role}.");
        }

        private static void ValidateNonNegativeFinite(float[] values, int keyCount, string role)
        {
            foreach (float value in values)
                if (!float.IsFinite(value) || value < 0f)
                    throw new InvalidDataException($"El keymode {keyCount}K contiene un valor inválido en {role}.");
        }

        private static bool IsValidFrameRate(int frameRate) => frameRate == -1 || frameRate is >= 1 and <= 1000;
        private static bool IsFinite(float? value) => value is null || float.IsFinite(value.Value);
        private static bool IsPositiveFinite(float value) => float.IsFinite(value) && value > 0f;
        private static bool IsOptionalPositiveFinite(float? value) => value is null || IsPositiveFinite(value.Value);
        private static bool IsValidHoldAnchor(float value) => float.IsFinite(value) && value >= 0f && value <= 0.5f;
    }

    public static class SkinSourceDecoder
    {
        public static ManiaSkinDefinition Load(string directory, out SkinConfig? legacyConfig,
            LegacySkinCompatibilityMode compatibilityMode = LegacySkinCompatibilityMode.Stable)
        {
            string root = Path.GetFullPath(directory);
            string nativeManifest = Path.Combine(root, NativeSkinAdapter.ManifestFileName);
            if (File.Exists(nativeManifest))
            {
                legacyConfig = null;
                return NativeSkinAdapter.Load(root);
            }

            legacyConfig = SkinConfigParser.Parse(root);
            if (legacyConfig.ParseError != null)
                throw new InvalidDataException(legacyConfig.ParseError);
            return LegacyManiaSkinAdapter.Decode(legacyConfig, root, compatibilityMode);
        }
    }
}
