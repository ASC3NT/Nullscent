#nullable enable

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nullscent.Skin
{
    /// <summary>An already-resolved animation shared by notes, keys, stage and lighting.</summary>
    public sealed class ResolvedSkinAnimation
    {
        public IReadOnlyList<Texture2D> Frames { get; }
        public IReadOnlyList<Texture2D> Textures => Frames;
        public double FramesPerSecond { get; }
        public double FrameDurationMilliseconds { get; }
        public Texture2D? FirstFrame => Frames.Count == 0 ? null : Frames[0];

        public ResolvedSkinAnimation(IEnumerable<Texture2D> frames, int framesPerSecond,
            AnimationTimingDefinition? timing = null)
        {
            Frames = Array.AsReadOnly(frames.ToArray());
            int resolvedRate = ResolveFrameRate(framesPerSecond, Frames.Count);
            FrameDurationMilliseconds = timing?.ResolveFrameDurationMs(Frames.Count) ?? 1000d / resolvedRate;
            FramesPerSecond = 1000d / FrameDurationMilliseconds;
        }

        /// <summary>
        /// osu! skin.ini uses -1 to play all frames over one second. Convert that
        /// convention to a stable frame rate when creating the resolved animation.
        /// </summary>
        public static int ResolveFrameRate(int configuredFramesPerSecond, int frameCount) =>
            configuredFramesPerSecond == -1
                ? Math.Clamp(frameCount, 1, 1000)
                : Math.Clamp(configuredFramesPerSecond, 1, 1000);

        public Texture2D? FrameAt(double timeMs)
        {
            int index = FrameIndexAt(timeMs);
            return index < 0 ? null : Frames[index];
        }

        public Texture2D? FrameAtClamped(double timeMs)
        {
            int index = ClampedFrameIndexAt(timeMs);
            return index < 0 ? null : Frames[index];
        }

        internal int FrameIndexAt(double timeMs)
        {
            if (Frames.Count == 0) return -1;
            if (Frames.Count == 1 || !double.IsFinite(timeMs) || timeMs < 0) return 0;
            return (int)(timeMs / FrameDurationMilliseconds) % Frames.Count;
        }

        internal int ClampedFrameIndexAt(double timeMs)
        {
            if (Frames.Count == 0) return -1;
            if (Frames.Count == 1 || !double.IsFinite(timeMs) || timeMs < 0) return 0;
            return Math.Clamp((int)(timeMs / FrameDurationMilliseconds), 0, Frames.Count - 1);
        }
    }

    public sealed class ResolvedManiaLane
    {
        public ManiaLaneDefinition Definition { get; }
        public ResolvedSkinAnimation? Note { get; }
        public ResolvedSkinAnimation? Receptor { get; }
        public ResolvedSkinAnimation? PressedReceptor { get; }
        public ResolvedSkinAnimation? LongHead { get; }
        public ResolvedSkinAnimation? LongBody { get; }
        public ResolvedSkinAnimation? LongTail { get; }
        public ResolvedSkinAnimation? NoteLighting { get; }
        public ResolvedSkinAnimation? HoldLighting { get; }

        public ResolvedManiaLane(ManiaLaneDefinition definition, ResolvedSkinAnimation? note,
            ResolvedSkinAnimation? receptor, ResolvedSkinAnimation? pressedReceptor,
            ResolvedSkinAnimation? longHead, ResolvedSkinAnimation? longBody,
            ResolvedSkinAnimation? longTail, ResolvedSkinAnimation? noteLighting,
            ResolvedSkinAnimation? holdLighting)
        {
            Definition = definition;
            Note = note;
            Receptor = receptor;
            PressedReceptor = pressedReceptor;
            LongHead = longHead;
            LongBody = longBody;
            LongTail = longTail;
            NoteLighting = noteLighting;
            HoldLighting = holdLighting;
        }
    }

    /// <summary>
    /// Resolved per-keymode snapshot consumed by the renderer. Its animation and asset
    /// collections are read-only, so consumers cannot mutate cached frame or asset order.
    /// </summary>
    public sealed class ResolvedManiaKeymode
    {
        public ManiaKeymodeDefinition Definition { get; }
        public IReadOnlyList<ResolvedManiaLane> Lanes { get; }
        public IReadOnlyDictionary<string, ResolvedSkinAnimation> StageAssets { get; }
        public IReadOnlyDictionary<string, ResolvedSkinAnimation> Judgements { get; }

        public ResolvedManiaKeymode(ManiaKeymodeDefinition definition, IEnumerable<ResolvedManiaLane> lanes,
            IDictionary<string, ResolvedSkinAnimation> stageAssets, IDictionary<string, ResolvedSkinAnimation> judgements)
        {
            Definition = definition;
            Lanes = Array.AsReadOnly(lanes.ToArray());
            StageAssets = new ReadOnlyDictionary<string, ResolvedSkinAnimation>(
                new Dictionary<string, ResolvedSkinAnimation>(stageAssets, StringComparer.OrdinalIgnoreCase));
            Judgements = new ReadOnlyDictionary<string, ResolvedSkinAnimation>(
                new Dictionary<string, ResolvedSkinAnimation>(judgements, StringComparer.OrdinalIgnoreCase));
        }
    }
}
