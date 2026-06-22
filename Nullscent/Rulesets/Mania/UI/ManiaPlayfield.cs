#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Rulesets.Mania.UI.Drawables;
using Nullscent.UI; // Import for TrueTypeFontRenderer
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.Rulesets.Mania.UI
{
    /// <summary>
    /// The playfield for mania. Contains columns and handles note rendering.
    /// Fully rewritten using drawable hit object architecture inspired by osu!mania.
    /// </summary>
    public class ManiaPlayfield : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly List<Column> _columns;
        private readonly int _columnCount;
        private double _overallDifficulty = 5.0;

        // Visual properties
        private readonly int _stageX;
        private readonly int _stageWidth;
        private readonly int _screenHeight;
        public int ReceptorY { get; private set; }

        // Shared rendering resources
        private Texture2D? _whitePixel;

        // Font renderer for judgements
        private readonly TrueTypeFontRenderer? _fontRenderer;
        private JudgementDisplayManager? _judgementDisplayManager;

        public JudgementDisplayManager? JudgementDisplayManager => _judgementDisplayManager;

        public ManiaPlayfield(
            GraphicsDevice graphicsDevice, 
            SpriteBatch spriteBatch, 
            int columnCount, 
            int screenWidth, 
            int screenHeight,
            TrueTypeFontRenderer? fontRenderer = null,
            double overallDifficulty = 5.0)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _columnCount = columnCount;
            _screenHeight = screenHeight;
            _fontRenderer = fontRenderer;
            _overallDifficulty = overallDifficulty;

            // Calculate stage dimensions (centered on screen)
            int columnWidth = 80; // Standard column width
            _stageWidth = columnWidth * columnCount;
            _stageX = (screenWidth - _stageWidth) / 2;

            // Create white pixel texture for rendering
            _whitePixel = new Texture2D(graphicsDevice, 1, 1);
            _whitePixel.SetData(new[] { Color.White });

            // Initialize columns
            _columns = new List<Column>();
            for (int i = 0; i < columnCount; i++)
            {
                int x = _stageX + (i * columnWidth);
                _columns.Add(new Column(i, x, columnWidth, screenHeight, _whitePixel));
            }
            // Create judgement display manager and position it at receptor center
            _judgementDisplayManager = new JudgementDisplayManager(_graphicsDevice, _spriteBatch, _fontRenderer);
            _judgementDisplayManager.Position = new Vector2(_stageX + _stageWidth / 2f, (float)(_screenHeight * 0.45));

            // Assign manager to columns
            foreach (var col in _columns)
            {
                col.SetJudgementDisplayManager(_judgementDisplayManager);
            }

            Console.WriteLine($"[ManiaPlayfield] Created {columnCount} columns, stage: {_stageWidth}px wide at x={_stageX}");
        }

        public void AddHitObject(ManiaHitObject hitObject)
        {
            // Convert HoldNote to LongNote with explicit OD
            if (hitObject is HoldNote holdNote)
            {
                hitObject = new LongNote(holdNote.StartTime, holdNote.EndTime, holdNote.Column, _overallDifficulty)
                {
                    Samples = holdNote.Samples
                };
            }

            if (hitObject.Column >= 0 && hitObject.Column < _columns.Count)
            {
                _columns[hitObject.Column].AddHitObject(hitObject);
            }
        }

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float receptorPosition, bool downScroll)
        {
            // Calculate receptor position once
            ReceptorY = (int)(receptorPosition * _screenHeight);

            foreach (var column in _columns)
            {
                column.Update(currentTime, scoreProcessor, scrollSpeed, receptorPosition, downScroll, ReceptorY);
            }

            // Update central judgement display
            _judgementDisplayManager?.Update(currentTime);
        }

        public void HandleKeyPress(int columnIndex, double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            if (columnIndex >= 0 && columnIndex < _columns.Count)
            {
                _columns[columnIndex].OnKeyPress(currentTime, scoreProcessor);
            }
        }

        public void HandleKeyRelease(int columnIndex, double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            if (columnIndex >= 0 && columnIndex < _columns.Count)
            {
                _columns[columnIndex].OnKeyRelease(currentTime, scoreProcessor);
            }
        }

        public void Draw(double currentTime, double scrollSpeed, float receptorPosition, bool downScroll)
        {
            if (_whitePixel == null) return;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // Draw stage background
            DrawStageBackground();

            // Draw each column
            foreach (var column in _columns)
            {
                column.Draw(_spriteBatch, _whitePixel, _fontRenderer, currentTime, scrollSpeed, receptorPosition, downScroll, ReceptorY);
            }

            // Draw central judgement display
            _judgementDisplayManager?.Draw(currentTime);

            _spriteBatch.End();
        }

        private void DrawStageBackground()
        {
            if (_whitePixel == null) return;

            // Dark stage background
            var stageRect = new Rectangle(_stageX, 0, _stageWidth, _screenHeight);
            _spriteBatch.Draw(_whitePixel, stageRect, new Color(15, 15, 20, 220));

            // Draw column separators
            for (int i = 1; i < _columnCount; i++)
            {
                int separatorX = _columns[i].X;
                var separatorRect = new Rectangle(separatorX, 0, 2, _screenHeight);
                _spriteBatch.Draw(_whitePixel, separatorRect, new Color(40, 40, 50, 255));
            }
        }

        public void Dispose()
        {
            _whitePixel?.Dispose();
            foreach (var column in _columns)
            {
                column.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents a single column in the playfield.
    /// Now uses drawable hit objects for better lifecycle management.
    /// </summary>
    public class Column : IDisposable
    {
        private readonly int _index;
        private readonly int _x;
        private readonly int _width;
        private readonly int _screenHeight;
        private readonly Texture2D _pixel;

        // Drawable containers
        private readonly List<DrawableManiaHitObject> _drawableHitObjects = new();
        private readonly List<HitExplosion> _explosions = new();
        private JudgementDisplayManager? _judgementDisplayManager;

        private readonly ManiaHitWindows _hitWindows;

        private bool _isPressed;
        private double _lightingEndTime;

        public int X => _x;

        public Column(int index, int x, int width, int screenHeight, Texture2D pixel)
        {
            _index = index;
            _x = x;
            _width = width;
            _screenHeight = screenHeight;
            _pixel = pixel;
            _hitWindows = new ManiaHitWindows(5.0); // OD 5.0 default
            // Judgement display manager created by parent playfield during Playfield construction
        }

        public void SetJudgementDisplayManager(JudgementDisplayManager? manager)
        {
            _judgementDisplayManager = manager;
        }

        public void AddHitObject(ManiaHitObject hitObject)
        {
            // Create drawable wrapper based on hit object type
            DrawableManiaHitObject? drawable = null;

            if (hitObject is LongNote longNote)
            {
                // Long notes (LN) with ScoreV2-style head+tail separation
                drawable = new DrawableLongNote(longNote);
            }
            else if (hitObject is Note note)
            {
                drawable = new DrawableNote(note);
            }
            else if (hitObject is HoldNote holdNote)
            {
                drawable = new DrawableHoldNote(holdNote);
            }

            if (drawable != null)
            {
                _drawableHitObjects.Add(drawable);
            }
        }

        public void Update(double currentTime, ManiaScoreProcessor scoreProcessor, double scrollSpeed, float receptorPosition, bool downScroll, int receptorY)
        {
            // Update all drawable hit objects
            foreach (var drawable in _drawableHitObjects.ToList())
            {
                drawable.Update(currentTime, scrollSpeed, receptorPosition, downScroll, _screenHeight);

                // Special handling for LN hold state updates
                if (drawable is DrawableLongNote drawableLN)
                {
                    // Update hold state every frame (deterministic)
                    drawableLN.UpdateHold(currentTime, _isPressed);

                    // Check for auto-misses (head and tail)
                    drawableLN.CheckAutoMiss(currentTime, _hitWindows);

                    // If LN is fully judged, apply both head and tail results
                    if (drawableLN.HitObject is LongNote ln && ln.IsFullyJudged)
                    {
                        // Apply head result
                        if (ln.HeadJudged && !drawableLN.IsJudged)
                        {
                            scoreProcessor.ApplyResult(ln.HeadResult);

                            // Create visual effects for head (explosion only, judgement is centralized)
                            _explosions.Add(new HitExplosion(ln.HeadResult, currentTime));
                        }

                        // Apply tail result
                        if (ln.TailJudged)
                        {
                            scoreProcessor.ApplyResult(ln.TailResult);

                            // Create visual effects for tail (explosion only, judgement is centralized)
                            _explosions.Add(new HitExplosion(ln.TailResult, currentTime));

                            // Mark as judged and remove
                            drawableLN.IsJudged = true;
                            _drawableHitObjects.Remove(drawable);
                            drawable.Dispose();
                        }
                    }
                }
                // Auto-judge regular hit objects if result is ready
                else if (drawable.HasResult)
                {
                    OnHitObjectJudged(drawable, currentTime, scoreProcessor);
                    _drawableHitObjects.Remove(drawable);
                    drawable.Dispose();
                }
            }

            // Update effects
            foreach (var explosion in _explosions.ToList())
            {
                explosion.Update(currentTime);
                if (!explosion.IsActive)
                    _explosions.Remove(explosion);
            }
        }

        public void OnKeyPress(double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            _isPressed = true;

            // Find closest unjudged drawable
            DrawableManiaHitObject? closest = null;
            double closestDistance = double.MaxValue;

            foreach (var drawable in _drawableHitObjects)
            {
                if (drawable.HasResult) continue;

                double distance = Math.Abs(currentTime - drawable.HitObject.StartTime);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = drawable;
                }
            }

            // Try to judge the closest hit object
            if (closest != null)
            {
                // Special handling for LN heads
                if (closest is DrawableLongNote drawableLN)
                {
                    // Attempt to judge LN head
                    closest.CheckForResult(currentTime, _hitWindows);

                    if (drawableLN.HitObject is LongNote ln && ln.HeadJudged)
                    {
                        // Trigger column lighting
                        _lightingEndTime = currentTime + 150;

                        // Centralized judgement display
                        int headOffset = (int)Math.Truncate(ln.HeadHitTime - ln.StartTime);
                        _judgementDisplayManager?.EnqueueJudgement(ln.HeadResult, _index, headOffset, scoreProcessor.Combo, currentTime);

                        // Don't remove yet - LN continues to tail
                    }
                }
                // Regular note handling
                else
                {
                    double offset = currentTime - closest.HitObject.StartTime;
                    var result = _hitWindows.JudgeHit(offset);

                    // Only judge if within valid hit window
                    if (result != HitResult.None && result != HitResult.Miss)
                    {
                        closest.CheckForResult(currentTime, _hitWindows);

                    if (closest.HasResult)
                    {
                        // For regular notes, centralize judgement display
                        if (closest.Result.HasValue)
                        {
                            _judgementDisplayManager?.EnqueueJudgement(closest.Result.Value, _index, (int)Math.Truncate(currentTime - closest.HitObject.StartTime), scoreProcessor.Combo, currentTime);
                        }

                        OnHitObjectJudged(closest, currentTime, scoreProcessor);
                        _drawableHitObjects.Remove(closest);

                        // Trigger column lighting
                        _lightingEndTime = currentTime + 150;
                    }
                    }
                }
            }
        }

        public void OnKeyRelease(double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            _isPressed = false;

            // Check for LN tail judgements on key release
            foreach (var drawable in _drawableHitObjects.ToList())
            {
                if (drawable is DrawableLongNote drawableLN)
                {
                    // Try to judge tail
                    drawableLN.OnKeyRelease(currentTime, _hitWindows);

                    if (drawableLN.HitObject is LongNote ln && ln.TailJudged)
                    {
                        // Centralized judgement for tail
                        int tailOffset = (int)Math.Truncate(ln.TailReleaseTime - ln.EndTime);
                        _judgementDisplayManager?.EnqueueJudgement(ln.TailResult, _index, tailOffset, scoreProcessor.Combo, currentTime);
                    }

                    // Note: actual scoring is handled in Update() when LN is fully judged
                }
            }
        }

        private void OnHitObjectJudged(DrawableManiaHitObject drawable, double currentTime, ManiaScoreProcessor scoreProcessor)
        {
            if (!drawable.Result.HasValue) return;

            var result = drawable.Result.Value;

            // Apply to score processor
            scoreProcessor.ApplyResult(result);

            // Create visual effects (explosion only, judgement is centralized)
            _explosions.Add(new HitExplosion(result, currentTime));

            Console.WriteLine($"[Column {_index}] Judged: {result} at {currentTime:F2}ms");
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, TrueTypeFontRenderer? fontRenderer, 
            double currentTime, double scrollSpeed, float receptorPosition, bool downScroll, int receptorY)
        {
            // Draw column background with alternating colors
            Color columnBg = _index % 2 == 0 ? new Color(20, 20, 25, 180) : new Color(25, 25, 30, 180);
            var columnRect = new Rectangle(_x, 0, _width, _screenHeight);
            spriteBatch.Draw(pixel, columnRect, columnBg);

            // End and begin spritebatch with translation for column-relative drawing
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, 
                transformMatrix: Matrix.CreateTranslation(_x, 0, 0));

            // Draw all drawable hit objects (using column-relative coordinates)
            foreach (var drawable in _drawableHitObjects)
            {
                drawable.Draw(spriteBatch, pixel, currentTime, scrollSpeed, receptorY, downScroll, _screenHeight, _width);
            }

            // End translation and restart normal drawing
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // Draw receptor (judgement line)
            var receptorRect = new Rectangle(_x + 3, receptorY - 2, _width - 6, 4);
            Color receptorColor = _isPressed ? Color.Yellow : Color.White;
            spriteBatch.Draw(pixel, receptorRect, receptorColor);

            // Draw column lighting when pressed or hit
            if (_isPressed || currentTime < _lightingEndTime)
            {
                float alpha = _isPressed ? 0.4f : 0.3f;
                var lightRect = new Rectangle(_x + 2, receptorY - 40, _width - 4, 80);
                spriteBatch.Draw(pixel, lightRect, Color.White * alpha);
            }

            // Draw hit explosions (use pooled list to avoid allocation during enumeration)
            foreach (var explosion in _explosions.ToArray())
            {
                explosion.Draw(spriteBatch, pixel, _x, _width, receptorY, currentTime);
            }
        }

        public void Dispose()
        {
            foreach (var drawable in _drawableHitObjects)
            {
                drawable.Dispose();
            }
            _drawableHitObjects.Clear();
        }
    }
}
