#nullable enable
#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Nullscent.Skin
{
    internal static class SkinCoreSelfTests
    {
        public static void RunAll()
        {
            TestLegacyNormalizationAndTextAlias();
            TestResolvedCollectionsAreReadOnlySnapshots();
            TestSkinWithoutIniHasConventionalKeymodes();
            TestResponsiveLayoutAndScrollMirror();
            TestNativeLeftPlacement();
            TestNativeManifests();
            TestUserOverridePrecedence();
            TestSkinScrollPreferenceAndLegacyFlips();
            TestCompatibilityProfilesAndReceptorAnchors();
            TestStagePresentationNormalizationAndLayout();
            TestLongNoteBodyVisualRules();
            TestLegacyHoldColourSuffixIsNotMisrepresentedAsSupported();
            TestLegacyAnimationRateSemantics();
            TestProviderScopedAssetCascade();
            TestHitSoundSourcePrecedence();
            Console.WriteLine("[SkinTests] 15 pruebas deterministas pasaron.");
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
                Assert(mode.BodyMode == NoteBodyRenderMode.Stretch, "The textual stretch alias should parse.");
                Assert(skin.AnimationFramerate == 24 && mode.NoteStyle.FramesPerSecond == 60,
                    "General animation speed should normalize without overriding mania note animation speed.");
                Assert(mode.JudgementFramesPerSecond == 60,
                    "Mania hit bursts should retain their fixed 60 FPS rate despite the general animation setting.");
                Assert(mode.Lanes[2].BodyMode == NoteBodyRenderMode.TileFromHead, "Per-column body style should survive normalization.");
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

        private static void TestNativeManifests()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "Skins");
            ManiaSkinDefinition bars = NativeSkinAdapter.Load(Path.Combine(root, "Bars"));
            ManiaSkinDefinition circles = NativeSkinAdapter.Load(Path.Combine(root, "Circles"));
            Assert(bars.Provider == SkinProviderKind.NullscentNative && circles.Provider == SkinProviderKind.NullscentNative,
                "Built-in definitions should use the native provider.");
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
                          "stageStyle": { "sideHeightScale": 0.8, "hintHeightScale": 1.25, "bottomScale": 1.6 },
                          "lanes": [{ "width": 48 }, { "width": 48 }, { "width": 48 }, { "width": 48 }]
                        }
                      }
                    }
                    """);
                ManiaStageStyleDefinition stageStyle = NativeSkinAdapter.Load(temp).Keymodes[4].StageStyle;
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
                    && lazer.Keymodes[4].JudgementFramesPerSecond == 20,
                    "Stable hit bursts should use the wiki's fixed 60 FPS while the experimental Lazer profile follows its explicit 50 ms frame length.");
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

        private static void TestLegacyAnimationRateSemantics()
        {
            Assert(ResolvedSkinAnimation.ResolveFrameRate(-1, 8) == 8,
                "AnimationFramerate -1 should play the complete animation in one second.");
            Assert(ResolvedSkinAnimation.ResolveFrameRate(24, 8) == 24,
                "An explicit positive animation frame rate should be preserved.");
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
