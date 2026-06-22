#nullable enable

using System;

namespace Nullscent.Rulesets.Mania.Objects
{
    /// <summary>
    /// Base class for mania hit objects.
    /// </summary>
    public abstract class ManiaHitObject
    {
        public double StartTime { get; set; }
        public int Column { get; set; }
        public HitSampleInfo[] Samples { get; set; } = Array.Empty<HitSampleInfo>();

        // Judgement tracking
        public bool IsJudged { get; set; }
        public Judgements.HitResult HitResult { get; set; } = Judgements.HitResult.None;
    }

    /// <summary>
    /// A single tap note.
    /// </summary>
    public class Note : ManiaHitObject
    {
    }

    /// <summary>
    /// A hold note (long note / LN).
    /// </summary>
    public class HoldNote : ManiaHitObject
    {
        public double EndTime { get; set; }
        public double Duration => EndTime - StartTime;
    }

    /// <summary>
    /// Information about a hit sample (sound effect).
    /// </summary>
    public class HitSampleInfo
    {
        public string Name { get; set; }
        public string Bank { get; set; }
        public int Volume { get; set; }
        public string? CustomSampleBank { get; set; }

        public HitSampleInfo(string name, string bank = "normal", int volume = 100, string? customSampleBank = null)
        {
            Name = name;
            Bank = bank;
            Volume = volume;
            CustomSampleBank = customSampleBank;
        }
    }
}
