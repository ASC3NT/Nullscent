#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        public virtual void Update(double currentTime, double scrollSpeed, float receptorPosition, bool downScroll, int screenHeight)
        {
            // Calculate if object should be visible
            double timeUntilHit = HitObject.StartTime - currentTime;

            if (timeUntilHit <= TimePreempt && timeUntilHit >= -500)
            {
                _isVisible = true;

                // Calculate fade in alpha
                if (timeUntilHit > TimePreempt - TimeFadeIn)
                {
                    double fadeProgress = 1.0 - ((timeUntilHit - (TimePreempt - TimeFadeIn)) / TimeFadeIn);
                    _alpha = Math.Clamp(fadeProgress, 0, 1);
                }
                else
                {
                    _alpha = 1.0;
                }
            }
            else
            {
                _isVisible = false;
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
        protected int CalculateYPosition(double currentTime, double scrollSpeed, int receptorY, bool downScroll)
        {
            double timeDiff = HitObject.StartTime - currentTime;
            double distance = (timeDiff / 1000.0) * scrollSpeed;
            return downScroll ? receptorY - (int)distance : receptorY + (int)distance;
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

            int y = CalculateYPosition(currentTime, scrollSpeed, receptorY, downScroll);

            // Culling check
            if (y < -50 || y > screenHeight + 50) return;

            double alpha = GetAlpha();
            int padding = 4;
            int columnX = 0; // Will be set by column

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

            double offset = currentTime - HitObject.StartTime;
            var result = hitWindows.JudgeHit(offset);

            if (result != HitResult.None && result != HitResult.Miss)
            {
                OnJudged(result);
            }
            // Auto-miss check
            else if (currentTime > HitObject.StartTime + hitWindows.WindowFor(HitResult.Miss))
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

            double alpha = GetAlpha();
            int padding = 4;

            // Calculate head and tail positions
            double headTimeDiff = _holdNote.StartTime - currentTime;
            double tailTimeDiff = _holdNote.EndTime - currentTime;

            double headDistance = (headTimeDiff / 1000.0) * scrollSpeed;
            double tailDistance = (tailTimeDiff / 1000.0) * scrollSpeed;

            int headY = downScroll ? receptorY - (int)headDistance : receptorY + (int)headDistance;
            int tailY = downScroll ? receptorY - (int)tailDistance : receptorY + (int)tailDistance;

            int topY = Math.Min(headY, tailY);
            int bottomY = Math.Max(headY, tailY);

            // Culling
            if (bottomY < -50 || topY > screenHeight + 50) return;

            // Clamp to screen
            topY = Math.Max(-50, topY);
            bottomY = Math.Min(screenHeight + 50, bottomY);
            int bodyHeight = bottomY - topY;

            if (bodyHeight <= 0) return;

            // Draw body
            var bodyRect = new Rectangle(padding, topY, columnWidth - padding * 2, bodyHeight);
            Color bodyColor = new Color(255, 180, 100) * (float)(alpha * 0.7f);
            spriteBatch.Draw(pixel, bodyRect, bodyColor);

            // Draw left border
            var leftBorder = new Rectangle(padding, topY, 2, bodyHeight);
            spriteBatch.Draw(pixel, leftBorder, Color.White * (float)alpha);

            // Draw right border
            var rightBorder = new Rectangle(columnWidth - padding - 2, topY, 2, bodyHeight);
            spriteBatch.Draw(pixel, rightBorder, Color.White * (float)alpha);

            // Draw head if visible
            if (headY >= -50 && headY <= screenHeight + 50)
            {
                var headRect = new Rectangle(
                    padding,
                    headY - HEAD_HEIGHT / 2,
                    columnWidth - padding * 2,
                    HEAD_HEIGHT
                );
                Color headColor = new Color(255, 200, 100) * (float)alpha;
                spriteBatch.Draw(pixel, headRect, headColor);
            }

            // Draw tail if visible
            if (tailY >= -50 && tailY <= screenHeight + 50)
            {
                var tailRect = new Rectangle(
                    padding,
                    tailY - TAIL_HEIGHT / 2,
                    columnWidth - padding * 2,
                    TAIL_HEIGHT
                );
                Color tailColor = new Color(255, 160, 80) * (float)alpha;
                spriteBatch.Draw(pixel, tailRect, tailColor);
            }
        }

        public override void CheckForResult(double currentTime, ManiaHitWindows hitWindows)
        {
            if (IsJudged) return;

            // For hold notes, check if we're past the end time
            double offset = currentTime - _holdNote.StartTime;

            // Simplified: judge based on head timing for now
            // TODO: Implement proper LN judging (hold duration check)
            var result = hitWindows.JudgeHit(offset);

            if (result != HitResult.None && result != HitResult.Miss)
            {
                OnJudged(result);
            }
            // Auto-miss check
            else if (currentTime > _holdNote.StartTime + hitWindows.WindowFor(HitResult.Miss))
            {
                OnJudged(HitResult.Miss);
            }
        }
    }
}
