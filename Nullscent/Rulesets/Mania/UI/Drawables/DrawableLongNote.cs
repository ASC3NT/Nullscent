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
    /// - Head: Remains at the receptor while a successful hold is active
    /// - Body: Visible while holding OR broken
    /// - Tail: Visible until judged
    /// - Receptor lighting: Active during hold
    /// 
    /// Visual feedback:
    /// - Holding: Body highlighted
    /// - Broken: Body color changes (red tint)
    /// - Head judged: Head remains visible during the hold and after a break
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
#if DEBUG
            bool traceFrame = LongNoteRenderDiagnostics.ShouldTraceFrame(_longNote, currentTime);
#endif

            // Calculate head and tail Y positions (use release time to prevent jumping/teleporting when released early)
            double bottomTime = (_longNote.TailReleaseTime > 0) ? _longNote.TailReleaseTime : _longNote.StartTime;
            int headY = CalculateYPosition(bottomTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);
            int tailY = CalculateYPosition(_longNote.EndTime, currentTime, scrollSpeed, receptorY, downScroll, screenHeight);
            int rawHeadY = headY;
            int rawTailY = tailY;
            bool headClamped = false;
            bool tailClamped = false;
            Rectangle? bodyDebugRect = null;
            Rectangle? tailDebugRect = null;
            Rectangle? headDebugRect = null;

            // Clampar la cabeza de la LN al receptor si está siendo sostenida (Holding) y no se ha soltado antes
            if (_longNote.State == LongNoteState.Holding && _longNote.HasHeldContinuously)
            {
                headY = receptorY;
                headClamped = headY != rawHeadY;

                // Capping: prevent drawing the note past the receptor in the direction of scroll when holding past EndTime
                if (downScroll)
                {
                    headY = Math.Min(headY, receptorY);
                    int beforeTailClamp = tailY;
                    tailY = Math.Min(tailY, receptorY);
                    tailClamped = tailY != beforeTailClamp;
                }
                else
                {
                    headY = Math.Max(headY, receptorY);
                    int beforeTailClamp = tailY;
                    tailY = Math.Max(tailY, receptorY);
                    tailClamped = tailY != beforeTailClamp;
                }
            }

            // Obtener texturas de cabeza y cola para calcular su altura real
            ResolvedManiaKeymode? resolvedMode = skinManager?.GetResolvedKeymode(keyCount);
            ResolvedManiaLane? resolvedLane = resolvedMode != null && columnIndex >= 0 && columnIndex < resolvedMode.Lanes.Count
                ? resolvedMode.Lanes[columnIndex] : null;
            bool tailMaskedAtReceptor = resolvedMode?.Definition.ClipHoldTailAtReceptorWhileHolding == true
                                        && _longNote.State == LongNoteState.Holding
                                        && _longNote.HeadJudged && _longNote.HeadResult != HitResult.Miss;
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
#if DEBUG
            if (skinManager != null && resolvedMode != null && resolvedLane != null)
                LongNoteRenderDiagnostics.TraceResolvedPieces(_longNote, keyCount, columnIndex, downScroll,
                    skinManager, resolvedMode, resolvedLane, headAnimation, resolvedLane.LongBody, tailAnimation,
                    headMetrics, bodyMetrics, tailMetrics);
#endif
            int headX = (int)MathF.Round(columnX + (columnWidth - headWidth) / 2f + columnWidth * headMetrics.OffsetXRatio);
            int tailX = (int)MathF.Round(columnX + (columnWidth - tailWidth) / 2f + columnWidth * tailMetrics.OffsetXRatio);
            int bodyX = (int)MathF.Round(columnX + (columnWidth - bodyWidth) / 2f + columnWidth * bodyMetrics.OffsetXRatio);

            float PieceHeight(Texture2D texture, SkinSpriteMetrics metrics, float drawWidth)
            {
                Vector2 logicalSize = skinManager!.GetLogicalSize(texture);
                return LongNoteRenderRules.GetPieceHeight(metrics, logicalSize.X, logicalSize.Y,
                    drawWidth, columnWidth, layoutScale, noteHeightScale);
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

            // The keymode supplies overlap ratios for cap/body assembly. Legacy uses
            // edge joins; native families can extend the body under transparent caps.
            ManiaHoldAssemblyDefinition assembly = resolvedMode?.Definition.HoldAssembly ?? ManiaHoldAssemblyDefinition.Default;
            (int bodyTopY, int bodyBottomY) = LongNoteRenderRules.GetBodySpan(
                headY, tailY, headHeight, tailHeight, downScroll,
                assembly.HeadConnectionRatio, assembly.TailConnectionRatio);
            int bodyOffsetY = (int)MathF.Round(bodyMetrics.OffsetY * layoutScale);
            (bodyTopY, bodyBottomY) = LongNoteRenderRules.OffsetBodySpan(bodyTopY, bodyBottomY, bodyOffsetY);
            int rawBodyTopY = bodyTopY;
            int rawBodyBottomY = bodyBottomY;

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
                bodyDebugRect = new Rectangle(bodyX, clampedTopY, Math.Max(1, (int)MathF.Round(bodyWidth)), bodyHeight);

            if (bodyHeight > 0)
            {
                ResolvedSkinAnimation? bodyAnimation = resolvedLane?.LongBody;
                double animationEndTime = LongNoteRenderRules.ResolveAnimationEndTime(
                    _longNote.State, _longNote.HeadHitTime, _longNote.TailReleaseTime,
                    _longNote.EndTime, _longNote.HeadJudged && _longNote.HeadResult != HitResult.Miss);
                double animationTime = LongNoteRenderRules.BodyAnimationElapsedMs(_longNote.State, currentTime,
                    _longNote.HeadHitTime, animationEndTime, _longNote.HeadJudged && _longNote.HeadResult != HitResult.Miss,
                    resolvedMode?.Definition.BodyAnimationReleaseMode ?? HoldBodyAnimationReleaseMode.FreezeAtRelease);
                Texture2D? bodyTexture = bodyAnimation?.FrameAt(animationTime);
                if (bodyTexture != null)
                {
                    int totalLNHeight = bottomY - topY;
                    if (totalLNHeight > 0)
                    {
                        double stateAlphaMult = (_longNote.State == LongNoteState.Broken) ? 0.5 : 1.0;
                        NoteBodyRenderMode bodyMode = skinManager?.GetNoteBodyMode(keyCount, columnIndex)
                            ?? NoteBodyRenderMode.TileFromTail;
                        bool flipBody = skinManager!.FlipNote(keyCount, columnIndex, "L", downScroll);

                        // The texture may have been reduced for GPU upload. Its source
                        // coordinates are physical; its display height is still based on
                        // the original legacy image dimensions.
                        int blockH = (int)MathF.Round(PieceHeight(bodyTexture, bodyMetrics, bodyWidth));
                        if (blockH <= 0) blockH = 1;
                        Color holdColor = Color.White;

                        if (IsHiddenActive)
                        {
                            int segmentHeight = 8;
                            for (int currY = clampedTopY; currY < clampedBottomY;)
                            {
                                int h = Math.Min(segmentHeight, clampedBottomY - currY);
                                if (bodyMode != NoteBodyRenderMode.Stretch)
                                    h = LongNoteRenderRules.MaxTileSegmentHeight(currY, h, topY, bottomY, blockH, bodyMode, downScroll);
                                double bodyAlpha = GetHiddenAlpha(currY + h / 2, downScroll, receptorY, screenHeight, CurrentCombo) * stateAlphaMult;
                                if (bodyAlpha > 0.0)
                                {
                                    var bodyRect = new Rectangle(bodyX, currY, (int)MathF.Round(bodyWidth), h);
                                    Rectangle srcRect = GetSourceRectForSegment(currY, h, topY, bottomY,
                                        bodyMode, bodyTexture, blockH, downScroll, flipBody, out SpriteEffects effects);
                                    spriteBatch.Draw(bodyTexture, bodyRect, srcRect, holdColor * (float)bodyAlpha,
                                        0f, Vector2.Zero, effects, 0f);
#if DEBUG
                                    LongNoteRenderDiagnostics.TraceDraw("body", bodyTexture,
                                        skinManager!.GetLogicalSize(bodyTexture), bodyRect, srcRect, effects, traceFrame);
#endif
                                }
                                currY += h;
                            }
                        }
                        else
                        {
                            double bodyAlpha = GetHiddenAlpha((clampedTopY + clampedBottomY) / 2, downScroll, receptorY, screenHeight, CurrentCombo) * stateAlphaMult;
                            if (bodyAlpha > 0.0)
                            {
                                if (bodyMode == NoteBodyRenderMode.Stretch)
                                {
                                    var bodyRect = new Rectangle(bodyX, clampedTopY, (int)MathF.Round(bodyWidth), bodyHeight);
                                    var srcRect = GetSourceRectForSegment(clampedTopY, bodyHeight, topY, bottomY,
                                        bodyMode, bodyTexture, blockH,
                                        downScroll, flipBody, out SpriteEffects effects);
                                    spriteBatch.Draw(bodyTexture, bodyRect, srcRect, holdColor * (float)bodyAlpha,
                                        0f, Vector2.Zero, effects, 0f);
#if DEBUG
                                    LongNoteRenderDiagnostics.TraceDraw("body", bodyTexture,
                                        skinManager!.GetLogicalSize(bodyTexture), bodyRect, srcRect, effects, traceFrame);
#endif
                                }
                                else
                                {
                                    int tileCount = (totalLNHeight + blockH - 1) / blockH;
                                    int drawWidth = Math.Max(1, (int)MathF.Round(bodyWidth));
                                    for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
                                    {
                                        int tileOriginY = LongNoteRenderRules.TileOriginY(topY, bottomY, blockH,
                                            bodyMode, downScroll, tileIndex);
                                        int drawY = Math.Max(clampedTopY, tileOriginY);
                                        int drawBottomY = Math.Min(clampedBottomY, tileOriginY + blockH);
                                        int drawH = drawBottomY - drawY;
                                        if (drawH <= 0) continue;

                                        Rectangle srcRect = GetSourceRectForSegment(drawY, drawH, topY, bottomY,
                                            bodyMode, bodyTexture, blockH, downScroll, flipBody, out SpriteEffects effects);
                                        var tileRect = new Rectangle(bodyX, drawY, drawWidth, drawH);
                                        spriteBatch.Draw(bodyTexture, tileRect, srcRect, holdColor * (float)bodyAlpha,
                                            0f, Vector2.Zero, effects, 0f);
#if DEBUG
                                        LongNoteRenderDiagnostics.TraceDraw("body", bodyTexture,
                                            skinManager!.GetLogicalSize(bodyTexture), tileRect, srcRect, effects, traceFrame);
#endif
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
#if DEBUG
                            LongNoteRenderDiagnostics.TraceDraw("fallback-body", pixel, Vector2.One,
                                bodyRect, null, SpriteEffects.None, traceFrame);
#endif

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
#if DEBUG
                            LongNoteRenderDiagnostics.TraceDraw("fallback-body", pixel, Vector2.One,
                                bodyRect, null, SpriteEffects.None, traceFrame);
#endif

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

            // osu! draws the head above the masked body and tail. Preserve that
            // order explicitly: an opaque tail texture must not cover the head
            // when short holds overlap their caps.
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
                        Rectangle destination = tailRect;
                        Rectangle source = new Rectangle(0, 0, tailFrame.Width, tailFrame.Height);
                        bool visible = !tailMaskedAtReceptor || LongNoteRenderRules.TryClipTailToReceptor(
                            tailRect, receptorY, downScroll, tailFrame.Width, tailFrame.Height,
                            (effects & SpriteEffects.FlipVertically) != 0, out destination, out source);
                        if (visible)
                        {
                            tailDebugRect = destination;
                            spriteBatch.Draw(tailFrame, destination, tailMaskedAtReceptor ? source : null, Color.White * (float)tailAlpha,
                                0f, Vector2.Zero, effects, 0f);
#if DEBUG
                            LongNoteRenderDiagnostics.TraceDraw("tail", tailFrame,
                                skinManager.GetLogicalSize(tailFrame), destination,
                                tailMaskedAtReceptor ? source : null, effects, traceFrame);
#endif
                        }
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
                        Rectangle destination = tailRect;
                        bool visible = !tailMaskedAtReceptor || LongNoteRenderRules.TryClipTailToReceptor(
                            tailRect, receptorY, downScroll, pixel.Width, pixel.Height, false,
                            out destination, out _);
                        if (visible)
                        {
                            tailDebugRect = destination;
                            spriteBatch.Draw(pixel, destination, COLOR_TAIL * (float)tailAlpha);
#if DEBUG
                            LongNoteRenderDiagnostics.TraceDraw("fallback-tail", pixel, Vector2.One,
                                destination, null, SpriteEffects.None, traceFrame);
#endif
                        }
                    }
                }
            }

            // Keep a successfully judged legacy hold head at the receptor. The
            // Stable expectation recorded in ppy/osu#12127 is that the cap covers
            // the body's mask at the judgement point; hiding it exposes that edge.
            if (LongNoteRenderRules.ShouldDrawHead(_longNote.HeadJudged, _longNote.State,
                    resolvedMode?.Definition.HoldHeadAtReceptorWhileHolding ?? false)
                && headY >= -50 && headY <= screenHeight + 50)
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
                        headDebugRect = headRect;
                        SpriteEffects effects = skinManager!.FlipNote(keyCount, columnIndex, "H", downScroll)
                            ? SpriteEffects.FlipVertically : SpriteEffects.None;
                        spriteBatch.Draw(headFrame, headRect, null, Color.White * (float)headAlpha,
                            0f, Vector2.Zero, effects, 0f);
#if DEBUG
                        LongNoteRenderDiagnostics.TraceDraw("head", headFrame,
                            skinManager.GetLogicalSize(headFrame), headRect, null, effects, traceFrame);
#endif
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
                        headDebugRect = headRect;
                        spriteBatch.Draw(pixel, headRect, COLOR_HEAD * (float)headAlpha);
#if DEBUG
                        LongNoteRenderDiagnostics.TraceDraw("fallback-head", pixel, Vector2.One,
                            headRect, null, SpriteEffects.None, traceFrame);
#endif

                        // Head border
                        var headBorder = new Rectangle(columnX + PADDING, headRenderY, columnWidth - PADDING * 2, 2);
                        spriteBatch.Draw(pixel, headBorder, COLOR_BORDER * (float)headAlpha);
                    }
                }
            }

#if DEBUG
            if (LongNoteRenderDiagnostics.Enabled)
            {
                bool bodyDrawn = bodyDebugRect.HasValue && bodyHeight > 0;
                LongNoteRenderDiagnostics.LogFrame(_longNote, currentTime, downScroll, receptorY,
                    rawHeadY, headY, rawTailY, tailY, rawBodyTopY, rawBodyBottomY,
                    bodyTopY, bodyBottomY, headClamped, tailClamped, bodyDrawn, tailMaskedAtReceptor,
                    columnIndex, IsJudged, traceFrame);

                if (LongNoteRenderDiagnostics.DrawOutlines)
                {
                    if (bodyDebugRect is { } bodyOutline)
                        LongNoteRenderDiagnostics.DrawOutline(spriteBatch, pixel, bodyOutline, Color.Lime);
                    if (tailDebugRect is { } tailOutline)
                        LongNoteRenderDiagnostics.DrawOutline(spriteBatch, pixel, tailOutline, Color.Magenta);
                    if (headDebugRect is { } headOutline)
                        LongNoteRenderDiagnostics.DrawOutline(spriteBatch, pixel, headOutline, Color.Red);
                    LongNoteRenderDiagnostics.DrawOutline(spriteBatch, pixel,
                        new Rectangle(columnX, receptorY - 1, Math.Max(1, columnWidth), 2), Color.Yellow);
                }
            }
#endif

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

        private Rectangle GetSourceRectForSegment(int currY, int h, int bodyTopY, int bodyBottomY,
            NoteBodyRenderMode bodyMode, Texture2D bodyTexture, int blockH, bool downScroll,
            bool flipBody, out SpriteEffects effects)
        {
            effects = flipBody ? SpriteEffects.FlipVertically : SpriteEffects.None;

            int totalLNHeight = bodyBottomY - bodyTopY;
            if (totalLNHeight <= 0 || h <= 0)
                return new Rectangle(0, 0, bodyTexture.Width, bodyTexture.Height);

            if (bodyMode == NoteBodyRenderMode.Stretch)
            {
                int displaySourceY = flipBody
                    ? bodyBottomY - (currY + h)
                    : currY - bodyTopY;
                double sourcePixelsPerDisplayPixel = bodyTexture.Height / (double)totalLNHeight;
                int stretchSourceY = (int)Math.Floor(displaySourceY * sourcePixelsPerDisplayPixel);
                int stretchSourceBottom = (int)Math.Ceiling((displaySourceY + h) * sourcePixelsPerDisplayPixel);
                stretchSourceY = Math.Clamp(stretchSourceY, 0, bodyTexture.Height - 1);
                stretchSourceBottom = Math.Clamp(stretchSourceBottom, stretchSourceY + 1, bodyTexture.Height);
                return new Rectangle(0, stretchSourceY, bodyTexture.Width, stretchSourceBottom - stretchSourceY);
            }

            (int tileSourceY, int sourceHeight) = LongNoteRenderRules.GetTileSourceRange(currY, h,
                bodyTopY, bodyBottomY, blockH, bodyTexture.Height, bodyMode, downScroll, flipBody);
            return new Rectangle(0, tileSourceY, bodyTexture.Width, sourceHeight);
        }
    }
}
