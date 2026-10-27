#if DEBUG
#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Objects;
using System;

namespace Nullscent.Rulesets.Mania.UI.Drawables;

/// <summary>
/// Opt-in diagnostics for the generic DEBUG-only visual smoke harness.
/// These outlines and traces are never enabled by normal gameplay or Release builds.
/// </summary>
internal static class LongNoteRenderDiagnostics
{
    public static bool Enabled { get; set; }
    public static bool DrawOutlines { get; set; }
    public static bool DrawCheckerboardLaneBackground { get; set; }
    public static int DrawCallCount { get; private set; }

    public static void ResetDrawCallCount() => DrawCallCount = 0;

    public static void LogFrame(LongNote note, double currentTime, bool downScroll, int receptorY,
        int rawHeadY, int headY, int rawTailY, int tailY,
        int rawBodyTop, int rawBodyBottom, int bodyTop, int bodyBottom,
        bool headClamped, bool tailClamped, bool isDrawn, bool tailMaskedAtReceptor)
    {
        if (!Enabled) return;

        Console.WriteLine(
            $"[LN-Debug] time={currentTime:F3} start={note.StartTime:F3} end={note.EndTime:F3} release={note.TailReleaseTime:F3} " +
            $"headJudged={note.HeadJudged}/{note.HeadResult} tailJudged={note.TailJudged}/{note.TailResult} state={note.State} " +
            $"scroll={(downScroll ? "down" : "up")} receptorY={receptorY} " +
            $"headY={rawHeadY}->{headY} clamped={headClamped} tailY={rawTailY}->{tailY} clamped={tailClamped} " +
            $"body={rawBodyTop}..{rawBodyBottom}->{bodyTop}..{bodyBottom} " +
            $"tailClip={(tailMaskedAtReceptor ? $"at-{receptorY}" : "none")} drawn={isDrawn} draws={DrawCallCount}");
    }

    public static void TraceDraw(string piece, Texture2D texture, Vector2 logicalSize,
        Rectangle destination, Rectangle? source, SpriteEffects effects)
    {
        if (!Enabled) return;
        DrawCallCount++;
        string sourceText = source is { } rect ? rect.ToString() : "full";
        Console.WriteLine(
            $"[LN-Debug] draw#{DrawCallCount} piece={piece} src={sourceText} dst={destination} " +
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
