#nullable enable

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.Skin
{
    /// <summary>An already-resolved animation shared by notes, keys, stage and lighting.</summary>
    public sealed class ResolvedSkinAnimation
    {
        public IReadOnlyList<Texture2D> Frames { get; }
        public List<Texture2D> Textures { get; }
        public int FramesPerSecond { get; }
        public Texture2D? FirstFrame => Frames.Count == 0 ? null : Frames[0];

        public ResolvedSkinAnimation(IEnumerable<Texture2D> frames, int framesPerSecond)
        {
            Textures = frames.ToList();
            Frames = Textures.AsReadOnly();
            FramesPerSecond = ResolveFrameRate(framesPerSecond, Frames.Count);
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
            if (Frames.Count == 0) return null;
            if (Frames.Count == 1 || !double.IsFinite(timeMs) || timeMs < 0) return Frames[0];
            int index = (int)(timeMs * FramesPerSecond / 1000d) % Frames.Count;
            return Frames[index];
        }

        public Texture2D? FrameAtClamped(double timeMs)
        {
            if (Frames.Count == 0) return null;
            if (Frames.Count == 1 || !double.IsFinite(timeMs) || timeMs < 0) return Frames[0];
            int index = (int)(timeMs * FramesPerSecond / 1000d);
            return Frames[Math.Clamp(index, 0, Frames.Count - 1)];
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
    /// Immutable per-keymode snapshot consumed by the renderer. It contains normalized
    /// metrics and loaded assets, so the draw path performs no config parsing or file lookup.
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
            StageAssets = new Dictionary<string, ResolvedSkinAnimation>(stageAssets, StringComparer.OrdinalIgnoreCase);
            Judgements = new Dictionary<string, ResolvedSkinAnimation>(judgements, StringComparer.OrdinalIgnoreCase);
        }
    }
}
