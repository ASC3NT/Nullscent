#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
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
        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, double currentTime)
        {
            // Draw background
            var bgRect = new Rectangle(_x, _y, _width, _height);
            spriteBatch.Draw(pixel, bgRect, new Color(0, 0, 0, 180));

            // Draw center line (perfect timing)
            int centerX = _x + _width / 2;
            var centerLine = new Rectangle(centerX - 1, _y, 2, _height);
            spriteBatch.Draw(pixel, centerLine, Color.White);

            // Draw hit window boundaries
            DrawWindowBoundaries(spriteBatch, pixel);

            // Draw hit markers
            foreach (var hit in _recentHits)
            {
                DrawHitMarker(spriteBatch, pixel, hit, currentTime);
            }

            // Draw early/late labels
            DrawLabels(spriteBatch);
        }

        private void DrawWindowBoundaries(SpriteBatch spriteBatch, Texture2D pixel)
        {
            int centerX = _x + _width / 2;
            double maxWindow = _hitWindows.WindowFor(HitResult.Meh);

            // Draw boundaries for each judgement tier
            DrawBoundary(spriteBatch, pixel, centerX, HitResult.Perfect, maxWindow, new Color(255, 255, 100, 100));
            DrawBoundary(spriteBatch, pixel, centerX, HitResult.Great, maxWindow, new Color(100, 255, 100, 80));
            DrawBoundary(spriteBatch, pixel, centerX, HitResult.Good, maxWindow, new Color(100, 200, 255, 60));
        }

        private void DrawBoundary(SpriteBatch spriteBatch, Texture2D pixel, int centerX, 
            HitResult result, double maxWindow, Color color)
        {
            double window = _hitWindows.WindowFor(result);
            if (window <= 0) return;

            // Calculate pixel position
            double ratio = window / maxWindow;
            int offset = (int)(ratio * (_width / 2));

            // Draw left and right boundary lines
            int leftX = centerX - offset;
            int rightX = centerX + offset;

            var leftLine = new Rectangle(leftX, _y, 1, _height);
            var rightLine = new Rectangle(rightX, _y, 1, _height);

            spriteBatch.Draw(pixel, leftLine, color);
            spriteBatch.Draw(pixel, rightLine, color);
        }

        private void DrawHitMarker(SpriteBatch spriteBatch, Texture2D pixel, HitErrorEntry hit, double currentTime)
        {
            double age = currentTime - hit.Time;
            float alpha = (float)(1.0 - (age / MARKER_LIFETIME));

            if (alpha <= 0) return;

            int centerX = _x + _width / 2;
            double maxWindow = _hitWindows.WindowFor(HitResult.Meh);

            // Calculate position based on offset
            double ratio = Math.Clamp(hit.Offset / maxWindow, -1.0, 1.0);
            int offsetX = (int)(ratio * (_width / 2));
            int markerX = centerX + offsetX;

            // Draw marker
            Color markerColor = GetColorForResult(hit.Result) * alpha;
            int markerSize = 4;
            var markerRect = new Rectangle(
                markerX - markerSize / 2,
                _y + (_height - markerSize) / 2,
                markerSize,
                markerSize
            );

            spriteBatch.Draw(pixel, markerRect, markerColor);
        }

        private void DrawLabels(SpriteBatch spriteBatch)
        {
            // TODO: Add "Early" and "Late" text labels using TrueTypeFontRenderer
        }

        private Color GetColorForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => new Color(255, 255, 100),
                HitResult.Great => new Color(100, 255, 100),
                HitResult.Good => new Color(100, 200, 255),
                HitResult.Ok => new Color(200, 150, 255),
                HitResult.Meh => new Color(200, 200, 200),
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
