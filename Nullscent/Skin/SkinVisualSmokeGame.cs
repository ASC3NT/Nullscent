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
    private SpriteBatch? batch;
    private SkinManager? skin;
    private ManiaPlayfield? playfield;
    private ManiaScoreProcessor? score;
    private int captureStage;

    public SkinVisualSmokeGame(string skinPath, int keyCount, string outputPath, bool downScroll,
        string? switchSkinPath = null, string? switchOutputPath = null)
    {
        if (keyCount < 1 || keyCount > 18) throw new ArgumentOutOfRangeException(nameof(keyCount));
        if ((switchSkinPath == null) != (switchOutputPath == null)) throw new ArgumentException("El cambio de skin requiere skin destino y captura de salida.");
        this.skinPath = skinPath;
        this.keyCount = keyCount;
        this.outputPath = Path.GetFullPath(outputPath);
        this.downScroll = downScroll;
        this.switchSkinPath = switchSkinPath;
        this.switchOutputPath = switchOutputPath == null ? null : Path.GetFullPath(switchOutputPath);
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        IsFixedTimeStep = false;
        IsMouseVisible = false;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        SettingsService.Instance.Initialize(new GameSettings());
        skin = new SkinManager(GraphicsDevice);
        if (!ActivateSkin(skinPath)) throw new InvalidDataException($"No se pudo activar skin inicial: {skin.CurrentValidation?.Summary ?? skinPath}");
        ReportResolvedHighResolutionAssets();

        playfield = new ManiaPlayfield(GraphicsDevice, batch, keyCount, 1280, 720, skinManager: skin);
        score = new ManiaScoreProcessor(keyCount, keyCount, 5, false, new List<Nullscent.Rulesets.Mania.Mods.ManiaMod>());
        for (int column = 0; column < keyCount; column++)
        {
            double noteTime = column == keyCount - 1 && keyCount > 1 ? 1000 : 1200 + column * 75;
            playfield.AddHitObject(new Note { StartTime = noteTime, Column = column });

            double longNoteStart = column == 0 ? 1000 : 1400 + column * 75;
            double longNoteDuration = column % 2 == 0 ? 300 : 1100;
            playfield.AddHitObject(new LongNote(longNoteStart, longNoteStart + longNoteDuration, column));
        }
        if (keyCount > 1) playfield.HandleKeyPress(keyCount - 1, 1000, score);
        playfield.HandleKeyPress(0, 1000, score);
        playfield.JudgementDisplayManager?.EnqueueJudgement(HitResult.Perfect, 0, 0, 12, 1000);
        playfield.Update(1040, score, 8, 0.83f, downScroll);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        playfield!.Draw(1040, 8, 0.83f, downScroll);
        if (captureStage == 0)
        {
            SaveBackbuffer(outputPath);
            Console.WriteLine($"[SkinVisualSmoke] {keyCount}K initial: {outputPath}");
            captureStage = 1;
            if (switchSkinPath == null)
            {
                Exit();
            }
            else
            {
                long previousVersion = skin!.Version;
                if (!ActivateSkin(switchSkinPath)) throw new InvalidDataException($"No se pudo activar skin destino: {skin.CurrentValidation?.Summary ?? switchSkinPath}");
                ReportResolvedHighResolutionAssets();
                if (skin.Version != previousVersion + 1) throw new InvalidOperationException("La versión de skin no avanzó exactamente una vez.");
                playfield!.Update(1040, score!, 8, 0.83f, downScroll);
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
        base.Draw(gameTime);
    }

    private bool ActivateSkin(string path)
    {
        if (path.Equals("internal", StringComparison.OrdinalIgnoreCase))
            return skin!.TryActivateInternal();

        string fullPath = Path.GetFullPath(path);
        var validation = new SkinCatalog(Path.GetDirectoryName(fullPath)!).Validate(fullPath);
        Console.WriteLine($"[SkinVisualSmoke] validation: {validation.Status}; {validation.Summary}");
        return skin!.TryActivate(validation);
    }

    private void ReportResolvedHighResolutionAssets()
    {
        if (skin == null) return;

        var config = skin.CurrentSkinConfig?.GetConfigForKeyCount(keyCount);
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{skin.GetComboPrefix()}-1"
        };
        if (!string.IsNullOrWhiteSpace(config?.StageHint) && !config.StageHint.Equals("_blank", StringComparison.OrdinalIgnoreCase))
            candidates.Add(config.StageHint);
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
