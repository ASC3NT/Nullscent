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

        public TailHitWindows(double overallDifficulty, bool isForCurrentRuleset = true)
            : base(overallDifficulty, isForCurrentRuleset)
        {
            // Multiply all windows in the protected dictionary by 1.5
            var keys = new[] { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss };
            foreach (var key in keys)
            {
                if (_windows.ContainsKey(key))
                {
                    _windows[key] = _windows[key] * TAIL_MULTIPLIER;
                }
            }
        }

        /// <summary>
        /// Judge tail hit with 1.5× multiplied windows.
        /// Uses truncated integer timing for osu!mania compatibility.
        /// </summary>
        public HitResult JudgeTail(int timeOffsetMs)
        {
            return JudgeHit(timeOffsetMs);
        }
    }
}
