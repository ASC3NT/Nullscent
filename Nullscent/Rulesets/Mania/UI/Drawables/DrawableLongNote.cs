#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Skin;
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
        public bool IsHeadApplied { get; set; }
        public bool IsTailApplied { get; set; }


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

        public override void Update(double currentTime, double scrollSpeed, float receptorPosition, bool downScroll,
            int screenHeight, int? layoutReceptorY = null, int scrollTop = 0, int? scrollBottom = null)
        {
            // Guardar estados de fallo
            bool isBrokenOrMissed = _longNote.State == LongNoteState.Broken || 
                                    (_longNote.TailJudged && _longNote.TailResult == HitResult.Miss);

            if (isBrokenOrMissed)
            {
                // Forzar temporalmente false para que la clase base actualice su movimiento y visibilidad
                IsJudged = false;
            }

            // Actualizar visibilidad de la clase base
            base.Update(currentTime, scrollSpeed, receptorPosition, downScroll, screenHeight, layoutReceptorY, scrollTop, scrollBottom);

            // Determinar si la nota ya salió físicamente de los límites de la pantalla
            int receptorY = layoutReceptorY ?? (int)(downScroll ? screenHeight * receptorPosition : screenHeight * (1 - receptorPosition));

            double bottomTime = (_longNote.TailReleaseTime > 0) ? _longNote.TailReleaseTime : _longNote.StartTime;
            int headY = CalculateYPosition(bottomTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);
            int tailY = CalculateYPosition(_longNote.EndTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);
            int topY = Math.Min(headY, tailY);
            int bottomY = Math.Max(headY, tailY);
            bool isOutOfScreen = bottomY < -100 || topY > screenHeight + 100;

            if (_longNote.TailJudged)
            {
                if (_longNote.TailResult != HitResult.Miss)
                {
                    // Éxito: desaparece instantáneamente
                    IsJudged = true;
                    Result = CombineResults(_longNote.HeadResult, _longNote.TailResult);
                }
                else if (isOutOfScreen || currentTime > _longNote.EndTime + 500)
                {
                    // Fallo: sólo finaliza cuando sale de pantalla o pasa el tiempo límite
                    IsJudged = true;
                    Result = CombineResults(_longNote.HeadResult, _longNote.TailResult);
                }
            }
            else if (isOutOfScreen && currentTime > _longNote.EndTime)
            {
                // Salvaguarda: si por alguna razón no se juzgó pero ya salió, finalizarla
                IsJudged = true;
                Result = CombineResults(_longNote.HeadResult, HitResult.Miss);
            }
        }

        public override void Draw(SpriteBatch spriteBatch, Texture2D pixel, SkinManager? skinManager, int keyCount, int columnIndex, int columnX, double currentTime, 
            double scrollSpeed, int receptorY, bool downScroll, int screenHeight, int columnWidth, float layoutScale, float noteHeightScale)
        {
            if (IsJudged) return;

            // Calculate head and tail Y positions (use release time to prevent jumping/teleporting when released early)
            double bottomTime = (_longNote.TailReleaseTime > 0) ? _longNote.TailReleaseTime : _longNote.StartTime;
            int headY = CalculateYPosition(bottomTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);
            int tailY = CalculateYPosition(_longNote.EndTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);

            // Clampar la cabeza de la LN al receptor si está siendo sostenida (Holding) y no se ha soltado antes
            if (_longNote.State == LongNoteState.Holding && _longNote.HasHeldContinuously)
            {
                headY = receptorY;

                // Capping: prevent drawing the note past the receptor in the direction of scroll when holding past EndTime
                if (downScroll)
                {
                    headY = Math.Min(headY, receptorY);
                    tailY = Math.Min(tailY, receptorY);
                }
                else
                {
                    headY = Math.Max(headY, receptorY);
                    tailY = Math.Max(tailY, receptorY);
                }
            }

            // Obtener texturas de cabeza y cola para calcular su altura real
            ResolvedManiaLane? resolvedLane = skinManager != null && columnIndex >= 0 && columnIndex < skinManager.GetResolvedKeymode(keyCount).Lanes.Count
                ? skinManager.GetResolvedKeymode(keyCount).Lanes[columnIndex] : null;
            ResolvedSkinAnimation? headAnimation = resolvedLane?.LongHead;
            ResolvedSkinAnimation? tailAnimation = resolvedLane?.LongTail;
            Texture2D? headTexture = headAnimation?.FrameAt(currentTime);
            Texture2D? tailTexture = tailAnimation?.FrameAt(currentTime);
            SkinSpriteMetrics headMetrics = skinManager?.GetSpriteMetrics(keyCount, columnIndex, "head") ?? default;
            SkinSpriteMetrics tailMetrics = skinManager?.GetSpriteMetrics(keyCount, columnIndex, "tail") ?? default;
            SkinSpriteMetrics bodyMetrics = skinManager?.GetSpriteMetrics(keyCount, columnIndex, "body") ?? default;
            float headWidth = Math.Max(1f, columnWidth * (headMetrics.WidthRatio > 0 ? headMetrics.WidthRatio : 1f));
            float tailWidth = Math.Max(1f, columnWidth * (tailMetrics.WidthRatio > 0 ? tailMetrics.WidthRatio : 1f));
            float bodyWidth = Math.Max(1f, columnWidth * (bodyMetrics.WidthRatio > 0 ? bodyMetrics.WidthRatio : 1f));
            int headX = (int)MathF.Round(columnX + (columnWidth - headWidth) / 2f + columnWidth * headMetrics.OffsetXRatio);
            int tailX = (int)MathF.Round(columnX + (columnWidth - tailWidth) / 2f + columnWidth * tailMetrics.OffsetXRatio);
            int bodyX = (int)MathF.Round(columnX + (columnWidth - bodyWidth) / 2f + columnWidth * bodyMetrics.OffsetXRatio);

            float PieceHeight(Texture2D texture, SkinSpriteMetrics metrics, float drawWidth)
            {
                Vector2 logicalSize = skinManager!.GetLogicalSize(texture);
                float aspect = logicalSize.Y / logicalSize.X;
                return metrics.HeightMode switch
                {
                    SpriteHeightMode.ReferenceWidthAspect when metrics.ReferenceWidth > 0f => metrics.ReferenceWidth * layoutScale * aspect,
                    SpriteHeightMode.LaneWidthFraction => columnWidth * metrics.HeightValue,
                    _ => drawWidth * aspect
                } * noteHeightScale;
            }

            int headHeight = HEAD_HEIGHT;
            if (headTexture != null)
            {
                if (headTexture.Width <= 2 || headTexture.Height <= 2)
                {
                    headHeight = 0;
                }
                else
                {
                    headHeight = Math.Max(1, (int)MathF.Round(PieceHeight(headTexture, headMetrics, headWidth)));
                }
            }

            int tailHeight = TAIL_HEIGHT;
            if (tailTexture != null)
            {
                if (tailTexture.Width <= 2 || tailTexture.Height <= 2)
                {
                    tailHeight = 0;
                }
                else
                {
                    tailHeight = Math.Max(1, (int)MathF.Round(PieceHeight(tailTexture, tailMetrics, tailWidth)));
                }
            }
            else
            {
                tailHeight = 0;
            }

            // ALINEACIÓN DE CUERPO (osu!stable style):
            // El cuerpo se dibuja entre el extremo de la cola y el extremo de la cabeza (o receptor si está holding).
            // Dibujar el cuerpo cubriendo las notas evita saltos de posición al pulsar la cabeza.
            int bodyTopY = downScroll ? tailY : headY;
            int bodyBottomY = downScroll ? headY : tailY;

            if (_longNote.State == LongNoteState.Holding && _longNote.HasHeldContinuously)
            {
                if (downScroll) bodyBottomY = receptorY;
                else bodyTopY = receptorY;
            }

            // Capping: prevent the body from extending past the receptor in the direction of scroll
            if (downScroll)
            {
                bodyTopY = Math.Min(bodyTopY, receptorY);
                bodyBottomY = Math.Min(bodyBottomY, receptorY);
            }
            else
            {
                bodyTopY = Math.Max(bodyTopY, receptorY);
                bodyBottomY = Math.Max(bodyBottomY, receptorY);
            }

            int topY = Math.Min(bodyTopY, bodyBottomY);
            int bottomY = Math.Max(bodyTopY, bodyBottomY);

            // Culling check
            if (bottomY < -50 || topY > screenHeight + 50)
                return;

            // Clamp to screen
            int clampedTopY = Math.Max(-50, topY);
            int clampedBottomY = Math.Min(screenHeight + 50, bottomY);
            int bodyHeight = clampedBottomY - clampedTopY;

            if (bodyHeight > 0)
            {
                ResolvedSkinAnimation? bodyAnimation = resolvedLane?.LongBody;
                double animationTime = _longNote.State == LongNoteState.Holding
                    ? Math.Max(0, currentTime - _longNote.HeadHitTime) : currentTime;
                Texture2D? bodyTexture = bodyAnimation?.FrameAt(animationTime);
                if (bodyTexture != null)
                {
                    int totalLNHeight = bottomY - topY;
                    if (totalLNHeight > 0)
                    {
                        double stateAlphaMult = (_longNote.State == LongNoteState.Broken) ? 0.5 : 1.0;
                        int bodyStyle = skinManager?.GetNoteBodyStyle(keyCount, columnIndex) ?? 1;
                        bool flipBody = skinManager!.FlipNote(keyCount, columnIndex, "L", downScroll);

                        Vector2 bodySize = skinManager!.GetLogicalSize(bodyTexture);
                        // The texture may have been reduced for GPU upload. Its source
                        // coordinates are physical; its display height is still based on
                        // the original legacy image dimensions.
                        int blockH = (int)MathF.Round(bodySize.Y * (bodyWidth / bodySize.X) * noteHeightScale);
                        if (blockH <= 0) blockH = 1;
                        float sourcePixelsPerDisplayPixel = (float)bodyTexture.Height / blockH;

                        Color holdColor = Color.White;

                        if (IsHiddenActive)
                        {
                            int segmentHeight = 8;
                            for (int currY = clampedTopY; currY < clampedBottomY; currY += segmentHeight)
                            {
                                int h = Math.Min(segmentHeight, clampedBottomY - currY);
                                double bodyAlpha = GetHiddenAlpha(currY + h / 2, downScroll, receptorY, screenHeight, CurrentCombo) * stateAlphaMult;
                                if (bodyAlpha <= 0.0) continue;

                                var bodyRect = new Rectangle(bodyX, currY, (int)MathF.Round(bodyWidth), h);
                                SpriteEffects effects;
                                Rectangle srcRect = GetSourceRectForSegment(currY, h, topY, bottomY, totalLNHeight, bodyStyle, bodyTexture, sourcePixelsPerDisplayPixel, blockH, downScroll, flipBody, out effects);

                                spriteBatch.Draw(bodyTexture, bodyRect, srcRect, holdColor * (float)bodyAlpha, 0f, Vector2.Zero, effects, 0f);
                            }
                        }
                        else
                        {
                            double bodyAlpha = GetHiddenAlpha((clampedTopY + clampedBottomY) / 2, downScroll, receptorY, screenHeight, CurrentCombo) * stateAlphaMult;
                            if (bodyAlpha > 0.0)
                            {
                                if (bodyStyle == 0) // Stretch
                                {
                                    var bodyRect = new Rectangle(bodyX, clampedTopY, (int)MathF.Round(bodyWidth), bodyHeight);
                                    var srcRect = GetSourceRectForSegment(clampedTopY, bodyHeight, topY, bottomY,
                                        totalLNHeight, bodyStyle, bodyTexture, sourcePixelsPerDisplayPixel, blockH,
                                        downScroll, flipBody, out SpriteEffects effects);
                                    spriteBatch.Draw(bodyTexture, bodyRect, srcRect, holdColor * (float)bodyAlpha,
                                        0f, Vector2.Zero, effects, 0f);
                                }
                                else // Tiled (style 1 or 2)
                                {
                                    bool tileFromTail = (bodyStyle == 1);

                                    if (downScroll)
                                    {
                                        if (tileFromTail)
                                        {
                                            for (int currY = bodyTopY; currY < bodyBottomY; currY += blockH)
                                            {
                                                int drawH = Math.Min(blockH, bodyBottomY - currY);
                                                int drawY = currY;

                                                if (drawY + drawH < clampedTopY || drawY > clampedBottomY)
                                                    continue;

                                                int finalY = Math.Max(clampedTopY, drawY);
                                                int finalH = Math.Min(clampedBottomY, drawY + drawH) - finalY;
                                                if (finalH <= 0) continue;

                                                int cropTop = finalY - drawY;
                                                int cropBottom = (drawY + drawH) - (finalY + finalH);

                                                int srcY = (int)(cropTop * sourcePixelsPerDisplayPixel);
                                                int srcH = (int)MathF.Ceiling(finalH * sourcePixelsPerDisplayPixel);
                                                srcY = Math.Clamp(srcY, 0, bodyTexture.Height - 1);
                                                srcH = Math.Clamp(srcH, 1, bodyTexture.Height - srcY);

                                                var blockRect = new Rectangle(bodyX, finalY, (int)MathF.Round(bodyWidth), finalH);
                                                var srcRectBlock = new Rectangle(0, srcY, bodyTexture.Width, srcH);
                                                spriteBatch.Draw(bodyTexture, blockRect, srcRectBlock, holdColor * (float)bodyAlpha);
                                            }
                                        }
                                        else
                                        {
                                            for (int currY = bodyBottomY; currY > bodyTopY; currY -= blockH)
                                            {
                                                int drawH = Math.Min(blockH, currY - bodyTopY);
                                                int drawY = currY - drawH;

                                                if (drawY + drawH < clampedTopY || drawY > clampedBottomY)
                                                    continue;

                                                int finalY = Math.Max(clampedTopY, drawY);
                                                int finalH = Math.Min(clampedBottomY, drawY + drawH) - finalY;
                                                if (finalH <= 0) continue;

                                                int cropTop = finalY - drawY;
                                                int cropBottom = (drawY + drawH) - (finalY + finalH);

                                                int srcY = (int)(cropBottom * sourcePixelsPerDisplayPixel);
                                                int srcH = (int)MathF.Ceiling(finalH * sourcePixelsPerDisplayPixel);
                                                srcY = Math.Clamp(srcY, 0, bodyTexture.Height - 1);
                                                srcH = Math.Clamp(srcH, 1, bodyTexture.Height - srcY);

                                                var blockRect = new Rectangle(bodyX, finalY, (int)MathF.Round(bodyWidth), finalH);
                                                var srcRectBlock = new Rectangle(0, srcY, bodyTexture.Width, srcH);
                                                spriteBatch.Draw(bodyTexture, blockRect, srcRectBlock, holdColor * (float)bodyAlpha);
                                            }
                                        }
                                    }
                                    else // upScroll
                                    {
                                        if (tileFromTail)
                                        {
                                            for (int currY = bodyBottomY; currY > bodyTopY; currY -= blockH)
                                            {
                                                int drawH = Math.Min(blockH, currY - bodyTopY);
                                                int drawY = currY - drawH;

                                                if (drawY + drawH < clampedTopY || drawY > clampedBottomY)
                                                    continue;

                                                int finalY = Math.Max(clampedTopY, drawY);
                                                int finalH = Math.Min(clampedBottomY, drawY + drawH) - finalY;
                                                if (finalH <= 0) continue;

                                                int cropTop = finalY - drawY;
                                                int cropBottom = (drawY + drawH) - (finalY + finalH);

                                                int srcY = flipBody
                                                    ? (int)(cropBottom * sourcePixelsPerDisplayPixel)
                                                    : bodyTexture.Height - (int)MathF.Ceiling((cropBottom + finalH) * sourcePixelsPerDisplayPixel);
                                                int srcH = (int)MathF.Ceiling(finalH * sourcePixelsPerDisplayPixel);
                                                srcY = Math.Clamp(srcY, 0, bodyTexture.Height - 1);
                                                srcH = Math.Clamp(srcH, 1, bodyTexture.Height - srcY);

                                                var blockRect = new Rectangle(bodyX, finalY, (int)MathF.Round(bodyWidth), finalH);
                                                var srcRectBlock = new Rectangle(0, srcY, bodyTexture.Width, srcH);
                                                spriteBatch.Draw(bodyTexture, blockRect, srcRectBlock, holdColor * (float)bodyAlpha,
                                                    0f, Vector2.Zero, flipBody ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
                                            }
                                        }
                                        else
                                        {
                                            for (int currY = bodyTopY; currY < bodyBottomY; currY += blockH)
                                            {
                                                int drawH = Math.Min(blockH, bodyBottomY - currY);
                                                int drawY = currY;

                                                if (drawY + drawH < clampedTopY || drawY > clampedBottomY)
                                                    continue;

                                                int finalY = Math.Max(clampedTopY, drawY);
                                                int finalH = Math.Min(clampedBottomY, drawY + drawH) - finalY;
                                                if (finalH <= 0) continue;

                                                int cropTop = finalY - drawY;
                                                int cropBottom = (drawY + drawH) - (finalY + finalH);

                                                int srcY = (int)(cropTop * sourcePixelsPerDisplayPixel);
                                                int srcH = (int)MathF.Ceiling(finalH * sourcePixelsPerDisplayPixel);
                                                srcY = Math.Clamp(srcY, 0, bodyTexture.Height - 1);
                                                srcH = Math.Clamp(srcH, 1, bodyTexture.Height - srcY);

                                                var blockRect = new Rectangle(bodyX, finalY, (int)MathF.Round(bodyWidth), finalH);
                                                var srcRectBlock = new Rectangle(0, srcY, bodyTexture.Width, srcH);
                                                spriteBatch.Draw(bodyTexture, blockRect, srcRectBlock, holdColor * (float)bodyAlpha,
                                                    0f, Vector2.Zero, flipBody ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    double stateAlphaMult = (_longNote.State == LongNoteState.Broken) ? 0.5 : 1.0;
                    Color baseBodyColor = GetBodyColor();

                    if (IsHiddenActive)
                    {
                        // Draw body in segments to allow gradual fade out for Hidden
                        int segmentHeight = 8;

                        for (int currY = clampedTopY; currY < clampedBottomY; currY += segmentHeight)
                        {
                            int h = Math.Min(segmentHeight, clampedBottomY - currY);
                            double bodyAlpha = GetHiddenAlpha(currY + h / 2, downScroll, receptorY, screenHeight, CurrentCombo) * stateAlphaMult;
                            if (bodyAlpha <= 0.0) continue;

                            var bodyRect = new Rectangle(columnX + PADDING, currY, columnWidth - PADDING * 2, h);
                            spriteBatch.Draw(pixel, bodyRect, baseBodyColor * (float)(bodyAlpha * 0.7f));

                            // Draw left border segment
                            var leftBorder = new Rectangle(columnX + PADDING, currY, 2, h);
                            spriteBatch.Draw(pixel, leftBorder, COLOR_BORDER * (float)bodyAlpha);

                            // Draw right border segment
                            var rightBorder = new Rectangle(columnX + columnWidth - PADDING - 2, currY, 2, h);
                            spriteBatch.Draw(pixel, rightBorder, COLOR_BORDER * (float)bodyAlpha);
                        }
                    }
                    else
                    {
                        // Single rectangle draw for standard gameplay (massive optimization!)
                        double bodyAlpha = GetHiddenAlpha((clampedTopY + clampedBottomY) / 2, downScroll, receptorY, screenHeight, CurrentCombo) * stateAlphaMult;
                        if (bodyAlpha > 0.0)
                        {
                            var bodyRect = new Rectangle(columnX + PADDING, clampedTopY, columnWidth - PADDING * 2, bodyHeight);
                            spriteBatch.Draw(pixel, bodyRect, baseBodyColor * (float)(bodyAlpha * 0.7f));

                            // Draw left border
                            var leftBorder = new Rectangle(columnX + PADDING, clampedTopY, 2, bodyHeight);
                            spriteBatch.Draw(pixel, leftBorder, COLOR_BORDER * (float)bodyAlpha);

                            // Draw right border
                            var rightBorder = new Rectangle(columnX + columnWidth - PADDING - 2, clampedTopY, 2, bodyHeight);
                            spriteBatch.Draw(pixel, rightBorder, COLOR_BORDER * (float)bodyAlpha);
                        }
                    }
                }
            }

            // Draw head (if not judged yet OR if note is broken and scrolling down)
            if ((!_longNote.HeadJudged || _longNote.State == LongNoteState.Broken) && headY >= -50 && headY <= screenHeight + 50)
            {
                double headAlpha = GetHiddenAlpha(headY, downScroll, receptorY, screenHeight, CurrentCombo);
                if (_longNote.State == LongNoteState.Broken)
                    headAlpha *= 0.5;

                if (headAlpha > 0.0)
                {
                    if (headTexture != null)
                    {
                        Texture2D? headFrame = headAnimation?.FrameAt(currentTime) ?? headTexture;
                        int headRenderY = (int)MathF.Round(downScroll ? headY - headHeight + headMetrics.OffsetY * layoutScale : headY + headMetrics.OffsetY * layoutScale);
                        var headRect = new Rectangle(
                            headX,
                            headRenderY,
                            (int)MathF.Round(headWidth),
                            headHeight
                        );
                        SpriteEffects effects = skinManager!.FlipNote(keyCount, columnIndex, "H", downScroll)
                            ? SpriteEffects.FlipVertically : SpriteEffects.None;
                        spriteBatch.Draw(headFrame, headRect, null, Color.White * (float)headAlpha,
                            0f, Vector2.Zero, effects, 0f);
                    }
                    else
                    {
                        int headRenderY = downScroll ? headY - HEAD_HEIGHT : headY;
                        var headRect = new Rectangle(
                            columnX + PADDING,
                            headRenderY,
                            columnWidth - PADDING * 2,
                            HEAD_HEIGHT
                        );
                        spriteBatch.Draw(pixel, headRect, COLOR_HEAD * (float)headAlpha);

                        // Head border
                        var headBorder = new Rectangle(columnX + PADDING, headRenderY, columnWidth - PADDING * 2, 2);
                        spriteBatch.Draw(pixel, headBorder, COLOR_BORDER * (float)headAlpha);
                    }
                }
            }

            // Draw tail (if not judged yet OR if note is broken and scrolling down)
            if ((!_longNote.TailJudged || _longNote.State == LongNoteState.Broken) && tailY >= -50 && tailY <= screenHeight + 50)
            {
                double tailAlpha = GetHiddenAlpha(tailY, downScroll, receptorY, screenHeight, CurrentCombo);
                if (_longNote.State == LongNoteState.Broken)
                    tailAlpha *= 0.5;

                if (tailAlpha > 0.0)
                {
                    if (tailTexture != null && tailHeight > 0)
                    {
                        Texture2D? tailFrame = tailAnimation?.FrameAt(currentTime) ?? tailTexture;
                        int tailRenderY = (int)MathF.Round(downScroll ? tailY + tailMetrics.OffsetY * layoutScale : tailY - tailHeight + tailMetrics.OffsetY * layoutScale);
                        var tailRect = new Rectangle(
                            tailX,
                            tailRenderY,
                            (int)MathF.Round(tailWidth),
                            tailHeight
                        );
                        SpriteEffects effects = skinManager!.FlipNote(keyCount, columnIndex, "T", downScroll)
                            ? SpriteEffects.FlipVertically : SpriteEffects.None;
                        spriteBatch.Draw(tailFrame, tailRect, null, Color.White * (float)tailAlpha,
                            0f, Vector2.Zero, effects, 0f);
                    }
                    else if (tailTexture == null)
                    {
                        int tailRenderY = downScroll ? tailY : tailY - TAIL_HEIGHT;
                        var tailRect = new Rectangle(
                            columnX + PADDING,
                            tailRenderY,
                            columnWidth - PADDING * 2,
                            TAIL_HEIGHT
                        );
                        spriteBatch.Draw(pixel, tailRect, COLOR_TAIL * (float)tailAlpha);
                    }
                }
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

        private Rectangle GetSourceRectForSegment(int currY, int h, int bodyTopY, int bodyBottomY, int totalLNHeight, int bodyStyle, Texture2D bodyTexture, float sourcePixelsPerDisplayPixel, int blockH, bool downScroll, bool flipBody, out SpriteEffects effects)
        {
            effects = flipBody ? SpriteEffects.FlipVertically : SpriteEffects.None;

            if (totalLNHeight <= 0)
                return new Rectangle(0, 0, bodyTexture.Width, bodyTexture.Height);

            if (bodyStyle == 0) // Stretch
            {
                if (!flipBody)
                {
                    float startPct = (float)(currY - bodyTopY) / totalLNHeight;
                    float endPct = (float)(currY + h - bodyTopY) / totalLNHeight;
                    
                    int srcY = (int)(startPct * bodyTexture.Height);
                    int srcH = (int)((endPct - startPct) * bodyTexture.Height);
                    srcY = Math.Clamp(srcY, 0, bodyTexture.Height - 1);
                    srcH = Math.Clamp(srcH, 1, bodyTexture.Height - srcY);
                    return new Rectangle(0, srcY, bodyTexture.Width, srcH);
                }
                else // upscroll (flipped)
                {
                    float startPct = (float)(bodyBottomY - (currY + h)) / totalLNHeight;
                    float endPct = (float)(bodyBottomY - currY) / totalLNHeight;
                    
                    int srcY = (int)(startPct * bodyTexture.Height);
                    int srcH = (int)((endPct - startPct) * bodyTexture.Height);
                    srcY = Math.Clamp(srcY, 0, bodyTexture.Height - 1);
                    srcH = Math.Clamp(srcH, 1, bodyTexture.Height - srcY);
                    return new Rectangle(0, srcY, bodyTexture.Width, srcH);
                }
            }
            else // Tiled (1 or 2)
            {
                bool tileFromTail = (bodyStyle == 1);
                int srcY;
                
                if (downScroll)
                {
                    if (tileFromTail)
                    {
                        int offsetY = Math.Max(0, currY - bodyTopY);
                        srcY = (int)((offsetY % blockH) * sourcePixelsPerDisplayPixel);
                    }
                    else // tile from head
                    {
                        int offsetY = Math.Max(0, bodyBottomY - (currY + h));
                        srcY = (int)((offsetY % blockH) * sourcePixelsPerDisplayPixel);
                    }
                }
                else // upscroll
                {
                    if (tileFromTail)
                    {
                        int offsetY = Math.Max(0, bodyBottomY - (currY + h));
                        srcY = flipBody
                            ? (int)((offsetY % blockH) * sourcePixelsPerDisplayPixel)
                            : bodyTexture.Height - (int)MathF.Ceiling(((offsetY % blockH) + h) * sourcePixelsPerDisplayPixel);
                    }
                    else // tile from head
                    {
                        int offsetY = Math.Max(0, currY - bodyTopY);
                        int offsetInBlock = offsetY % blockH;
                        srcY = (int)((flipBody ? blockH - offsetInBlock - h : offsetInBlock) * sourcePixelsPerDisplayPixel);
                    }
                }

                int srcH = (int)(h * sourcePixelsPerDisplayPixel);
                srcY = Math.Clamp(srcY, 0, bodyTexture.Height - 1);
                srcH = Math.Clamp(srcH, 1, bodyTexture.Height - srcY);
                return new Rectangle(0, srcY, bodyTexture.Width, srcH);
            }
        }
    }
}
