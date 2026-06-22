#nullable enable

using System;

namespace Nullscent.Rulesets.Mania.Configuration
{
    /// <summary>
    /// Configuration for mania ruleset gameplay and visuals.
    /// Inspired by osu!mania configuration options.
    /// </summary>
    public class ManiaConfig
    {
        // Gameplay settings
        public double ScrollSpeed { get; set; } = 20.0;
        public bool DownScroll { get; set; } = false;
        public double ReceptorPosition { get; set; } = 0.1;
        public double LaneCoverTop { get; set; } = 0.0;
        public double LaneCoverBottom { get; set; } = 0.0;

        // Visual settings
        public bool ShowJudgementText { get; set; } = true;
        public bool ShowCombo { get; set; } = true;
        public bool ShowScore { get; set; } = true;
        public bool ShowAccuracy { get; set; } = true;
        public bool ShowHealthBar { get; set; } = true;
        public bool ShowProgressBar { get; set; } = true;
        public bool ShowKeyOverlay { get; set; } = true;

        // Appearance
        public bool ShowHitLighting { get; set; } = true;
        public bool ShowStageBorder { get; set; } = true;
        public double BarlineVisibility { get; set; } = 0.5;
        public double ColumnLightBrightness { get; set; } = 1.0;

        // Note skin
        public string NoteSkin { get; set; } = "default";

        // Calibration
        public int LocalOffset { get; set; } = 0;

        /// <summary>
        /// Validates and clamps all settings to valid ranges.
        /// </summary>
        public void Validate()
        {
            ScrollSpeed = Math.Clamp(ScrollSpeed, 1.0, 50.0);
            ReceptorPosition = Math.Clamp(ReceptorPosition, 0.0, 0.9);
            LaneCoverTop = Math.Clamp(LaneCoverTop, 0.0, 0.5);
            LaneCoverBottom = Math.Clamp(LaneCoverBottom, 0.0, 0.5);
            BarlineVisibility = Math.Clamp(BarlineVisibility, 0.0, 1.0);
            ColumnLightBrightness = Math.Clamp(ColumnLightBrightness, 0.0, 2.0);
            LocalOffset = Math.Clamp(LocalOffset, -500, 500);
        }

        /// <summary>
        /// Creates a deep copy of this configuration.
        /// </summary>
        public ManiaConfig Clone()
        {
            return new ManiaConfig
            {
                ScrollSpeed = ScrollSpeed,
                DownScroll = DownScroll,
                ReceptorPosition = ReceptorPosition,
                LaneCoverTop = LaneCoverTop,
                LaneCoverBottom = LaneCoverBottom,
                ShowJudgementText = ShowJudgementText,
                ShowCombo = ShowCombo,
                ShowScore = ShowScore,
                ShowAccuracy = ShowAccuracy,
                ShowHealthBar = ShowHealthBar,
                ShowProgressBar = ShowProgressBar,
                ShowKeyOverlay = ShowKeyOverlay,
                ShowHitLighting = ShowHitLighting,
                ShowStageBorder = ShowStageBorder,
                BarlineVisibility = BarlineVisibility,
                ColumnLightBrightness = ColumnLightBrightness,
                NoteSkin = NoteSkin,
                LocalOffset = LocalOffset
            };
        }
    }
}
