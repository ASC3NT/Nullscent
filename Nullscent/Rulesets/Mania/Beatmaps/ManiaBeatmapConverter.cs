#nullable enable

using Nullscent.Beatmap;
using Nullscent.Rulesets.Mania.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nullscent.Rulesets.Mania.Beatmaps
{
    public class ManiaBeatmapConverter
    {
        private readonly Beatmap.Beatmap _beatmap;
        private readonly int _targetColumns;
        private readonly Random _random;

        public ManiaBeatmapConverter(Beatmap.Beatmap beatmap, int? targetColumns = null)
        {
            _beatmap = beatmap;
            _targetColumns = targetColumns ?? beatmap.KeyCount;
            _random = new Random(beatmap.Metadata?.Title?.GetHashCode() ?? 0);
        }

        public ManiaBeatmap Convert()
        {
            var maniaObjects = new List<ManiaHitObject>();

            foreach (var hitObject in _beatmap.HitObjects)
            {
                int column = CalculateColumn(hitObject.X, _targetColumns);
                var samples = CreateSamples(hitObject);

                if (hitObject.IsLongNote)
                {
                    maniaObjects.Add(new HoldNote
                    {
                        StartTime = hitObject.Time,
                        EndTime = hitObject.EndTime,
                        Column = column,
                        Samples = samples
                    });
                }
                else
                {
                    maniaObjects.Add(new Note
                    {
                        StartTime = hitObject.Time,
                        Column = column,
                        Samples = samples
                    });
                }
            }

            return new ManiaBeatmap
            {
                HitObjects = maniaObjects,
                Metadata = _beatmap.Metadata,
                KeyCount = _targetColumns,
                TimingPoints = _beatmap.TimingPoints,
                OriginalBeatmap = _beatmap
            };
        }

        private int CalculateColumn(int x, int columns)
        {
            return Math.Clamp((int)Math.Floor(x * columns / 512.0), 0, columns - 1);
        }

        private HitSampleInfo[] CreateSamples(HitObject hitObject)
        {
            var samples = new List<HitSampleInfo> { new HitSampleInfo("hitnormal", "normal") };

            if ((hitObject.HitSound & 2) != 0) samples.Add(new HitSampleInfo("hitwhistle", "normal"));
            if ((hitObject.HitSound & 4) != 0) samples.Add(new HitSampleInfo("hitfinish", "normal"));
            if ((hitObject.HitSound & 8) != 0) samples.Add(new HitSampleInfo("hitclap", "normal"));

            return samples.ToArray();
        }

        public void ApplyRandomization(List<ManiaHitObject> hitObjects)
        {
            var columnMapping = Enumerable.Range(0, _targetColumns).OrderBy(x => _random.Next()).ToList();
            foreach (var hitObject in hitObjects)
                hitObject.Column = columnMapping[hitObject.Column];
        }

        public void ApplyMirror(List<ManiaHitObject> hitObjects)
        {
            foreach (var hitObject in hitObjects)
                hitObject.Column = _targetColumns - 1 - hitObject.Column;
        }
    }

    public class ManiaBeatmap
    {
        public List<ManiaHitObject> HitObjects { get; set; } = new();
        public BeatmapMetadata? Metadata { get; set; }
        public int KeyCount { get; set; }
        public List<TimingPoint> TimingPoints { get; set; } = new();
        public Beatmap.Beatmap? OriginalBeatmap { get; set; }

        public IEnumerable<Note> Notes => HitObjects.OfType<Note>();
        public IEnumerable<HoldNote> HoldNotes => HitObjects.OfType<HoldNote>();
        public int TotalHitObjects => HitObjects.Count;
    }
}
