#nullable enable

using System;

namespace Nullscent.Rulesets.Mania.Judgements
{
    /// <summary>
    /// Tail hit windows for Long Notes in ScoreV2 mode.
    /// 
    /// ScoreV2 uses 1.5x longer windows for LN tails to allow more leniency
    /// when releasing hold keys. This is to balance the difficulty of holding.
    /// 
    /// Formula: tail_window = base_window × 1.5
    /// </summary>
    public class TailHitWindows : ManiaHitWindows
    {
        private const double TAIL_MULTIPLIER = 1.5;
        private readonly ManiaHitWindows _baseWindows;

        public TailHitWindows(double overallDifficulty, bool isForCurrentRuleset = true)
            : base(overallDifficulty, isForCurrentRuleset)
        {
            _baseWindows = new ManiaHitWindows(overallDifficulty, isForCurrentRuleset);
        }

        /// <summary>
        /// Judge tail hit with 1.5× multiplied windows.
        /// Uses truncated integer timing for osu!mania compatibility.
        /// </summary>
        public HitResult JudgeTail(int timeOffsetMs)
        {
            // Use truncated integer offset (osu!mania behavior)
            double absOffset = Math.Abs(timeOffsetMs);

            // Get base windows
            double perfectWindow = _baseWindows.WindowFor(HitResult.Perfect) * TAIL_MULTIPLIER;
            double greatWindow = _baseWindows.WindowFor(HitResult.Great) * TAIL_MULTIPLIER;
            double goodWindow = _baseWindows.WindowFor(HitResult.Good) * TAIL_MULTIPLIER;
            double okWindow = _baseWindows.WindowFor(HitResult.Ok) * TAIL_MULTIPLIER;
            double mehWindow = _baseWindows.WindowFor(HitResult.Meh) * TAIL_MULTIPLIER;
            double missWindow = _baseWindows.WindowFor(HitResult.Miss) * TAIL_MULTIPLIER;

            // Early hits: check all windows
            if (absOffset <= perfectWindow) return HitResult.Perfect;
            if (absOffset <= greatWindow) return HitResult.Great;
            if (absOffset <= goodWindow) return HitResult.Good;
            if (absOffset <= okWindow) return HitResult.Ok;

            // MEH window check: late MEH is impossible
            if (absOffset <= mehWindow)
            {
                // Late MEH (positive offset past Ok window) → Miss
                if (timeOffsetMs > okWindow)
                    return HitResult.Miss;

                return HitResult.Meh;
            }

            // Past all windows → Miss
            if (absOffset <= missWindow)
                return HitResult.Miss;

            // Too early or too late → None (no judgement)
            return HitResult.None;
        }
    }
}
