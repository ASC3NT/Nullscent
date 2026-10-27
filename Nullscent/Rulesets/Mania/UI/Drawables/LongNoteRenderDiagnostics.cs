#if DEBUG
#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Objects;
using System;
using System.Runtime.CompilerServices;

namespace Nullscent.Rulesets.Mania.UI.Drawables;

/// <summary>
/// Opt-in diagnostics for the generic DEBUG-only visual smoke harness.
/// These outlines and traces are never enabled by normal gameplay or Release builds.
/// </summary>
internal static class LongNoteRenderDiagnostics
{
    private sealed class FrameTraceState
    {
        public long LastBucket = long.MinValue;
    }

    private static readonly ConditionalWeakTable<LongNote, FrameTraceState> frameTraceStates = new();

    public static bool Enabled { get; set; }
    public static bool DrawOutlines { get; set; }
    public static bool DrawCheckerboardLaneBackground { get; set; }
    public static bool GameplayTraceMode { get; private set; }
    public static int DrawCallCount { get; private set; }

    public static void ResetDrawCallCount() => DrawCallCount = 0;

    public static void EnableGameplayDiagnostics()
    {
        Enabled = true;
        DrawOutlines = true;
        DrawCheckerboardLaneBackground = true;
        GameplayTraceMode = true;
        Console.WriteLine("[LN-Debug] Gameplay diagnostics enabled: checkerboard and outlines are debug-only; lifecycle traces are sampled near each hold tail.");
    }

    public static bool ShouldTraceFrame(LongNote note, double currentTime)
    {
        if (!Enabled) return false;
        if (!GameplayTraceMode) return true;
        if (!double.IsFinite(currentTime) || currentTime < note.EndTime - 400 || currentTime > note.EndTime + 200)
            return false;

        long bucket = (long)Math.Floor(currentTime / 16d);
        FrameTraceState state = frameTraceStates.GetValue(note, static _ => new FrameTraceState());
        if (state.LastBucket == bucket) return false;
        state.LastBucket = bucket;
        return true;
    }

    public static bool ShouldTraceEffect(double elapsedMs) =>
        Enabled && (!GameplayTraceMode || (double.IsFinite(elapsedMs) && elapsedMs >= 0 && elapsedMs <= 48));

    public static void TraceStateEvent(string eventName, LongNote note, int column, double currentTime,
        LongNoteState previousState, bool previousTailJudged, bool drawableIsJudged, bool keyPressed)
    {
        bool isHeadPress = eventName == "key-press";
        if (!Enabled || !GameplayTraceMode || !double.IsFinite(currentTime)
            || (!isHeadPress && (currentTime < note.EndTime - 500 || currentTime > note.EndTime + 300)))
            return;

        Console.WriteLine(
            $"[LN-State] event={eventName} col={column} time={currentTime:F3} start={note.StartTime:F3} end={note.EndTime:F3} " +
            $"state={previousState}->{note.State} tailJudged={previousTailJudged}->{note.TailJudged} " +
            $"head={note.HeadJudged}/{note.HeadResult}@{note.HeadHitTime:F3} tail={note.TailResult}@{note.TailReleaseTime:F3} " +
            $"drawableJudged={drawableIsJudged} keyPressed={keyPressed}");
    }

    public static void LogFrame(LongNote note, double currentTime, bool downScroll, int receptorY,
        int rawHeadY, int headY, int rawTailY, int tailY,
        int rawBodyTop, int rawBodyBottom, int bodyTop, int bodyBottom,
        bool headClamped, bool tailClamped, bool isDrawn, bool tailMaskedAtReceptor, int column,
        bool drawableIsJudged, bool traceFrame)
    {
        if (!Enabled || !traceFrame) return;

        Console.WriteLine(
            $"[LN-Debug] col={column} time={currentTime:F3} start={note.StartTime:F3} end={note.EndTime:F3} release={note.TailReleaseTime:F3} " +
            $"headJudged={note.HeadJudged}/{note.HeadResult} tailJudged={note.TailJudged}/{note.TailResult} state={note.State} " +
            $"scroll={(downScroll ? "down" : "up")} receptorY={receptorY} " +
            $"headY={rawHeadY}->{headY} clamped={headClamped} tailY={rawTailY}->{tailY} clamped={tailClamped} " +
            $"body={rawBodyTop}..{rawBodyBottom}->{bodyTop}..{bodyBottom} " +
            $"tailClip={(tailMaskedAtReceptor ? $"at-{receptorY}" : "none")} drawableJudged={drawableIsJudged} " +
            $"drawn={isDrawn} draws={DrawCallCount}");
    }

    public static void TraceDraw(string piece, Texture2D texture, Vector2 logicalSize,
        Rectangle destination, Rectangle? source, SpriteEffects effects, bool traceFrame = true)
    {
        if (!Enabled || !traceFrame) return;
        DrawCallCount++;
        string sourceText = source is { } rect ? rect.ToString() : "full";
        Console.WriteLine(
            $"[LN-Debug] draw#{DrawCallCount} piece={piece} texture={texture.Name ?? "<unnamed>"} src={sourceText} dst={destination} " +
            $"texture={texture.Width}x{texture.Height} logical={logicalSize.X:0.##}x{logicalSize.Y:0.##} effects={effects}");
    }

    public static void TraceLayer(string layer, Texture2D texture, Vector2 logicalSize,
        Rectangle destination, Rectangle? source, SpriteEffects effects, bool traceFrame = true)
    {
        if (!Enabled || !traceFrame) return;
        string sourceText = source is { } rect ? rect.ToString() : "full";
        Console.WriteLine(
            $"[LN-Layer] layer={layer} texture={texture.Name ?? "<unnamed>"} src={sourceText} dst={destination} " +
            $"texture={texture.Width}x{texture.Height} logical={logicalSize.X:0.##}x{logicalSize.Y:0.##} effects={effects}");
    }

    public static void DrawOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rectangle, Color color)
    {
        if (!Enabled || !DrawOutlines || rectangle.Width <= 0 || rectangle.Height <= 0) return;

        const int thickness = 2;
        spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
#endif
