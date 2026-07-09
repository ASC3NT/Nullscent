#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Beatmap;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using System;

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

        protected DrawableManiaHitObject(ManiaHitObject hitObject)
        {
            HitObject = hitObject;
        }

        /// <summary>
        /// Updates the drawable state based on current time.
        /// </summary>
        public virtual void Update(double currentTime, double scrollSpeedSetting, float receptorPosition, bool downScroll, int screenHeight)
        {
            double scrollTime = 2000.0 - (scrollSpeedSetting - 1.0) * 45.0;
            scrollTime = Math.Clamp(scrollTime, 150.0, 2000.0);

            TimePreempt = scrollTime;
            TimeFadeIn = Math.Min(200.0, scrollTime * 0.2);

            int receptorY = (int)(screenHeight * receptorPosition);
            if (!downScroll)
            {
                receptorY = (int)(screenHeight * (1 - receptorPosition));
            }

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
        public abstract void Draw(SpriteBatch spriteBatch, Texture2D pixel, double currentTime, 
            double scrollSpeed, int receptorY, bool downScroll, int screenHeight, int columnWidth);

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
                return receptorY - (int)(ratio * receptorY);
            }
            else
            {
                double ratio = timeDiff / scrollTime;
                return receptorY + (int)(ratio * (screenHeight - receptorY));
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

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel, double currentTime, 
            double scrollSpeed, int receptorY, bool downScroll, int screenHeight, int columnWidth)
        {
            if (!IsVisible() || IsJudged) return;

            int y = CalculateYPosition(HitObject.StartTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);

            // Culling check
            if (y < -50 || y > screenHeight + 50) return;

            double alpha = GetHiddenAlpha(y, downScroll, receptorY, screenHeight, CurrentCombo);
            if (alpha <= 0.0) return;

            int padding = 4;
            // Draw note body
            var noteRect = new Rectangle(
                padding, 
                y - NOTE_HEIGHT / 2, 
                columnWidth - padding * 2, 
                NOTE_HEIGHT
            );

            Color noteColor = new Color(100, 180, 255) * (float)alpha;
            spriteBatch.Draw(pixel, noteRect, noteColor);

            // Draw top border
            var borderTop = new Rectangle(
                padding,
                y - NOTE_HEIGHT / 2,
                columnWidth - padding * 2,
                BORDER_THICKNESS
            );
            spriteBatch.Draw(pixel, borderTop, Color.White * (float)alpha);

            // Draw bottom border
            var borderBottom = new Rectangle(
                padding,
                y + NOTE_HEIGHT / 2 - BORDER_THICKNESS,
                columnWidth - padding * 2,
                BORDER_THICKNESS
            );
            spriteBatch.Draw(pixel, borderBottom, Color.White * (float)alpha);
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

    /// <summary>
    /// Drawable representation of a hold note (LN).
    /// </summary>
    public class DrawableHoldNote : DrawableManiaHitObject
    {
        private readonly HoldNote _holdNote;
        private const int HEAD_HEIGHT = 12;
        private const int TAIL_HEIGHT = 6;

        public DrawableHoldNote(HoldNote holdNote) : base(holdNote)
        {
            _holdNote = holdNote;
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel, double currentTime, 
            double scrollSpeed, int receptorY, bool downScroll, int screenHeight, int columnWidth)
        {
            if (!IsVisible() || IsJudged) return;

            int padding = 4;

            // Calculate head and tail positions
            int headY = CalculateYPosition(_holdNote.StartTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);
            int tailY = CalculateYPosition(_holdNote.EndTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);

            int topY = Math.Min(headY, tailY);
            int bottomY = Math.Max(headY, tailY);

            // Culling
            if (bottomY < -50 || topY > screenHeight + 50) return;

            // Clamp to screen
            int clampedTopY = Math.Max(-50, topY);
            int clampedBottomY = Math.Min(screenHeight + 50, bottomY);
            int bodyHeight = clampedBottomY - clampedTopY;

            if (bodyHeight > 0)
            {
                // Draw body in segments to allow gradual fade out for Hidden
                int segmentHeight = 8;
                Color baseBodyColor = new Color(255, 180, 100);

                for (int currY = clampedTopY; currY < clampedBottomY; currY += segmentHeight)
                {
                    int h = Math.Min(segmentHeight, clampedBottomY - currY);
                    double bodyAlpha = GetHiddenAlpha(currY + h / 2, downScroll, receptorY, screenHeight, CurrentCombo);
                    if (bodyAlpha <= 0.0) continue;

                    var bodyRect = new Rectangle(padding, currY, columnWidth - padding * 2, h);
                    spriteBatch.Draw(pixel, bodyRect, baseBodyColor * (float)(bodyAlpha * 0.7f));

                    // Draw left border segment
                    var leftBorder = new Rectangle(padding, currY, 2, h);
                    spriteBatch.Draw(pixel, leftBorder, Color.White * (float)bodyAlpha);

                    // Draw right border segment
                    var rightBorder = new Rectangle(columnWidth - padding - 2, currY, 2, h);
                    spriteBatch.Draw(pixel, rightBorder, Color.White * (float)bodyAlpha);
                }
            }

            // Draw head if visible
            if (headY >= -50 && headY <= screenHeight + 50)
            {
                double headAlpha = GetHiddenAlpha(headY, downScroll, receptorY, screenHeight, CurrentCombo);
                if (headAlpha > 0.0)
                {
                    var headRect = new Rectangle(
                        padding,
                        headY - HEAD_HEIGHT / 2,
                        columnWidth - padding * 2,
                        HEAD_HEIGHT
                    );
                    Color headColor = new Color(255, 200, 100) * (float)headAlpha;
                    spriteBatch.Draw(pixel, headRect, headColor);
                }
            }

            // Draw tail if visible
            if (tailY >= -50 && tailY <= screenHeight + 50)
            {
                double tailAlpha = GetHiddenAlpha(tailY, downScroll, receptorY, screenHeight, CurrentCombo);
                if (tailAlpha > 0.0)
                {
                    var tailRect = new Rectangle(
                        padding,
                        tailY - TAIL_HEIGHT / 2,
                        columnWidth - padding * 2,
                        TAIL_HEIGHT
                    );
                    Color tailColor = new Color(255, 160, 80) * (float)tailAlpha;
                    spriteBatch.Draw(pixel, tailRect, tailColor);
                }
            }
        }

        public override void CheckForResult(double currentTime, ManiaHitWindows hitWindows)
        {
            if (IsJudged) return;

            // Only check for auto-miss during frame updates
            double offset = currentTime - _holdNote.StartTime;
            if (offset > hitWindows.WindowFor(HitResult.Miss))
            {
                OnJudged(HitResult.Miss);
            }
        }
    }
}
