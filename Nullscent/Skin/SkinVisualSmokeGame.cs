#nullable enable
#if DEBUG
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Audio;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Rulesets.Mania.UI;
using Nullscent.Rulesets.Mania.UI.Drawables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Color = Microsoft.Xna.Framework.Color;

namespace Nullscent.Skin;

// Deterministic renderer exercise for any supported legacy key mode.
internal sealed class SkinVisualSmokeGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly string skinPath;
    private readonly int keyCount;
    private readonly string outputPath;
    private readonly bool downScroll;
    private readonly string? switchSkinPath;
    private readonly string? switchOutputPath;
    private readonly int viewportWidth;
    private readonly int viewportHeight;
    private readonly string? resizeOutputPath;
    private readonly int? resizedViewportWidth;
    private readonly int? resizedViewportHeight;
    private readonly LegacySkinCompatibilityMode legacyCompatibilityMode;
    private readonly LegacySkinCompatibilityMode? switchCompatibilityMode;
    private readonly string? compatibilityOutputPath;
    private readonly string? timelineOutputDirectory;
    private readonly string? atReleaseTimelineOutputDirectory;
    private readonly double atReleaseTimelineDurationMs;
    private readonly string[]? switchSequence;
    private readonly string? switchSequenceOutputDirectory;
    private readonly LegacySkinCompatibilityMode[]? compatibilitySequence;
    private readonly string? compatibilitySequenceOutputDirectory;
    private readonly bool[]? highResolutionSequence;
    private readonly string? highResolutionSequenceOutputDirectory;
    private readonly bool stageOnly;
    private SpriteBatch? batch;
    private SkinManager? skin;
    private SkinCatalog? skinCatalog;
    private GameSettings? smokeSettings;
    private HitSoundPlayer? hitSoundPlayer;
    private ManiaPlayfield? playfield;
    private ManiaScoreProcessor? score;
    private int captureStage;
    private bool resizeApplied;
    private int persistenceCount;
    private int switchSequenceIndex;
    private int compatibilitySequenceIndex;
    private int highResolutionSequenceIndex;
    private Vector2? highResolutionReferenceLogicalSize;
    private int timelineStage;
    private double timelineCurrentTime = 1040;
    private LongNote? timelineLongNote;
    private LongNote? timelineAutoCompleteNote;
    private LongNote? timelineEarlyReleaseNote;
    private double timelineLongNoteEndTime;
    private const double TIMELINE_AUTO_COMPLETE_START = 3000;
    private const double TIMELINE_AUTO_COMPLETE_END = 3500;
    private int timelineLongColumn;
    private int atReleaseTimelineSampleIndex;
    private bool atReleaseTimelineColumnPressed;
    private LongNote? atReleaseTimelineNote;
    private readonly bool holdThroughAutoCompletion;
    private const double AT_RELEASE_TIMELINE_START = 2000;

    public SkinVisualSmokeGame(string skinPath, int keyCount, string outputPath, bool downScroll,
        string? switchSkinPath = null, string? switchOutputPath = null, int viewportWidth = 1280, int viewportHeight = 720,
        string? resizeOutputPath = null, int? resizedViewportWidth = null, int? resizedViewportHeight = null,
        LegacySkinCompatibilityMode legacyCompatibilityMode = LegacySkinCompatibilityMode.Stable,
        LegacySkinCompatibilityMode? switchCompatibilityMode = null, string? compatibilityOutputPath = null,
        string? timelineOutputDirectory = null, IReadOnlyList<string>? switchSequence = null,
        string? switchSequenceOutputDirectory = null,
        IReadOnlyList<LegacySkinCompatibilityMode>? compatibilitySequence = null,
        string? compatibilitySequenceOutputDirectory = null,
        bool stageOnly = false,
        IReadOnlyList<bool>? highResolutionSequence = null,
        string? highResolutionSequenceOutputDirectory = null,
        string? atReleaseTimelineOutputDirectory = null,
        double atReleaseTimelineDurationMs = 1200,
        bool holdThroughAutoCompletion = false)
    {
        if (keyCount < 1 || keyCount > 18) throw new ArgumentOutOfRangeException(nameof(keyCount));
        if (viewportWidth < 640 || viewportHeight < 480) throw new ArgumentOutOfRangeException(nameof(viewportWidth), "La resolución de smoke debe ser de al menos 640x480.");
        if ((switchSkinPath == null) != (switchOutputPath == null)) throw new ArgumentException("El cambio de skin requiere skin destino y captura de salida.");
        if ((resizeOutputPath == null) != (resizedViewportWidth == null) || (resizeOutputPath == null) != (resizedViewportHeight == null))
            throw new ArgumentException("El resize smoke requiere ruta de salida y ambas dimensiones destino.");
        if (resizedViewportWidth is < 640 || resizedViewportHeight is < 480)
            throw new ArgumentOutOfRangeException(nameof(resizedViewportWidth), "La resoluciÃ³n destino debe ser de al menos 640x480.");
        if ((switchCompatibilityMode == null) != (compatibilityOutputPath == null))
            throw new ArgumentException("El cambio de perfil requiere un perfil destino y captura de salida.");
        if (switchCompatibilityMode != null && (switchSkinPath != null || resizeOutputPath != null))
            throw new ArgumentException("El smoke de perfil se ejecuta separado del cambio de skin y resize.");
        if (timelineOutputDirectory != null && (switchSkinPath != null || resizeOutputPath != null || switchCompatibilityMode != null))
            throw new ArgumentException("El smoke temporal se ejecuta separado de los cambios de skin, perfil y viewport.");
        if ((switchSequence == null) != (switchSequenceOutputDirectory == null)
            || switchSequence is { Count: 0 }
            || (switchSequence != null && (switchSkinPath != null || resizeOutputPath != null || switchCompatibilityMode != null || timelineOutputDirectory != null)))
            throw new ArgumentException("El smoke de secuencia requiere destinos y una carpeta de capturas, y se ejecuta de forma independiente.");
        if ((compatibilitySequence == null) != (compatibilitySequenceOutputDirectory == null)
            || compatibilitySequence is { Count: 0 }
            || (compatibilitySequence != null && (switchSkinPath != null || resizeOutputPath != null
                || switchCompatibilityMode != null || timelineOutputDirectory != null || switchSequence != null)))
            throw new ArgumentException("El ciclo de perfil requiere destinos y una carpeta de capturas, y se ejecuta de forma independiente.");
        if (compatibilitySequence != null && compatibilitySequence.Any(mode => !Enum.IsDefined(mode)))
            throw new ArgumentOutOfRangeException(nameof(compatibilitySequence));
        if ((highResolutionSequence == null) != (highResolutionSequenceOutputDirectory == null)
            || highResolutionSequence is { Count: 0 }
            || (highResolutionSequence != null && (switchSkinPath != null || resizeOutputPath != null
                || switchCompatibilityMode != null || timelineOutputDirectory != null || switchSequence != null
                || compatibilitySequence != null)))
            throw new ArgumentException("El ciclo HD requiere destinos y una carpeta de capturas, y se ejecuta de forma independiente.");
        if (atReleaseTimelineOutputDirectory != null && (switchSkinPath != null || resizeOutputPath != null
            || switchCompatibilityMode != null || timelineOutputDirectory != null || switchSequence != null
            || compatibilitySequence != null || highResolutionSequence != null || stageOnly))
            throw new ArgumentException("El smoke AT de LN se ejecuta aislado de otros cambios de skin, perfil, viewport y stage.");
        if (!double.IsFinite(atReleaseTimelineDurationMs) || atReleaseTimelineDurationMs < 100 || atReleaseTimelineDurationMs > 60000)
            throw new ArgumentOutOfRangeException(nameof(atReleaseTimelineDurationMs), "La duración del fixture AT debe estar entre 100 y 60000 ms.");
        this.skinPath = skinPath;
        this.keyCount = keyCount;
        this.outputPath = Path.GetFullPath(outputPath);
        this.downScroll = downScroll;
        this.switchSkinPath = switchSkinPath;
        this.switchOutputPath = switchOutputPath == null ? null : Path.GetFullPath(switchOutputPath);
        this.viewportWidth = viewportWidth;
        this.viewportHeight = viewportHeight;
        this.resizeOutputPath = resizeOutputPath == null ? null : Path.GetFullPath(resizeOutputPath);
        this.resizedViewportWidth = resizedViewportWidth;
        this.resizedViewportHeight = resizedViewportHeight;
        this.legacyCompatibilityMode = legacyCompatibilityMode;
        this.switchCompatibilityMode = switchCompatibilityMode;
        this.compatibilityOutputPath = compatibilityOutputPath == null ? null : Path.GetFullPath(compatibilityOutputPath);
        this.timelineOutputDirectory = timelineOutputDirectory == null ? null : Path.GetFullPath(timelineOutputDirectory);
        this.atReleaseTimelineOutputDirectory = atReleaseTimelineOutputDirectory == null
            ? null : Path.GetFullPath(atReleaseTimelineOutputDirectory);
        this.atReleaseTimelineDurationMs = atReleaseTimelineDurationMs;
        this.holdThroughAutoCompletion = holdThroughAutoCompletion;
        this.switchSequence = switchSequence?.Select(path =>
            path.Equals("internal", StringComparison.OrdinalIgnoreCase) ? "internal" : Path.GetFullPath(path)).ToArray();
        this.switchSequenceOutputDirectory = switchSequenceOutputDirectory == null ? null : Path.GetFullPath(switchSequenceOutputDirectory);
        this.compatibilitySequence = compatibilitySequence?.ToArray();
        this.compatibilitySequenceOutputDirectory = compatibilitySequenceOutputDirectory == null
            ? null : Path.GetFullPath(compatibilitySequenceOutputDirectory);
        this.highResolutionSequence = highResolutionSequence?.ToArray();
        this.highResolutionSequenceOutputDirectory = highResolutionSequenceOutputDirectory == null
            ? null : Path.GetFullPath(highResolutionSequenceOutputDirectory);
        this.stageOnly = stageOnly;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = viewportWidth,
            PreferredBackBufferHeight = viewportHeight
        };
        graphics.GraphicsProfile = GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef)
            ? GraphicsProfile.HiDef
            : GraphicsProfile.Reach;
        IsFixedTimeStep = false;
        IsMouseVisible = false;
    }

    protected override void LoadContent()
    {
        Console.WriteLine($"[SkinVisualSmoke] Graphics profile: {GraphicsDevice.GraphicsProfile}");
        batch = new SpriteBatch(GraphicsDevice);
        string builtInSkinsRoot = Path.Combine(AppContext.BaseDirectory, "Skins");
        string userSkinsRoot = skinPath.Equals("internal", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(AppContext.BaseDirectory, "Skin")
            : Path.GetDirectoryName(Path.GetFullPath(skinPath))!;
        smokeSettings = new GameSettings
        {
            LegacySkinCompatibilityMode = legacyCompatibilityMode,
            SkinsRootDirectory = userSkinsRoot
        };
        SettingsService.Instance.Initialize(smokeSettings);
        skinCatalog = new SkinCatalog(userSkinsRoot, builtInSkinsRoot, legacyCompatibilityMode);
        skinCatalog.Refresh();
        skin = new SkinManager(GraphicsDevice);
        hitSoundPlayer = new HitSoundPlayer();
        if (!ActivateSkin(skinPath)) throw new InvalidDataException($"No se pudo activar skin inicial: {skin.CurrentValidation?.Summary ?? skinPath}");
        AssertResolvedKeymodesPrepared();
        AssertHudGlyphsPrepared();
        ReportResolvedHighResolutionAssets();

        playfield = new ManiaPlayfield(GraphicsDevice, batch, keyCount, viewportWidth, viewportHeight, skinManager: skin);
        score = new ManiaScoreProcessor(keyCount, keyCount, 5, false, new List<Nullscent.Rulesets.Mania.Mods.ManiaMod>());
        timelineLongColumn = keyCount > 1 ? 1 : 0;
        double timelineLongNoteStart = keyCount > 2 ? 1475 : 1600;
        var smokeObjects = new List<ManiaHitObject>();
        if (atReleaseTimelineOutputDirectory != null)
        {
            timelineLongColumn = keyCount / 2;
            atReleaseTimelineNote = new LongNote(AT_RELEASE_TIMELINE_START,
                AT_RELEASE_TIMELINE_START + atReleaseTimelineDurationMs, timelineLongColumn);
            smokeObjects.Add(atReleaseTimelineNote);
            timelineLongNoteEndTime = atReleaseTimelineNote.EndTime;
        }
        else
        {
            for (int column = 0; column < keyCount; column++)
            {
                double noteTime = column == keyCount - 1 && keyCount > 1 ? 1000 : 1200 + column * 75;
                smokeObjects.Add(new Note { StartTime = noteTime, Column = column });
                if (column % 2 == 0)
                    smokeObjects.Add(new Note { StartTime = 1550, Column = column });
                if (column % 2 != 0 && (column == 1 || column == keyCount / 2))
                    smokeObjects.Add(new Note { StartTime = 1550, Column = column });

                double longNoteStart = column == 0 || column == keyCount - 1 ? 1000 : 1400 + column * 75;
                double longNoteDuration = timelineOutputDirectory != null ? 1100 : column % 2 == 0 ? 300 : 1100;
                var longNote = new LongNote(longNoteStart, longNoteStart + longNoteDuration, column);
                smokeObjects.Add(longNote);
                if (timelineOutputDirectory != null && column == 0) timelineEarlyReleaseNote = longNote;
                if (timelineOutputDirectory != null && column == timelineLongColumn && longNoteStart == timelineLongNoteStart)
                    timelineLongNote = longNote;
            }
            if (timelineOutputDirectory != null && keyCount <= 2)
            {
                timelineLongNote = new LongNote(timelineLongNoteStart, timelineLongNoteStart + 1100, timelineLongColumn);
                smokeObjects.Add(timelineLongNote);
            }
        }
        if (timelineOutputDirectory != null)
        {
            timelineAutoCompleteNote = new LongNote(
                TIMELINE_AUTO_COMPLETE_START, TIMELINE_AUTO_COMPLETE_END, timelineLongColumn);
            smokeObjects.Add(timelineAutoCompleteNote);
        }
        if (atReleaseTimelineOutputDirectory == null)
            timelineLongNoteEndTime = timelineLongNoteStart + 1100;
        if (!stageOnly)
        {
            foreach (ManiaHitObject smokeObject in smokeObjects.OrderBy(hitObject => hitObject.StartTime))
                playfield.AddHitObject(smokeObject);

            if (atReleaseTimelineOutputDirectory != null)
            {
                timelineCurrentTime = AT_RELEASE_TIMELINE_START - 16;
                LongNoteRenderDiagnostics.Enabled = true;
                LongNoteRenderDiagnostics.DrawOutlines = true;
                LongNoteRenderDiagnostics.DrawCheckerboardLaneBackground = true;
                ReportAtReleaseLongNoteAssets();
                playfield.Update(timelineCurrentTime, score, 8, null, downScroll);
            }
            else if (timelineOutputDirectory == null)
            {
                if (keyCount > 1) playfield.HandleKeyPress(keyCount - 1, 1000, score);
                playfield.HandleKeyPress(0, 1000, score);
                playfield.JudgementDisplayManager?.EnqueueJudgement(HitResult.Perfect, 0, 0, 12, 1000);
                playfield.Update(1040, score, 8, null, downScroll);
            }
            else
            {
                timelineCurrentTime = 700;
                playfield.Update(timelineCurrentTime, score, 8, null, downScroll);
            }
        }
        else
        {
            playfield.Update(1040, score, 8, null, downScroll);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(atReleaseTimelineOutputDirectory == null
            ? new Color(6, 9, 16) : new Color(40, 74, 102));
        playfield!.Draw(timelineOutputDirectory == null && atReleaseTimelineOutputDirectory == null
            ? 1040 : timelineCurrentTime, 8, null, downScroll);
        if (atReleaseTimelineOutputDirectory != null)
        {
            AdvanceAtReleaseTimelineSmoke();
            base.Draw(gameTime);
            return;
        }
        if (timelineOutputDirectory != null)
        {
            AdvanceTimelineSmoke();
            base.Draw(gameTime);
            return;
        }

        if (captureStage == 0)
        {
            SaveBackbuffer(outputPath);
            Console.WriteLine($"[SkinVisualSmoke] {keyCount}K initial: {outputPath}");
            captureStage = 1;
            if (switchSkinPath == null && switchCompatibilityMode == null && switchSequence == null
                && compatibilitySequence == null && highResolutionSequence == null)
            {
                if (resizeOutputPath == null) Exit();
            }
            else if (switchSequence != null)
            {
                ApplySwitchSequenceEntry(switchSequenceIndex++);
            }
            else if (compatibilitySequence != null)
            {
                ApplyCompatibilitySequenceEntry(compatibilitySequenceIndex++);
            }
            else if (highResolutionSequence != null)
            {
                ApplyHighResolutionSequenceEntry(highResolutionSequenceIndex++);
            }
            else if (switchCompatibilityMode != null)
            {
                long previousVersion = skin!.Version;
                SkinProviderKind previousProvider = skin.CurrentDefinition.Provider;
                if (previousProvider != SkinProviderKind.OsuLegacy)
                    throw new InvalidOperationException("El smoke de perfil requiere una skin osu! legacy activa.");
                ManiaSkinDefinition previousDefinition = skin.CurrentDefinition;
                ResolvedManiaKeymode previousResolvedMode = skin.GetResolvedKeymode(keyCount);
                object? previousHudPrefixState = skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix);
                if (skin.TrySetLegacyCompatibilityMode(switchCompatibilityMode.Value, () => false)
                    || skin.Version != previousVersion
                    || !ReferenceEquals(skin.CurrentDefinition, previousDefinition)
                    || !ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
                    || !ReferenceEquals(skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix), previousHudPrefixState))
                    throw new InvalidOperationException("Un fallo de persistencia alteró el perfil o el snapshot de skin activo.");
                if (!skin.TrySetLegacyCompatibilityMode(switchCompatibilityMode.Value, () => true))
                    throw new InvalidDataException("No se pudo reconstruir la skin con el perfil de compatibilidad destino.");
                if (ReferenceEquals(skin.GetPreparedNumberPrefixState(skin.CurrentDefinition.ComboPrefix), previousHudPrefixState))
                    throw new InvalidOperationException("El cambio de perfil conservó el snapshot HUD anterior.");
                ManiaSkinCompatibilityProfile expectedProfile = switchCompatibilityMode == LegacySkinCompatibilityMode.Stable
                    ? ManiaSkinCompatibilityProfile.StableLegacy : ManiaSkinCompatibilityProfile.LazerLegacy;
                if (skin.Version != previousVersion + 1
                    || skin.CurrentDefinition.Provider != previousProvider
                    || skin.CurrentDefinition.CompatibilityProfile != expectedProfile)
                    throw new InvalidOperationException("El cambio de perfil no fue atÃ³mico o cambiÃ³ el proveedor de la skin.");
                AssertResolvedKeymodesPrepared();
                AssertHudGlyphsPrepared();
                ReportResolvedHighResolutionAssets();
                playfield!.Update(1040, score!, 8, null, downScroll);
                Console.WriteLine($"[SkinVisualSmoke] perfil runtime aplicado: {legacyCompatibilityMode} -> {switchCompatibilityMode}; proveedor={skin.CurrentDefinition.Provider}; {compatibilityOutputPath}");
            }
            else
            {
                long previousVersion = skin!.Version;
                int previousPersistenceCount = persistenceCount;
                string previousDirectory = skin.CurrentSkinDirectory;
                ManiaSkinDefinition previousDefinition = skin.CurrentDefinition;
                ResolvedManiaKeymode previousResolvedMode = skin.GetResolvedKeymode(keyCount);
                object? previousHudPrefixState = skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix);
                SkinValidation rejectedCandidate = skinCatalog!.Validate(switchSkinPath!);
                if (rejectedCandidate.Status != SkinValidity.Invalid
                    && (skin.TryActivate(rejectedCandidate, () => false)
                        || skin.Version != previousVersion
                        || !string.Equals(skin.CurrentSkinDirectory, previousDirectory, StringComparison.OrdinalIgnoreCase)
                        || !ReferenceEquals(skin.CurrentDefinition, previousDefinition)
                        || !ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
                        || !ReferenceEquals(skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix), previousHudPrefixState)))
                    throw new InvalidOperationException("Un fallo de persistencia alteró la skin o su snapshot activo.");
                if (!ActivateSkin(switchSkinPath!)) throw new InvalidDataException($"No se pudo activar skin destino: {skin.CurrentValidation?.Summary ?? switchSkinPath}");
                ReportResolvedHighResolutionAssets();
                if (skin.Version != previousVersion + 1) throw new InvalidOperationException("La versión de skin no avanzó exactamente una vez.");
                if (persistenceCount != previousPersistenceCount + 1
                    || !string.Equals(hitSoundPlayer!.SkinDirectory, skin.CurrentSkinDirectory, StringComparison.OrdinalIgnoreCase)
                    || !PersistedSelectionMatchesActiveSkin())
                    throw new InvalidOperationException("El flujo común de selección no persistió el destino o no actualizó los hitsounds.");
                if (ReferenceEquals(skin.GetPreparedNumberPrefixState(skin.CurrentDefinition.ComboPrefix), previousHudPrefixState))
                    throw new InvalidOperationException("El cambio de skin conservó el snapshot HUD de la skin anterior.");
                AssertResolvedKeymodesPrepared();
                AssertHudGlyphsPrepared();
                playfield!.Update(1040, score!, 8, null, downScroll);
                Console.WriteLine($"[SkinVisualSmoke] cambio runtime aplicado: v{previousVersion} -> v{skin.Version}; {switchOutputPath}");
            }
        }
        else if (captureStage == 1 && switchOutputPath != null)
        {
            SaveBackbuffer(switchOutputPath);
            Console.WriteLine($"[SkinVisualSmoke] {keyCount}K after switch: {switchOutputPath}");
            captureStage = 2;
            Exit();
        }
        else if (captureStage == 1 && compatibilityOutputPath != null)
        {
            SaveBackbuffer(compatibilityOutputPath);
            Console.WriteLine($"[SkinVisualSmoke] {keyCount}K tras cambio de perfil: {compatibilityOutputPath}");
            captureStage = 2;
            Exit();
        }
        else if (captureStage == 1 && switchSequence != null)
        {
            string sequenceOutput = Path.Combine(switchSequenceOutputDirectory!, $"{switchSequenceIndex:00}-switch.png");
            SaveBackbuffer(sequenceOutput);
            Console.WriteLine($"[SkinVisualSmoke] switch sequence capture {switchSequenceIndex}/{switchSequence.Length}: {sequenceOutput}; provider={skin!.CurrentDefinition.Provider}; profile={skin.CurrentDefinition.CompatibilityProfile}");
            if (switchSequenceIndex >= switchSequence.Length)
                Exit();
            else
                ApplySwitchSequenceEntry(switchSequenceIndex++);
        }
        else if (captureStage == 1 && compatibilitySequence != null)
        {
            string profileName = skin!.CurrentDefinition.CompatibilityProfile.ToString();
            string sequenceOutput = Path.Combine(compatibilitySequenceOutputDirectory!,
                $"{compatibilitySequenceIndex:00}-{profileName}.png");
            SaveBackbuffer(sequenceOutput);
            Console.WriteLine($"[SkinVisualSmoke] profile sequence capture {compatibilitySequenceIndex}/{compatibilitySequence.Length}: {sequenceOutput}; provider={skin.CurrentDefinition.Provider}; profile={profileName}");
            if (compatibilitySequenceIndex >= compatibilitySequence.Length)
                Exit();
            else
                ApplyCompatibilitySequenceEntry(compatibilitySequenceIndex++);
        }
        else if (captureStage == 1 && highResolutionSequence != null)
        {
            bool current = skin!.HighResolutionSkinAssets;
            string modeName = current ? "hd" : "sd";
            string sequenceOutput = Path.Combine(highResolutionSequenceOutputDirectory!,
                $"{highResolutionSequenceIndex:00}-{modeName}.png");
            SaveBackbuffer(sequenceOutput);
            Console.WriteLine($"[SkinVisualSmoke] HD sequence capture {highResolutionSequenceIndex}/{highResolutionSequence.Length}: {sequenceOutput}; active={current}");
            if (highResolutionSequenceIndex >= highResolutionSequence.Length)
                Exit();
            else
                ApplyHighResolutionSequenceEntry(highResolutionSequenceIndex++);
        }
        else if (captureStage == 1 && resizeOutputPath != null && resizeApplied)
        {
            SaveBackbuffer(resizeOutputPath);
            Console.WriteLine($"[SkinVisualSmoke] tras resize {resizedViewportWidth}x{resizedViewportHeight}: {resizeOutputPath}");
            captureStage = 2;
            Exit();
        }
        base.Draw(gameTime);
    }

    private void AdvanceAtReleaseTimelineSmoke()
    {
        double start = AT_RELEASE_TIMELINE_START;
        double end = start + atReleaseTimelineDurationMs;
        var samples = new List<(string Name, double Time)>
        {
            ("head-m16", start - 16), ("head-m8", start - 8), ("head-m4", start - 4), ("head-m1", start - 1),
            ("head-event", start), ("head-p1", start + 1), ("head-p4", start + 4), ("head-p8", start + 8), ("head-p16", start + 16),
            ("tail-m32", end - 32), ("tail-m16", end - 16), ("tail-m8", end - 8), ("tail-m4", end - 4),
            ("tail-m1", end - 1), ("tail-event-held", end)
        };
        if (holdThroughAutoCompletion)
        {
            double missWindow = new TailHitWindows(5, true).WindowFor(HitResult.Miss);
            samples.Add(("tail-auto-m8", end + missWindow - 8));
            samples.Add(("tail-auto-m1", end + missWindow - 1));
            samples.Add(("tail-auto-window", end + missWindow));
            samples.Add(("tail-auto-completed", end + missWindow + 1));
            samples.Add(("tail-auto-expired", end + missWindow + 2));
        }
        else
        {
            samples.AddRange(new[]
            {
                ("tail-p1-release", end + 1), ("tail-p4", end + 4), ("tail-p8", end + 8),
                ("tail-p16", end + 16), ("tail-p32", end + 32)
            });
        }

        if (atReleaseTimelineSampleIndex >= samples.Count)
        {
            LongNoteRenderDiagnostics.Enabled = false;
            LongNoteRenderDiagnostics.DrawOutlines = false;
            Exit();
            return;
        }

        var sample = samples[atReleaseTimelineSampleIndex];
        string outputPath = Path.Combine(atReleaseTimelineOutputDirectory!, $"{atReleaseTimelineSampleIndex:D2}-{sample.Name}.png");
        SaveBackbuffer(outputPath);
        Console.WriteLine(
            $"[SkinVisualSmoke][AT-LN] sample={sample.Name} time={sample.Time:F3} start={start:F3} end={end:F3} " +
            $"release={atReleaseTimelineNote!.TailReleaseTime:F3} head={atReleaseTimelineNote.HeadJudged}/{atReleaseTimelineNote.HeadResult} " +
            $"tail={atReleaseTimelineNote.TailJudged}/{atReleaseTimelineNote.TailResult} state={atReleaseTimelineNote.State} " +
            $"pressed={playfield!.Columns[timelineLongColumn].IsPressed} holding={playfield.Columns[timelineLongColumn].IsHolding} " +
            $"drawCalls={LongNoteRenderDiagnostics.DrawCallCount} file={outputPath}");
        if (sample.Name == "tail-auto-completed"
            && (atReleaseTimelineNote is not { State: LongNoteState.Finished, TailResult: HitResult.Perfect, TailReleaseTime: 0 }
                || LongNoteRenderDiagnostics.DrawCallCount != 0))
            throw new InvalidOperationException("La LN AT debe desaparecer al auto-completar el tail sin dejar sprites residuales.");
        LongNoteRenderDiagnostics.ResetDrawCallCount();
        atReleaseTimelineSampleIndex++;
        if (atReleaseTimelineSampleIndex >= samples.Count)
        {
            LongNoteRenderDiagnostics.Enabled = false;
            LongNoteRenderDiagnostics.DrawOutlines = false;
            Exit();
            return;
        }

        var next = samples[atReleaseTimelineSampleIndex];
        bool shouldPress = next.Time >= start && (holdThroughAutoCompletion || next.Time <= end);
        if (shouldPress && !atReleaseTimelineColumnPressed)
        {
            playfield.HandleKeyPress(timelineLongColumn, next.Time, score!);
            atReleaseTimelineColumnPressed = true;
        }
        else if (!shouldPress && atReleaseTimelineColumnPressed)
        {
            playfield.HandleKeyRelease(timelineLongColumn, next.Time, score!);
            atReleaseTimelineColumnPressed = false;
        }

        timelineCurrentTime = next.Time;
        playfield.Update(next.Time, score!, 8, null, downScroll);
        if (next.Name == "tail-p1-release"
            && (atReleaseTimelineNote is not { State: LongNoteState.Finished }
                || atReleaseTimelineNote.TailResult == HitResult.Miss))
            throw new InvalidOperationException("El ciclo AT de LN no produjo un tail exitoso al soltar después de EndTime.");
    }

    private void ReportAtReleaseLongNoteAssets()
    {
        ResolvedManiaKeymode mode = skin!.GetResolvedKeymode(keyCount);
        ManiaLaneDefinition lane = mode.Lanes[timelineLongColumn].Definition;
        string? noteName = lane.NoteAsset ?? mode.Definition.NoteStyle.Asset;
        string? headName = lane.LongHeadAsset ?? mode.Definition.LongHeadStyle.Asset ?? noteName;
        string? bodyName = lane.LongBodyAsset ?? mode.Definition.LongBodyStyle.Asset;
        string? tailName = lane.LongTailAsset ?? mode.Definition.LongTailStyle.Asset ?? headName ?? noteName;

        Report("head", headName, mode.Lanes[timelineLongColumn].LongHead);
        Report("body", bodyName, mode.Lanes[timelineLongColumn].LongBody);
        Report("tail", tailName, mode.Lanes[timelineLongColumn].LongTail);
        Console.WriteLine($"[SkinVisualSmoke][AT-LN] bodyMode={skin.GetNoteBodyMode(keyCount, timelineLongColumn)} " +
            $"connection=head:{mode.Definition.HoldAssembly.HeadConnectionRatio:0.###},tail:{mode.Definition.HoldAssembly.TailConnectionRatio:0.###}");

        void Report(string role, string? requested, ResolvedSkinAnimation? animation)
        {
            string? active = requested;
            if (active != null && !skin.HasTextureFile(active)) active = null;
            if (active == null && role == "tail")
            {
                string?[] fallbacks = { headName, noteName };
                active = fallbacks.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name) && skin.HasTextureFile(name!));
            }

            ResolvedSkinAsset? resolved = active == null ? null : skin.ResolveAsset(active);
            Texture2D? frame = animation?.FirstFrame;
            Vector2 logical = frame == null ? Vector2.Zero : skin.GetLogicalSize(frame);
            Console.WriteLine($"[SkinVisualSmoke][AT-LN] asset role={role} requested={requested ?? "<none>"} " +
                $"resolvedActive={active ?? "<none; internal semantic fallback>"} origin={resolved?.Origin.ToString() ?? "animation"} " +
                $"source={resolved?.SourcePixelSize.X:0.##}x{resolved?.SourcePixelSize.Y:0.##} " +
                $"gpu={frame?.Width}x{frame?.Height} logical={logical.X:0.##}x{logical.Y:0.##} @2x={resolved?.IsHighResolution} " +
                $"frames={animation?.Frames.Count ?? 0} fps={animation?.FramesPerSecond:0.##}");
        }
    }

    private void AdvanceTimelineSmoke()
    {
        string[] frameNames =
        {
            "00-approach-0700ms.png",
            "01-holding-1040ms.png",
            "02-holding-1090ms.png",
            "03-early-release-1160ms.png",
            "04-stage-light-half-fade-1275ms.png",
            "05-stage-light-faded-1410ms.png",
            "06-long-hold-start.png",
            "07-long-hold-progress.png",
            "08-tail-approach.png",
            "09-after-tail-release.png",
            "10-auto-complete-hold.png",
            "11-auto-complete-tail.png",
            "12-auto-complete-fade.png"
        };

        string framePath = Path.Combine(timelineOutputDirectory!, frameNames[timelineStage]);
        SaveBackbuffer(framePath);
        Console.WriteLine($"[SkinVisualSmoke] timeline {timelineStage + 1}/{frameNames.Length}: {framePath}");

        void UpdateAt(double time)
        {
            timelineCurrentTime = time;
            playfield!.Update(time, score!, 8, null, downScroll);
        }

        switch (timelineStage++)
        {
            case 0:
                playfield!.HandleKeyPress(0, 1000, score!);
                if (keyCount > 1) playfield.HandleKeyPress(keyCount - 1, 1000, score!);
                UpdateAt(1040);
                if (timelineEarlyReleaseNote is not { State: LongNoteState.Holding })
                    throw new InvalidOperationException("El fixture temporal no pudo entrar en el estado de LN Holding.");
                break;
            case 1:
                UpdateAt(1090);
                break;
            case 2:
                playfield!.HandleKeyRelease(0, 1150, score!);
                if (keyCount > 1) playfield.HandleKeyRelease(keyCount - 1, 1150, score!);
                UpdateAt(1160);
                if (timelineEarlyReleaseNote is not { State: LongNoteState.Broken, TailResult: HitResult.Miss })
                    throw new InvalidOperationException("La fase de early release no produjo una LN rota verificable.");
                break;
            case 3:
                // A released StageLight fades for 250ms while keeping its draw rectangle fixed.
                UpdateAt(1275);
                break;
            case 4:
                UpdateAt(1410);
                break;
            case 5:
                double longNoteStart = timelineLongNoteEndTime - 1100;
                UpdateAt(longNoteStart - 25);
                playfield!.HandleKeyPress(timelineLongColumn, longNoteStart, score!);
                UpdateAt(longNoteStart + 40);
                if (timelineLongNote is not { State: LongNoteState.Holding } || timelineLongNote.HeadResult == HitResult.Miss)
                    throw new InvalidOperationException("La LN larga temporal no entró en sostén tras pulsar su head.");
                break;
            case 6:
                UpdateAt(timelineLongNoteEndTime - 875);
                break;
            case 7:
                UpdateAt(timelineLongNoteEndTime - 25);
                ResolvedSkinAnimation? bodyAnimation = skin!.GetResolvedKeymode(keyCount).Lanes[timelineLongColumn].LongBody;
                if (bodyAnimation is { Frames.Count: > 1 }
                    && ReferenceEquals(bodyAnimation.FrameAt(40), bodyAnimation.FrameAt(225)))
                    throw new InvalidOperationException("La animación del body LN no avanzó entre dos instantes del sostén.");
                break;
            case 8:
                playfield!.HandleKeyRelease(timelineLongColumn, timelineLongNoteEndTime, score!);
                UpdateAt(timelineLongNoteEndTime + 5);
                if (timelineLongNote is not { State: LongNoteState.Finished } || timelineLongNote.TailResult == HitResult.Miss)
                    throw new InvalidOperationException("La liberación de tail temporal no completó la LN larga.");
                break;
            case 9:
                UpdateAt(TIMELINE_AUTO_COMPLETE_START - 25);
                playfield!.HandleKeyPress(timelineLongColumn, TIMELINE_AUTO_COMPLETE_START, score!);
                UpdateAt(TIMELINE_AUTO_COMPLETE_START + 40);
                if (timelineAutoCompleteNote is not { State: LongNoteState.Holding }
                    || timelineAutoCompleteNote.HeadResult == HitResult.Miss)
                    throw new InvalidOperationException("La LN de auto-completado no entró en sostén.");
                break;
            case 10:
                UpdateAt(TIMELINE_AUTO_COMPLETE_END + 500);
                if (timelineAutoCompleteNote is not { State: LongNoteState.Finished, TailResult: HitResult.Perfect })
                    throw new InvalidOperationException("El sostén automático no completó la LN.");
                break;
            case 11:
                UpdateAt(TIMELINE_AUTO_COMPLETE_END + 560);
                break;
            case 12:
                Exit();
                break;
            default:
                Exit();
                break;
        }
    }

    private void ApplySwitchSequenceEntry(int index)
    {
        string targetPath = switchSequence![index];
        long previousVersion = skin!.Version;
        string previousDirectory = skin.CurrentSkinDirectory;
        ManiaSkinDefinition previousDefinition = skin.CurrentDefinition;
        ResolvedManiaKeymode previousResolvedMode = skin.GetResolvedKeymode(keyCount);
        object? previousHudPrefixState = skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix);

        bool persistenceFailurePreservedState;
        if (targetPath.Equals("internal", StringComparison.OrdinalIgnoreCase))
        {
            persistenceFailurePreservedState = !skin.TryActivateInternal(() => false);
        }
        else
        {
            SkinValidation rejectedCandidate = skinCatalog!.Validate(targetPath);
            if (rejectedCandidate.Status == SkinValidity.Invalid)
                throw new InvalidDataException($"La entrada {index + 1} de la secuencia es inválida: {rejectedCandidate.Summary}");
            persistenceFailurePreservedState = !skin.TryActivate(rejectedCandidate, () => false);
        }

        if (!persistenceFailurePreservedState
            || skin.Version != previousVersion
            || !string.Equals(skin.CurrentSkinDirectory, previousDirectory, StringComparison.OrdinalIgnoreCase)
            || !ReferenceEquals(skin.CurrentDefinition, previousDefinition)
            || !ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
            || !ReferenceEquals(skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix), previousHudPrefixState))
            throw new InvalidOperationException($"Falló el rollback transaccional antes de activar la entrada {index + 1} de la secuencia.");

        if (!ActivateSkin(targetPath))
            throw new InvalidDataException($"No se pudo activar entrada {index + 1} de la secuencia: {skin.CurrentValidation?.Summary ?? targetPath}");

        if (skin.Version != previousVersion + 1
            || ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
            || ReferenceEquals(skin.GetPreparedNumberPrefixState(skin.CurrentDefinition.ComboPrefix), previousHudPrefixState)
            || persistenceCount != index + 2
            || !string.Equals(hitSoundPlayer!.SkinDirectory, skin.CurrentSkinDirectory, StringComparison.OrdinalIgnoreCase)
            || !PersistedSelectionMatchesActiveSkin())
            throw new InvalidOperationException($"La entrada {index + 1} no publicó un snapshot, provider, hitsounds y selección persistida coherentes.");

        AssertResolvedKeymodesPrepared();
        AssertHudGlyphsPrepared();
        ReportResolvedHighResolutionAssets();
        playfield!.Update(1040, score!, 8, null, downScroll);
    }

    private void ApplyCompatibilitySequenceEntry(int index)
    {
        LegacySkinCompatibilityMode targetMode = compatibilitySequence![index];
        SkinProviderKind previousProvider = skin!.CurrentDefinition.Provider;
        bool isLegacyProvider = previousProvider == SkinProviderKind.OsuLegacy;
        if (!isLegacyProvider && previousProvider != SkinProviderKind.NullscentNative)
            throw new InvalidOperationException("El ciclo de perfil requiere una skin Native o osu! legacy activa.");

        long previousVersion = skin.Version;
        LegacySkinCompatibilityMode previousMode = skin.LegacyCompatibilityMode;
        ManiaSkinDefinition previousDefinition = skin.CurrentDefinition;
        ManiaSkinCompatibilityProfile previousProfile = previousDefinition.CompatibilityProfile;
        ResolvedManiaKeymode previousResolvedMode = skin.GetResolvedKeymode(keyCount);
        object? previousHudPrefixState = skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix);
        string previousDirectory = skin.CurrentSkinDirectory;
        string? previousSelection = smokeSettings!.SelectedSkinDirectory;
        string? previousHitsoundDirectory = hitSoundPlayer!.SkinDirectory;
        int saveAttempts = 0;

        if (targetMode == previousMode)
            throw new InvalidOperationException("Cada paso del ciclo de perfil debe cambiar la interpretación activa.");

        if (LegacySkinCompatibilityWorkflow.TrySet(skin, skinCatalog!, smokeSettings, targetMode,
                mode => SettingsService.Instance.LegacySkinCompatibilityMode = mode,
                () => { saveAttempts++; return false; })
            || skin.Version != previousVersion
            || skin.LegacyCompatibilityMode != previousMode
            || smokeSettings.LegacySkinCompatibilityMode != previousMode
            || skinCatalog!.CompatibilityMode != previousMode
            || skin.CurrentDefinition.Provider != previousProvider
            || !ReferenceEquals(skin.CurrentDefinition, previousDefinition)
            || !ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
            || !ReferenceEquals(skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix), previousHudPrefixState)
            || !string.Equals(skin.CurrentSkinDirectory, previousDirectory, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(smokeSettings.SelectedSkinDirectory, previousSelection, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(hitSoundPlayer!.SkinDirectory, previousHitsoundDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El rollback por persistencia fallida alteró la skin o su estado resuelto.");

        if (!LegacySkinCompatibilityWorkflow.TrySet(skin, skinCatalog!, smokeSettings, targetMode,
                mode => SettingsService.Instance.LegacySkinCompatibilityMode = mode,
                () => { saveAttempts++; return true; }))
            throw new InvalidDataException("No se pudo reconstruir la skin con el perfil legacy destino.");

        bool invalidProfileTransition = isLegacyProvider
            ? skin.Version != previousVersion + 1
                || skin.CurrentDefinition.Provider != SkinProviderKind.OsuLegacy
                || skin.CurrentDefinition.CompatibilityProfile != (targetMode == LegacySkinCompatibilityMode.Stable
                    ? ManiaSkinCompatibilityProfile.StableLegacy : ManiaSkinCompatibilityProfile.LazerLegacy)
                || ReferenceEquals(skin.CurrentDefinition, previousDefinition)
                || ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
                || ReferenceEquals(skin.GetPreparedNumberPrefixState(skin.CurrentDefinition.ComboPrefix), previousHudPrefixState)
            : skin.Version != previousVersion
                || skin.CurrentDefinition.Provider != previousProvider
                || skin.CurrentDefinition.CompatibilityProfile != previousProfile
                || !ReferenceEquals(skin.CurrentDefinition, previousDefinition)
                || !ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
                || !ReferenceEquals(skin.GetPreparedNumberPrefixState(previousDefinition.ComboPrefix), previousHudPrefixState);
        if (invalidProfileTransition
            || skin.LegacyCompatibilityMode != targetMode
            || !string.Equals(skin.CurrentSkinDirectory, previousDirectory, StringComparison.OrdinalIgnoreCase)
            || smokeSettings.LegacySkinCompatibilityMode != targetMode
            || skinCatalog!.CompatibilityMode != targetMode
            || saveAttempts != 2
            || !string.Equals(smokeSettings.SelectedSkinDirectory, previousSelection, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(hitSoundPlayer.SkinDirectory, previousHitsoundDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El cambio de perfil dejó provider, selección, caches o hitsounds inconsistentes.");

        if (isLegacyProvider)
        {
            AssertResolvedKeymodesPrepared();
            AssertHudGlyphsPrepared();
            ReportResolvedHighResolutionAssets();
        }
        playfield!.Update(1040, score!, 8, null, downScroll);
        Console.WriteLine($"[SkinVisualSmoke] perfil runtime aplicado: {previousMode} -> {targetMode}; provider={skin.CurrentDefinition.Provider}; profile={skin.CurrentDefinition.CompatibilityProfile}; version={skin.Version}");
    }

    private void ApplyHighResolutionSequenceEntry(int index)
    {
        bool target = highResolutionSequence![index];
        bool previous = skin!.HighResolutionSkinAssets;
        if (target == previous)
            throw new InvalidOperationException("Cada paso del ciclo HD debe cambiar la preferencia activa.");

        long previousVersion = skin.Version;
        SkinProviderKind previousProvider = skin.CurrentDefinition.Provider;
        ManiaSkinCompatibilityProfile previousProfile = skin.CurrentDefinition.CompatibilityProfile;
        ResolvedManiaKeymode previousResolvedMode = skin.GetResolvedKeymode(keyCount);
        Dictionary<string, Vector2> previousLogicalSizes = CaptureLogicalSizes(previousResolvedMode);
        int previousReceptorY = playfield!.ReceptorY;
        int previousReceptorSpriteY = playfield.ReceptorSpriteY;
        object? previousHudState = skin.GetPreparedNumberPrefixState(skin.CurrentDefinition.ComboPrefix);
        SkinValidation? previousValidation = skin.CurrentValidation;
        int previousWarningCount = previousValidation?.Warnings.Count ?? 0;
        int previousErrorCount = previousValidation?.Errors.Count ?? 0;
        SkinValidity? previousValidationStatus = previousValidation?.Status;
        ResolvedSkinAsset previousProbeAsset = skin.ResolveAsset("bar-note");
        bool canVerifyProbeAsset = previousProbeAsset.Texture != null
            && previousProbeAsset.Origin != SkinAssetOrigin.IntentionalBlank;

        int failedSaveAttempts = 0;
        bool rollbackPreservedState = !HighResolutionSkinWorkflow.TrySet(skin, smokeSettings!, !previous,
            value => SettingsService.Instance.HighResolutionSkins = value,
            () => { failedSaveAttempts++; return false; });
        if (!rollbackPreservedState || failedSaveAttempts != 1
            || skin.Version != previousVersion
            || skin.HighResolutionSkinAssets != previous
            || smokeSettings!.HighResolutionSkins != previous
            || !ReferenceEquals(skin.CurrentValidation, previousValidation)
            || (previousValidation != null
                && (previousValidation.Warnings.Count != previousWarningCount
                    || previousValidation.Errors.Count != previousErrorCount
                    || previousValidation.Status != previousValidationStatus))
            || !ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
            || !ReferenceEquals(skin.GetPreparedNumberPrefixState(skin.CurrentDefinition.ComboPrefix), previousHudState)
            || (canVerifyProbeAsset && previousProbeAsset.Texture!.IsDisposed))
            throw new InvalidOperationException("El rollback HD por fallo de persistencia alteró el proveedor o sus caches.");

        int successfulSaveAttempts = 0;
        if (!HighResolutionSkinWorkflow.TrySet(skin, smokeSettings, target,
                value => SettingsService.Instance.HighResolutionSkins = value,
                () => { successfulSaveAttempts++; return true; }))
            throw new InvalidDataException("No se pudo activar la preferencia HD destino.");

        if (successfulSaveAttempts != 1
            || skin.Version != previousVersion + 1
            || skin.HighResolutionSkinAssets != target
            || smokeSettings.HighResolutionSkins != target
            || skin.CurrentDefinition.Provider != previousProvider
            || skin.CurrentDefinition.CompatibilityProfile != previousProfile
            || ReferenceEquals(skin.GetResolvedKeymode(keyCount), previousResolvedMode)
            || ReferenceEquals(skin.GetPreparedNumberPrefixState(skin.CurrentDefinition.ComboPrefix), previousHudState)
            || (canVerifyProbeAsset && !previousProbeAsset.Texture!.IsDisposed))
            throw new InvalidOperationException("El cambio HD no reemplazó los snapshots/proveedores de forma transaccional.");

        ResolvedSkinAsset currentProbeAsset = skin.ResolveAsset("bar-note");
        if (canVerifyProbeAsset && (currentProbeAsset.Origin != previousProbeAsset.Origin
            || currentProbeAsset.Texture == null
            || ReferenceEquals(currentProbeAsset.Texture, previousProbeAsset.Texture)))
            throw new InvalidOperationException("La textura probe conserva un proveedor o recurso anterior al cambio HD.");
        if (!canVerifyProbeAsset)
            Console.WriteLine("[SkinVisualSmoke] texture identity check skipped: bar-note resolves to an intentional blank.");

        playfield.Update(1040, score!, 8, null, downScroll);
        ResolvedManiaKeymode currentResolvedMode = skin.GetResolvedKeymode(keyCount);
        Dictionary<string, Vector2> currentLogicalSizes = CaptureLogicalSizes(currentResolvedMode);
        if (playfield.ReceptorY != previousReceptorY || playfield.ReceptorSpriteY != previousReceptorSpriteY)
            throw new InvalidOperationException($"El toggle @2x movió el ancla del receptor: {previousReceptorY}/{previousReceptorSpriteY} -> {playfield.ReceptorY}/{playfield.ReceptorSpriteY}.");
        string[] logicalSizeChanges = previousLogicalSizes
            .Where(pair => !currentLogicalSizes.TryGetValue(pair.Key, out Vector2 size)
                || Math.Abs(pair.Value.X - size.X) > 0.01f || Math.Abs(pair.Value.Y - size.Y) > 0.01f)
            .Select(pair => currentLogicalSizes.TryGetValue(pair.Key, out Vector2 size)
                ? $"{pair.Key} {pair.Value.X}x{pair.Value.Y}->{size.X}x{size.Y}"
                : $"{pair.Key} {pair.Value.X}x{pair.Value.Y}->missing")
            .Concat(currentLogicalSizes.Keys.Where(key => !previousLogicalSizes.ContainsKey(key))
                .Select(key => $"{key} missing->{currentLogicalSizes[key].X}x{currentLogicalSizes[key].Y}"))
            .ToArray();
        if (logicalSizeChanges.Length > 0)
            throw new InvalidOperationException("El toggle @2x cambió tamaños lógicos de assets de Mania: "
                + string.Join("; ", logicalSizeChanges.Take(12)));

        VerifyHighResolutionAssetSelection(target);
        AssertResolvedKeymodesPrepared();
        AssertHudGlyphsPrepared();
        ReportResolvedHighResolutionAssets();
        Console.WriteLine($"[SkinVisualSmoke] HD runtime aplicado: {previous} -> {target}; provider={skin.CurrentDefinition.Provider}; version={skin.Version}");
    }

    private Dictionary<string, Vector2> CaptureLogicalSizes(ResolvedManiaKeymode mode)
    {
        var result = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        void CaptureAnimation(string name, ResolvedSkinAnimation? animation)
        {
            if (animation == null) return;
            for (int frame = 0; frame < animation.Frames.Count; frame++)
                result[$"{name}:{frame}"] = skin!.GetLogicalSize(animation.Frames[frame]);
        }

        for (int laneIndex = 0; laneIndex < mode.Lanes.Count; laneIndex++)
        {
            ResolvedManiaLane lane = mode.Lanes[laneIndex];
            CaptureAnimation($"lane{laneIndex}:note({lane.Definition.NoteAsset})", lane.Note);
            CaptureAnimation($"lane{laneIndex}:receptor({lane.Definition.ReceptorAsset})", lane.Receptor);
            CaptureAnimation($"lane{laneIndex}:pressed({lane.Definition.PressedReceptorAsset})", lane.PressedReceptor);
            CaptureAnimation($"lane{laneIndex}:head({lane.Definition.LongHeadAsset})", lane.LongHead);
            CaptureAnimation($"lane{laneIndex}:body({lane.Definition.LongBodyAsset})", lane.LongBody);
            CaptureAnimation($"lane{laneIndex}:tail({lane.Definition.LongTailAsset})", lane.LongTail);
            CaptureAnimation($"lane{laneIndex}:lightingN", lane.NoteLighting);
            CaptureAnimation($"lane{laneIndex}:lightingL", lane.HoldLighting);
        }
        foreach (var stage in mode.StageAssets)
            CaptureAnimation($"stage:{stage.Key}", stage.Value);
        foreach (var judgement in mode.Judgements)
            CaptureAnimation($"judgement:{judgement.Key}", judgement.Value);
        return result;
    }

    private void VerifyHighResolutionAssetSelection(bool preferHighResolution)
    {
        if (skin == null) return;
        string? directory = skin.CurrentSkinDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            Console.WriteLine("[SkinVisualSmoke] 1x/@2x selection check skipped: active skin has no external directory.");
            return;
        }

        static string GetStem(string name)
        {
            string normalized = name.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string extension = Path.GetExtension(normalized);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^extension.Length];
            if (normalized.EndsWith("@2x", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^3];
            return normalized;
        }

        bool HasValidHighResolutionPair(string stem)
        {
            string pathStem = Path.Combine(directory, stem);
            foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
            {
                string standardPath = pathStem + extension;
                string highResolutionPath = pathStem + "@2x" + extension;
                if (!File.Exists(standardPath) || !File.Exists(highResolutionPath)) continue;
                try
                {
                    using var standardStream = File.OpenRead(standardPath);
                    using var highResolutionStream = File.OpenRead(highResolutionPath);
                    var standardInfo = StbImageSharp.ImageInfo.FromStream(standardStream);
                    var highResolutionInfo = StbImageSharp.ImageInfo.FromStream(highResolutionStream);
                    if (standardInfo is { Width: > 0, Height: > 0 } standardDimensions
                        && highResolutionInfo is { Width: > 0, Height: > 0 } highResolutionDimensions
                        && SkinAssetResolver.HasExpectedHighResolutionDimensions(
                            new Vector2(standardDimensions.Width, standardDimensions.Height),
                            new Vector2(highResolutionDimensions.Width, highResolutionDimensions.Height)))
                        return true;
                }
                catch
                {
                    // An unreadable pair is not suitable for proving @2x selection.
                }
            }

            return false;
        }

        var candidates = new List<string> { "score-1" };
        ResolvedManiaKeymode resolvedMode = skin.GetResolvedKeymode(keyCount);
        foreach (ResolvedManiaLane lane in resolvedMode.Lanes)
        {
            candidates.AddRange(new[]
            {
                lane.Definition.NoteAsset,
                lane.Definition.ReceptorAsset,
                lane.Definition.PressedReceptorAsset,
                lane.Definition.LongHeadAsset,
                lane.Definition.LongBodyAsset,
                lane.Definition.LongTailAsset
            }.Where(asset => !string.IsNullOrWhiteSpace(asset)).Select(asset => asset!));
        }
        ManiaKeymodeDefinition profile = skin.GetManiaProfile(keyCount);
        if (profile.StageAssets.TryGetValue("light", out string? configuredLight)
            && !string.IsNullOrWhiteSpace(configuredLight))
            candidates.Add(configuredLight);
        candidates.AddRange(SkinCatalog.ReferencedImages(skin.CurrentDefinition));

        string? assetName = null;
        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                continue;
            string stem = GetStem(candidate);
            string pathStem = Path.Combine(directory, stem);
            bool hasStandard = new[] { ".png", ".jpg", ".jpeg" }
                .Any(extension => File.Exists(pathStem + extension));
            bool hasHighResolution = new[] { ".png", ".jpg", ".jpeg" }
                .Any(extension => File.Exists(pathStem + "@2x" + extension));
            if (!hasStandard || !hasHighResolution || !HasValidHighResolutionPair(stem)) continue;
            assetName = stem;
            break;
        }

        if (assetName == null)
        {
            Console.WriteLine("[SkinVisualSmoke] 1x/@2x selection check skipped: no valid 2x pair in the active skin.");
            return;
        }

        ResolvedSkinAsset asset = skin.ResolveAsset(assetName);
        if (asset.Origin != SkinAssetOrigin.Skin || asset.IsHighResolution != preferHighResolution)
            throw new InvalidOperationException($"El resolver no seleccionó la variante {(preferHighResolution ? "@2x" : "1x")} de {assetName}.");

        if (highResolutionReferenceLogicalSize is Vector2 reference
            && (Math.Abs(reference.X - asset.LogicalSize.X) > 0.01f
                || Math.Abs(reference.Y - asset.LogicalSize.Y) > 0.01f))
            throw new InvalidOperationException($"El tamaño lógico de {assetName} cambió durante el ciclo HD.");

        highResolutionReferenceLogicalSize = asset.LogicalSize;
        foreach (string warning in skin.CurrentValidation?.Warnings
                     .Where(message => message.StartsWith("@2x variant", StringComparison.OrdinalIgnoreCase))
                     .Take(4) ?? Array.Empty<string>())
            Console.WriteLine($"[SkinVisualSmoke] asset diagnostic: {warning}");
        Console.WriteLine($"[SkinVisualSmoke] 1x/@2x gameplay asset {assetName}: selected={asset.IsHighResolution}, source={asset.SourcePixelSize.X}x{asset.SourcePixelSize.Y}, logical={asset.LogicalSize.X}x{asset.LogicalSize.Y}.");
    }

    protected override void Update(GameTime gameTime)
    {
        if (captureStage == 1 && resizeOutputPath != null && !resizeApplied)
        {
            graphics.PreferredBackBufferWidth = resizedViewportWidth!.Value;
            graphics.PreferredBackBufferHeight = resizedViewportHeight!.Value;
            graphics.ApplyChanges();
            resizeApplied = true;
            Console.WriteLine($"[SkinVisualSmoke] viewport cambiado en runtime: {viewportWidth}x{viewportHeight} -> {resizedViewportWidth}x{resizedViewportHeight}");
        }

        base.Update(gameTime);
    }

    private bool ActivateSkin(string path)
    {
        string? selectedPath = path.Equals("internal", StringComparison.OrdinalIgnoreCase) ? null : path;
        bool selected = SkinSelectionWorkflow.TrySelect(
            skin!,
            skinCatalog!,
            smokeSettings!,
            selectedPath,
            () =>
            {
                persistenceCount++;
                return true;
            },
            directory =>
            {
                hitSoundPlayer!.SkinDirectory = directory;
                hitSoundPlayer.ClearCache();
            },
            message => Console.WriteLine($"[SkinVisualSmoke] {message}"));
        if (skin!.CurrentValidation is { } validation)
            Console.WriteLine($"[SkinVisualSmoke] validation: {validation.Status}; {validation.Summary}");
        return selected;
    }

    private void AssertResolvedKeymodesPrepared()
    {
        for (int count = 1; count <= 18; count++)
            if (!skin!.HasResolvedKeymode(count))
                throw new InvalidOperationException($"El keymode {count}K no estaba resuelto antes del primer Draw.");
    }

    private void AssertHudGlyphsPrepared()
    {
        foreach (string prefix in new[] { "score", "default", skin!.CurrentDefinition.ComboPrefix })
            if (!skin.HasPreparedNumberPrefix(prefix))
                throw new InvalidOperationException($"El prefijo HUD '{prefix}' no estaba preparado antes del primer Draw.");
    }

    private bool PersistedSelectionMatchesActiveSkin()
    {
        if (smokeSettings == null || skinCatalog == null || skin == null) return false;
        string? selection = smokeSettings.SelectedSkinDirectory;
        if (selection == null) return string.IsNullOrEmpty(skin.CurrentSkinDirectory);
        if (selection.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
        {
            string key = selection["builtin:".Length..];
            selection = skinCatalog.Entries.FirstOrDefault(entry => entry.IsNative
                && string.Equals(Path.GetFileName(entry.Directory), key, StringComparison.OrdinalIgnoreCase))?.Directory;
        }
        return selection != null
            && string.Equals(Path.GetFullPath(selection), Path.GetFullPath(skin.CurrentSkinDirectory), StringComparison.OrdinalIgnoreCase);
    }

    private void ReportResolvedHighResolutionAssets()
    {
        if (skin == null) return;

        ManiaKeymodeDefinition profile = skin.GetManiaProfile(keyCount);
        string comboAsset = $"{skin.GetComboPrefix()}-1";
        if (skin.HasTextureFile(comboAsset))
        {
            ResolvedSkinAsset combo = skin.ResolveAsset(comboAsset);
            Console.WriteLine($"[SkinVisualSmoke] asset {comboAsset}: {combo.Origin}, @2x={combo.IsHighResolution}, logical={combo.LogicalSize.X}x{combo.LogicalSize.Y}");
        }

        foreach (string part in new[] { "left", "right", "bottom", "hint", "light" })
        {
            string? configured = profile.StageAssets.TryGetValue(part, out string? value) ? value : null;
            if (string.IsNullOrWhiteSpace(configured))
            {
                Console.WriteLine($"[SkinVisualSmoke] stage {part}: no active skin asset configured.");
                continue;
            }
            string name = configured;
            IReadOnlyList<Texture2D>? frames = skin.GetStageAnimation(keyCount, part);
            string resolvedName = skin.HasTextureFile($"{name}-0") ? $"{name}-0" : name;
            ResolvedSkinAsset resolved = skin.ResolveAsset(resolvedName);
            Texture2D? firstFrame = frames is { Count: > 0 } ? frames[0] : null;
            Vector2 logicalSize = firstFrame == null ? Vector2.Zero : skin.GetLogicalSize(firstFrame);
            Console.WriteLine($"[SkinVisualSmoke] stage {part}: requested={name}, activeBase={skin.HasTextureFile(name)}, activeFrame0={skin.HasTextureFile($"{name}-0")}, frames={frames?.Count ?? 0}, origin={resolved.Origin}, logical={logicalSize.X}x{logicalSize.Y}");
        }
    }

    private void SaveBackbuffer(string path)
    {
        int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var pixels = new Color[width * height];
        GraphicsDevice.GetBackBufferData(pixels);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var packed = new int[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                packed[i] = (pixels[i].A << 24) | (pixels[i].R << 16) | (pixels[i].G << 8) | pixels[i].B;
            Marshal.Copy(packed, 0, data.Scan0, packed.Length);
        }
        finally { bitmap.UnlockBits(data); }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, ImageFormat.Png);
    }

    protected override void UnloadContent()
    {
        playfield?.Dispose();
        skin?.Dispose();
        batch?.Dispose();
        base.UnloadContent();
    }
}
#endif
