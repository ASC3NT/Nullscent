#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Beatmap;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Skin;
using System;
using System.Collections.Generic;

namespace Nullscent.Rulesets.Mania.UI.Drawables
{
    /// <summary>
    /// Base class for drawable mania hit objects.
    /// Inspired by osu!'s DrawableHitObject architecture.
    /// Handles rendering, animations, and lifecycle of hit objects.
    /// </summary>
    public abstract class DrawableManiaHitObject : IDisposable
    {
        public ManiaHitObject HitObject { get; }
        public bool IsJudged { get; set; }
        public HitResult? Result { get; protected set; }
        public bool HasResult => Result.HasValue;
        public Nullscent.Beatmap.Beatmap? Beatmap { get; set; }
        public bool IsHiddenActive { get; set; }
        public int CurrentCombo { get; set; }

        protected double TimePreempt { get; set; } = 1000; // 1 second preempt
        protected double TimeFadeIn { get; set; } = 200;    // 200ms fade in

        private double _alpha = 0;
        private bool _isVisible = false;
        protected int ScrollTop { get; private set; }
        protected int ScrollBottom { get; private set; }
        protected int CurrentReceptorY { get; private set; }

        protected DrawableManiaHitObject(ManiaHitObject hitObject)
        {
            HitObject = hitObject;
        }

        /// <summary>
        /// Updates the drawable state based on current time.
        /// </summary>
        public virtual void Update(double currentTime, double scrollSpeedSetting, float receptorPosition, bool downScroll,
            int screenHeight, int? layoutReceptorY = null, int scrollTop = 0, int? scrollBottom = null)
        {
            ScrollTop = scrollTop;
            ScrollBottom = scrollBottom ?? screenHeight;
            double scrollTime = 2000.0 - (scrollSpeedSetting - 1.0) * 45.0;
            scrollTime = Math.Clamp(scrollTime, 150.0, 2000.0);

            TimePreempt = scrollTime;
            TimeFadeIn = Math.Min(200.0, scrollTime * 0.2);

            int receptorY = layoutReceptorY ?? (int)(downScroll ? screenHeight * receptorPosition : screenHeight * (1 - receptorPosition));
            CurrentReceptorY = receptorY;

            int y = CalculateYPosition(HitObject.StartTime, currentTime, scrollSpeedSetting, receptorY, downScroll, screenHeight);

            // Determinar visibilidad basada en si el objeto (o parte de él) está en pantalla
            bool visible = false;
            if (HitObject is LongNote ln)
            {
                int tailY = CalculateYPosition(ln.EndTime, currentTime, scrollSpeedSetting, receptorY, downScroll, screenHeight);
                int topY = Math.Min(y, tailY);
                int bottomY = Math.Max(y, tailY);
                visible = bottomY >= -100 && topY <= screenHeight + 100;
            }
            else if (HitObject is HoldNote hn)
            {
                int tailY = CalculateYPosition(hn.EndTime, currentTime, scrollSpeedSetting, receptorY, downScroll, screenHeight);
                int topY = Math.Min(y, tailY);
                int bottomY = Math.Max(y, tailY);
                visible = bottomY >= -100 && topY <= screenHeight + 100;
            }
            else
            {
                visible = y >= -100 && y <= screenHeight + 100;
            }

            if (IsJudged)
            {
                visible = false;
            }

            if (visible)
            {
                _isVisible = true;
                _alpha = 1.0;
            }
            else
            {
                _isVisible = false;
                _alpha = 0.0;
            }
        }

        protected double GetHiddenAlpha(int y, bool downScroll, int receptorY, int screenHeight, int combo)
        {
            if (!IsHiddenActive) return 1.0;

            double comboFactor = Math.Min(1.0, combo / 200.0);

            if (downScroll)
            {
                // Con downScroll, las notas caen de y=0 hacia y=receptorY.
                // Ocultamos las notas antes de que alcancen el receptor.
                double startFadeY = receptorY * (0.5 - comboFactor * 0.15); // A mayor combo, desvanece antes (más arriba)
                double endFadeY = receptorY * (0.75 - comboFactor * 0.15);

                if (y <= startFadeY) return 1.0;
                if (y >= endFadeY) return 0.0;
                return 1.0 - (y - startFadeY) / (endFadeY - startFadeY);
            }
            else
            {
                // Con upScroll, las notas suben de y=screenHeight hacia y=receptorY.
                // Ocultamos las notas antes de que alcancen el receptor (que está arriba).
                double startFadeY = receptorY + (screenHeight - receptorY) * (0.5 + comboFactor * 0.15); // A mayor combo, desvanece antes (más abajo)
                double endFadeY = receptorY + (screenHeight - receptorY) * (0.25 + comboFactor * 0.15);

                if (y >= startFadeY) return 1.0;
                if (y <= endFadeY) return 0.0;
                return (y - endFadeY) / (startFadeY - endFadeY);
            }
        }

        /// <summary>
        /// Draws the hit object.
        /// </summary>
        public abstract void Draw(SpriteBatch spriteBatch, Texture2D pixel, SkinManager? skinManager, int keyCount, int columnIndex, int columnX, double currentTime, 
            double scrollSpeed, int receptorY, bool downScroll, int screenHeight, int columnWidth, float layoutScale, float noteHeightScale);

        /// <summary>
        /// Checks if this object should be judged at the given time.
        /// Returns the hit result if within window, otherwise None.
        /// </summary>
        public abstract void CheckForResult(double currentTime, ManiaHitWindows hitWindows);

        /// <summary>
        /// Called when the object has been judged.
        /// </summary>
        public virtual void OnJudged(HitResult result)
        {
            IsJudged = true;
            Result = result;
        }

        protected double GetAlpha()
        {
            return _alpha;
        }

        protected bool IsVisible()
        {
            return _isVisible;
        }

        /// <summary>
        /// Calculates the Y position of the hit object based on current time and scroll settings.
        /// </summary>
        protected int CalculateYPosition(double objectTime, double currentTime, double scrollSpeedSetting, int receptorY, bool downScroll, int screenHeight)
        {
            double scrollTime = 2000.0 - (scrollSpeedSetting - 1.0) * 45.0;
            scrollTime = Math.Clamp(scrollTime, 150.0, 2000.0);

            double timeDiff;
            if (Beatmap != null)
            {
                timeDiff = Beatmap.GetScrollPosition(objectTime) - Beatmap.GetScrollPosition(currentTime);
            }
            else
            {
                timeDiff = objectTime - currentTime;
            }

            if (downScroll)
            {
                double ratio = timeDiff / scrollTime;
                return receptorY - (int)(ratio * (receptorY - ScrollTop));
            }
            else
            {
                double ratio = timeDiff / scrollTime;
                return receptorY + (int)(ratio * (ScrollBottom - receptorY));
            }
        }

        public virtual void Dispose()
        {
            // Override in derived classes if needed
        }
    }

    /// <summary>
    /// Drawable representation of a single tap note.
    /// </summary>
    public class DrawableNote : DrawableManiaHitObject
    {
        private const int NOTE_HEIGHT = 12;
        private const int BORDER_THICKNESS = 2;


        public DrawableNote(Note note) : base(note)
        {
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel, SkinManager? skinManager, int keyCount, int columnIndex, int columnX, double currentTime, 
            double scrollSpeed, int receptorY, bool downScroll, int screenHeight, int columnWidth, float layoutScale, float noteHeightScale)
        {
            if (!IsVisible() || IsJudged) return;

            int y = CalculateYPosition(HitObject.StartTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);

            // Culling check
            if (y < -50 || y > screenHeight + 50) return;

            double alpha = GetHiddenAlpha(y, downScroll, receptorY, screenHeight, CurrentCombo);
            if (alpha <= 0.0) return;

            ResolvedManiaLane? resolvedLane = skinManager != null && columnIndex >= 0 && columnIndex < skinManager.GetResolvedKeymode(keyCount).Lanes.Count
                ? skinManager.GetResolvedKeymode(keyCount).Lanes[columnIndex] : null;
            Texture2D? noteTexture = resolvedLane?.Note?.FrameAt(currentTime);
            if (noteTexture != null)
            {
                SkinSpriteMetrics metrics = skinManager!.GetSpriteMetrics(keyCount, columnIndex, "note");
                Vector2 noteSize = skinManager!.GetLogicalSize(noteTexture);
                float aspect = noteSize.Y / noteSize.X;
                float noteWidth = Math.Max(1f, columnWidth * metrics.WidthRatio);
                float noteHeight = metrics.HeightMode switch
                {
                    SpriteHeightMode.ReferenceWidthAspect when metrics.ReferenceWidth > 0f => metrics.ReferenceWidth * layoutScale * aspect,
                    SpriteHeightMode.LaneWidthFraction => columnWidth * metrics.HeightValue,
                    _ => noteWidth * aspect
                } * noteHeightScale;
                noteWidth = Math.Max(1f, noteWidth);
                noteHeight = Math.Max(1f, noteHeight);

                // ALINEACIÓN DE BORDE DELANTERO (osu!stable style):
                // En downScroll, el borde inferior coincide con y.
                // En upScroll, el borde superior coincide con y.
                int noteY = (int)MathF.Round(downScroll ? y - noteHeight + metrics.OffsetY * layoutScale : y + metrics.OffsetY * layoutScale);
                int noteX = (int)MathF.Round(columnX + (columnWidth - noteWidth) / 2f + columnWidth * metrics.OffsetXRatio);

                var noteRect = new Rectangle(
                    noteX,
                    noteY, 
                    (int)MathF.Round(noteWidth),
                    (int)MathF.Round(noteHeight)
                );
                SpriteEffects effects = skinManager.FlipNote(keyCount, columnIndex, "", downScroll)
                    ? SpriteEffects.FlipVertically : SpriteEffects.None;
                spriteBatch.Draw(noteTexture, noteRect, null, Color.White * (float)alpha,
                    0f, Vector2.Zero, effects, 0f);
            }
            else
            {
                int padding = 4;
                int noteY = downScroll ? y - NOTE_HEIGHT : y;

                // Draw note body
                var noteRect = new Rectangle(
                    columnX + padding, 
                    noteY, 
                    columnWidth - padding * 2, 
                    NOTE_HEIGHT
                );

                Color noteColor = new Color(100, 180, 255) * (float)alpha;
                spriteBatch.Draw(pixel, noteRect, noteColor);

                // Draw top border
                var borderTop = new Rectangle(
                    columnX + padding,
                    noteY,
                    columnWidth - padding * 2,
                    BORDER_THICKNESS
                );
                spriteBatch.Draw(pixel, borderTop, Color.White * (float)alpha);

                // Draw bottom border
                var borderBottom = new Rectangle(
                    columnX + padding,
                    noteY + NOTE_HEIGHT - BORDER_THICKNESS,
                    columnWidth - padding * 2,
                    BORDER_THICKNESS
                );
                spriteBatch.Draw(pixel, borderBottom, Color.White * (float)alpha);
            }
        }

        public override void CheckForResult(double currentTime, ManiaHitWindows hitWindows)
        {
            if (IsJudged) return;

            // Only check for auto-miss during frame updates
            double offset = currentTime - HitObject.StartTime;
            if (offset > hitWindows.WindowFor(HitResult.Miss))
            {
                OnJudged(HitResult.Miss);
            }
        }
    }
}
