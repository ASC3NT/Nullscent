#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.Rulesets.Mania.UI
{
    /// <summary>
    /// Hit Error Bar (timing distribution visualization).
    /// 
    /// Shows:
    /// - Visual representation of hit timing accuracy
    /// - Early/late distribution
    /// - Hit window boundaries
    /// - Recent hit offsets
    /// 
    /// Similar to osu!mania's error meter.
    /// </summary>
    public class HitErrorBar
    {
        private readonly Queue<HitErrorEntry> _recentHits = new();
        private readonly int _maxDisplayedHits = 50;

        // Visual settings
        private readonly int _width;
        private readonly int _height;
        private readonly int _x;
        private readonly int _y;

        // Hit windows for reference lines
        private readonly ManiaHitWindows _hitWindows;

        // Display duration for each hit marker
        private const double MARKER_LIFETIME = 2000; // 2 seconds

        public HitErrorBar(int x, int y, int width, int height, ManiaHitWindows hitWindows)
        {
            _x = x;
            _y = y;
            _width = width;
            _height = height;
            _hitWindows = hitWindows;
        }

        /// <summary>
        /// Record a hit for display.
        /// </summary>
        /// <param name="timeOffset">Hit offset in milliseconds (positive = late, negative = early)</param>
        /// <param name="result">The judgement result</param>
        /// <param name="currentTime">Current game time</param>
        public void RecordHit(double timeOffset, HitResult result, double currentTime)
        {
            if (result == HitResult.None || result == HitResult.Miss)
                return;

            _recentHits.Enqueue(new HitErrorEntry
            {
                Offset = timeOffset,
                Result = result,
                Time = currentTime
            });

            // Limit stored hits
            while (_recentHits.Count > _maxDisplayedHits)
                _recentHits.Dequeue();
        }

        /// <summary>
        /// Update and remove expired hits.
        /// </summary>
        public void Update(double currentTime)
        {
            // Remove expired markers
            while (_recentHits.Count > 0 && 
                   currentTime - _recentHits.Peek().Time > MARKER_LIFETIME)
            {
                _recentHits.Dequeue();
            }
        }

        /// <summary>
        /// Draw the error bar.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, double currentTime, TrueTypeFontRenderer? fontRenderer = null)
        {
            // Draw background
            var bgRect = new Rectangle(_x, _y, _width, _height);
            spriteBatch.Draw(pixel, bgRect, new Color(0, 0, 0, 180));

            // Draw hit window boundaries as colored segments
            DrawWindowBoundaries(spriteBatch, pixel);

            // Draw center line (perfect timing)
            int centerX = _x + _width / 2;
            var centerLine = new Rectangle(centerX - 1, _y, 2, _height);
            spriteBatch.Draw(pixel, centerLine, Color.White * 0.8f);

            // Draw hit markers
            foreach (var hit in _recentHits)
            {
                DrawHitMarker(spriteBatch, pixel, hit, currentTime);
            }

            // Draw average offset indicator if there are recent hits
            if (_recentHits.Count > 0)
            {
                double maxWindow = _hitWindows.WindowFor(HitResult.Meh);
                if (maxWindow <= 0) maxWindow = 200.0;

                double avgOffset = _recentHits.Average(h => h.Offset);
                double ratio = Math.Clamp(avgOffset / maxWindow, -1.0, 1.0);
                int offsetX = (int)(ratio * (_width / 2));
                int avgX = centerX + offsetX;

                // Draw average indicator (thick white tick extending slightly)
                var avgRect = new Rectangle(avgX - 2, _y - 2, 4, _height + 4);
                spriteBatch.Draw(pixel, avgRect, Color.White * 0.9f);
            }

            // Draw early/late labels
            if (fontRenderer != null)
            {
                DrawLabels(spriteBatch, fontRenderer);
            }
        }

        private void DrawWindowBoundaries(SpriteBatch spriteBatch, Texture2D pixel)
        {
            int centerX = _x + _width / 2;
            double maxWindow = _hitWindows.WindowFor(HitResult.Meh);
            if (maxWindow <= 0) maxWindow = 200.0;

            // Ordenados desde el más grande (Meh) al más pequeño (Perfect)
            var results = new[] { HitResult.Meh, HitResult.Ok, HitResult.Good, HitResult.Great, HitResult.Perfect };
            var colors = new[]
            {
                new Color(60, 60, 60, 180),     // Meh (Dark Grey)
                new Color(200, 100, 0, 180),    // Ok (Orange)
                new Color(200, 200, 0, 180),    // Good (Yellow)
                new Color(0, 180, 0, 180),      // Great (Green)
                new Color(0, 150, 255, 180)     // Perfect (Cyan)
            };

            for (int i = 0; i < results.Length; i++)
            {
                double window = _hitWindows.WindowFor(results[i]);
                if (window <= 0) continue;

                // Para el segmento, el límite interno es la ventana de la siguiente prioridad (más pequeña)
                double innerWindow = (i + 1 < results.Length) ? _hitWindows.WindowFor(results[i + 1]) : 0.0;

                double ratioOuter = window / maxWindow;
                double ratioInner = innerWindow / maxWindow;

                int outerHalfWidth = (int)(ratioOuter * (_width / 2));
                int innerHalfWidth = (int)(ratioInner * (_width / 2));

                if (innerWindow > 0)
                {
                    // Dibujar el segmento izquierdo
                    var leftRect = new Rectangle(centerX - outerHalfWidth, _y, outerHalfWidth - innerHalfWidth, _height);
                    spriteBatch.Draw(pixel, leftRect, colors[i]);

                    // Dibujar el segmento derecho
                    var rightRect = new Rectangle(centerX + innerHalfWidth, _y, outerHalfWidth - innerHalfWidth, _height);
                    spriteBatch.Draw(pixel, rightRect, colors[i]);
                }
                else
                {
                    // Para el elemento central (Perfect), dibujamos el bloque completo
                    var centerRect = new Rectangle(centerX - outerHalfWidth, _y, outerHalfWidth * 2, _height);
                    spriteBatch.Draw(pixel, centerRect, colors[i]);
                }
            }
        }

        private void DrawHitMarker(SpriteBatch spriteBatch, Texture2D pixel, HitErrorEntry hit, double currentTime)
        {
            double age = currentTime - hit.Time;
            float alpha = (float)(1.0 - (age / MARKER_LIFETIME));

            if (alpha <= 0) return;

            int centerX = _x + _width / 2;
            double maxWindow = _hitWindows.WindowFor(HitResult.Meh);
            if (maxWindow <= 0) maxWindow = 200.0;

            // Calculate position based on offset
            double ratio = Math.Clamp(hit.Offset / maxWindow, -1.0, 1.0);
            int offsetX = (int)(ratio * (_width / 2));
            int markerX = centerX + offsetX;

            // Draw marker as a vertical tick
            Color markerColor = GetColorForResult(hit.Result) * alpha;
            int tickWidth = 2;
            var markerRect = new Rectangle(
                markerX - tickWidth / 2,
                _y,
                tickWidth,
                _height
            );

            spriteBatch.Draw(pixel, markerRect, markerColor);
        }

        private void DrawLabels(SpriteBatch spriteBatch, TrueTypeFontRenderer fontRenderer)
        {
            fontRenderer.SetFontSize(12);
            // Draw "Early" on the left
            fontRenderer.DrawTextRight("Early", _x - 8, _y + (_height - 12) / 2, Color.White * 0.7f, 1.0f);
            // Draw "Late" on the right
            fontRenderer.DrawText("Late", _x + _width + 8, _y + (_height - 12) / 2, Color.White * 0.7f, 1.0f);
        }

        private Color GetColorForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => new Color(84, 200, 255),  // Cyan
                HitResult.Great => new Color(100, 255, 100),    // Green
                HitResult.Good => new Color(255, 255, 100),     // Yellow
                HitResult.Ok => new Color(255, 150, 50),        // Orange
                HitResult.Meh => new Color(180, 100, 255),      // Purple
                _ => Color.Gray
            };
        }

        /// <summary>
        /// Get statistics about recent hits.
        /// </summary>
        public HitErrorStats GetStats()
        {
            if (_recentHits.Count == 0)
                return new HitErrorStats();

            var offsets = _recentHits.Select(h => h.Offset).ToList();

            return new HitErrorStats
            {
                AverageOffset = offsets.Average(),
                StandardDeviation = CalculateStdDev(offsets),
                EarlyHits = offsets.Count(o => o < 0),
                LateHits = offsets.Count(o => o > 0),
                PerfectHits = offsets.Count(o => Math.Abs(o) <= _hitWindows.WindowFor(HitResult.Perfect))
            };
        }

        private double CalculateStdDev(List<double> values)
        {
            if (values.Count == 0) return 0;

            double average = values.Average();
            double sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        private class HitErrorEntry
        {
            public double Offset { get; set; }
            public HitResult Result { get; set; }
            public double Time { get; set; }
        }
    }

    /// <summary>
    /// Statistics about hit timing accuracy.
    /// </summary>
    public class HitErrorStats
    {
        public double AverageOffset { get; set; }
        public double StandardDeviation { get; set; }
        public int EarlyHits { get; set; }
        public int LateHits { get; set; }
        public int PerfectHits { get; set; }

        public string GetTimingBias()
        {
            if (Math.Abs(AverageOffset) < 1.0)
                return "Perfect";
            else if (AverageOffset < 0)
                return "Early";
            else
                return "Late";
        }
    }
}
