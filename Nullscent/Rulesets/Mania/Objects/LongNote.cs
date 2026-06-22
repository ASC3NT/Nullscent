#nullable enable

using Microsoft.Xna.Framework;
using Nullscent.Rulesets.Mania.Judgements;
using System;

namespace Nullscent.Rulesets.Mania.Objects
{
    /// <summary>
    /// Long Note (LN) state machine for osu!mania ScoreV2-style gameplay.
    /// 
    /// States:
    /// - Idle: Waiting for head timing window
    /// - Holding: Key is held after successful head hit
    /// - Broken: Key was released early during hold
    /// - Released: Key released at tail (successful or not)
    /// - Finished: Both head and tail judged
    /// 
    /// Rules (ScoreV2):
    /// 1. Head and tail are judged SEPARATELY
    /// 2. If head missed → entire LN fails, never enters holding
    /// 3. If key released early → broken state, tail becomes Miss
    /// 4. Broken state persists even if key pressed again
    /// 5. Tail uses 1.5x longer windows
    /// 
    /// Deterministic behavior:
    /// - Uses song time (not frame delta)
    /// - State transitions are explicit
    /// - No frame-rate dependent logic
    /// </summary>
    public class LongNote : ManiaHitObject
    {
        // Timing
        public double EndTime { get; set; }
        public double Duration => EndTime - StartTime;

        // State machine
        public LongNoteState State { get; private set; } = LongNoteState.Idle;

        // Head judgement
        public bool HeadJudged { get; private set; }
        public HitResult HeadResult { get; private set; } = HitResult.None;
        public double HeadHitTime { get; private set; }

        // Tail judgement
        public bool TailJudged { get; private set; }
        public HitResult TailResult { get; private set; } = HitResult.None;
        public double TailReleaseTime { get; private set; }

        // Hold tracking
        public bool IsHolding => State == LongNoteState.Holding;
        public bool IsBroken => State == LongNoteState.Broken;
        public bool IsFinished => State == LongNoteState.Finished;

        // Break tracking
        private double _breakTime;
        private double _overallDifficulty;

        public LongNote(double startTime, double endTime, int column, double overallDifficulty = 5.0)
        {
            StartTime = startTime;
            EndTime = endTime;
            Column = column;
            _overallDifficulty = overallDifficulty;

            if (endTime <= startTime)
                throw new ArgumentException("EndTime must be after StartTime");
        }

        /// <summary>
        /// Attempt to judge the head when key is pressed.
        /// Returns true if head was successfully hit.
        /// </summary>
        public bool TryJudgeHead(double currentTime, ManiaHitWindows hitWindows)
        {
            if (HeadJudged || State != LongNoteState.Idle)
                return false;

            double offset = currentTime - StartTime;
            HitResult result = hitWindows.JudgeHit(offset);

            if (result == HitResult.None)
                return false; // Too early

            // Judge head
            HeadJudged = true;
            HeadResult = result;
            HeadHitTime = currentTime;

            Console.WriteLine($"[LN] Head judged: {result} at {currentTime:F1}ms (offset: {offset:+0.0;-0.0}ms)");

            // State transition
            if (result == HitResult.Miss)
            {
                // Head missed → entire LN fails immediately
                State = LongNoteState.Finished;
                TailJudged = true;
                TailResult = HitResult.Miss;
                Console.WriteLine($"[LN] Head missed → LN failed immediately");
            }
            else
            {
                // Head hit → enter holding state
                State = LongNoteState.Holding;
            }

            return result != HitResult.Miss;
        }

        /// <summary>
        /// Update hold state. Must be called every frame during holding.
        /// </summary>
        public void UpdateHold(double currentTime, bool isKeyHeld)
        {
            if (State != LongNoteState.Holding)
                return;

            // Check if key is still held
            if (!isKeyHeld)
            {
                // Key released early → break
                State = LongNoteState.Broken;
                _breakTime = currentTime;
                Console.WriteLine($"[LN] Broken at {currentTime:F1}ms (released early)");
            }
        }

        /// <summary>
        /// Attempt to judge the tail when key is released.
        /// Uses integer truncation for osu!mania timing compatibility.
        /// </summary>
        public void TryJudgeTail(double currentTime, ManiaHitWindows hitWindows)
        {
            if (TailJudged)
                return;

            if (State == LongNoteState.Idle)
                return; // Can't judge tail before head

            TailReleaseTime = currentTime;

            // If broken, tail is automatically Miss
            if (State == LongNoteState.Broken)
            {
                TailJudged = true;
                TailResult = HitResult.Miss;
                State = LongNoteState.Finished;
                Console.WriteLine($"[LN] Tail: Miss (was broken at {_breakTime:F1}ms)");
                return;
            }

            // Judge tail timing with truncated offset (osu!mania compatibility)
            double rawOffset = currentTime - EndTime;
            int offsetMs = (int)Math.Truncate(rawOffset);

            // ScoreV2: tail windows are 1.5x longer
            var tailWindows = new TailHitWindows(_overallDifficulty, true);
            HitResult result = tailWindows.JudgeTail(offsetMs);

            if (result == HitResult.None)
                return; // Too early for tail

            TailJudged = true;
            TailResult = result;
            State = LongNoteState.Finished;

            Console.WriteLine($"[LN] Tail judged: {result} at {currentTime:F1}ms (offset: {offsetMs}ms)");
        }

        /// <summary>
        /// Auto-miss check for head (if not hit in time).
        /// </summary>
        public void CheckHeadAutoMiss(double currentTime, ManiaHitWindows hitWindows)
        {
            if (HeadJudged || State != LongNoteState.Idle)
                return;

            double offset = currentTime - StartTime;

            if (hitWindows.IsPastAutoMissWindow(offset))
            {
                HeadJudged = true;
                HeadResult = HitResult.Miss;
                TailJudged = true;
                TailResult = HitResult.Miss;
                State = LongNoteState.Finished;
                Console.WriteLine($"[LN] Auto-missed (head not hit in time)");
            }
        }

        /// <summary>
        /// Auto-miss check for tail (if holding past tail window).
        /// </summary>
        public void CheckTailAutoMiss(double currentTime, ManiaHitWindows hitWindows)
        {
            if (TailJudged || State == LongNoteState.Idle)
                return;

            double offset = currentTime - EndTime;

            if (hitWindows.IsPastAutoMissWindow(offset))
            {
                TailJudged = true;
                TailResult = HitResult.Miss;
                State = LongNoteState.Finished;
                Console.WriteLine($"[LN] Tail auto-missed (held too long past window)");
            }
        }

        /// <summary>
        /// Returns true if both head and tail need to be judged.
        /// </summary>
        public bool IsFullyJudged => HeadJudged && TailJudged;
    }

    /// <summary>
    /// Long Note state machine states.
    /// </summary>
    public enum LongNoteState
    {
        /// <summary>
        /// Waiting for head to be hit.
        /// </summary>
        Idle,

        /// <summary>
        /// Head was hit, key is being held.
        /// </summary>
        Holding,

        /// <summary>
        /// Key was released early, tail will be Miss.
        /// </summary>
        Broken,

        /// <summary>
        /// Key released, waiting for tail judgement.
        /// </summary>
        Released,

        /// <summary>
        /// Both head and tail judged, LN is done.
        /// </summary>
        Finished
    }
}
