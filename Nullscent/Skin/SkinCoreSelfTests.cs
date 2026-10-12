#nullable enable
#if DEBUG
using System;
using System.IO;

namespace Nullscent.Skin
{
    internal static class SkinCoreSelfTests
    {
        public static void RunAll()
        {
            TestLegacyNormalizationAndTextAlias();
            TestSkinWithoutIniHasConventionalKeymodes();
            TestResponsiveLayoutAndScrollMirror();
            TestNativeLeftPlacement();
            TestNativeManifests();
            TestUserOverridePrecedence();
            TestSkinScrollPreferenceAndLegacyFlips();
            TestCatalogReflectsAppliedColumnHoldColour();
            TestLegacyAnimationRateSemantics();
            Console.WriteLine("[SkinTests] 9 pruebas deterministas pasaron.");
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
            Assert(bars.Keymodes[4].Lanes[0].Width != circles.Keymodes[4].Lanes[0].Width,
                "Native families should define their own lane geometry.");
            Assert(circles.Keymodes[4].NoteStyle.LaneWidthRatio < circles.Keymodes[4].ReceptorStyle.LaneWidthRatio,
                "Native note and receptor widths should remain independent.");
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

        private static void TestCatalogReflectsAppliedColumnHoldColour()
        {
            string temp = Path.Combine(Path.GetTempPath(), "NullscentSkinTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                File.WriteAllText(Path.Combine(temp, "skin.ini"), """
                    [Mania]
                    Keys: 4
                    ColourHold1: 255,128,32
                    """);
                SkinValidation validation = new SkinCatalog(Path.GetTempPath()).Validate(temp);
                Assert(!validation.Unsupported.Contains("ColourHold1"),
                    "The catalogue should not label per-column hold colours unsupported when the adapter applies them.");
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
