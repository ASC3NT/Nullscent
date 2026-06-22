#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.UI;
using System;
using System.Collections.Generic;

namespace Nullscent.Rulesets.Mania.UI
{
    /// <summary>
    /// Central display for judgements and combo similar to osu!mania.
    /// Receives judgement events and renders a single centered visual.
    /// </summary>
    public class JudgementDisplayManager : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly TrueTypeFontRenderer? _fontRenderer;

        private readonly List<QueuedJudgement> _queue = new();
        private readonly object _lock = new();

        // Display position (center of playfield)
        public Vector2 Position { get; set; }

        // Timings
        private const double DISPLAY_LIFETIME = 800; // ms

        // Event for subscribers (e.g., HitErrorBar)
        public event Action<HitResult, double, double>? JudgementOccurred;

        public JudgementDisplayManager(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, TrueTypeFontRenderer? fontRenderer)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _fontRenderer = fontRenderer;
        }

        public void EnqueueJudgement(HitResult result, int column, double offsetMs, int combo, double time)
        {
            lock (_lock)
            {
                _queue.Add(new QueuedJudgement
                {
                    Result = result,
                    Column = column,
                    Offset = offsetMs,
                    Combo = combo,
                    Time = time
                });

                // Keep queue size reasonable
                if (_queue.Count > 8) _queue.RemoveAt(0);
            }

            // Trigger event for subscribers
            JudgementOccurred?.Invoke(result, offsetMs, time);
        }

        public void Update(double currentTime)
        {
            lock (_lock)
            {
                _queue.RemoveAll(j => currentTime - j.Time > DISPLAY_LIFETIME);
            }
        }

        public void Draw(double currentTime)
        {
            if (_fontRenderer == null) return;

            _spriteBatch.Begin();

            // Draw most recent judgement centered at Position
            QueuedJudgement? latest = null;
            lock (_lock)
            {
                if (_queue.Count > 0) latest = _queue[^1];
            }

            if (latest != null)
            {
                double age = currentTime - latest.Time;
                float progress = 1f - (float)(age / DISPLAY_LIFETIME);
                progress = Math.Clamp(progress, 0f, 1f);

                string text = latest.Result.ToString();
                int combo = latest.Combo;

                // Scale and fade
                float scale = 1.0f + 0.5f * progress;
                float alpha = 0.6f + 0.4f * progress;

                _fontRenderer.SetFontSize(48);
                var color = GetColorForResult(latest.Result) * alpha;

                // Draw judgement text centered
                _fontRenderer.DrawTextCentered(text, (int)Position.X, (int)Position.Y - 20, color, scale);

                // Draw combo below judgement if > 1
                if (combo > 1)
                {
                    _fontRenderer.SetFontSize(36);
                    _fontRenderer.DrawTextCentered(combo + "x", (int)Position.X, (int)Position.Y + 30, Color.Yellow * alpha, 1.0f + 0.3f * progress);
                }
            }

            _spriteBatch.End();
        }

        private Color GetColorForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => new Color(255, 215, 100),
                HitResult.Great => new Color(150, 255, 150),
                HitResult.Good => new Color(150, 200, 255),
                HitResult.Ok => new Color(200, 150, 255),
                HitResult.Meh => new Color(200, 200, 200),
                _ => Color.White
            };
        }

        public void Dispose()
        {
            _queue.Clear();
        }

        private class QueuedJudgement
        {
            public HitResult Result { get; set; }
            public int Column { get; set; }
            public double Offset { get; set; }
            public int Combo { get; set; }
            public double Time { get; set; }
        }
    }
}
