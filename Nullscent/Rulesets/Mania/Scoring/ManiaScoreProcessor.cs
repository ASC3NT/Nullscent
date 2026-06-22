#nullable enable

using Nullscent.Rulesets.Mania.Judgements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.Rulesets.Mania.Scoring
{
    public enum ScoreRank { D, C, B, A, S, SH, X, XH }

    /// <summary>
    /// osu!mania score processor with EXACT accuracy calculation.
    /// Uses AccuracyWeightFor() for proper osu!mania accuracy matching.
    /// 
    /// ScoreV2 mode:
    /// - LN heads and tails are separate judgements
    /// - Total objects = notes + (LN heads × 2)
    /// </summary>
    public class ManiaScoreProcessor
    {
        public long Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public double Health { get; private set; } = 1.0;
        public int TotalJudgements => Statistics.Values.Sum();
        public Dictionary<HitResult, int> Statistics { get; } = new();
        public ScoreRank Rank => CalculateRank();
        public double AccuracyPercent => CalculateAccuracy() * 100.0;
        public bool HasFailed => Health <= 0.0;

        private readonly int _totalHitObjects;
        private readonly ManiaJudgement _judgement = new();
        private readonly ManiaHealthProcessor _healthProcessor;

        /// <summary>
        /// Creates a score processor.
        /// </summary>
        /// <param name="totalNotes">Number of regular notes</param>
        /// <param name="totalHoldNotes">Number of hold notes (each counts as 1 in standard, 2 in ScoreV2)</param>
        /// <param name="hpDrainRate">HP drain rate</param>
        /// <param name="healthDrainEnabled">Enable passive health drain</param>
        /// <param name="isScoreV2">If true, hold notes count as 2 objects (head + tail)</param>
        public ManiaScoreProcessor(
            int totalNotes, 
            int totalHoldNotes, 
            double hpDrainRate, 
            bool healthDrainEnabled,
            bool isScoreV2 = false)
        {
            // In ScoreV2, each LN = 2 judgements (head + tail)
            _totalHitObjects = isScoreV2 
                ? totalNotes + (totalHoldNotes * 2) 
                : totalNotes + totalHoldNotes;

            _healthProcessor = new ManiaHealthProcessor(hpDrainRate, healthDrainEnabled);

            foreach (HitResult result in Enum.GetValues(typeof(HitResult)))
                Statistics[result] = 0;

            Console.WriteLine($"[ManiaScoreProcessor] Initialized: {_totalHitObjects} total judgeable objects");
        }

        /// <summary>
        /// Applies a judgement result to the score.
        /// </summary>
        public void ApplyResult(HitResult result)
        {
            Statistics[result]++;

            // Update combo
            if (result != HitResult.None && result != HitResult.Miss)
            {
                Combo++;
                MaxCombo = Math.Max(MaxCombo, Combo);
            }
            else if (result == HitResult.Miss)
            {
                Combo = 0;
            }

            // Add score
            Score += CalculateScoreFor(result);

            // Update health
            double healthChange = _judgement.HealthIncreaseFor(result);
            Health = Math.Clamp(Health + healthChange, 0.0, 1.0);
        }

        public void UpdateHealthDrain(double deltaSeconds)
        {
            Health = Math.Clamp(Health - _healthProcessor.GetDrainFor(deltaSeconds), 0.0, 1.0);
        }

        private long CalculateScoreFor(HitResult result)
        {
            int baseScore = _judgement.NumericResultFor(result);
            double comboMultiplier = 1.0 + Math.Log10(1.0 + Combo) * 0.1;
            return (long)(baseScore * comboMultiplier);
        }

        /// <summary>
        /// Calculates accuracy using EXACT osu!mania formula.
        /// 
        /// Formula: sum(judgement_weights) / total_objects
        /// 
        /// Where:
        ///   Perfect = 1.0    (100%)
        ///   Great   = 1.0    (100%)
        ///   Good    = 0.6667 (66.67%)
        ///   Ok      = 0.3333 (33.33%)
        ///   Meh     = 0.1667 (16.67%)
        ///   Miss    = 0.0    (0%)
        /// </summary>
        private double CalculateAccuracy()
        {
            int totalJudgements = TotalJudgements;
            if (totalJudgements == 0) return 1.0;

            double totalWeight = 0.0;

            foreach (var (result, count) in Statistics)
            {
                if (result == HitResult.None) continue;
                double weight = _judgement.AccuracyWeightFor(result);
                totalWeight += weight * count;
            }

            return totalWeight / totalJudgements;
        }

        private ScoreRank CalculateRank()
        {
            double acc = AccuracyPercent;
            int misses = Statistics[HitResult.Miss];

            if (acc == 100.0) return ScoreRank.X;
            if (acc >= 95.0 && misses == 0) return ScoreRank.S;
            if (acc >= 90.0) return ScoreRank.A;
            if (acc >= 80.0) return ScoreRank.B;
            if (acc >= 70.0) return ScoreRank.C;
            return ScoreRank.D;
        }
    }

    public class ManiaHealthProcessor
    {
        private readonly double _drainRate;
        private readonly bool _enabled;

        public ManiaHealthProcessor(double hpDrainRate, bool enabled)
        {
            _drainRate = hpDrainRate;
            _enabled = enabled;
        }

        public double GetDrainFor(double deltaSeconds)
        {
            if (!_enabled) return 0.0;
            return _drainRate * 0.01 * deltaSeconds;
        }
    }
}
