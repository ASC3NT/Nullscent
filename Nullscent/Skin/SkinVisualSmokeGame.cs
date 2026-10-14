#nullable enable
#if DEBUG
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.Rulesets.Mania.Objects;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Rulesets.Mania.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
    private SpriteBatch? batch;
    private SkinManager? skin;
    private ManiaPlayfield? playfield;
    private ManiaScoreProcessor? score;
    private int captureStage;
    private bool resizeApplied;

    public SkinVisualSmokeGame(string skinPath, int keyCount, string outputPath, bool downScroll,
        string? switchSkinPath = null, string? switchOutputPath = null, int viewportWidth = 1280, int viewportHeight = 720,
        string? resizeOutputPath = null, int? resizedViewportWidth = null, int? resizedViewportHeight = null,
        LegacySkinCompatibilityMode legacyCompatibilityMode = LegacySkinCompatibilityMode.Stable,
        LegacySkinCompatibilityMode? switchCompatibilityMode = null, string? compatibilityOutputPath = null)
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
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = viewportWidth,
            PreferredBackBufferHeight = viewportHeight
        };
        IsFixedTimeStep = false;
        IsMouseVisible = false;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        SettingsService.Instance.Initialize(new GameSettings { LegacySkinCompatibilityMode = legacyCompatibilityMode });
        skin = new SkinManager(GraphicsDevice);
        if (!ActivateSkin(skinPath)) throw new InvalidDataException($"No se pudo activar skin inicial: {skin.CurrentValidation?.Summary ?? skinPath}");
        ReportResolvedHighResolutionAssets();

        playfield = new ManiaPlayfield(GraphicsDevice, batch, keyCount, viewportWidth, viewportHeight, skinManager: skin);
        score = new ManiaScoreProcessor(keyCount, keyCount, 5, false, new List<Nullscent.Rulesets.Mania.Mods.ManiaMod>());
        for (int column = 0; column < keyCount; column++)
        {
            double noteTime = column == keyCount - 1 && keyCount > 1 ? 1000 : 1200 + column * 75;
            playfield.AddHitObject(new Note { StartTime = noteTime, Column = column });
            if (column % 2 == 0)
                playfield.AddHitObject(new Note { StartTime = 1550, Column = column });
            if (column % 2 != 0 && (column == 1 || column == keyCount / 2))
                playfield.AddHitObject(new Note { StartTime = 1550, Column = column });

            double longNoteStart = column == 0 || column == keyCount - 1 ? 1000 : 1400 + column * 75;
            double longNoteDuration = column % 2 == 0 ? 300 : 1100;
            playfield.AddHitObject(new LongNote(longNoteStart, longNoteStart + longNoteDuration, column));
        }
        if (keyCount > 1) playfield.HandleKeyPress(keyCount - 1, 1000, score);
        playfield.HandleKeyPress(0, 1000, score);
        playfield.JudgementDisplayManager?.EnqueueJudgement(HitResult.Perfect, 0, 0, 12, 1000);
        playfield.Update(1040, score, 8, null, downScroll);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(6, 9, 16));
        playfield!.Draw(1040, 8, null, downScroll);
        if (captureStage == 0)
        {
            SaveBackbuffer(outputPath);
            Console.WriteLine($"[SkinVisualSmoke] {keyCount}K initial: {outputPath}");
            captureStage = 1;
            if (switchSkinPath == null && switchCompatibilityMode == null)
            {
                if (resizeOutputPath == null) Exit();
            }
            else if (switchCompatibilityMode != null)
            {
                long previousVersion = skin!.Version;
                SkinProviderKind previousProvider = skin.CurrentDefinition.Provider;
                if (previousProvider != SkinProviderKind.OsuLegacy)
                    throw new InvalidOperationException("El smoke de perfil requiere una skin osu! legacy activa.");
                if (!skin.TrySetLegacyCompatibilityMode(switchCompatibilityMode.Value, () => true))
                    throw new InvalidDataException("No se pudo reconstruir la skin con el perfil de compatibilidad destino.");
                ManiaSkinCompatibilityProfile expectedProfile = switchCompatibilityMode == LegacySkinCompatibilityMode.Stable
                    ? ManiaSkinCompatibilityProfile.StableLegacy : ManiaSkinCompatibilityProfile.LazerLegacy;
                if (skin.Version != previousVersion + 1
                    || skin.CurrentDefinition.Provider != previousProvider
                    || skin.CurrentDefinition.CompatibilityProfile != expectedProfile)
                    throw new InvalidOperationException("El cambio de perfil no fue atÃ³mico o cambiÃ³ el proveedor de la skin.");
                playfield!.Update(1040, score!, 8, null, downScroll);
                Console.WriteLine($"[SkinVisualSmoke] perfil runtime aplicado: {legacyCompatibilityMode} -> {switchCompatibilityMode}; proveedor={skin.CurrentDefinition.Provider}; {compatibilityOutputPath}");
            }
            else
            {
                long previousVersion = skin!.Version;
                if (!ActivateSkin(switchSkinPath!)) throw new InvalidDataException($"No se pudo activar skin destino: {skin.CurrentValidation?.Summary ?? switchSkinPath}");
                ReportResolvedHighResolutionAssets();
                if (skin.Version != previousVersion + 1) throw new InvalidOperationException("La versión de skin no avanzó exactamente una vez.");
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
        else if (captureStage == 1 && resizeOutputPath != null && resizeApplied)
        {
            SaveBackbuffer(resizeOutputPath);
            Console.WriteLine($"[SkinVisualSmoke] tras resize {resizedViewportWidth}x{resizedViewportHeight}: {resizeOutputPath}");
            captureStage = 2;
            Exit();
        }
        base.Draw(gameTime);
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
        if (path.Equals("internal", StringComparison.OrdinalIgnoreCase))
            return skin!.TryActivateInternal();

        string fullPath = Path.GetFullPath(path);
        var validation = new SkinCatalog(Path.GetDirectoryName(fullPath)!,
            compatibilityMode: skin!.LegacyCompatibilityMode).Validate(fullPath, skin.LegacyCompatibilityMode);
        Console.WriteLine($"[SkinVisualSmoke] validation: {validation.Status}; {validation.Summary}");
        return skin!.TryActivate(validation);
    }

    private void ReportResolvedHighResolutionAssets()
    {
        if (skin == null) return;

        ManiaKeymodeDefinition profile = skin.GetManiaProfile(keyCount);
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{skin.GetComboPrefix()}-1",
            "mania-stage-bottom"
        };
        if (profile.StageAssets.TryGetValue("hint", out string? stageHint)
            && !string.IsNullOrWhiteSpace(stageHint) && !stageHint.Equals("_blank", StringComparison.OrdinalIgnoreCase))
            candidates.Add(stageHint);
        else
            candidates.Add("mania-stage-hint");

        foreach (string name in candidates)
        {
            if (!skin.HasTextureFile(name)) continue;
            ResolvedSkinAsset resolved = skin.ResolveAsset(name);
            Console.WriteLine($"[SkinVisualSmoke] asset {name}: {resolved.Origin}, @2x={resolved.IsHighResolution}, logical={resolved.LogicalSize.X}x{resolved.LogicalSize.Y}");
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
