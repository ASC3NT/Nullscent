#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using System;

namespace Nullscent.Rulesets.Mania.UI.Drawables
{
    /// <summary>
    /// Drawable for Long Notes (LN) with ScoreV2-style head+tail separation.
    /// 
    /// Rendering behavior:
    /// - Head: Visible until judged
    /// - Body: Visible while holding OR broken
    /// - Tail: Visible until judged
    /// - Receptor lighting: Active during hold
    /// 
    /// Visual feedback:
    /// - Holding: Body highlighted
    /// - Broken: Body color changes (red tint)
    /// - Head judged: Head disappears
    /// - Tail judged: Entire LN disappears
    /// 
    /// Deterministic: Uses song time, not frame delta.
    /// </summary>
    public class DrawableLongNote : DrawableManiaHitObject
    {
        private readonly LongNote _longNote;

        // Visual constants
        private const int HEAD_HEIGHT = 12;
        private const int TAIL_HEIGHT = 6;
        private const int PADDING = 4;

        // Colors
        private static readonly Color COLOR_BODY_NORMAL = new Color(255, 180, 100);
        private static readonly Color COLOR_BODY_HOLDING = new Color(100, 255, 150);
        private static readonly Color COLOR_BODY_BROKEN = new Color(255, 100, 100);
        private static readonly Color COLOR_HEAD = new Color(255, 200, 100);
        private static readonly Color COLOR_TAIL = new Color(255, 160, 80);
        private static readonly Color COLOR_BORDER = Color.White;

        public DrawableLongNote(LongNote longNote) : base(longNote)
        {
            _longNote = longNote;
        }

        public override void Update(double currentTime, double scrollSpeed, float receptorPosition, bool downScroll, int screenHeight)
        {
            // Base visibility update
            base.Update(currentTime, scrollSpeed, receptorPosition, downScroll, screenHeight);

            // LN is finished when both head and tail are judged
            if (_longNote.IsFullyJudged)
            {
                IsJudged = true;

                // Set result based on head+tail average (for effects)
                // In actual scoring, head and tail are counted separately
                Result = CombineResults(_longNote.HeadResult, _longNote.TailResult);
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel, double currentTime, 
            double scrollSpeed, int receptorY, bool downScroll, int screenHeight, int columnWidth)
        {
            if (IsJudged) return;

            double alpha = GetAlpha();

            // Calculate head and tail Y positions
            double headTimeDiff = _longNote.StartTime - currentTime;
            double tailTimeDiff = _longNote.EndTime - currentTime;

            double headDistance = (headTimeDiff / 1000.0) * scrollSpeed;
            double tailDistance = (tailTimeDiff / 1000.0) * scrollSpeed;

            int headY = downScroll ? receptorY - (int)headDistance : receptorY + (int)headDistance;
            int tailY = downScroll ? receptorY - (int)tailDistance : receptorY + (int)tailDistance;

            int topY = Math.Min(headY, tailY);
            int bottomY = Math.Max(headY, tailY);

            // Culling check
            if (bottomY < -50 || topY > screenHeight + 50)
                return;

            // Clamp to screen
            topY = Math.Max(-50, topY);
            bottomY = Math.Min(screenHeight + 50, bottomY);
            int bodyHeight = bottomY - topY;

            if (bodyHeight <= 0) return;

            // Determine body color based on state
            Color bodyColor = GetBodyColor() * (float)(alpha * 0.7f);

            // Draw body
            var bodyRect = new Rectangle(PADDING, topY, columnWidth - PADDING * 2, bodyHeight);
            spriteBatch.Draw(pixel, bodyRect, bodyColor);

            // Draw left border
            var leftBorder = new Rectangle(PADDING, topY, 2, bodyHeight);
            spriteBatch.Draw(pixel, leftBorder, COLOR_BORDER * (float)alpha);

            // Draw right border
            var rightBorder = new Rectangle(columnWidth - PADDING - 2, topY, 2, bodyHeight);
            spriteBatch.Draw(pixel, rightBorder, COLOR_BORDER * (float)alpha);

            // Draw head (if not judged yet)
            if (!_longNote.HeadJudged && headY >= -50 && headY <= screenHeight + 50)
            {
                var headRect = new Rectangle(
                    PADDING,
                    headY - HEAD_HEIGHT / 2,
                    columnWidth - PADDING * 2,
                    HEAD_HEIGHT
                );
                spriteBatch.Draw(pixel, headRect, COLOR_HEAD * (float)alpha);

                // Head border
                var headBorder = new Rectangle(PADDING, headY - HEAD_HEIGHT / 2, columnWidth - PADDING * 2, 2);
                spriteBatch.Draw(pixel, headBorder, COLOR_BORDER * (float)alpha);
            }

            // Draw tail (if not judged yet)
            if (!_longNote.TailJudged && tailY >= -50 && tailY <= screenHeight + 50)
            {
                var tailRect = new Rectangle(
                    PADDING,
                    tailY - TAIL_HEIGHT / 2,
                    columnWidth - PADDING * 2,
                    TAIL_HEIGHT
                );
                spriteBatch.Draw(pixel, tailRect, COLOR_TAIL * (float)alpha);
            }
        }

        public override void CheckForResult(double currentTime, ManiaHitWindows hitWindows)
        {
            // This is called when key is pressed
            // Only judge head here
            if (!_longNote.HeadJudged && _longNote.State == LongNoteState.Idle)
            {
                bool headHit = _longNote.TryJudgeHead(currentTime, hitWindows);

                if (headHit && _longNote.HeadResult != HitResult.Miss)
                {
                    // Head hit successfully, now in holding state
                    // Don't set Result yet, wait for tail
                }
            }
        }

        /// <summary>
        /// Called when key is released.
        /// </summary>
        public void OnKeyRelease(double currentTime, ManiaHitWindows hitWindows)
        {
            if (_longNote.State == LongNoteState.Idle)
                return; // Can't release before head is hit

            _longNote.TryJudgeTail(currentTime, hitWindows);
        }

        /// <summary>
        /// Update hold state. Call every frame during gameplay.
        /// </summary>
        public void UpdateHold(double currentTime, bool isKeyHeld)
        {
            _longNote.UpdateHold(currentTime, isKeyHeld);
        }

        /// <summary>
        /// Check for auto-misses.
        /// </summary>
        public void CheckAutoMiss(double currentTime, ManiaHitWindows hitWindows)
        {
            _longNote.CheckHeadAutoMiss(currentTime, hitWindows);
            _longNote.CheckTailAutoMiss(currentTime, hitWindows);
        }

        /// <summary>
        /// Returns true if receptor should be lit (during hold).
        /// </summary>
        public bool ShouldLightReceptor()
        {
            return _longNote.IsHolding;
        }

        private Color GetBodyColor()
        {
            return _longNote.State switch
            {
                LongNoteState.Holding => COLOR_BODY_HOLDING,
                LongNoteState.Broken => COLOR_BODY_BROKEN,
                _ => COLOR_BODY_NORMAL
            };
        }

        private HitResult CombineResults(HitResult head, HitResult tail)
        {
            // Simple average for visual feedback
            // Actual scoring uses head and tail separately
            if (head == HitResult.Miss || tail == HitResult.Miss)
                return HitResult.Miss;

            int headValue = (int)head;
            int tailValue = (int)tail;
            int average = (headValue + tailValue) / 2;

            return (HitResult)average;
        }
    }
}
