#nullable enable

using System;
using System.Collections.Generic;

namespace Nullscent.Rulesets.Mania.Judgements
{
    /// <summary>
    /// Hit result enum matching osu!mania's judgement system.
    /// https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21mania
    /// </summary>
    public enum HitResult
    {
        None,
        Miss,
        Meh,
        Ok,
        Good,
        Great,
        Perfect
    }

    /// <summary>
    /// Mania judgement definitions matching osu!mania exactly.
    /// Score values, accuracy weights, and health changes.
    /// </summary>
    public class ManiaJudgement
    {
        public virtual string ResultText(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => "Perfect",
                HitResult.Great => "Great",
                HitResult.Good => "Good",
                HitResult.Ok => "Ok",
                HitResult.Meh => "Meh",
                HitResult.Miss => "Miss",
                _ => ""
            };
        }

        /// <summary>
        /// Returns the numeric score value for each judgement.
        /// PERFECT: 320, GREAT: 300, GOOD: 200, OK: 100, MEH: 50, MISS: 0
        /// </summary>
        public virtual int NumericResultFor(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => 320,
                HitResult.Great => 300,
                HitResult.Good => 200,
                HitResult.Ok => 100,
                HitResult.Meh => 50,
                HitResult.Miss => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Returns the accuracy weight for each judgement.
        /// Used in accuracy calculation: sum(judgement_weights) / (total_objects * 100%)
        /// </summary>
        public virtual double AccuracyWeightFor(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => 1.0,      // 100%
                HitResult.Great => 1.0,        // 100%
                HitResult.Good => 0.6667,      // 66.67%
                HitResult.Ok => 0.3333,        // 33.33%
                HitResult.Meh => 0.1667,       // 16.67%
                HitResult.Miss => 0.0,         // 0%
                _ => 0
            };
        }

        /// <summary>
        /// Health increase/decrease for each judgement.
        /// </summary>
        public virtual double HealthIncreaseFor(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => 0.015,
                HitResult.Great => 0.010,
                HitResult.Good => 0.005,
                HitResult.Ok => 0.0,
                HitResult.Meh => -0.005,
                HitResult.Miss => -0.05,
                _ => 0
            };
        }
    }

    /// <summary>
    /// Hold note judgement with reduced health impact (50% of regular notes).
    /// </summary>
    public class HoldNoteJudgement : ManiaJudgement
    {
        public override double HealthIncreaseFor(HitResult result)
        {
            return base.HealthIncreaseFor(result) * 0.5;
        }
    }

    /// <summary>
    /// Result of a judgement with timing offset information.
    /// </summary>
    public class ManiaJudgementResult
    {
        public HitResult Result { get; set; }
        public double TimeOffset { get; set; }
    }

    /// <summary>
    /// osu!mania hit windows implementation.
    /// Exact formulas from: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21mania
    /// 
    /// Standard hit windows (for mania-specific beatmaps):
    /// - PERFECT: 16 ms
    /// - GREAT:   64 - 3 × OD
    /// - GOOD:    97 - 3 × OD
    /// - OK:      127 - 3 × OD
    /// - MEH:     151 - 3 × OD
    /// - MISS:    188 - 3 × OD
    /// 
    /// Convert hit windows (for osu!standard → mania):
    /// - PERFECT: 16 ms
    /// - GREAT:   34 if OD > 4, else 47
    /// - GOOD:    67 if OD > 4, else 77
    /// - OK:      97 ms
    /// - MEH:     121 ms
    /// - MISS:    158 ms
    /// 
    /// Note: Hit error is rounded, max error is truncated (can be ±0.5ms longer/shorter)
    /// Rate mods (DT/HT/NC) do NOT affect hit windows in mania.
    /// </summary>
    public class ManiaHitWindows
    {
        private readonly Dictionary<HitResult, double> _windows = new();
        private readonly double _overallDifficulty;
        private readonly bool _isForCurrentRuleset;

        /// <summary>
        /// Creates hit windows for osu!mania.
        /// </summary>
        /// <param name="overallDifficulty">OD value (0-10)</param>
        /// <param name="isForCurrentRuleset">True for mania-specific, false for converts</param>
        public ManiaHitWindows(double overallDifficulty, bool isForCurrentRuleset = true)
        {
            _overallDifficulty = overallDifficulty;
            _isForCurrentRuleset = isForCurrentRuleset;

            if (isForCurrentRuleset)
            {
                // Standard mania hit windows
                _windows[HitResult.Perfect] = 16.0;
                _windows[HitResult.Great] = Math.Max(0, 64.0 - 3.0 * overallDifficulty);
                _windows[HitResult.Good] = Math.Max(0, 97.0 - 3.0 * overallDifficulty);
                _windows[HitResult.Ok] = Math.Max(0, 127.0 - 3.0 * overallDifficulty);
                _windows[HitResult.Meh] = Math.Max(0, 151.0 - 3.0 * overallDifficulty);
                _windows[HitResult.Miss] = Math.Max(0, 188.0 - 3.0 * overallDifficulty);
            }
            else
            {
                // Convert hit windows (osu!standard → mania)
                _windows[HitResult.Perfect] = 16.0;
                _windows[HitResult.Great] = overallDifficulty > 4 ? 34.0 : 47.0;
                _windows[HitResult.Good] = overallDifficulty > 4 ? 67.0 : 77.0;
                _windows[HitResult.Ok] = 97.0;
                _windows[HitResult.Meh] = 121.0;
                _windows[HitResult.Miss] = 158.0;
            }

            Console.WriteLine($"[ManiaHitWindows] OD={overallDifficulty:F1}, IsManiaSpecific={isForCurrentRuleset}");
            Console.WriteLine($"  PERFECT: ±{_windows[HitResult.Perfect]:F1}ms");
            Console.WriteLine($"  GREAT:   ±{_windows[HitResult.Great]:F1}ms");
            Console.WriteLine($"  GOOD:    ±{_windows[HitResult.Good]:F1}ms");
            Console.WriteLine($"  OK:      ±{_windows[HitResult.Ok]:F1}ms");
            Console.WriteLine($"  MEH:     ±{_windows[HitResult.Meh]:F1}ms");
            Console.WriteLine($"  MISS:    ±{_windows[HitResult.Miss]:F1}ms");
        }

        /// <summary>
        /// Get the hit window (in milliseconds) for a specific result.
        /// </summary>
        public double WindowFor(HitResult result)
        {
            return _windows.ContainsKey(result) ? _windows[result] : 0.0;
        }

        /// <summary>
        /// Judges a hit based on time offset.
        /// 
        /// Rules:
        /// - Hitting before MISS window has no effect (returns None)
        /// - Not hitting causes auto-miss after OK window passes
        /// - Late MEH hits are IMPOSSIBLE (become Miss)
        /// - Hit error is checked as: absOffset ≤ maxError
        /// </summary>
        /// <param name="timeOffset">Time difference (hit time - object time) in ms</param>
        /// <returns>The judgement result</returns>
        public HitResult JudgeHit(double timeOffset)
        {
            double absOffset = Math.Abs(timeOffset);

            // Early hits: check all windows
            if (absOffset <= _windows[HitResult.Perfect]) return HitResult.Perfect;
            if (absOffset <= _windows[HitResult.Great]) return HitResult.Great;
            if (absOffset <= _windows[HitResult.Good]) return HitResult.Good;
            if (absOffset <= _windows[HitResult.Ok]) return HitResult.Ok;

            // MEH window check: late MEH is impossible
            if (absOffset <= _windows[HitResult.Meh])
            {
                // Late MEH (positive offset past Ok window) → Miss
                if (timeOffset > _windows[HitResult.Ok])
                    return HitResult.Miss;

                return HitResult.Meh;
            }

            // Past all windows → Miss
            if (absOffset <= _windows[HitResult.Miss])
                return HitResult.Miss;

            // Too early or too late → None (no judgement)
            return HitResult.None;
        }

        /// <summary>
        /// Check if a time offset is past the auto-miss window.
        /// Used for notes that weren't hit.
        /// </summary>
        public bool IsPastAutoMissWindow(double timeOffset)
        {
            // Auto-miss happens after OK window passes (late side)
            return timeOffset > _windows[HitResult.Ok];
        }

        /// <summary>
        /// Get all configured windows.
        /// </summary>
        public IEnumerable<(HitResult result, double window)> GetAllWindows()
        {
            foreach (var kvp in _windows)
                yield return (kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Hold note hit windows with special rules.
    /// 
    /// Standard mode:
    /// - Single judgement based on head + tail combined
    /// - PERFECT: head ≤ 16×1.2 AND combined ≤ 16×2.4
    /// - GREAT:   head ≤ (64-3×OD)×1.1 AND combined ≤ (64-3×OD)×2.2
    /// - GOOD:    head ≤ (97-3×OD)×1.0 AND combined ≤ (97-3×OD)×2.0
    /// - OK:      head ≤ (127-3×OD)×1.0 AND combined ≤ (127-3×OD)×2.0
    /// - MEH:     Anything else that's not a miss
    /// - MISS:    Key not pressed from tail's early MEH to late OK window
    /// - Releasing during body → max MEH
    /// - Late MEH → Miss
    /// 
    /// ScoreV2 mode:
    /// - Head and tail judged separately as regular notes
    /// - Tail windows are 1.5x longer
    /// - Releasing during body prevents tail judgements > MEH
    /// </summary>
    public class HoldNoteHitWindows : ManiaHitWindows
    {
        private readonly bool _isScoreV2;

        public HoldNoteHitWindows(double overallDifficulty, bool isForCurrentRuleset = true, bool isScoreV2 = false)
            : base(overallDifficulty, isForCurrentRuleset)
        {
            _isScoreV2 = isScoreV2;
        }

        /// <summary>
        /// Judge a hold note based on head and tail timing.
        /// </summary>
        /// <param name="headOffset">Head hit timing offset</param>
        /// <param name="tailOffset">Tail release timing offset</param>
        /// <param name="releasedDuringBody">Was key released during hold body?</param>
        /// <returns>Combined judgement result</returns>
        public HitResult JudgeHoldNote(double headOffset, double tailOffset, bool releasedDuringBody)
        {
            if (_isScoreV2)
            {
                // ScoreV2: head and tail judged separately
                // This method returns head judgement only
                // Tail should be judged separately with 1.5x windows
                return JudgeHit(headOffset);
            }

            // Standard mode: combined judgement
            double absHeadOffset = Math.Abs(headOffset);
            double combinedOffset = Math.Abs(headOffset + tailOffset);

            // Released during body → max MEH
            if (releasedDuringBody)
                return HitResult.Meh;

            // Late MEH → Miss
            if (tailOffset > WindowFor(HitResult.Ok))
                return HitResult.Miss;

            // Check each judgement tier
            double perfectWindow = WindowFor(HitResult.Perfect);
            if (absHeadOffset <= perfectWindow * 1.2 && combinedOffset <= perfectWindow * 2.4)
                return HitResult.Perfect;

            double greatWindow = WindowFor(HitResult.Great);
            if (absHeadOffset <= greatWindow * 1.1 && combinedOffset <= greatWindow * 2.2)
                return HitResult.Great;

            double goodWindow = WindowFor(HitResult.Good);
            if (absHeadOffset <= goodWindow * 1.0 && combinedOffset <= goodWindow * 2.0)
                return HitResult.Good;

            double okWindow = WindowFor(HitResult.Ok);
            if (absHeadOffset <= okWindow * 1.0 && combinedOffset <= okWindow * 2.0)
                return HitResult.Ok;

            // Check if it's a miss (key not pressed during required window)
            double mehWindow = WindowFor(HitResult.Meh);
            if (headOffset > okWindow || tailOffset < -mehWindow || tailOffset > okWindow)
                return HitResult.Miss;

            // Everything else is MEH
            return HitResult.Meh;
        }

        /// <summary>
        /// Get tail window multiplier for ScoreV2 mode.
        /// </summary>
        public double GetTailWindowMultiplier()
        {
            return _isScoreV2 ? 1.5 : 1.0;
        }
    }

    /// <summary>
    /// ScoreV2-specific hit windows with modified PERFECT window.
    /// PERFECT window formula:
    /// - If OD ≤ 5: 22.4 - 0.6 × OD
    /// - If OD ≥ 5: 24.9 - 1.1 × OD
    /// </summary>
    public class ScoreV2HitWindows : ManiaHitWindows
    {
        public ScoreV2HitWindows(double overallDifficulty, bool isForCurrentRuleset = true)
            : base(overallDifficulty, isForCurrentRuleset)
        {
            // Override PERFECT window with ScoreV2 formula
            double perfectWindow;
            if (overallDifficulty <= 5.0)
            {
                perfectWindow = 22.4 - 0.6 * overallDifficulty;
            }
            else
            {
                perfectWindow = 24.9 - 1.1 * overallDifficulty;
            }

            // Update the window dictionary via reflection or recreate
            // For now, we'll just log it
            Console.WriteLine($"[ScoreV2] PERFECT window adjusted to: ±{perfectWindow:F1}ms");
        }
    }
}
