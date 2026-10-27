#if DEBUG
#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Skin;
using System;
using System.Linq;
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
        public bool AssetsLogged;
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

    public static void TraceResolvedPieces(LongNote note, int keyCount, int column, bool downScroll,
        SkinManager skinManager, ResolvedManiaKeymode mode, ResolvedManiaLane lane,
        ResolvedSkinAnimation? head, ResolvedSkinAnimation? body, ResolvedSkinAnimation? tail,
        SkinSpriteMetrics headMetrics, SkinSpriteMetrics bodyMetrics, SkinSpriteMetrics tailMetrics)
    {
        if (!Enabled) return;

        FrameTraceState state = frameTraceStates.GetValue(note, static _ => new FrameTraceState());
        if (state.AssetsLogged) return;
        state.AssetsLogged = true;

        ManiaSkinDefinition skin = skinManager.CurrentDefinition;
        ManiaLaneDefinition laneDefinition = lane.Definition;
        ManiaKeymodeDefinition modeDefinition = mode.Definition;
        Console.WriteLine(
            $"[LN-Assets] skin={skin.Name} id={skin.Id} profile={skin.CompatibilityProfile} version={skin.LegacyVersion?.ToString("0.##") ?? "latest"} " +
            $"keys={keyCount} column={column} scroll={(downScroll ? "down" : "up")} " +
            $"bodyMode={skinManager.GetNoteBodyMode(keyCount, column)} " +
            $"connections={modeDefinition.HoldAssembly.HeadConnectionRatio:0.###}/{modeDefinition.HoldAssembly.TailConnectionRatio:0.###}");

        TracePiece("H", CandidateList(laneDefinition.LongHeadAsset, modeDefinition.LongHeadStyle.Asset,
                laneDefinition.NoteAsset, modeDefinition.NoteStyle.Asset),
            head, headMetrics, skinManager.FlipNote(keyCount, column, "H", downScroll), skinManager);
        TracePiece("L", CandidateList(laneDefinition.LongBodyAsset, modeDefinition.LongBodyStyle.Asset),
            body, bodyMetrics, skinManager.FlipNote(keyCount, column, "L", downScroll), skinManager);
        TracePiece("T", CandidateList(laneDefinition.LongTailAsset, modeDefinition.LongTailStyle.Asset,
                laneDefinition.LongHeadAsset, modeDefinition.LongHeadStyle.Asset,
                laneDefinition.NoteAsset, modeDefinition.NoteStyle.Asset),
            tail, tailMetrics, skinManager.FlipNote(keyCount, column, "T", downScroll), skinManager);
    }

    private static string CandidateList(params string?[] candidates) =>
        string.Join(" -> ", candidates.Where(static candidate => !string.IsNullOrWhiteSpace(candidate)));

    private static void TracePiece(string role, string candidates, ResolvedSkinAnimation? animation,
        SkinSpriteMetrics metrics, bool flipped, SkinManager skinManager)
    {
        Texture2D? texture = animation?.FirstFrame;
        Console.WriteLine(
            $"[LN-Assets] piece={role} candidates=[{candidates}] frames={animation?.Frames.Count ?? 0} " +
            $"flip={flipped} metrics=width:{metrics.WidthRatio:0.###},height:{metrics.HeightMode}/{metrics.HeightValue:0.###}," +
            $"reference:{metrics.ReferenceWidth:0.###},offset:{metrics.OffsetXRatio:0.###}/{metrics.OffsetY:0.###} " +
            $"resolved=({skinManager.DescribeResolvedAsset(texture)})");
    }

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
