#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nullscent.Skin
{
    /// <summary>
    /// Gestiona carga y acceso a elementos de skin (texturas, configuración, animaciones).
    /// Proporciona fallbacks cuando elementos de skin no están disponibles.
    /// Soporta formato de skin osu!stable/lazer para mania.
    /// </summary>
    public class SkinManager : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private SkinConfig? _currentSkinConfig;
        private SkinAssetResolver _assets;
        private readonly Dictionary<Texture2D, ResolvedSkinAsset> _assetInfo = new();
        private readonly Dictionary<string, List<Texture2D>?> _animationCache = new();
        private Texture2D? _blankTexture;

        public SkinConfig? CurrentSkinConfig => _currentSkinConfig;
        public string CurrentSkinDirectory { get; private set; } = string.Empty;
        public long Version { get; private set; }
        public event Action<long>? SkinChanged;
        public SkinValidation? CurrentValidation { get; private set; }
        public ManiaKeyConfig? GetManiaConfig(int keyCount) => _currentSkinConfig?.GetConfigForKeyCount(keyCount);

        public SkinManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _assets = new SkinAssetResolver(graphicsDevice, null, Core.SettingsService.Instance.HighResolutionSkins);
            _blankTexture = new Texture2D(graphicsDevice, 1, 1);
            _blankTexture.SetData(new[] { Color.Transparent });
        }

        /// <summary>
        /// Carga una skin desde un directorio.
        /// </summary>
        public bool LoadSkin(string skinDirectory)
        {
            return TryActivate(new SkinCatalog(Path.GetDirectoryName(Path.GetFullPath(skinDirectory))!).Validate(skinDirectory));
        }

        public bool TryActivate(SkinValidation validation, Func<bool>? persistSelection = null)
        {
            if (validation.Status == SkinValidity.Invalid) return false;
            SkinAssetResolver? candidate = null;
            try
            {
                candidate = new SkinAssetResolver(_graphicsDevice, validation.Directory, Core.SettingsService.Instance.HighResolutionSkins);
                // Prepare conventional assets and every image referenced by skin.ini before commit.
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "cursor", "mania-note1", "mania-note2", "mania-key1", "mania-key2" };
                if (validation.Config != null)
                    foreach (var config in validation.Config.ManiaConfigs.Values)
                        foreach (string name in SkinCatalog.ReferencedImages(config)) names.Add(name);
                foreach (string name in names)
                    candidate.Resolve(name);
                if (candidate.Warnings.Count > 0)
                {
                    validation.Warnings.AddRange(candidate.Warnings);
                    validation.Status = SkinValidity.Partial;
                }
                var previous = _assets;
                var previousConfig = _currentSkinConfig;
                var previousDirectory = CurrentSkinDirectory;
                var previousValidation = CurrentValidation;
                _assets = candidate;
                candidate = null;
                _currentSkinConfig = validation.Config;
                CurrentSkinDirectory = validation.Directory;
                CurrentValidation = validation;
                bool persisted;
                try { persisted = persistSelection?.Invoke() ?? true; }
                catch (Exception ex) { validation.Errors.Add(ex.Message); persisted = false; }
                if (!persisted)
                {
                    _assets.Dispose();
                    _assets = previous;
                    _currentSkinConfig = previousConfig;
                    CurrentSkinDirectory = previousDirectory;
                    CurrentValidation = previousValidation;
                    validation.Errors.Add("No se pudo guardar la selección; se conservó la skin anterior.");
                    return false;
                }
                _assetInfo.Clear();
                _animationCache.Clear();
                Version++;
                NotifySkinChanged();
                try { previous.Dispose(); }
                catch (Exception ex) { Console.WriteLine($"[SkinManager] Previous skin cleanup failed: {ex.Message}"); }
                return true;
            }
            catch (Exception ex)
            {
                validation.Errors.Add($"No se pudo preparar la skin: {ex.Message}");
                candidate?.Dispose();
                return false;
            }
        }

        /// <summary>
        /// Descarga la skin actual y libera recursos.
        /// </summary>
        public void UnloadSkin()
        {
            TryActivateInternal();
        }

        public bool TryActivateInternal(Func<bool>? persistSelection = null)
        {
            var previous = _assets;
            var previousConfig = _currentSkinConfig;
            var previousDirectory = CurrentSkinDirectory;
            var previousValidation = CurrentValidation;
            var candidate = new SkinAssetResolver(_graphicsDevice, null, Core.SettingsService.Instance.HighResolutionSkins);
            _assets = candidate;
            _currentSkinConfig = null;
            CurrentSkinDirectory = string.Empty;
            CurrentValidation = null;
            bool persisted;
            try { persisted = persistSelection?.Invoke() ?? true; }
            catch (Exception ex) { Console.WriteLine($"[SkinManager] Could not persist internal skin: {ex.Message}"); persisted = false; }
            if (!persisted)
            {
                candidate.Dispose();
                _assets = previous;
                _currentSkinConfig = previousConfig;
                CurrentSkinDirectory = previousDirectory;
                CurrentValidation = previousValidation;
                return false;
            }
            _assetInfo.Clear();
            _animationCache.Clear();
            Version++;
            NotifySkinChanged();
            try { previous.Dispose(); }
            catch (Exception ex) { Console.WriteLine($"[SkinManager] Previous skin cleanup failed: {ex.Message}"); }
            return true;
        }

        private void NotifySkinChanged()
        {
            if (SkinChanged == null) return;
            foreach (Action<long> handler in SkinChanged.GetInvocationList())
            {
                try { handler(Version); }
                catch (Exception ex) { Console.WriteLine($"[SkinManager] SkinChanged subscriber failed: {ex.Message}"); }
            }
        }

        public ResolvedSkinAsset ResolveAsset(string fileName)
        {
            var asset = _assets.Resolve(fileName);
            if (asset.Texture != null) _assetInfo[asset.Texture] = asset;
            return asset;
        }

        public bool HasTextureFile(string fileName) => _assets.HasFile(fileName);
        public Texture2D? GetTexture(string fileName) => ResolveAsset(fileName).Texture;
        public Vector2 GetLogicalSize(Texture2D texture) => _assetInfo.TryGetValue(texture, out var asset)
            ? asset.LogicalSize : new Vector2(texture.Width, texture.Height);
        /// <summary>
        /// Obtiene una animación de texturas (frames con sufijo -0, -1, -2...).
        /// Si no existe la animación, intenta retornar una lista con una sola textura estática.
        /// </summary>
        public List<Texture2D>? GetAnimation(string fileName)
        {
            if (string.IsNullOrWhiteSpace(CurrentSkinDirectory))
                return null;

            if (_animationCache.TryGetValue(fileName, out var cachedAnimation))
                return cachedAnimation;

            var frames = new List<Texture2D>();
            int frameIndex = 0;

            while (true)
            {
                string frameName = $"{fileName}-{frameIndex}";
                var tex = GetTexture(frameName);
                if (tex == null)
                    break;

                frames.Add(tex);
                frameIndex++;
            }

            if (frames.Count > 0)
            {
                _animationCache[fileName] = frames;
                return frames;
            }

            // Fallback a un único frame estático
            var singleTex = GetTexture(fileName);
            if (singleTex != null)
            {
                var list = new List<Texture2D> { singleTex };
                _animationCache[fileName] = list;
                return list;
            }

            _animationCache[fileName] = null;
            return null;
        }

        /// <summary>
        /// Resuelve el frame activo de una animación basándose en el tiempo y el framerate de la skin.
        /// </summary>
        public Texture2D? GetAnimationFrame(List<Texture2D>? animation, double timeMs, int? framesPerSecond = null)
        {
            if (animation == null || animation.Count == 0) return null;
            if (animation.Count == 1) return animation[0];

            if (timeMs < 0) return animation[0];

            int fps = framesPerSecond ?? _currentSkinConfig?.AnimationFramerate ?? -1;
            if (fps <= 0)
                fps = 15; // default en osu!mania

            double frameDuration = 1000.0 / fps;
            if (frameDuration <= 0) return animation[0];

            int frameIdx = (int)(timeMs / frameDuration) % animation.Count;
            return animation[Math.Clamp(frameIdx, 0, animation.Count - 1)];
        }

        private string GetDefaultStyleSuffix(int keyCount, int columnIndex)
        {
            if (keyCount % 2 == 1 && columnIndex == keyCount / 2) return "S";
            int distanceToEdge = Math.Min(columnIndex, keyCount - 1 - columnIndex);
            return distanceToEdge % 2 == 0 ? "1" : "2";
        }

        /// <summary>
        /// Obtiene la textura de nota para una columna específica.
        /// </summary>
        public Texture2D? GetNoteTexture(int keyCount, int columnIndex)
        {
            var anim = GetNoteAnimation(keyCount, columnIndex);
            return anim != null && anim.Count > 0 ? anim[0] : null;
        }

        public List<Texture2D>? GetNoteAnimation(int keyCount, int columnIndex)
        {
            return GetNoteAnimationRaw(keyCount, columnIndex);
        }

        private List<Texture2D>? GetNoteAnimationRaw(int keyCount, int columnIndex)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;

            if (config != null && config.NoteImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            
            // Legacy fallback is selected by the column's edge distance, or its
            // special-column role. Per-column filenames come from skin.ini.
            var anim = GetAnimation($"mania-note{style}");
            return anim;
        }

        /// <summary>
        /// Obtiene la textura de head de LN.
        /// </summary>
        public Texture2D? GetLNHeadTexture(int keyCount, int columnIndex)
        {
            var anim = GetLNHeadAnimation(keyCount, columnIndex);
            return anim != null && anim.Count > 0 ? anim[0] : null;
        }

        public List<Texture2D>? GetLNHeadAnimation(int keyCount, int columnIndex)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;

            if (config != null && config.NoteHeadImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            // The legacy default name is derived from the column style.
            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            var anim = GetAnimation($"mania-note{style}H");
            if (anim != null) return anim;

            // A skin may omit an explicit head; stable uses the normal note.
            anim = GetNoteAnimation(keyCount, columnIndex);
            return anim;
        }

        /// <summary>
        /// Obtiene la textura de body de LN.
        /// </summary>
        public Texture2D? GetLNBodyTexture(int keyCount, int columnIndex)
        {
            var anim = GetLNBodyAnimation(keyCount, columnIndex);
            return anim != null && anim.Count > 0 ? anim[0] : null;
        }

        public List<Texture2D>? GetLNBodyAnimation(int keyCount, int columnIndex)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;

            if (config != null && config.NoteBodyImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            // The body uses the same legacy column-style fallback as notes.
            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            return GetAnimation($"mania-note{style}L");
        }

        /// <summary>
        /// Obtiene la textura de tail de LN.
        /// </summary>
        public Texture2D? GetLNTailTexture(int keyCount, int columnIndex)
        {
            var anim = GetLNTailAnimation(keyCount, columnIndex);
            return anim != null && anim.Count > 0 ? anim[0] : null;
        }

        public List<Texture2D>? GetLNTailAnimation(int keyCount, int columnIndex)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;

            if (config != null && config.NoteTailImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            // Stable's default tail name follows the legacy column style.
            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            var anim = GetAnimation($"mania-note{style}T");
            if (anim != null) return anim;

            // 3. Fallback to LN Head
            anim = GetLNHeadAnimation(keyCount, columnIndex);
            if (anim != null) return anim;

            // 4. Default game asset fallback
            return GetAnimation("mania-note-hold-tail");
        }

        /// <summary>
        /// Obtiene la textura de key (receptor).
        /// </summary>
        public Texture2D? GetKeyTexture(int keyCount, int columnIndex, bool pressed = false)
        {
            var anim = GetKeyAnimation(keyCount, columnIndex, pressed);
            return anim != null && anim.Count > 0 ? anim[0] : null;
        }

        public List<Texture2D>? GetKeyAnimation(int keyCount, int columnIndex, bool pressed = false)
        {
            return GetKeyAnimationRaw(keyCount, columnIndex, pressed) ?? (pressed ? GetKeyAnimationRaw(keyCount, columnIndex, false) : null);
        }

        private List<Texture2D>? GetKeyAnimationRaw(int keyCount, int columnIndex, bool pressed)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;

            if (config != null)
            {
                var imageDict = pressed ? config.KeyPressedImages : config.KeyImages;
                if (imageDict.TryGetValue(columnIndex, out string? customPath))
                    return GetAnimation(customPath);
            }

            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            string suffix = pressed ? "D" : "";

            // Buscar en orden de prioridad estándar de osu! (ej. mania-key1 o mania-key1D)
            var anim = GetAnimation($"mania-key{style}{suffix}");
            if (anim != null) return anim;

            anim = GetAnimation($"mania-key-{style}{suffix}");
            if (anim != null) return anim;

            return GetAnimation($"mania-key{style}{suffix}");
        }

        /// <summary>
        /// Obtiene la animación de lighting (haz de luz).
        /// </summary>
        public List<Texture2D>? GetLightingAnimation(int keyCount, int columnIndex)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;
            if (config != null && !string.IsNullOrWhiteSpace(config.LightingN))
            {
                if (config.LightingN.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                    return _blankTexture != null ? new List<Texture2D> { _blankTexture } : null;
                return GetAnimation(config.LightingN);
            }

            string fileName = $"lightingN-{columnIndex}";
            return GetAnimation(fileName) ?? GetAnimation("lightingN");
        }

        public List<Texture2D>? GetLightingLAnimation(int keyCount, int columnIndex)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;
            if (config != null && !string.IsNullOrWhiteSpace(config.LightingL))
            {
                if (config.LightingL.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                    return _blankTexture != null ? new List<Texture2D> { _blankTexture } : null;
                return GetAnimation(config.LightingL);
            }

            string fileName = $"lightingL-{columnIndex}";
            return GetAnimation(fileName) ?? GetAnimation("lightingL");
        }

        public Texture2D? GetLightingLTexture(int keyCount, int columnIndex)
        {
            var anim = GetLightingLAnimation(keyCount, columnIndex);
            return anim != null && anim.Count > 0 ? anim[0] : null;
        }

        public Texture2D? GetLightingTexture(int keyCount, int columnIndex)
        {
            var anim = GetLightingAnimation(keyCount, columnIndex);
            return anim != null && anim.Count > 0 ? anim[0] : null;
        }

        /// <summary>
        /// Obtiene la textura del stage.
        /// </summary>
        public Texture2D? GetStageTexture(int keyCount, string? part = null)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;

            if (part != null)
            {
                string? customName = null;
                if (config != null)
                {
                    if (part == "left") customName = config.StageLeft;
                    else if (part == "right") customName = config.StageRight;
                    else if (part == "bottom") customName = config.StageBottom;
                    else if (part == "hint") customName = config.StageHint;
                    else if (part == "light") customName = config.StageLight;
                }

                if (customName != null)
                {
                    if (customName.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                        return _blankTexture;
                    return GetTexture(customName);
                }

                string fileName = $"mania-stage-{part}";
                return GetTexture(fileName);
            }

            return GetTexture("mania-stage-bottom");
        }

        public List<Texture2D>? GetStageAnimation(int keyCount, string part)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;
            string? customName = part switch
            {
                "bottom" => config.StageBottom,
                "light" => config.StageLight,
                _ => null
            };
            return GetAnimation(customName ?? $"mania-stage-{part}");
        }

        public int GetLightFramePerSecond(int keyCount) => KeyConfig(keyCount)?.LightFramePerSecond ?? 60;
        // v2.4 scales mania combo and hitbursts in the 768-high virtual space
        // instead of the 480-high stage space (480 / 768 = 0.625).
        public float GetLegacyHudScale(int viewportHeight) => viewportHeight / 480f
            * (_currentSkinConfig?.IsVersionAtLeast(2.4) == true ? 0.625f : 1f);

        public List<Texture2D>? GetHitExplosionAnimation(int keyCount, int columnIndex)
        {
            return GetLightingAnimation(keyCount, columnIndex);
        }

        public Texture2D? GetHitExplosionTexture()
        {
            return GetTexture("hitlighting") ?? GetStageTexture(4, "hint");
        }

        /// <summary>
        /// Traduce los juicios y obtiene su textura mapeada correctamente a los sprites de osu!.
        /// </summary>
        public Texture2D? GetJudgementTexture(int keyCount, string judgement)
        {
            var frames = GetJudgementAnimation(keyCount, judgement);
            return frames is { Count: > 0 } ? frames[0] : null;
        }

        public List<Texture2D>? GetJudgementAnimation(int keyCount, string judgement)
        {
            var config = GetManiaConfig(keyCount);
            if (config == null) return null;
            string suffix = judgement switch
            {
                "Perfect" => "300g", "Great" => "300", "Good" => "200",
                "Ok" => "100", "Meh" => "50", "Miss" => "0", _ => ""
            };
            if (suffix.Length == 0) return null;
            if (config.HitImages.TryGetValue("Hit" + suffix, out string? custom))
                return GetAnimation(custom);
            return GetAnimation("mania-hit" + suffix) ?? GetAnimation("hit" + suffix);
        }

        /// <summary>
        /// Propiedades del layout expuestas desde la skin activa.
        /// </summary>
        private ManiaKeyConfig? KeyConfig(int keyCount) => GetManiaConfig(keyCount);
        public float? GetColumnWidth(int keyCount, int columnIndex)
        {
            var widths = KeyConfig(keyCount)?.ColumnWidths;
            return widths != null && columnIndex >= 0 && columnIndex < widths.Length ? widths[columnIndex] : null;
        }
        public float[]? GetColumnSpacing(int keyCount) => KeyConfig(keyCount)?.ColumnSpacing;
        public bool[]? GetColumnLines(int keyCount) => KeyConfig(keyCount)?.ColumnLines;
        public float? GetColumnStart(int keyCount) => KeyConfig(keyCount)?.ColumnStart;
        public float? GetWidthForNoteHeightScale(int keyCount) => KeyConfig(keyCount)?.WidthForNoteHeightScale;
        public float GetNoteHeightReferenceWidth(int keyCount)
        {
            var config = KeyConfig(keyCount);
            if (config?.WidthForNoteHeightScale is float referenceWidth && referenceWidth > 0f && float.IsFinite(referenceWidth))
                return referenceWidth;
            float minimum = float.PositiveInfinity;
            for (int column = 0; column < keyCount; column++)
            {
                float width = config?.ColumnWidths != null && column < config.ColumnWidths.Length
                    && float.IsFinite(config.ColumnWidths[column]) && config.ColumnWidths[column] > 0
                    ? config.ColumnWidths[column] : 30f;
                minimum = Math.Min(minimum, width);
            }
            return float.IsPositiveInfinity(minimum) ? 30f : minimum;
        }
        public float[]? GetColumnLineWidths(int keyCount) => KeyConfig(keyCount)?.ColumnLineWidths;
        public int? GetHitPosition(int keyCount) => KeyConfig(keyCount)?.HitPosition;
        public int? GetLightPosition(int keyCount) => KeyConfig(keyCount)?.LightPosition;
        public bool IsLightingEnabled(int keyCount) => KeyConfig(keyCount)?.ColumnLighting ?? true;
        public bool HasJudgementLine(int keyCount) => KeyConfig(keyCount)?.JudgementLine ?? true;
        public bool IsUpsideDown(int keyCount) => KeyConfig(keyCount)?.UpsideDown ?? false;
        public bool AreKeysUnderNotes(int keyCount) => KeyConfig(keyCount)?.KeysUnderNotes ?? false;
        public int? GetScorePosition(int keyCount) => KeyConfig(keyCount)?.ScorePosition;
        public int? GetComboPosition(int keyCount) => KeyConfig(keyCount)?.ComboPosition;
        public (byte R, byte G, byte B, byte A)? GetColourBarline(int keyCount) => KeyConfig(keyCount)?.ColourBarline;
        public (byte R, byte G, byte B, byte A)? GetColourJudgementLine(int keyCount) => KeyConfig(keyCount)?.ColourJudgementLine;
        public (byte R, byte G, byte B, byte A)? GetColourColumnLine(int keyCount) => KeyConfig(keyCount)?.ColourColumnLine;
        public (byte R, byte G, byte B, byte A)? GetColourBreak(int keyCount) => KeyConfig(keyCount)?.ColourBreak;
        public (byte R, byte G, byte B, byte A)? GetGlobalColourHold(int keyCount) => KeyConfig(keyCount)?.ColourHold;
        public string GetComboPrefix() => _currentSkinConfig?.ComboPrefix ?? "score";
        public float GetComboOverlap() => _currentSkinConfig?.ComboOverlap ?? 0f;
        public float[]? GetLightingNWidths(int keyCount) => _currentSkinConfig?.IsVersionAtLeast(2.5) == true
            ? KeyConfig(keyCount)?.LightingNWidths : null;
        public float[]? GetLightingLWidths(int keyCount) => _currentSkinConfig?.IsVersionAtLeast(2.5) == true
            ? KeyConfig(keyCount)?.LightingLWidths : null;
        public (byte R, byte G, byte B, byte A)? GetColourLight(int keyCount, int columnIndex)
        {
            var config = KeyConfig(keyCount);
            return config != null && config.ColourLights.TryGetValue(columnIndex, out var color) ? color : null;
        }
        public (byte R, byte G, byte B, byte A)? GetColourHold(int keyCount, int columnIndex)
        {
            var config = KeyConfig(keyCount);
            if (config == null) return null;
            return config.ColourHolds.TryGetValue(columnIndex, out var color) ? color : config.ColourHold;
        }
        public (byte R, byte G, byte B, byte A)? GetColumnColorMap(int keyCount, int columnIndex)
        {
            var config = KeyConfig(keyCount);
            return config != null && config.ColumnColorsMap.TryGetValue(columnIndex, out var color) ? color : null;
        }
        public int GetNoteBodyStyle(int keyCount, int columnIndex)
        {
            if (_currentSkinConfig?.IsVersionAtLeast(2.5) == false) return 1;
            var config = KeyConfig(keyCount);
            return config != null && config.NoteBodyStyles.TryGetValue(columnIndex, out int style) ? style : config?.NoteBodyStyle ?? 1;
        }

        public bool FlipNote(int keyCount, int columnIndex, string part, bool downScroll)
        {
            var config = KeyConfig(keyCount);
            bool versionAllowsOverrides = _currentSkinConfig?.IsVersionAtLeast(2.5) == true;
            bool flip = true;
            if (versionAllowsOverrides && config != null)
            {
                flip = config.NoteFlips.TryGetValue($"{columnIndex}{part}", out bool partFlip) ? partFlip
                    : config.NoteFlips.TryGetValue(columnIndex.ToString(), out bool columnFlip) ? columnFlip
                    : config.NoteFlipWhenUpsideDown ?? true;
            }

            // The normal note/head/body face the playfield in upscroll. A tail
            // faces the opposite direction. Version 2.5 adds per-skin overrides.
            return part.Equals("T", StringComparison.OrdinalIgnoreCase) ? downScroll && flip : !downScroll && flip;
        }

        public bool FlipKey(int keyCount, int columnIndex, bool pressed, bool downScroll)
        {
            if (downScroll) return false;
            if (_currentSkinConfig?.IsVersionAtLeast(2.5) != true) return true;
            var config = KeyConfig(keyCount);
            if (config == null) return true;
            return config.KeyFlips.TryGetValue($"{columnIndex}{(pressed ? "D" : "")}", out bool stateFlip) ? stateFlip
                : config.KeyFlips.TryGetValue(columnIndex.ToString(), out bool columnFlip) ? columnFlip
                : config.KeyFlipWhenUpsideDown ?? true;
        }
        public Texture2D? GetScorebarBg() => GetTexture("scorebar-bg");
        public Texture2D? GetScorebarColour() => GetTexture("scorebar-colour");
        public Texture2D? GetCursorTexture() => GetTexture("cursor");

        /// <summary>
        /// Dibuja un número en pantalla usando texturas del skin con un prefijo dado (ej: "default" para combo).
        /// </summary>
        public bool DrawNumber(SpriteBatch spriteBatch, string text, Vector2 position, string prefix, float scale, float alpha,
            bool centered = true, float spacing = -2f, Color? tint = null)
        {
            var textures = new List<Texture2D?>();
            float maxOriginalHeight = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string textureName;
                if (c == '%') textureName = $"{prefix}-percent";
                else if (c == '.') textureName = $"{prefix}-dot";
                else if (c == ',') textureName = $"{prefix}-comma";
                else textureName = $"{prefix}-{c}";

                var tex = GetTexture(textureName) ?? GetTexture(textureName + ".png");
                if (tex == null && prefix == "default")
                {
                    textureName = c == '%' ? "score-percent" : (c == '.' ? "score-dot" : $"score-{c}");
                    tex = GetTexture(textureName) ?? GetTexture(textureName + ".png");
                }

                textures.Add(tex);
                if (tex != null)
                {
                    float height = GetLogicalSize(tex).Y;
                    if (height > maxOriginalHeight)
                    {
                        maxOriginalHeight = height;
                    }
                }
            }

            bool hasValidTexture = false;
            foreach (var t in textures)
            {
                if (t != null)
                {
                    hasValidTexture = true;
                    break;
                }
            }
            if (!hasValidTexture)
                return false;

            float totalWidth = 0;
            var widths = new List<float>();
            foreach (var tex in textures)
            {
                if (tex != null)
                {
                    Vector2 logicalSize = GetLogicalSize(tex);
                    float logicalWidth = logicalSize.X;
                    float logicalHeight = logicalSize.Y;
                    float w = logicalWidth * (maxOriginalHeight / logicalHeight) * scale;
                    widths.Add(w);
                    totalWidth += w + spacing;
                }
                else
                {
                    float w = (maxOriginalHeight > 0 ? maxOriginalHeight : 40f) * 0.6f * scale;
                    widths.Add(w);
                    totalWidth += w + spacing;
                }
            }
            totalWidth -= spacing;

            float currentX = centered ? position.X - totalWidth / 2f : position.X;
            float uniformH = maxOriginalHeight * scale;

            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                float w = widths[i];
                if (tex != null)
                {
                    spriteBatch.Draw(tex, new Rectangle((int)currentX, (int)(position.Y - uniformH / 2f), (int)w, (int)uniformH), (tint ?? Color.White) * alpha);
                }
                currentX += w + spacing;
            }

            return true;
        }

        public void Dispose()
        {
            _assets.Dispose();
            _blankTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
