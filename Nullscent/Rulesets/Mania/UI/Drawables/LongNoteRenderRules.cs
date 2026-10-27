#nullable enable

using System;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Skin;

namespace Nullscent.Rulesets.Mania.UI.Drawables
{
    /// <summary>Pure geometry and clock rules shared by the long-note renderer and tests.</summary>
    internal static class LongNoteRenderRules
    {
        public static bool ShouldDrawHead(bool headJudged, LongNoteState state, bool keepAtReceptorWhileHolding = false) =>
            !headJudged || state == LongNoteState.Broken
                || (keepAtReceptorWhileHolding && state == LongNoteState.Holding);

        public static float GetPieceHeight(SkinSpriteMetrics metrics, float logicalWidth, float logicalHeight,
            float drawWidth, float columnWidth, float layoutScale, float noteHeightScale)
        {
            float aspect = logicalHeight / logicalWidth;
            float height = metrics.HeightMode switch
            {
                SpriteHeightMode.ReferenceWidthAspect when metrics.ReferenceWidth > 0f =>
                    metrics.ReferenceWidth * layoutScale * aspect,
                SpriteHeightMode.LaneWidthFraction => columnWidth * metrics.HeightValue,
                _ => drawWidth * aspect
            };
            return height * noteHeightScale;
        }

        public static (int TopY, int BottomY) OffsetBodySpan(int topY, int bottomY, int offsetY) =>
            (topY + offsetY, bottomY + offsetY);

        public static (int TopY, int BottomY) GetBodySpan(int headY, int tailY, int headHeight, int tailHeight,
            bool downScroll, float headConnectionRatio, float tailConnectionRatio)
        {
            float headRatio = NormalizeAnchorRatio(headConnectionRatio);
            float tailRatio = NormalizeAnchorRatio(tailConnectionRatio);
            float headConnection = downScroll
                ? headY - Math.Max(0, headHeight) * headRatio
                : headY + Math.Max(0, headHeight) * headRatio;
            float tailConnection = downScroll
                ? tailY + Math.Max(0, tailHeight) * tailRatio
                : tailY - Math.Max(0, tailHeight) * tailRatio;

            return ((int)MathF.Round(Math.Min(headConnection, tailConnection)),
                (int)MathF.Round(Math.Max(headConnection, tailConnection)));
        }

        public static double BodyAnimationElapsedMs(LongNoteState state, double currentTime,
            double headHitTime, double tailReleaseTime, bool headWasHit,
            HoldBodyAnimationReleaseMode releaseMode)
        {
            if (state == LongNoteState.Holding)
                return Math.Max(0, currentTime - headHitTime);

            if (releaseMode == HoldBodyAnimationReleaseMode.FreezeAtRelease
                && (state == LongNoteState.Broken || state == LongNoteState.Finished)
                && headWasHit && tailReleaseTime >= headHitTime)
                return Math.Max(0, tailReleaseTime - headHitTime);

            return 0;
        }

        public static double ResolveAnimationEndTime(LongNoteState state, double headHitTime,
            double tailReleaseTime, double noteEndTime, bool headWasHit)
        {
            // An auto-judged held tail reaches Finished without a physical key-release timestamp.
            // Keep its body animation at the charted tail instead of resetting it to frame zero.
            if (state == LongNoteState.Finished && headWasHit
                && tailReleaseTime < headHitTime && double.IsFinite(noteEndTime))
                return noteEndTime;

            return tailReleaseTime;
        }

        public static bool TileFromTop(NoteBodyRenderMode mode, bool downScroll) => mode switch
        {
            // In downscroll, a hold starts at the lower head and ends at the upper tail.
            // Upscroll mirrors those physical positions. Keep the semantic anchor
            // (head or tail) fixed while mapping it to the current screen edge.
            NoteBodyRenderMode.TileFromHead => !downScroll,
            NoteBodyRenderMode.TileFromTail => downScroll,
            _ => true
        };

        public static int TileOriginY(int bodyTopY, int bodyBottomY, int blockHeight,
            NoteBodyRenderMode mode, bool downScroll, int tileIndex)
        {
            if (blockHeight <= 0) throw new ArgumentOutOfRangeException(nameof(blockHeight));
            if (tileIndex < 0) throw new ArgumentOutOfRangeException(nameof(tileIndex));
            return TileFromTop(mode, downScroll)
                ? bodyTopY + tileIndex * blockHeight
                : bodyBottomY - (tileIndex + 1) * blockHeight;
        }

        public static int MaxTileSegmentHeight(int y, int requestedHeight, int bodyTopY, int bodyBottomY,
            int blockHeight, NoteBodyRenderMode mode, bool downScroll)
        {
            if (requestedHeight <= 0) return 0;
            if (blockHeight <= 0 || bodyBottomY <= bodyTopY) return requestedHeight;

            int tileIndex = GetTileIndex(y, bodyTopY, bodyBottomY, blockHeight, mode, downScroll);
            int tileOriginY = TileOriginY(bodyTopY, bodyBottomY, blockHeight, mode, downScroll, tileIndex);
            int offsetWithinTile = Math.Max(0, y - tileOriginY);
            return Math.Min(requestedHeight, Math.Max(1, blockHeight - offsetWithinTile));
        }

        public static (int Y, int Height) GetTileSourceRange(int y, int height, int bodyTopY, int bodyBottomY,
            int blockHeight, int textureHeight, NoteBodyRenderMode mode, bool downScroll, bool flipVertically)
        {
            if (height <= 0 || blockHeight <= 0 || textureHeight <= 0)
                return (0, Math.Max(1, textureHeight));

            int tileIndex = GetTileIndex(y, bodyTopY, bodyBottomY, blockHeight, mode, downScroll);
            int tileOriginY = TileOriginY(bodyTopY, bodyBottomY, blockHeight, mode, downScroll, tileIndex);
            int offsetWithinTile = Math.Clamp(y - tileOriginY, 0, blockHeight - 1);
            int displaySourceY = flipVertically
                ? Math.Max(0, blockHeight - offsetWithinTile - height)
                : offsetWithinTile;

            double sourcePixelsPerDisplayPixel = textureHeight / (double)blockHeight;
            int sourceY = (int)Math.Floor(displaySourceY * sourcePixelsPerDisplayPixel);
            int sourceBottom = (int)Math.Ceiling((displaySourceY + height) * sourcePixelsPerDisplayPixel);
            sourceY = Math.Clamp(sourceY, 0, textureHeight - 1);
            sourceBottom = Math.Clamp(sourceBottom, sourceY + 1, textureHeight);
            return (sourceY, sourceBottom - sourceY);
        }

        private static int GetTileIndex(int y, int bodyTopY, int bodyBottomY, int blockHeight,
            NoteBodyRenderMode mode, bool downScroll)
        {
            if (TileFromTop(mode, downScroll))
                return Math.Max(0, (y - bodyTopY) / blockHeight);

            int distanceFromBottom = Math.Max(1, bodyBottomY - y);
            return Math.Max(0, (distanceFromBottom - 1) / blockHeight);
        }

        private static float NormalizeAnchorRatio(float value) =>
            float.IsFinite(value) ? Math.Clamp(value, 0f, 0.5f) : 0f;
    }
}
