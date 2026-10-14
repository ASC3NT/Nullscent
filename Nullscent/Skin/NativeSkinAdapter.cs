#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nullscent.Skin
{
    public static class NativeSkinAdapter
    {
        public const string ManifestFileName = "NullscentSkin.json";
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        static NativeSkinAdapter() => jsonOptions.Converters.Add(new JsonStringEnumConverter());

        public static ManiaSkinDefinition Load(string directory)
        {
            string root = Path.GetFullPath(directory);
            string manifestPath = Path.Combine(root, ManifestFileName);
            ManiaSkinDefinition loaded = JsonSerializer.Deserialize<ManiaSkinDefinition>(File.ReadAllText(manifestPath), jsonOptions)
                ?? throw new InvalidDataException("El manifiesto nativo no contiene una skin.");
            if (loaded.FormatVersion != 1)
                throw new InvalidDataException($"Versión de NullscentSkin no compatible: {loaded.FormatVersion}.");

            foreach (var pair in loaded.Keymodes)
            {
                if (pair.Key != pair.Value.KeyCount || pair.Key < 1 || pair.Key > 18)
                    throw new InvalidDataException($"Keymode inválido en el manifiesto: {pair.Key}.");
                if (pair.Value.Lanes.Count != pair.Key)
                    throw new InvalidDataException($"El keymode {pair.Key}K requiere una definición de lane por columna.");
                if (pair.Value.StageStyle is not { } stageStyle
                    || !IsPositiveFinite(stageStyle.SideHeightScale)
                    || !IsPositiveFinite(stageStyle.HintHeightScale)
                    || !IsPositiveFinite(stageStyle.BottomScale))
                    throw new InvalidDataException($"El keymode {pair.Key}K contiene escalas de stage inválidas.");
                foreach (var lane in pair.Value.Lanes)
                    if (!float.IsFinite(lane.Width) || lane.Width <= 0 || !float.IsFinite(lane.GapAfter) || lane.GapAfter < 0)
                        throw new InvalidDataException($"El keymode {pair.Key}K contiene métricas de lane inválidas.");
            }

            return new ManiaSkinDefinition
            {
                FormatVersion = loaded.FormatVersion,
                Id = string.IsNullOrWhiteSpace(loaded.Id) ? root : loaded.Id,
                Name = string.IsNullOrWhiteSpace(loaded.Name) ? Path.GetFileName(root) : loaded.Name,
                Author = loaded.Author,
                Provider = SkinProviderKind.NullscentNative,
                CompatibilityProfile = ManiaSkinCompatibilityProfile.NullscentNative,
                UsesStableLegacyUnits = false,
                LegacyHudScale = loaded.LegacyHudScale,
                AnimationFramerate = loaded.AnimationFramerate,
                ComboPrefix = loaded.ComboPrefix,
                ComboOverlap = loaded.ComboOverlap,
                Keymodes = loaded.Keymodes,
                SharedAssets = loaded.SharedAssets
            };
        }

        private static bool IsPositiveFinite(float value) => float.IsFinite(value) && value > 0;
    }

    public static class SkinSourceDecoder
    {
        public static ManiaSkinDefinition Load(string directory, out SkinConfig? legacyConfig,
            LegacySkinCompatibilityMode compatibilityMode = LegacySkinCompatibilityMode.Stable)
        {
            string root = Path.GetFullPath(directory);
            string nativeManifest = Path.Combine(root, NativeSkinAdapter.ManifestFileName);
            if (File.Exists(nativeManifest))
            {
                legacyConfig = null;
                return NativeSkinAdapter.Load(root);
            }

            legacyConfig = SkinConfigParser.Parse(root);
            if (legacyConfig.ParseError != null)
                throw new InvalidDataException(legacyConfig.ParseError);
            return LegacyManiaSkinAdapter.Decode(legacyConfig, root, compatibilityMode);
        }
    }
}
