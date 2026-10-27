#nullable enable
#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Config;

namespace Nullscent.Skin
{
    internal static class SkinCoreSelfTests
    {
        public static void RunAll()
        {
            TestLegacyNormalizationAndTextAlias();
            TestLegacyProfilesStayScopedToConfiguredKeymodes();
            TestMultipleLegacyKeymodeSections();
            TestLegacySpecialStyleAssetMapping();
            TestLegacyParserDiagnosticsAndColourContracts();
            TestLegacyReservedUserVersion();
            TestAssetlessSkinsRemainPartial();
            TestResolvedCollectionsAreReadOnlySnapshots();
            TestSkinWithoutIniHasConventionalKeymodes();
            TestResponsiveLayoutAndScrollMirror();
            TestStageLightPositionMirroring();
            TestNativeLeftPlacement();
            TestNativeManifests();
            TestUserOverridePrecedence();
            TestSkinScrollPreferenceAndLegacyFlips();
            TestCompatibilityProfilesAndReceptorAnchors();
            TestLegacyVersionFlipDefaults();
            TestFlipPropertyClassificationByProfile();
            TestLegacyCompatibilitySettingRollback();
            TestHighResolutionSettingRollback();
            TestStagePresentationNormalizationAndLayout();
            TestLongNoteBodyVisualRules();
            TestLegacyColourLightIsStageOnly();
            TestLegacyHoldColourSuffixIsNotMisrepresentedAsSupported();
            TestLegacyAnimationRateSemantics();
            TestAnimationTimingResolution();
            TestHoldLightingAnimationLifecycle();
            TestProviderScopedAssetCascade();
            TestHitSoundSourcePrecedence();
            TestPairedHighResolutionLogicalSizing();
            TestTextureDownscalePreservesStraightAlphaEdges();
            Console.WriteLine("[SkinTests] 31 deterministic skin checks passed.");
        }

        private static void TestPairedHighResolutionLogicalSizing()
        {
            Microsoft.Xna.Framework.Vector2 standard = new(75, 200);
            Microsoft.Xna.Framework.Vector2 validHighResolution = new(150, 400);
            Microsoft.Xna.Framework.Vector2 malformedHighResolution = new(150, 375);
            Microsoft.Xna.Framework.Vector2 unpairedHighResolution = new(320, 800);

            AssertNear(SkinAssetResolver.ResolveLogicalSize(standard, false, null).Y, 200,
                "A standard texture should use its physical dimensions as logical dimensions.");
            Assert(SkinAssetResolver.HasExpectedHighResolutionDimensions(standard, validHighResolution),
                "A valid @2x pair should be exactly twice the 1x dimensions on both axes.");
            Assert(!SkinAssetResolver.HasExpectedHighResolutionDimensions(standard, malformedHighResolution),
                "A malformed @2x pair should be rejected instead of distorting its visible geometry.");
            Microsoft.Xna.Framework.Vector2 validPair = SkinAssetResolver.ResolveLogicalSize(validHighResolution, true, standard);
            AssertNear(validPair.X, standard.X, "A valid @2x pair should preserve the 1x logical width.");
            AssertNear(validPair.Y, standard.Y, "A valid @2x pair should preserve the 1x logical height.");
            Microsoft.Xna.Framework.Vector2 unpaired = SkinAssetResolver.ResolveLogicalSize(unpairedHighResolution, true, null);
            AssertNear(unpaired.X, unpairedHighResolution.X / 2f,
                "An @2x-only texture should retain the conventional half-pixel logical size.");
            AssertNear(unpaired.Y, unpairedHighResolution.Y / 2f,
                "An @2x-only texture should retain the conventional half-pixel logical size.");
        }

        private static void TestTextureDownscalePreservesStraightAlphaEdges()
        {
            byte[] source =
            {
                255, 0, 0, 255,
                0, 255, 0, 0
            };
            byte[] downscaled = SkinAssetResolver.ResizeBilinear(source, 2, 1, 1, 1);
            Assert(downscaled[0] == 255 && downscaled[1] == 0 && downscaled[2] == 0
                && downscaled[3] is >= 127 and <= 128,
                "Straight-alpha downscaling must filter premultiplied colour, then restore straight RGB without bleeding transparent texel colour.");

            byte[] transparentSource = { 17, 33, 65, 0, 240, 180, 120, 0 };
            byte[] transparentResult = SkinAssetResolver.ResizeBilinear(transparentSource, 2, 1, 1, 1);
            Assert(transparentResult[3] == 0,
                "Downscaling fully transparent pixels must keep the resulting alpha transparent.");
        }

        private static void TestResolvedCollectionsAreReadOnlySnapshots()
        {
            var sourceFrames = new List<Texture2D>();
            var animation = new ResolvedSkinAnimation(sourceFrames, 24);
            sourceFrames.Add(null!);
            Assert(animation.Frames.Count == 0, "Resolved animation must own a snapshot, not the provider's mutable frame list.");
            Assert(ReferenceEquals(animation.Frames, animation.Textures), "The compatibility texture view should share the immutable frame snapshot.");
            Assert(animation.Textures is not IList<Texture2D> textures || textures.IsReadOnly,
                "Resolved animation must not expose a mutable texture list to renderer consumers.");

            var sourceAssets = new Dictionary<string, ResolvedSkinAnimation> { ["hint"] = animation };
            var sourceLanes = new List<ResolvedManiaLane>
            {
                new(new ManiaLaneDefinition(), null, null, null, null, null, null, null, null)
            };
            var mode = new ResolvedManiaKeymode(new ManiaKeymodeDefinition(), sourceLanes, sourceAssets, sourceAssets);
            sourceLanes.Clear();
            sourceAssets.Clear();
            Assert(mode.Lanes.Count == 1 && mode.Lanes is IList<ResolvedManiaLane> lanes && lanes.IsReadOnly,
                "Resolved lane definitions must be copied into a read-only snapshot.");
            Assert(mode.StageAssets.Count == 1, "Resolved stage assets must be copied from the mutable staging dictionary.");
            bool stageAssetsReadOnly = mode.StageAssets is not IDictionary<string, ResolvedSkinAnimation> stageMap
                || stageMap.IsReadOnly;
            Assert(stageAssetsReadOnly, "Resolved stage assets must not expose a mutable dictionary.");
            bool judgementsReadOnly = mode.Judgements is not IDictionary<string, ResolvedSkinAnimation> judgementMap
                || judgementMap.IsReadOnly;
            Assert(judgementsReadOnly, "Resolved judgement assets must not expose a mutable dictionary.");
        }

        private static void TestLegacyNormalizationAndTextAlias()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Name: Test Legacy
                    Version: 2.5
                    AnimationFramerate: 24
                    [Mania]
                    Keys: 4
                    ColumnStart: 336
                    ColumnRight: 19
                    ColumnWidth: 50,40
                    ColumnSpacing: 4
                    HitPosition: 400
                    LightPosition: 413
                    UpsideDown: 1
                    NoteFlipWhenUpsideDown: 0
                    NoteFlipWhenUpsideDown1H: 1
                    KeyFlipWhenUpsideDown: 0
                    KeyFlipWhenUpsideDown2D: 1
                    NoteBodyStyle: stretch
                    NoteBodyStyle2: tile-from-head
                    """);
                SkinConfig parsed = SkinConfigParser.Parse(temp);
                ManiaSkinDefinition skin = LegacyManiaSkinAdapter.Decode(parsed);
                ManiaKeymodeDefinition mode = skin.Keymodes[4];
                Assert(mode.Lanes.Count == 4, "Legacy keymode should fill missing lane defaults.");
                AssertNear(mode.Lanes[0].Width, 80f, "Legacy ColumnWidth should include the 1.6 stable factor.");
                AssertNear(mode.Lanes[1].Width, 64f, "Legacy lane widths should be independent.");
                AssertNear(mode.Lanes[2].Width, 48f, "Missing legacy lane values should use the format default.");
                AssertNear(mode.ColumnStart!.Value, 537.6f, "ColumnStart should be normalized into stable design units.");
                AssertNear(mode.HitPosition!.Value, 640f, "HitPosition should be normalized into stable design units.");
                AssertNear(mode.LightPosition!.Value, 660.8f,
                    "Legacy LightPosition should convert to the equivalent top-origin logical coordinate.");
                Assert(mode.BodyMode == NoteBodyRenderMode.Stretch, "The textual stretch alias should parse.");
                Assert(skin.AnimationFramerate == 24 && mode.NoteStyle.FramesPerSecond == 60,
                    "General animation speed should normalize without overriding mania note animation speed.");
                Assert(mode.JudgementFramesPerSecond == 60,
                    "Mania hit bursts should retain their fixed 60 FPS rate despite the general animation setting.");
                Assert(mode.Lanes[2].BodyMode == NoteBodyRenderMode.TileFromHead, "Per-column body style should survive normalization.");
                Assert(mode.LongBodyStyle.HeightMode == SpriteHeightMode.ReferenceWidthAspect,
                    "Legacy long-note body tiles must use WidthForNoteHeightScale just like the head and tail.");
                Assert(!mode.ReceptorStyle.Animate
                    && mode.StageAssetAnimations["left"] == false
                    && mode.StageAssetAnimations["right"] == false
                    && mode.StageAssetAnimations["hint"] == false
                    && mode.StageAssetAnimations["bottom"]
                    && mode.StageAssetAnimations["light"],
                    "Legacy numbered-frame discovery must follow osu!mania's static receptor/side/hint and animated bottom/light asset contracts.");
                Assert(mode.LongBodyStyle.FramesPerSecond == 30
                    && mode.NoteStyle.FramesPerSecond == 60,
                    "Legacy Mania animation defaults must stay component-specific instead of inheriting General.AnimationFramerate.");
                Assert(mode.UpsideDown == true, "The skin orientation preference should survive normalization.");
                Assert(mode.SupportsFlipOverrides && mode.NoteFlipWhenUpsideDown == false && mode.KeyFlipWhenUpsideDown == false,
                    "Version 2.5 global flip rules should be normalized.");
                Assert(ManiaSkinRules.FlipNote(mode, 1, "H", downScroll: false), "A per-part note flip should override the global note rule.");
                Assert(!ManiaSkinRules.FlipNote(mode, 2, "H", downScroll: false), "A global note flip should apply where no column override exists.");
                Assert(ManiaSkinRules.FlipKey(mode, 2, pressed: true, downScroll: false), "A pressed-key flip should override the global key rule.");
                Assert(!ManiaSkinRules.FlipKey(mode, 1, pressed: false, downScroll: false), "A global key flip should apply where no state override exists.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestSkinWithoutIniHasConventionalKeymodes()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                ManiaSkinDefinition skin = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp));
                Assert(skin.LegacyVersion == null, "A skin without skin.ini should use latest rules.");
                Assert(skin.Keymodes.ContainsKey(4) && skin.Keymodes.ContainsKey(7), "Conventional skins should resolve without a skin.ini keymode section.");
                Assert(skin.Keymodes[7].Lanes.Count == 7, "A keymode profile should be created for the requested count.");
                Assert(ManiaSkinRules.FlipNote(skin.Keymodes[4], 0, "", downScroll: false),
                    "A legacy skin without skin.ini should use the latest note auto-flip rules.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), "[Mania]\nKeys: 4\n");
                ManiaSkinDefinition iniWithoutVersion = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp));
                Assert(iniWithoutVersion.LegacyVersion == 1.0
                    && !ManiaSkinRules.FlipNote(iniWithoutVersion.Keymodes[4], 0, "", downScroll: false),
                    "A present skin.ini without Version should use v1.0 and not receive v2.5 note flips.");
                Assert(!iniWithoutVersion.Keymodes.ContainsKey(7),
                    "A legacy skin.ini must not synthesize an active keymode when that keymode section is absent.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestLegacyProfilesStayScopedToConfiguredKeymodes()
        {
            string temp = Path.Combine(Path.GetTempPath(), "nullscent-skin-keymode-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 4
                    ColumnWidth: 40,40,40,40
                    """);
                ManiaSkinDefinition definition = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp));
                Assert(definition.Keymodes.Count == 1 && definition.Keymodes.ContainsKey(4),
                    "A skin.ini with one explicit keymode should keep only that legacy profile so other keycounts use the coherent internal profile.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestMultipleLegacyKeymodeSections()
        {
            string temp = Path.Combine(Path.GetTempPath(), "nullscent-skin-multiple-keymodes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 4
                    ColumnWidth: 40,50,60,70,999
                    [Mania]
                    Keys: 7
                    ColumnWidth: 20,21,22,23,24,25,26
                    """);
                ManiaSkinDefinition definition = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp));
                Assert(definition.Keymodes.Count == 2 && definition.Keymodes.ContainsKey(4) && definition.Keymodes.ContainsKey(7),
                    "Each repeated [Mania] section should contribute its own configured keymode.");
                AssertNear(definition.Keymodes[4].Lanes[3].Width, 70f * LegacyManiaSkinAdapter.StableScale,
                    "Values beyond the configured key count must be ignored.");
                AssertNear(definition.Keymodes[7].Lanes[3].Width, 23f * LegacyManiaSkinAdapter.StableScale,
                    "Repeated Mania sections must keep their own per-column values.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestLegacyReservedUserVersion()
        {
            string root = Path.Combine(Path.GetTempPath(), "nullscent-user-version-" + Guid.NewGuid().ToString("N"));
            string userSkin = Path.Combine(root, "User");
            Directory.CreateDirectory(userSkin);
            try
            {
                File.WriteAllText(Path.Combine(userSkin, "skin.ini"), """
                    [General]
                    Version: 1.0
                    [Mania]
                    Keys: 4
                    """);
                SkinConfig config = SkinConfigParser.Parse(userSkin);
                Assert(config.Version == "latest" && config.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.IgnoredByDesign),
                    "The osu! reserved User skin folder must use latest rules regardless of its declared Version.");
            }
            finally { Directory.Delete(root, true); }
        }

        private static void TestLegacySpecialStyleAssetMapping()
        {
            string temp = Path.Combine(Path.GetTempPath(), "nullscent-skin-special-style-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                foreach ((int keys, int specialColumn) in new[] { (1, 0), (3, 1), (5, 2), (7, 3), (9, 4) })
                {
                    File.WriteAllText(Path.Combine(temp, "skin.ini"), $"[Mania]\nKeys: {keys}\n");
                    ManiaKeymodeDefinition defaultMode = LegacyManiaSkinAdapter.Decode(
                        SkinConfigParser.Parse(temp)).Keymodes[keys];
                    Assert(defaultMode.Lanes[specialColumn].NoteAsset == "mania-noteS",
                        $"The official conventional {keys}K note layout should assign its centred special lane to mania-noteS.");
                }

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 8
                    """);
                ManiaKeymodeDefinition evenMode = LegacyManiaSkinAdapter.Decode(
                    SkinConfigParser.Parse(temp)).Keymodes[8];
                Assert(evenMode.Lanes.Select(l => l.NoteAsset).SequenceEqual(new[]
                    { "mania-note1", "mania-note2", "mania-note1", "mania-note2", "mania-note2", "mania-note1", "mania-note2", "mania-note1" }),
                    "The conventional even-key layout should alternate inward from both outer edges without inventing a special lane.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.5
                    [Mania]
                    Keys: 7
                    SpecialStyle: 1
                    """);
                ManiaSkinDefinition definition = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp));
                ManiaKeymodeDefinition mode = definition.Keymodes[7];
                Assert(mode.Lanes[0].NoteAsset == "mania-noteS"
                    && mode.Lanes[0].ReceptorAsset == "mania-keyS"
                    && mode.Lanes[0].LongHeadAsset == "mania-noteSH",
                    "SpecialStyle 1 should select the conventional special assets on the outer left column.");
                ManiaKeymodeDefinition lazerMode = LegacyManiaSkinAdapter.Decode(
                    SkinConfigParser.Parse(temp), compatibilityMode: LegacySkinCompatibilityMode.Lazer).Keymodes[7];
                Assert(lazerMode.Lanes[0].NoteAsset == "mania-note1"
                    && lazerMode.Lanes[3].NoteAsset == "mania-noteS",
                    "The experimental Lazer profile must ignore the configured SpecialStyle override while retaining the conventional 7K asset pattern.");
                Assert(mode.Lanes[3].NoteAsset == "mania-note2",
                    "An unconfigured odd centre column must use the normal alternating asset pattern, not an implicit special asset.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.5
                    [Mania]
                    Keys: 7
                    SpecialStyle: 2
                    """);
                mode = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp)).Keymodes[7];
                Assert(mode.Lanes[6].NoteAsset == "mania-noteS",
                    "SpecialStyle 2 should select the conventional special assets on the outer right column.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 6
                    SpecialStyle: 1
                    """);
                mode = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp)).Keymodes[6];
                SkinValidation splitMode = new SkinCatalog(temp).Validate(temp);
                Assert(mode.Lanes[0].NoteAsset == "mania-note1"
                    && splitMode.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.UnsupportedProperty && d.Property == "SpecialStyle"),
                    "Double-play SpecialStyle lane placement must be diagnosed as unsupported instead of guessed as a single-stage edge.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestLegacyParserDiagnosticsAndColourContracts()
        {
            string temp = Path.Combine(Path.GetTempPath(), "nullscent-skin-diagnostics-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.5
                    [Colours]
                    Combo1: 1,2,3,4
                    [Mania]
                    Keys: 4
                    ColumnWidth: 30,invalid,60,30
                    KeysUnderNotes: true
                    Colour1: 10,20,30,0
                    ColourLight1: 40,50,60,0
                    ColourJudgementLine: 70,80,90,0
                    columnwidth: 200,200,200,200
                    """);
                SkinConfig config = SkinConfigParser.Parse(temp);
                ManiaKeymodeDefinition mode = LegacyManiaSkinAdapter.Decode(config).Keymodes[4];
                AssertNear(mode.Lanes[0].Width, 48f, "The valid first legacy column width should be scaled from the 480-high coordinate space.");
                AssertNear(mode.Lanes[1].Width, 48f, "A malformed column width should use the documented default at the same list index.");
                AssertNear(mode.Lanes[2].Width, 96f, "A malformed list entry must not shift later per-column values.");
                Assert(!mode.KeysUnderNotes, "skin.ini boolean commands accept 0 or 1; the text true must not silently become a valid override.");
                Assert(mode.Lanes[0].LaneColor == new SkinColor(10, 20, 30, 0),
                    "Colour# is RGB(a), so its explicit alpha must be preserved.");
                Assert(mode.Lanes[0].StageLightColor == new SkinColor(40, 50, 60, 255)
                    && mode.JudgementLineColor == new SkinColor(70, 80, 90, 255),
                    "ColourLight# and ColourJudgementLine are RGB properties; a fourth component must be ignored.");

                SkinValidation validation = new SkinCatalog(temp).Validate(temp);
                Assert(validation.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.MalformedProperty && d.Property == "ColumnWidth"),
                    "Malformed list values should be reported during validation.");
                Assert(validation.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.MalformedProperty && d.Property == "KeysUnderNotes"),
                    "Invalid boolean syntax should be reported instead of silently interpreted.");
                Assert(validation.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.UnsupportedProperty && d.Property == "columnwidth"),
                    "The official skin.ini command spelling is case-sensitive and misspellings must not be applied.");
                Assert(validation.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.IgnoredByDesign && d.Property == "Combo1"),
                    "Global [Colours] combo values should be identified as intentionally unused by the current Mania renderer.");
                Assert(validation.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.IgnoredByDesign && d.Property == "ColourLight1"),
                    "An extra alpha value on an RGB-only property should be explicitly reported as ignored.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Name: Blank-template example
                    Version:
                    [Mania]
                    Keys: 4
                    ColumnWidth:
                    Colour0:
                    KeyFlipWhenUpsideDown
                    """);
                SkinConfig templateConfig = SkinConfigParser.Parse(temp);
                ManiaKeymodeDefinition templateMode = LegacyManiaSkinAdapter.Decode(templateConfig).Keymodes[4];
                Assert(templateConfig.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.IgnoredByDesign
                        && d.Message.Contains("empty template placeholder", StringComparison.OrdinalIgnoreCase))
                    && !templateConfig.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.MalformedProperty),
                    "The official blank skin.ini template's empty key:value placeholders should be ignored, not reported as malformed entries.");
                AssertNear(templateMode.Lanes[0].Width, 48f,
                    "Empty template values should leave the property's format default in effect.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestAssetlessSkinsRemainPartial()
        {
            string temp = Path.Combine(Path.GetTempPath(), "nullscent-skin-assetless-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, NativeSkinAdapter.ManifestFileName), """
                    {
                      "formatVersion": 1,
                      "id": "assetless-native",
                      "keymodes": {
                        "4": {
                          "keyCount": 4,
                          "noteStyle": { "asset": "missing-note" },
                          "lanes": [{}, {}, {}, {}]
                        }
                      }
                    }
                    """);
                SkinValidation native = new SkinCatalog(temp).Validate(temp);
                Assert(native.Status == SkinValidity.Partial,
                    "A valid Native manifest with missing art should remain selectable and use component fallbacks.");
                Assert(native.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.MissingAsset),
                    "Missing Native assets should be visible in catalog diagnostics.");

                File.Delete(Path.Combine(temp, NativeSkinAdapter.ManifestFileName));
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 4
                    """);
                SkinValidation legacy = new SkinCatalog(temp).Validate(temp);
                Assert(legacy.Status == SkinValidity.Partial,
                    "A skin.ini-only legacy skin should be usable with the internal visual provider.");
                Assert(legacy.ManiaKeyModes.Contains(4),
                    "The configured legacy geometry should remain available even when its art falls back internally.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestResponsiveLayoutAndScrollMirror()
        {
            var lanes = new System.Collections.Generic.List<ManiaLaneDefinition>();
            for (int i = 0; i < 7; i++) lanes.Add(new ManiaLaneDefinition { Width = 48 + (i == 3 ? 14 : 0), GapAfter = i == 2 ? 8 : 2 });
            var profile = new ManiaKeymodeDefinition { KeyCount = 7, Placement = "center", Lanes = lanes, HitPosition = 640 };
            foreach ((int width, int height) in new[] { (1920, 1080), (1366, 768), (1280, 800), (2560, 1440) })
            {
                ManiaLayoutResult down = ManiaLayoutEngine.Calculate(profile, 7, width, height, true,
                    new ManiaLayoutOverrides(NoteScale: 1.25f, ReceptorScale: 0.75f));
                ManiaLayoutResult up = ManiaLayoutEngine.Calculate(profile, 7, width, height, false);
                Assert(down.Lanes.Count == 7, "Layout must preserve arbitrary keycount data.");
                Assert(down.Playfield.X >= 0 && down.Playfield.Right <= width + .1f, $"Playfield must fit {width}x{height}.");
                AssertNear(down.Lanes[0].Y, down.OffsetY,
                    "Lane backgrounds and scroll bounds should begin at the centered logical canvas offset.");
                AssertNear(down.Lanes[0].Bottom, down.OffsetY + ManiaLayoutEngine.DesignHeight * down.Scale,
                    "The logical playfield height should remain consistent across aspect ratios.");
                AssertNear(down.ReceptorY + up.ReceptorY, height, "Upscroll and downscroll receptor positions should mirror around the viewport center.");
                AssertNear(down.NoteScale, 1.25f, "Note height scale should travel through the layout snapshot.");
                AssertNear(down.ReceptorScale, 0.75f, "Receptor height scale should remain independent.");
            }

            var circles = new ManiaKeymodeDefinition
            {
                KeyCount = 4,
                Lanes = new System.Collections.Generic.List<ManiaLaneDefinition>
                {
                    new() { Width = 68, NoteWidthRatio = .46f }, new() { Width = 68, NoteWidthRatio = .46f },
                    new() { Width = 68, NoteWidthRatio = .46f }, new() { Width = 68, NoteWidthRatio = .46f }
                }
            };
            Assert(circles.Lanes[0].NoteWidthRatio < 1f && circles.Lanes[0].Width > circles.Lanes[0].Width * circles.Lanes[0].NoteWidthRatio,
                "Visual note width must be independently configurable from lane width.");
        }

        private static void TestStageLightPositionMirroring()
        {
            AssertNear(ManiaLayoutEngine.ResolveStageLightAnchorY(null, downScroll: true), ManiaLayoutEngine.DesignHeight,
                "An unspecified native StageLight should anchor at the downscroll edge.");
            AssertNear(ManiaLayoutEngine.ResolveStageLightAnchorY(null, downScroll: false), 0f,
                "An unspecified native StageLight should mirror to the upscroll edge.");
            float legacyLightPosition = 413f * LegacyManiaSkinAdapter.StableScale;
            AssertNear(ManiaLayoutEngine.ResolveStageLightAnchorY(legacyLightPosition, downScroll: true), legacyLightPosition,
                "A configured legacy LightPosition should keep its downscroll anchor.");
            AssertNear(ManiaLayoutEngine.ResolveStageLightAnchorY(legacyLightPosition, downScroll: false),
                ManiaLayoutEngine.DesignHeight - legacyLightPosition,
                "A configured legacy LightPosition should mirror around the logical stage for upscroll.");
        }

        private static void TestNativeManifests()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "Skins");
            ManiaSkinDefinition bars = NativeSkinAdapter.Load(Path.Combine(root, "Bars"));
            ManiaSkinDefinition circles = NativeSkinAdapter.Load(Path.Combine(root, "Circles"));
            Assert(bars.Provider == SkinProviderKind.NullscentNative && circles.Provider == SkinProviderKind.NullscentNative,
                "Built-in definitions should use the native provider.");
            Assert(!bars.Keymodes[4].StageAssets.Values.Any(asset => asset?.StartsWith("mania-", StringComparison.Ordinal) == true)
                && !circles.Keymodes[7].JudgementAssets.Values.Any(asset => asset.StartsWith("mania-", StringComparison.Ordinal)),
                "Native manifests must not acquire osu! conventional asset names from the legacy adapter.");
            ManiaSkinDefinition lazerRequested = SkinSourceDecoder.Load(Path.Combine(root, "Bars"), out _,
                LegacySkinCompatibilityMode.Lazer);
            Assert(lazerRequested.Provider == SkinProviderKind.NullscentNative
                && lazerRequested.CompatibilityProfile == ManiaSkinCompatibilityProfile.NullscentNative,
                "Selecting the Lazer legacy mode must not reinterpret a native manifest or change its provider.");
            Assert(bars.Keymodes[4].Lanes[0].Width != circles.Keymodes[4].Lanes[0].Width,
                "Native families should define their own lane geometry.");
            Assert(circles.Keymodes[4].NoteStyle.LaneWidthRatio < circles.Keymodes[4].ReceptorStyle.LaneWidthRatio,
                "Native note and receptor widths should remain independent.");
            AssertNear(bars.Keymodes[4].StageStyle.BottomScale, 1f,
                "Native manifests without stage overrides should keep their native StageBottom scale.");
            AssertNear(bars.Keymodes[4].HoldAssembly.HeadConnectionRatio, 0f,
                "Native skins should default to edge-to-edge hold assembly.");
            AssertNear(circles.Keymodes[4].HoldAssembly.HeadConnectionRatio, 0.5f,
                "A native circle family should be able to overlap the body beneath the centre of its caps.");
            Assert(bars.Keymodes[4].Lanes[0].LightColor.HasValue
                && bars.Keymodes[4].Lanes[0].StageLightColor == null,
                "Existing native lightColor should retain its lane-light tint semantics and remain the StageLight fallback.");
            Assert(!bars.Keymodes[4].FlipLightingHorizontallyWhenUpsideDown
                && !circles.Keymodes[4].FlipLightingHorizontallyWhenUpsideDown,
                "Native skins should keep their declared lighting orientation by default.");

            string temp = Path.Combine(Path.GetTempPath(), "NullscentNativeStageTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, NativeSkinAdapter.ManifestFileName), """
                    {
                      "formatVersion": 1,
                      "name": "Stage style test",
                      "keymodes": {
                        "4": {
                          "keyCount": 4,
                          "flipLightingHorizontallyWhenUpsideDown": true,
                          "judgementTiming": { "minimumFrameDurationMs": 20, "targetAnimationDurationMs": 80 },
                        "stageAssetAnimations": { "left": false, "bottom": true },
                        "receptorStyle": { "animate": false },
                          "stageStyle": { "sideHeightScale": 0.8, "hintHeightScale": 1.25, "bottomScale": 1.6 },
                          "lanes": [{ "width": 48 }, { "width": 48 }, { "width": 48 }, { "width": 48 }]
                        }
                      }
                    }
                    """);
                ManiaKeymodeDefinition nativeMode = NativeSkinAdapter.Load(temp).Keymodes[4];
                ManiaStageStyleDefinition stageStyle = nativeMode.StageStyle;
                Assert(nativeMode.FlipLightingHorizontallyWhenUpsideDown,
                    "Native manifests should be able to opt in to the normalized upside-down lighting transform.");
                Assert(!nativeMode.ReceptorStyle.Animate
                    && nativeMode.StageAssetAnimations["left"] == false
                    && nativeMode.StageAssetAnimations["bottom"],
                    "Native skins should be able to declare which resolved assets are static or animated without a legacy renderer branch.");
                Assert(nativeMode.JudgementTiming is { TargetAnimationDurationMs: 80d, MinimumFrameDurationMs: 20d },
                    "Native manifests should be able to define a validated normalized animation timing policy.");
                AssertNear(stageStyle.SideHeightScale, 0.8f, "Native stage side scaling should deserialize as normalized data.");
                AssertNear(stageStyle.HintHeightScale, 1.25f, "Native stage hint scaling should deserialize as normalized data.");
                AssertNear(stageStyle.BottomScale, 1.6f, "Native StageBottom scaling should deserialize as normalized data.");

                File.WriteAllText(Path.Combine(temp, NativeSkinAdapter.ManifestFileName), """
                    {
                      "formatVersion": 1,
                      "keymodes": {
                        "4": {
                          "keyCount": 4,
                          "stageStyle": { "bottomScale": 0 },
                          "lanes": [{ "width": 48 }, { "width": 48 }, { "width": 48 }, { "width": 48 }]
                        }
                      }
                    }
                    """);
                bool invalidStageRejected = false;
                try { _ = NativeSkinAdapter.Load(temp); }
                catch (InvalidDataException) { invalidStageRejected = true; }
                Assert(invalidStageRejected, "Native stage scales must be validated before the skin can activate.");

                File.WriteAllText(Path.Combine(temp, NativeSkinAdapter.ManifestFileName), """
                    {
                      "formatVersion": 1,
                      "keymodes": {
                        "4": {
                          "keyCount": 4,
                          "holdAssembly": { "headConnectionRatio": 0.75 },
                          "lanes": [{ "width": 48 }, { "width": 48 }, { "width": 48 }, { "width": 48 }]
                        }
                      }
                    }
                    """);
                bool invalidHoldAnchorRejected = false;
                try { _ = NativeSkinAdapter.Load(temp); }
                catch (InvalidDataException) { invalidHoldAnchorRejected = true; }
                Assert(invalidHoldAnchorRejected, "Native hold connection anchors outside 0..0.5 must be rejected during validation.");

                AssertNativeManifestRejected(temp, """
                    {
                      "formatVersion": 1,
                      "keymodes": {
                        "4": {
                          "keyCount": 4,
                          "noteStyle": { "laneWidthRatio": 0 },
                          "lanes": [{ "width": 48 }, { "width": 48 }, { "width": 48 }, { "width": 48 }]
                        }
                      }
                    }
                    """, "Native sprite width ratios must be positive before the skin can activate.");

                AssertNativeManifestRejected(temp, """
                    {
                      "formatVersion": 1,
                      "keymodes": {
                        "4": {
                          "keyCount": 4,
                          "defaultHitPositionRatio": 1.25,
                          "lanes": [{ "width": 48 }, { "width": 48 }, { "width": 48 }, { "width": 48 }]
                        }
                      }
                    }
                    """, "Native hit-position ratios outside 0..1 must be rejected rather than silently clamped.");

                AssertNativeManifestRejected(temp, """
                    {
                      "formatVersion": 1,
                      "keymodes": {
                        "4": {
                          "keyCount": 4,
                          "receptorStyle": { "framesPerSecond": 0 },
                          "lanes": [{ "width": 48 }, { "width": 48 }, { "width": 48 }, { "width": 48 }]
                        }
                      }
                    }
                    """, "Native animation frame rates must be explicitly valid or omitted.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void AssertNativeManifestRejected(string root, string manifest, string message)
        {
            File.WriteAllText(Path.Combine(root, NativeSkinAdapter.ManifestFileName), manifest);
            bool rejected = false;
            try { _ = NativeSkinAdapter.Load(root); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, message);
        }

        private static void TestNativeLeftPlacement()
        {
            var profile = new ManiaKeymodeDefinition
            {
                KeyCount = 4,
                Placement = "left",
                Lanes = new System.Collections.Generic.List<ManiaLaneDefinition>
                {
                    new() { Width = 48 }, new() { Width = 48 }, new() { Width = 48 }, new() { Width = 48 }
                }
            };
            ManiaLayoutResult layout = ManiaLayoutEngine.Calculate(profile, 4, 1920, 1080, true);
            AssertNear(layout.Playfield.X, 0f, "Native left placement should anchor the playfield to the logical design area's left edge.");
        }

        private static void TestUserOverridePrecedence()
        {
            var profile = new ManiaKeymodeDefinition
            {
                KeyCount = 4,
                HitPosition = 640f,
                Lanes = new System.Collections.Generic.List<ManiaLaneDefinition>
                {
                    new() { Width = 50 }, new() { Width = 50 }, new() { Width = 50 }, new() { Width = 50 }
                }
            };
            ManiaLayoutResult inherited = ManiaLayoutEngine.Calculate(profile, 4, 1920, 1080, true);
            ManiaLayoutResult overridden = ManiaLayoutEngine.Calculate(profile, 4, 1920, 1080, true,
                new ManiaLayoutOverrides(ReceptorPositionRatio: 0.25f));
            AssertNear(inherited.ReceptorY, 900f, "Skin hit position should apply when no user override is provided.");
            AssertNear(overridden.ReceptorY, 270f, "Explicit user receptor position should override the skin value.");
        }

        private static void TestSkinScrollPreferenceAndLegacyFlips()
        {
            Assert(!ManiaSkinRules.ResolveDownScroll(true, true, skinUpsideDown: true),
                "An enabled skin preference should map upside-down orientation to upscroll.");
            Assert(ManiaSkinRules.ResolveDownScroll(true, false, skinUpsideDown: true),
                "An explicit user scroll direction should take precedence over the skin.");
            Assert(!ManiaSkinRules.ResolveDownScroll(false, true, skinUpsideDown: null),
                "When the skin does not specify orientation, preserve the user direction.");
        }

        private static void TestCompatibilityProfilesAndReceptorAnchors()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 4
                    HitPosition: 400
                    NoteBodyStyle: 1
                    """);
                SkinConfig config = SkinConfigParser.Parse(temp);
                ManiaSkinDefinition stable = LegacyManiaSkinAdapter.Decode(config, "legacy-test", LegacySkinCompatibilityMode.Stable);
                ManiaSkinDefinition lazer = LegacyManiaSkinAdapter.Decode(config, "legacy-test", LegacySkinCompatibilityMode.Lazer);
                ManiaKeymodeDefinition stableMode = stable.Keymodes[4];

                Assert(stable.CompatibilityProfile == ManiaSkinCompatibilityProfile.StableLegacy,
                    "Legacy skins should default to the explicitly selected Stable compatibility profile.");
                Assert(lazer.CompatibilityProfile == ManiaSkinCompatibilityProfile.LazerLegacy,
                    "Lazer compatibility should be an explicit interpretation profile, not a provider kind.");
                Assert(stableMode.FlipLightingHorizontallyWhenUpsideDown
                    && !lazer.Keymodes[4].FlipLightingHorizontallyWhenUpsideDown,
                    "Stable legacy lighting follows the wiki's horizontal flip on an upside-down stage; current Lazer legacy keeps its source orientation.");
                Assert(!ManiaSkinRules.ShouldFlipLightingHorizontally(stableMode, downScroll: true)
                    && ManiaSkinRules.ShouldFlipLightingHorizontally(stableMode, downScroll: false)
                    && !ManiaSkinRules.ShouldFlipLightingHorizontally(lazer.Keymodes[4], downScroll: false),
                    "Only normalized profiles that request the legacy transform flip both lighting assets during upscroll.");
                Assert(stableMode.ReceptorAnchor == ReceptorAnchorMode.StageEdge,
                    "Legacy key images should anchor to the stage edge when the user has not overridden receptor position.");
                Assert(stableMode.BodyMode == NoteBodyRenderMode.TileFromTail
                    && lazer.Keymodes[4].BodyMode == NoteBodyRenderMode.TileFromTail,
                    "Stable and experimental Lazer profiles should normalize their body direction before rendering.");
                Assert(LegacyManiaSkinAdapter.DecodeBodyMode(1, LegacySkinCompatibilityMode.Stable) == NoteBodyRenderMode.TileFromTail
                    && LegacyManiaSkinAdapter.DecodeBodyMode(2, LegacySkinCompatibilityMode.Stable) == NoteBodyRenderMode.TileFromHead
                    && LegacyManiaSkinAdapter.DecodeBodyMode(1, LegacySkinCompatibilityMode.Lazer) == NoteBodyRenderMode.TileFromTail
                    && LegacyManiaSkinAdapter.DecodeBodyMode(2, LegacySkinCompatibilityMode.Lazer) == NoteBodyRenderMode.TileFromTail,
                    "Stable must preserve the distinct style 1/2 anchors; the experimental Lazer profile intentionally collapses both repeated styles to its tail-side repeat.");
                Assert(stableMode.BodyAnimationReleaseMode == HoldBodyAnimationReleaseMode.FreezeAtRelease
                    && lazer.Keymodes[4].BodyAnimationReleaseMode == HoldBodyAnimationReleaseMode.ResetToInitialFrame,
                    "The selected legacy profile should normalize its body-animation release behavior without changing providers.");
                Assert(stableMode.JudgementFramesPerSecond == 60
                    && stableMode.JudgementTiming == null
                    && lazer.Keymodes[4].JudgementFramesPerSecond == 20
                    && lazer.Keymodes[4].JudgementTiming == null,
                    "Stable hit bursts follow the wiki's fixed 60 FPS; Lazer's ManiaLegacySkinTransformer uses fixed 20 FPS.");
                AssertNear((float)(lazer.Keymodes[4].NoteLightingTiming?.MinimumFrameDurationMs ?? 0), 1000f / 60f,
                    "The Lazer lighting duration policy should cap playback at the source's 60 FPS minimum frame length.");
                AssertNear((float)(lazer.Keymodes[4].NoteLightingTiming?.TargetAnimationDurationMs ?? 0), 170f,
                    "Lazer note lighting should use the same duration-based frame pacing as LegacyHitExplosion.");
                AssertNear((float)(lazer.Keymodes[4].HoldLightingTiming?.TargetAnimationDurationMs ?? 0), 170f,
                    "Lazer hold lighting should use the same duration-based frame pacing as LegacyBodyPiece.");
                AssertNear((float)(lazer.Keymodes[4].LongBodyStyle.Timing?.MinimumFrameDurationMs ?? 0), 30f,
                    "Lazer legacy hold body frames should preserve the source's explicit 30 ms frame length.");
                Assert(lazer.Provider == SkinProviderKind.OsuLegacy,
                    "Selecting Lazer compatibility must not change an osu! legacy source into another provider.");

                ManiaLayoutResult down = ManiaLayoutEngine.Calculate(stableMode, 4, 1920, 1080, true);
                ManiaLayoutResult up = ManiaLayoutEngine.Calculate(stableMode, 4, 1920, 1080, false);
                ManiaLayoutResult userOverride = ManiaLayoutEngine.Calculate(stableMode, 4, 1920, 1080, true,
                    new ManiaLayoutOverrides(ReceptorPositionRatio: 0.4f));
                AssertNear(down.ReceptorSpriteY, down.Playfield.Bottom,
                    "Legacy downscroll keys should be placed on the stage bottom independently from HitPosition.");
                AssertNear(up.ReceptorSpriteY, up.Playfield.Y,
                    "Legacy upscroll keys should be placed on the stage top independently from HitPosition.");
                AssertNear(userOverride.ReceptorSpriteY, userOverride.ReceptorY,
                    "An explicit user receptor position should override the legacy stage-edge anchor.");

                var native = new ManiaKeymodeDefinition
                {
                    KeyCount = 4,
                    ReceptorAnchor = ReceptorAnchorMode.ReceptorLine,
                    Lanes = new System.Collections.Generic.List<ManiaLaneDefinition>
                    {
                        new() { Width = 48 }, new() { Width = 48 }, new() { Width = 48 }, new() { Width = 48 }
                    }
                };
                ManiaLayoutResult nativeLayout = ManiaLayoutEngine.Calculate(native, 4, 1920, 1080, true);
                AssertNear(nativeLayout.ReceptorSpriteY, nativeLayout.ReceptorY,
                    "Native skins should keep the receptor attached to their normalized receptor line.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestLegacyVersionFlipDefaults()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.4
                    [Mania]
                    Keys: 4
                    NoteFlipWhenUpsideDown: 1
                    NoteBodyStyle: 2
                    """);
                ManiaSkinDefinition pre25 = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp));
                ManiaKeymodeDefinition pre25Mode = pre25.Keymodes[4];
                Assert(!ManiaSkinRules.FlipNote(pre25Mode, 0, "", downScroll: false)
                    && !ManiaSkinRules.FlipNote(pre25Mode, 0, "H", downScroll: false)
                    && !ManiaSkinRules.FlipNote(pre25Mode, 0, "T", downScroll: true),
                    "Stable legacy skins below v2.5 must not vertically flip note sprites, even when the option is present.");
                SkinValidation pre25Validation = new SkinCatalog(temp).Validate(temp);
                Assert(pre25Mode.DrawColumnEdgesBothSides
                    && pre25Mode.BodyMode == NoteBodyRenderMode.TileFromTail
                    && pre25Validation.Diagnostics.Any(d => d.Kind == SkinDiagnosticKind.VersionInapplicable
                        && d.Property == "NoteBodyStyle"),
                    "Version 2.4 keeps the post-2.4 column-edge rule but ignores and diagnoses NoteBodyStyle until version 2.5.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.3
                    [Mania]
                    Keys: 4
                    """);
                Assert(!LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp)).Keymodes[4].DrawColumnEdgesBothSides,
                    "Legacy versions before 2.4 must retain the single-sided column-edge behavior.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.5
                    [Mania]
                    Keys: 4
                    """);
                ManiaKeymodeDefinition v25Mode = LegacyManiaSkinAdapter.Decode(SkinConfigParser.Parse(temp)).Keymodes[4];
                Assert(ManiaSkinRules.FlipNote(v25Mode, 0, "", downScroll: false)
                    && ManiaSkinRules.FlipNote(v25Mode, 0, "T", downScroll: true),
                    "Stable legacy v2.5+ note sprites should use the documented default upscroll/tail flipping.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.5
                    [Mania]
                    Keys: 4
                    NoteFlipWhenUpsideDown: 0
                    KeyFlipWhenUpsideDown: 0
                    """);
                ManiaKeymodeDefinition stableOverrideMode = LegacyManiaSkinAdapter.Decode(
                    SkinConfigParser.Parse(temp), compatibilityMode: LegacySkinCompatibilityMode.Stable).Keymodes[4];
                ManiaKeymodeDefinition lazerOverrideMode = LegacyManiaSkinAdapter.Decode(
                    SkinConfigParser.Parse(temp), compatibilityMode: LegacySkinCompatibilityMode.Lazer).Keymodes[4];
                Assert(!ManiaSkinRules.FlipNote(stableOverrideMode, 0, "", downScroll: false)
                    && !ManiaSkinRules.FlipKey(stableOverrideMode, 0, pressed: false, downScroll: false),
                    "Stable v2.5 flip overrides should control note and key orientation.");
                Assert(!lazerOverrideMode.SupportsFlipOverrides
                    && ManiaSkinRules.FlipNote(lazerOverrideMode, 0, "", downScroll: false)
                    && ManiaSkinRules.FlipKey(lazerOverrideMode, 0, pressed: false, downScroll: false),
                    "Current Lazer legacy drawables orient sprites from scroll direction and do not consume skin flip overrides.");

                var nativeMode = new ManiaKeymodeDefinition();
                Assert(ManiaSkinRules.FlipNote(nativeMode, 0, "", downScroll: false),
                    "The version-gated legacy flip rule must not change native skin defaults.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestFlipPropertyClassificationByProfile()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllBytes(Path.Combine(temp, "mania-note1.png"), Array.Empty<byte>());
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.4
                    [Mania]
                    Keys: 4
                    NoteFlipWhenUpsideDown: 1
                    NoteFlipWhenUpsideDown0H: 1
                    KeyFlipWhenUpsideDown: 1
                    KeyFlipWhenUpsideDown1D: 1
                    """);

                SkinValidation stablePre25 = new SkinCatalog(temp, compatibilityMode: LegacySkinCompatibilityMode.Stable)
                    .Validate(temp);
                Assert(stablePre25.ParsedButNotApplied.Contains("NoteFlipWhenUpsideDown")
                    && stablePre25.ParsedButNotApplied.Contains("NoteFlipWhenUpsideDown0H")
                    && stablePre25.ParsedButNotApplied.Contains("KeyFlipWhenUpsideDown")
                    && stablePre25.ParsedButNotApplied.Contains("KeyFlipWhenUpsideDown1D"),
                    "Flip properties requiring v2.5 should be reported as not applied for older Stable skins.");

                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.5
                    [Mania]
                    Keys: 4
                    NoteFlipWhenUpsideDown: 1
                    NoteFlipWhenUpsideDown0H: 1
                    KeyFlipWhenUpsideDown: 1
                    KeyFlipWhenUpsideDown1D: 1
                    """);
                SkinValidation stable25 = new SkinCatalog(temp, compatibilityMode: LegacySkinCompatibilityMode.Stable)
                    .Validate(temp);
                SkinValidation lazer25 = new SkinCatalog(temp, compatibilityMode: LegacySkinCompatibilityMode.Lazer)
                    .Validate(temp);
                Assert(!stable25.ParsedButNotApplied.Contains("NoteFlipWhenUpsideDown")
                    && !stable25.ParsedButNotApplied.Contains("NoteFlipWhenUpsideDown0H")
                    && !stable25.ParsedButNotApplied.Contains("KeyFlipWhenUpsideDown")
                    && !stable25.ParsedButNotApplied.Contains("KeyFlipWhenUpsideDown1D")
                    && !stable25.Unsupported.Contains("NoteFlipWhenUpsideDown")
                    && !stable25.Unsupported.Contains("NoteFlipWhenUpsideDown0H")
                    && !stable25.Unsupported.Contains("KeyFlipWhenUpsideDown"),
                    "Stable v2.5 flip properties should be reported as applied.");
                Assert(lazer25.ParsedButNotApplied.Contains("NoteFlipWhenUpsideDown")
                    && lazer25.ParsedButNotApplied.Contains("NoteFlipWhenUpsideDown0H")
                    && lazer25.ParsedButNotApplied.Contains("KeyFlipWhenUpsideDown")
                    && lazer25.ParsedButNotApplied.Contains("KeyFlipWhenUpsideDown1D"),
                    "The experimental Lazer profile should report parsed flip properties as not applied.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestLegacyCompatibilitySettingRollback()
        {
            var settings = new GameSettings { LegacySkinCompatibilityMode = LegacySkinCompatibilityMode.Stable };
            LegacySkinCompatibilityMode publishedMode = LegacySkinCompatibilityMode.Stable;
            bool saved = LegacySkinCompatibilityWorkflow.TryPersistSetting(settings, LegacySkinCompatibilityMode.Lazer,
                mode =>
                {
                    publishedMode = mode;
                    settings.LegacySkinCompatibilityMode = mode;
                }, () => false);

            Assert(!saved, "A failed settings save must reject the compatibility-profile transaction.");
            Assert(settings.LegacySkinCompatibilityMode == LegacySkinCompatibilityMode.Stable
                && publishedMode == LegacySkinCompatibilityMode.Stable,
                "A failed settings save must restore both the persisted setting and its published value.");

            saved = LegacySkinCompatibilityWorkflow.TryPersistSetting(settings, LegacySkinCompatibilityMode.Lazer,
                mode =>
                {
                    publishedMode = mode;
                    settings.LegacySkinCompatibilityMode = mode;
                }, () => true);
            Assert(saved && settings.LegacySkinCompatibilityMode == LegacySkinCompatibilityMode.Lazer
                && publishedMode == LegacySkinCompatibilityMode.Lazer,
                "A successful settings save must publish the target profile exactly once as active settings.");

            settings.LegacySkinCompatibilityMode = LegacySkinCompatibilityMode.Stable;
            publishedMode = LegacySkinCompatibilityMode.Stable;
            bool saveExceptionObserved = false;
            try
            {
                LegacySkinCompatibilityWorkflow.TryPersistSetting(settings, LegacySkinCompatibilityMode.Lazer,
                    mode =>
                    {
                        publishedMode = mode;
                        settings.LegacySkinCompatibilityMode = mode;
                    }, () => throw new InvalidOperationException("simulated save failure"));
            }
            catch (InvalidOperationException)
            {
                saveExceptionObserved = true;
            }

            Assert(saveExceptionObserved && settings.LegacySkinCompatibilityMode == LegacySkinCompatibilityMode.Stable
                && publishedMode == LegacySkinCompatibilityMode.Stable,
                "A thrown settings save must restore both settings values before propagating the failure.");
        }

        private static void TestHighResolutionSettingRollback()
        {
            var settings = new GameSettings { HighResolutionSkins = false };
            bool publishedValue = false;
            bool saved = HighResolutionSkinWorkflow.TryPersistSetting(settings, true,
                value =>
                {
                    publishedValue = value;
                    settings.HighResolutionSkins = value;
                }, () => false);

            Assert(!saved, "A failed save must reject the high-resolution asset preference change.");
            Assert(!settings.HighResolutionSkins && !publishedValue,
                "A failed save must restore both stored and published high-resolution settings.");

            saved = HighResolutionSkinWorkflow.TryPersistSetting(settings, true,
                value =>
                {
                    publishedValue = value;
                    settings.HighResolutionSkins = value;
                }, () => true);
            Assert(saved && settings.HighResolutionSkins && publishedValue,
                "A successful save must publish the high-resolution asset preference.");
        }

        private static void TestStagePresentationNormalizationAndLayout()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentStageSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [General]
                    Version: 2.5
                    [Mania]
                    Keys: 4
                    """);
                ManiaSkinDefinition legacy = SkinSourceDecoder.Load(temp, out _);
                ManiaKeymodeDefinition legacyMode = legacy.Keymodes[4];
                AssertNear(legacyMode.StageStyle.SideHeightScale, 1f,
                    "Legacy side pieces should use the full stage height.");
                AssertNear(legacyMode.StageStyle.BottomScale, 1.6f,
                    "Legacy StageBottom assets should be scaled from the 480-high skin canvas.");
                AssertNear(legacyMode.StageStyle.HintHeightScale, 0.9f * 1.6025f,
                    "The legacy hit target should retain the source-backed vertical StageHint scale.");

                foreach ((int width, int height) in new[] { (1920, 1080), (1366, 768) })
                {
                    ManiaLayoutResult layout = ManiaLayoutEngine.Calculate(legacyMode, 4, width, height, downScroll: true);
                    SkinRect left = ManiaLayoutEngine.GetStageSideRect(layout, 18f, isRight: false, legacyMode.StageStyle);
                    SkinRect right = ManiaLayoutEngine.GetStageSideRect(layout, 18f, isRight: true, legacyMode.StageStyle);
                    AssertNear(left.Y, layout.Playfield.Y, "StageLeft should start at the top edge of the stage.");
                    AssertNear(left.Height, layout.Playfield.Height, "StageLeft should fit the full stage height.");
                    AssertNear(left.Right, layout.Playfield.X, "StageLeft should align its inner edge with the playfield.");
                    AssertNear(right.X, layout.Playfield.Right, "StageRight should align its inner edge with the playfield.");
                    AssertNear(right.Height, layout.Playfield.Height, "StageRight should fit the full stage height.");

                    SkinRect hint = ManiaLayoutEngine.GetStageHintRect(layout, 100f, 20f, legacyMode.StageStyle);
                    AssertNear(hint.X, layout.Playfield.X, "StageHint should span the full playfield width.");
                    AssertNear(hint.Width, layout.Playfield.Width, "StageHint width should follow the stage width.");
                    AssertNear(hint.Y + hint.Height / 2f, layout.ReceptorY,
                        "StageHint should be centred on the judgement target.");

                    SkinRect bottomDown = ManiaLayoutEngine.GetStageBottomRect(layout, 80f, 40f,
                        downScroll: true, legacyMode.StageStyle);
                    SkinRect bottomUp = ManiaLayoutEngine.GetStageBottomRect(layout, 80f, 40f,
                        downScroll: false, legacyMode.StageStyle);
                    AssertNear(bottomDown.Width, 80f * 1.6f * layout.Scale,
                        "StageBottom should use its legacy scale without stretching to stage width.");
                    AssertNear(bottomDown.X + bottomDown.Width / 2f,
                        layout.Playfield.X + layout.Playfield.Width / 2f,
                        "StageBottom should remain centred on the stage.");
                    AssertNear(bottomDown.Bottom, layout.Playfield.Bottom,
                        "Downscroll StageBottom should anchor to the bottom of the stage.");
                    AssertNear(bottomUp.Y, layout.Playfield.Y,
                        "Upscroll StageBottom should anchor to the top of the stage.");
                }

                var nativeMode = new ManiaKeymodeDefinition { KeyCount = 4 };
                AssertNear(nativeMode.StageStyle.BottomScale, 1f,
                    "Native stage sizing should use its own data default instead of legacy scaling.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestHitSoundSourcePrecedence()
        {
            string[] skinFirst = Nullscent.Audio.HitSoundPlayer.BuildSearchDirectoryOrder(
                "beatmap", "skin", "application", useSkinHitsounds: true, overrideMapHitsounds: true);
            Assert(skinFirst.Length == 3 && skinFirst[0] == "skin" && skinFirst[1] == "beatmap" && skinFirst[2] == "application",
                "OverrideMapHitsounds should prioritize the active skin while preserving map and application fallbacks.");

            string[] mapFirst = Nullscent.Audio.HitSoundPlayer.BuildSearchDirectoryOrder(
                "beatmap", "skin", "application", useSkinHitsounds: true, overrideMapHitsounds: false);
            Assert(mapFirst.Length == 3 && mapFirst[0] == "beatmap" && mapFirst[1] == "skin" && mapFirst[2] == "application",
                "Enabled skin hitsounds should follow beatmap hitsounds unless explicitly overridden.");

            string[] skinDisabled = Nullscent.Audio.HitSoundPlayer.BuildSearchDirectoryOrder(
                "beatmap", "skin", "application", useSkinHitsounds: false, overrideMapHitsounds: false);
            Assert(skinDisabled.Length == 2 && skinDisabled[0] == "beatmap" && skinDisabled[1] == "application",
                "UseSkinHitsounds should disable only the skin source, keeping map and application fallbacks.");

            string[] disabledWithOverride = Nullscent.Audio.HitSoundPlayer.BuildSearchDirectoryOrder(
                "beatmap", "skin", "application", useSkinHitsounds: false, overrideMapHitsounds: true);
            Assert(disabledWithOverride.Length == 2 && disabledWithOverride[0] == "beatmap" && disabledWithOverride[1] == "application",
                "OverrideMapHitsounds should not re-enable a skin source that the user has disabled.");
        }

        private static void TestLongNoteBodyVisualRules()
        {
            AssertNear(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetPieceHeight(
                    new SkinSpriteMetrics(1f, SpriteHeightMode.TextureAspect, 0f, 0f, 0f, 0f, null),
                    20f, 80f, 10f, 100f, 2f, 1.5f), 60f,
                "Texture-aspect LN dimensions should preserve the existing scale.");
            AssertNear(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetPieceHeight(
                    new SkinSpriteMetrics(1f, SpriteHeightMode.ReferenceWidthAspect, 0f, 25f, 0f, 0f, null),
                    20f, 80f, 10f, 100f, 2f, 1.5f), 300f,
                "Tiled LN bodies should use their configured reference-width tile height.");
            AssertNear(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetPieceHeight(
                    new SkinSpriteMetrics(1f, SpriteHeightMode.LaneWidthFraction, 0.2f, 0f, 0f, 0f, null),
                    20f, 80f, 10f, 100f, 2f, 1.5f), 30f,
                "Tiled LN bodies should use their configured lane-relative tile height.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.OffsetBodySpan(10, 20, -6) == (4, 14),
                "An LN body sprite offset should translate both body boundaries together.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileFromTop(NoteBodyRenderMode.TileFromTail, true)
                && !Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileFromTop(NoteBodyRenderMode.TileFromHead, true),
                "In downscroll the tail is physically above the head, so tail-anchored and head-anchored tiling must start at opposite ends.");
            Assert(!Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileFromTop(NoteBodyRenderMode.TileFromTail, false)
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileFromTop(NoteBodyRenderMode.TileFromHead, false),
                "In upscroll the tail is physically below the head, so their tiling anchors must mirror downscroll.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetBodySpan(
                    200, 500, 40, 60, true, 0f, 0f) == (200, 500)
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetBodySpan(
                    200, 500, 40, 60, true, 0.5f, 0.5f) == (180, 530)
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetBodySpan(
                    200, 500, 40, 60, false, 0.5f, 0.5f) == (220, 470),
                "Hold cap overlap ratios should map consistently to body anchors in both scroll directions.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileOriginY(
                    0, 95, 40, NoteBodyRenderMode.TileFromTail, true, 0) == 0
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileOriginY(
                    0, 95, 40, NoteBodyRenderMode.TileFromTail, false, 0) == 55
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileOriginY(
                    0, 95, 40, NoteBodyRenderMode.TileFromHead, true, 0) == 55
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileOriginY(
                    0, 95, 40, NoteBodyRenderMode.TileFromHead, false, 0) == 0,
                "Each semantic LN anchor should map to its actual physical edge in both scroll directions.");

            foreach (NoteBodyRenderMode mode in new[] { NoteBodyRenderMode.TileFromTail, NoteBodyRenderMode.TileFromHead })
            foreach (bool downScroll in new[] { false, true })
            {
                var coverage = new int[95];
                for (int tileIndex = 0; tileIndex < 3; tileIndex++)
                {
                    int origin = Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.TileOriginY(
                        0, 95, 40, mode, downScroll, tileIndex);
                    int visibleTop = Math.Max(0, origin);
                    int visibleBottom = Math.Min(95, origin + 40);
                    for (int y = visibleTop; y < visibleBottom; y++) coverage[y]++;
                }
                Assert(Array.TrueForAll(coverage, count => count == 1),
                    $"{mode} must cover the complete body exactly once in {(downScroll ? "downscroll" : "upscroll")}.");
            }

            var firstTailTile = Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetTileSourceRange(
                0, 40, 0, 95, 40, 400, NoteBodyRenderMode.TileFromTail, true, false);
            Assert(firstTailTile == (0, 400), "The first full tile should preserve the complete physical texture region.");
            var partialTailTile = Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetTileSourceRange(
                0, 15, 0, 95, 40, 400, NoteBodyRenderMode.TileFromTail, true, false);
            Assert(partialTailTile == (0, 150), "A partial tail-anchored tile should begin at the texture origin without stretching.");
            var partialHeadTile = Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetTileSourceRange(
                0, 15, 0, 95, 40, 400, NoteBodyRenderMode.TileFromHead, true, false);
            Assert(partialHeadTile == (250, 150), "A head-anchored partial tile at the opposite end should crop the matching source pixels.");
            var flippedTile = Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.GetTileSourceRange(
                15, 10, 0, 95, 40, 400, NoteBodyRenderMode.TileFromTail, true, true);
            Assert(flippedTile == (150, 100), "A vertically flipped tile should crop the corresponding reversed source interval.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.MaxTileSegmentHeight(
                35, 8, 0, 95, 40, NoteBodyRenderMode.TileFromTail, true) == 5,
                "Hidden-fade segments should split at tile boundaries to avoid sampling across a repeat seam.");

            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.BodyAnimationElapsedMs(
                Nullscent.Rulesets.Mania.Objects.LongNoteState.Idle, 1200, 1000, 0, true,
                HoldBodyAnimationReleaseMode.FreezeAtRelease) == 0,
                "An idle hold body should remain on its first frame while the note approaches.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.BodyAnimationElapsedMs(
                Nullscent.Rulesets.Mania.Objects.LongNoteState.Holding, 1200, 1000, 0, true,
                HoldBodyAnimationReleaseMode.FreezeAtRelease) == 200,
                "The body animation should advance from the successful head hit while holding.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.BodyAnimationElapsedMs(
                Nullscent.Rulesets.Mania.Objects.LongNoteState.Broken, 1500, 1000, 1175, true,
                HoldBodyAnimationReleaseMode.FreezeAtRelease) == 175,
                "A broken hold body should freeze at the release time instead of following global song time.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.BodyAnimationElapsedMs(
                Nullscent.Rulesets.Mania.Objects.LongNoteState.Finished, 1500, 1000, 1450, true,
                HoldBodyAnimationReleaseMode.FreezeAtRelease) == 450,
                "A successful release at the tail should also freeze its body animation at the actual release time.");
            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.BodyAnimationElapsedMs(
                Nullscent.Rulesets.Mania.Objects.LongNoteState.Broken, 1500, 1000, 1175, true,
                HoldBodyAnimationReleaseMode.ResetToInitialFrame) == 0,
                "The Lazer legacy profile should reset its body animation after release, matching LegacyBodyPiece.");
        }

        private static void TestLegacyHoldColourSuffixIsNotMisrepresentedAsSupported()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 4
                    ColourHold1: 255,128,32
                    ColourHold: 10,20,30,0
                    """);
                SkinValidation validation = new SkinCatalog(Path.GetTempPath()).Validate(temp);
                Assert(validation.Config != null, "A legacy skin config should be available for normalization.");
                ManiaSkinDefinition definition = LegacyManiaSkinAdapter.Decode(validation.Config!, temp);
                Assert(validation.Unsupported.Contains("ColourHold1"),
                    "The catalog must report ColourHold# as unsupported because the osu! legacy contract defines only global ColourHold.");
                Assert(definition.Keymodes[4].Lanes[0].HoldColor == null,
                    "An undocumented ColourHold# field must not acquire per-column legacy behavior.");
                Assert(definition.Keymodes[4].HoldColor == new SkinColor(10, 20, 30, 0),
                    "The documented global ColourHold value must reach the combo counter with its alpha unchanged.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestLegacyColourLightIsStageOnly()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 4
                    ColourLight1: 40,50,60
                    """);
                SkinConfig config = SkinConfigParser.Parse(temp);
                ManiaSkinDefinition definition = LegacyManiaSkinAdapter.Decode(config, temp);
                ManiaLaneDefinition lane = definition.Keymodes[4].Lanes[0];
                Assert(lane.StageLightColor == new SkinColor(40, 50, 60),
                    "Legacy ColourLight# should normalize into the StageLight tint.");
                Assert(lane.LightColor == null,
                    "Legacy ColourLight# must not tint LightingN or LightingL; it configures StageLight only.");
            }
            finally { Directory.Delete(temp, true); }
        }

        private static void TestLegacyAnimationRateSemantics()
        {
            Assert(ResolvedSkinAnimation.ResolveFrameRate(-1, 8) == 8,
                "AnimationFramerate -1 should play the complete animation in one second.");
            Assert(ResolvedSkinAnimation.ResolveFrameRate(24, 8) == 24,
                "An explicit positive animation frame rate should be preserved.");
        }

        private static void TestAnimationTimingResolution()
        {
            var stableJudgement = new ResolvedSkinAnimation(
                new Texture2D[] { null!, null! }, 60);
            var lazerJudgement = new ResolvedSkinAnimation(
                new Texture2D[] { null!, null! }, 20);
            AssertNear((float)stableJudgement.FrameDurationMilliseconds, 1000f / 60f,
                "Stable mania judgement frames should resolve to the wiki's fixed 60 FPS interval.");
            AssertNear((float)lazerJudgement.FrameDurationMilliseconds, 50f,
                "Lazer mania judgement frames should resolve to the transformer's fixed 20 FPS interval.");
            Assert(stableJudgement.FrameIndexAt(stableJudgement.FrameDurationMilliseconds) == 1
                && lazerJudgement.FrameIndexAt(50d) == 1,
                "Resolved fixed-rate judgement animations should advance exactly at their configured frame boundary.");

            AnimationTimingDefinition lazerLighting = AnimationTimingDefinition.SpreadOverDuration(170d, 60d);
            AssertNear((float)lazerLighting.ResolveFrameDurationMs(4), 42.5f,
                "Lazer legacy lighting should spread four frames over 170 ms when below the 60 FPS cap.");
            AssertNear((float)lazerLighting.ResolveFrameDurationMs(1), 170f,
                "A one-frame Lazer lighting animation should use the complete 170 ms duration.");
            AssertNear((float)lazerLighting.ResolveFrameDurationMs(12), 1000f / 60f,
                "Lazer legacy lighting should not exceed 60 FPS when the frame count is high.");

            var resolved = new ResolvedSkinAnimation(
                new Texture2D[] { null!, null!, null!, null! }, 60, lazerLighting);
            AssertNear((float)resolved.FrameDurationMilliseconds, 42.5f,
                "The resolved lighting snapshot should retain the duration computed from its own frame count.");
            Assert(resolved.FrameIndexAt(42d) == 0 && resolved.FrameIndexAt(42.5d) == 1,
                "Resolved animation playback should use the exact frame duration instead of an integer FPS approximation.");

            var stableLighting = new ResolvedSkinAnimation(
                new Texture2D[] { null!, null! }, 60);
            var lazerTwoFrameLighting = new ResolvedSkinAnimation(
                new Texture2D[] { null!, null! }, 60, lazerLighting);
            Assert(stableLighting.FrameIndexAt(20d) == 1 && lazerTwoFrameLighting.FrameIndexAt(20d) == 0,
                "The Lazer lighting duration policy should remain distinct from Stable's fixed-rate animation at runtime.");

            AnimationTimingDefinition lazerBody = AnimationTimingDefinition.FixedFrameDuration(30d);
            AssertNear((float)lazerBody.ResolveFrameDurationMs(20), 30f,
                "The legacy Lazer LN body should keep its explicit 30 ms frame length for any frame count.");
        }

        private static void TestHoldLightingAnimationLifecycle()
        {
            var timeline = new Nullscent.Rulesets.Mania.UI.Drawables.HoldLightingTimeline();
            timeline.RecordKeyRelease(510, hadActiveHold: false);
            Assert(!timeline.ShouldDraw(511, isHolding: false),
                "Releasing a key after a tap must not display the hold-light animation.");

            timeline.RecordHoldStart(1000);
            Assert(timeline.ShouldDraw(1000, isHolding: true)
                && timeline.GetAnimationElapsedMs(1000) == 0
                && timeline.GetAnimationElapsedMs(1025) == 25,
                "LightingL must start at frame zero and advance from the successful LN head time.");

            timeline.RecordKeyRelease(1300, hadActiveHold: true);
            Assert(timeline.ShouldDraw(1350, isHolding: false)
                && timeline.GetAnimationElapsedMs(1350) == 350,
                "LightingL should continue from its hold clock through the release fade.");
            AssertNear(timeline.GetAlpha(1350, isHolding: false), 0.4f * (70f / 120f),
                "The hold-light fade should use time remaining after a real hold release.");
            Assert(!timeline.ShouldDraw(1420, isHolding: false),
                "LightingL should stop after the release fade has elapsed.");

            var automaticallyEnded = new Nullscent.Rulesets.Mania.UI.Drawables.HoldLightingTimeline();
            automaticallyEnded.RecordHoldStart(2000);
            automaticallyEnded.ObserveHoldState(2000, isHolding: true);
            automaticallyEnded.ObserveHoldState(2300, isHolding: false);
            Assert(automaticallyEnded.ShouldDraw(2350, isHolding: false),
                "LightingL should fade when an LN hold ends through automatic tail judgment without a key-release event.");
            AssertNear(automaticallyEnded.GetAlpha(2350, isHolding: false), 0.4f * (70f / 120f),
                "An automatically completed hold should receive the same 120 ms release fade.");

            Assert(Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.ResolveAnimationEndTime(
                    Nullscent.Rulesets.Mania.Objects.LongNoteState.Finished, 1000, 0, 2800, headWasHit: true) == 2800
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.ResolveAnimationEndTime(
                    Nullscent.Rulesets.Mania.Objects.LongNoteState.Finished, 1000, 1700, 2800, headWasHit: true) == 1700
                && Nullscent.Rulesets.Mania.UI.Drawables.LongNoteRenderRules.ResolveAnimationEndTime(
                    Nullscent.Rulesets.Mania.Objects.LongNoteState.Finished, 0, 0, 2800, headWasHit: false) == 0,
                "A successfully auto-completed LN body should freeze at its tail; explicit releases keep their actual time and a missed head has no hold animation.");

            var animation = new ResolvedSkinAnimation(new Texture2D[] { null!, null!, null!, null! }, 100);
            Assert(animation.FrameIndexAt(timeline.GetAnimationElapsedMs(1000)) == 0
                && animation.FrameIndexAt(timeline.GetAnimationElapsedMs(1100)) == 2,
                "Hold lighting frame selection should restart per hold instead of using global song time.");
        }

        private static void TestProviderScopedAssetCascade()
        {
            var active = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["note"] = "user-note",
                ["head"] = "user-head",
                ["blank"] = "intentional-blank"
            };
            var internalAssets = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bar-note"] = "internal-note",
                ["bar-hold-head"] = "internal-head",
                ["bar-hold-tail"] = "internal-tail",
                ["blank"] = "internal-blank"
            };
            int internalReads = 0;
            string? ResolveActive(string name) => active.TryGetValue(name, out string? value) ? value : null;
            string? ResolveInternal(string name)
            {
                internalReads++;
                return internalAssets.TryGetValue(name, out string? value) ? value : null;
            }

            string? head = SkinAssetCascade.Resolve(ResolveActive, new string?[] { "missing-head", "note" },
                ResolveInternal, new string?[] { "bar-hold-head", "bar-note" });
            Assert(head == "user-note", "A missing head should reuse the note from the active provider before consulting built-in assets.");
            Assert(internalReads == 0, "The internal provider must not be queried while the active provider can satisfy the semantic cascade.");

            string? tail = SkinAssetCascade.Resolve(ResolveActive, new string?[] { "missing-tail", "head", "note" },
                ResolveInternal, new string?[] { "bar-hold-tail", "bar-hold-head", "bar-note" });
            Assert(tail == "user-head", "A missing tail should reuse the resolved head from the active provider.");
            Assert(internalReads == 0, "A same-provider tail fallback must finish before internal fallback starts.");

            string? fallback = SkinAssetCascade.Resolve(ResolveActive, new string?[] { "missing" },
                ResolveInternal, new string?[] { "bar-hold-tail", "bar-note" });
            Assert(fallback == "internal-tail", "Internal fallback should be used after all active-provider candidates are exhausted.");

            string? intentionalBlank = SkinAssetCascade.Resolve(ResolveActive, new string?[] { "blank" },
                ResolveInternal, new string?[] { "blank" });
            Assert(intentionalBlank == "intentional-blank", "An intentional blank asset is present and must suppress built-in fallback.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertNear(float actual, float expected, string message)
        {
            if (MathF.Abs(actual - expected) > 0.02f)
                throw new InvalidOperationException($"{message} Expected {expected}, found {actual}.");
        }
    }
}
#endif
