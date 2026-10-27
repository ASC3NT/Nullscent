#nullable enable

using System;
using System.Collections.Generic;

namespace Nullscent.Skin
{
    public readonly record struct SkinRect(float X, float Y, float Width, float Height)
    {
        public float Right => X + Width;
        public float Bottom => Y + Height;
        public float CenterX => X + Width / 2f;
    }

    public readonly record struct ManiaLayoutOverrides(float PlayfieldScale = 1f, float LaneScale = 1f,
        float? ReceptorPositionRatio = null, float NoteScale = 1f, float ReceptorScale = 1f);

    public sealed class ManiaLayoutResult
    {
        public required IReadOnlyList<SkinRect> Lanes { get; init; }
        public required SkinRect Playfield { get; init; }
        public required float ReceptorY { get; init; }
        public required float ReceptorSpriteY { get; init; }
        public required float Scale { get; init; }
        public required float OffsetX { get; init; }
        public required float OffsetY { get; init; }
        public required float LogicalHeight { get; init; }
        public required float LogicalWidth { get; init; }
        public required float NoteScale { get; init; }
        public required float ReceptorScale { get; init; }
        public float ToViewportX(float x) => OffsetX + x * Scale;
        public float ToViewportY(float y) => OffsetY + y * Scale;
        public float ToViewportLength(float length) => length * Scale;
    }

    /// <summary>
    /// Lays out mania in a 16:9 design space whose height is 768 logical units.
    /// Legacy skin values are converted into this space in LegacyManiaSkinAdapter.
    /// Only this class maps logical positions into the physical viewport.
    /// </summary>
    public static class ManiaLayoutEngine
    {
        public const float DesignHeight = 768f;
        public const float DesignAspectRatio = 16f / 9f;
        public const float DesignWidth = DesignHeight * DesignAspectRatio;

        public static float ResolveStageLightAnchorY(float? configuredLogicalPosition, bool downScroll)
        {
            float logicalPosition = configuredLogicalPosition ?? DesignHeight;
            return downScroll ? logicalPosition : DesignHeight - logicalPosition;
        }

        public static ManiaLayoutResult Calculate(ManiaKeymodeDefinition? profile, int keyCount,
            int viewportWidth, int viewportHeight, bool downScroll, ManiaLayoutOverrides overrides = default)
        {
            if (keyCount < 1 || keyCount > 18)
                throw new ArgumentOutOfRangeException(nameof(keyCount));
            if (viewportWidth <= 0 || viewportHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport dimensions must be positive.");

            float scale = Math.Min(viewportWidth / DesignWidth, viewportHeight / DesignHeight);
            float offsetX = (viewportWidth - DesignWidth * scale) / 2f;
            float offsetY = (viewportHeight - DesignHeight * scale) / 2f;
            float playfieldScale = ClampScale(overrides.PlayfieldScale);
            float laneScale = ClampScale(overrides.LaneScale);
            float noteScale = ClampScale(overrides.NoteScale);
            float receptorScale = ClampScale(overrides.ReceptorScale);

            var widths = new float[keyCount];
            var gaps = new float[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                ManiaLaneDefinition? lane = profile != null && i < profile.Lanes.Count ? profile.Lanes[i] : null;
                widths[i] = PositiveOr(lane?.Width ?? 48f, 48f) * playfieldScale * laneScale;
                gaps[i] = i == keyCount - 1 ? 0 : FiniteOr(lane?.GapAfter ?? 0, 0) * playfieldScale * laneScale;
            }

            float totalWidth = 0;
            for (int i = 0; i < keyCount; i++) totalWidth += widths[i] + gaps[i];

            string placement = profile?.Placement ?? "center";
            float left;
            if (placement.Equals("legacy", StringComparison.OrdinalIgnoreCase))
            {
                left = Math.Max(0, FiniteOr(profile?.ColumnStart ?? 136f * LegacyManiaSkinAdapter.StableScale, 136f * LegacyManiaSkinAdapter.StableScale));
                // ColumnRight is a maximum usable right edge, measured inward from
                // the design canvas edge. It constrains placement without changing
                // lane geometry or using image dimensions as layout input.
                float maximumRight = DesignWidth - Math.Max(0, FiniteOr(profile?.ColumnRight ?? 19f * LegacyManiaSkinAdapter.StableScale, 19f * LegacyManiaSkinAdapter.StableScale));
                left = Math.Min(left, Math.Max(0, maximumRight - totalWidth));
            }
            else if (placement.Equals("right", StringComparison.OrdinalIgnoreCase))
            {
                left = DesignWidth - totalWidth;
            }
            else if (placement.Equals("left", StringComparison.OrdinalIgnoreCase))
            {
                left = 0;
            }
            else
            {
                left = (DesignWidth - totalWidth) / 2f;
            }

            var lanes = new List<SkinRect>(keyCount);
            float x = left;
            for (int i = 0; i < keyCount; i++)
            {
                lanes.Add(new SkinRect(offsetX + x * scale, offsetY, widths[i] * scale, DesignHeight * scale));
                x += widths[i] + gaps[i];
            }

            float logicalHit;
            if (overrides.ReceptorPositionRatio is float receptorRatio && float.IsFinite(receptorRatio))
            {
                logicalHit = Math.Clamp(receptorRatio, 0.05f, 0.95f) * DesignHeight;
            }
            else if (profile?.HitPosition is float hit && float.IsFinite(hit))
            {
                logicalHit = Math.Clamp(hit, 0, DesignHeight);
            }
            else
            {
                logicalHit = Math.Clamp(FiniteOr(profile?.DefaultHitPositionRatio ?? 0.86f, 0.86f), 0.05f, 0.95f) * DesignHeight;
            }

            if (!downScroll)
                logicalHit = DesignHeight - logicalHit;

            float receptorY = offsetY + logicalHit * scale;
            float receptorSpriteY = profile?.ReceptorAnchor == ReceptorAnchorMode.StageEdge
                && overrides.ReceptorPositionRatio is null
                    ? offsetY + (downScroll ? DesignHeight : 0f) * scale
                    : receptorY;

            var playfield = new SkinRect(offsetX + left * scale, offsetY,
                totalWidth * scale, DesignHeight * scale);
            return new ManiaLayoutResult
            {
                Lanes = lanes,
                Playfield = playfield,
                ReceptorY = receptorY,
                ReceptorSpriteY = receptorSpriteY,
                Scale = scale,
                OffsetX = offsetX,
                OffsetY = offsetY,
                LogicalHeight = DesignHeight,
                LogicalWidth = DesignWidth,
                NoteScale = noteScale,
                ReceptorScale = receptorScale
            };
        }

        public static SkinRect GetStageSideRect(ManiaLayoutResult layout, float logicalTextureWidth,
            bool isRight, ManiaStageStyleDefinition style)
        {
            float width = PositiveOr(logicalTextureWidth, 1f) * layout.Scale;
            float height = layout.Playfield.Height * ScaleOr(style.SideHeightScale, 1f);
            float x = isRight ? layout.Playfield.Right : layout.Playfield.X - width;
            return new SkinRect(x, layout.Playfield.Y, width, height);
        }

        public static SkinRect GetStageHintRect(ManiaLayoutResult layout, float logicalTextureWidth,
            float logicalTextureHeight, ManiaStageStyleDefinition style)
        {
            float width = layout.Playfield.Width;
            float aspect = PositiveOr(logicalTextureHeight, 1f) / PositiveOr(logicalTextureWidth, 1f);
            float height = width * aspect * ScaleOr(style.HintHeightScale, 0.9f);
            return new SkinRect(layout.Playfield.X, layout.ReceptorY - height / 2f, width, height);
        }

        public static SkinRect GetStageBottomRect(ManiaLayoutResult layout, float logicalTextureWidth,
            float logicalTextureHeight, bool downScroll, ManiaStageStyleDefinition style)
        {
            float scale = layout.Scale * ScaleOr(style.BottomScale, 1f);
            float width = PositiveOr(logicalTextureWidth, 1f) * scale;
            float height = PositiveOr(logicalTextureHeight, 1f) * scale;
            float x = layout.Playfield.CenterX - width / 2f;
            float y = downScroll ? layout.Playfield.Bottom - height : layout.Playfield.Y;
            return new SkinRect(x, y, width, height);
        }

        private static float ClampScale(float scale) => !float.IsFinite(scale) || scale <= 0 ? 1f : Math.Clamp(scale, 0.1f, 4f);
        private static float FiniteOr(float value, float fallback) => float.IsFinite(value) ? value : fallback;
        private static float PositiveOr(float value, float fallback) => float.IsFinite(value) && value > 0 ? value : fallback;
        private static float ScaleOr(float value, float fallback) =>
            float.IsFinite(value) && value > 0 ? Math.Clamp(value, 0.05f, 4f) : fallback;
    }
}
