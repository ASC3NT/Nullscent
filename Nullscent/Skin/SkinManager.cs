#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        private ManiaSkinDefinition _currentDefinition = null!;
        private ManiaSkinDefinition _internalDefinition = null!;
        private readonly string _internalSkinDirectory;
        private readonly SkinAssetResolver _internalAssets;
        private SkinAssetResolver _assets;
        private readonly Dictionary<Texture2D, ResolvedSkinAsset> _assetInfo = new();
        private readonly Dictionary<string, List<Texture2D>?> _animationCache = new();
        private readonly Dictionary<int, ResolvedManiaKeymode> _resolvedKeymodes = new();
        private readonly HashSet<string> _reportedDiagnostics = new(StringComparer.OrdinalIgnoreCase);
        private Texture2D? _blankTexture;

        public ManiaSkinDefinition CurrentDefinition => _currentDefinition;
        public string CurrentSkinDirectory { get; private set; } = string.Empty;
        public long Version { get; private set; }
        public event Action<long>? SkinChanged;
        public SkinValidation? CurrentValidation { get; private set; }

        public SkinManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _internalSkinDirectory = Path.Combine(AppContext.BaseDirectory, "Skins", "Bars");
            try { _internalDefinition = NativeSkinAdapter.Load(_internalSkinDirectory); }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkinManager] Built-in manifest unavailable; emergency visuals enabled: {ex.Message}");
                _internalDefinition = CreateEmergencyDefinition();
            }
            _currentDefinition = _internalDefinition;
            _internalAssets = new SkinAssetResolver(graphicsDevice,
                System.IO.Directory.Exists(_internalSkinDirectory) ? _internalSkinDirectory : null,
                Core.SettingsService.Instance.HighResolutionSkins);
            _assets = new SkinAssetResolver(graphicsDevice, null, Core.SettingsService.Instance.HighResolutionSkins);
            _blankTexture = new Texture2D(graphicsDevice, 1, 1);
            _blankTexture.SetData(new[] { Color.Transparent });
            TryActivateInternal();
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
                ManiaSkinDefinition definition = validation.Definition ?? SkinSourceDecoder.Load(validation.Directory, out _);
                candidate = new SkinAssetResolver(_graphicsDevice, validation.Directory, Core.SettingsService.Instance.HighResolutionSkins);
                var names = new HashSet<string>(SkinCatalog.ReferencedImages(definition), StringComparer.OrdinalIgnoreCase)
                { "cursor", "mania-note1", "mania-note2", "mania-key1", "mania-key2" };
                foreach (string name in names)
                    candidate.Resolve(name);
                if (candidate.Warnings.Count > 0)
                {
                    validation.Warnings.AddRange(candidate.Warnings);
                    validation.Status = SkinValidity.Partial;
                }
                var previous = _assets;
                var previousDefinition = _currentDefinition;
                var previousDirectory = CurrentSkinDirectory;
                var previousValidation = CurrentValidation;
                _assets = candidate;
                candidate = null;
                _currentDefinition = definition;
                CurrentSkinDirectory = validation.Directory;
                CurrentValidation = validation;
                bool persisted;
                try { persisted = persistSelection?.Invoke() ?? true; }
                catch (Exception ex) { validation.Errors.Add(ex.Message); persisted = false; }
                if (!persisted)
                {
                    _assets.Dispose();
                    _assets = previous;
                    _currentDefinition = previousDefinition;
                    CurrentSkinDirectory = previousDirectory;
                    CurrentValidation = previousValidation;
                    validation.Errors.Add("No se pudo guardar la selección; se conservó la skin anterior.");
                    return false;
                }
                _assetInfo.Clear();
                _animationCache.Clear();
                _resolvedKeymodes.Clear();
                _reportedDiagnostics.Clear();
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
            var previousDefinition = _currentDefinition;
            var previousDirectory = CurrentSkinDirectory;
            var previousValidation = CurrentValidation;
            string? root = System.IO.Directory.Exists(_internalSkinDirectory) ? _internalSkinDirectory : null;
            var candidate = new SkinAssetResolver(_graphicsDevice, root, Core.SettingsService.Instance.HighResolutionSkins);
            _assets = candidate;
            _currentDefinition = _internalDefinition;
            CurrentSkinDirectory = string.Empty;
            CurrentValidation = null;
            bool persisted;
            try { persisted = persistSelection?.Invoke() ?? true; }
            catch (Exception ex) { Console.WriteLine($"[SkinManager] Could not persist internal skin: {ex.Message}"); persisted = false; }
            if (!persisted)
            {
                candidate.Dispose();
                _assets = previous;
                _currentDefinition = previousDefinition;
                CurrentSkinDirectory = previousDirectory;
                CurrentValidation = previousValidation;
                return false;
            }
            _assetInfo.Clear();
            _animationCache.Clear();
            _resolvedKeymodes.Clear();
            _reportedDiagnostics.Clear();
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
            if (asset.Texture == null && asset.Origin != SkinAssetOrigin.IntentionalBlank)
            {
                ResolvedSkinAsset fallback = _internalAssets.Resolve(fileName);
                if (fallback.Texture != null)
                    asset = new ResolvedSkinAsset
                    {
                        Texture = fallback.Texture,
                        LogicalSize = fallback.LogicalSize,
                        SourcePixelSize = fallback.SourcePixelSize,
                        TexturePixelSize = fallback.TexturePixelSize,
                        Origin = SkinAssetOrigin.Internal,
                        IsHighResolution = fallback.IsHighResolution
                    };
            }
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
            if (_animationCache.TryGetValue(fileName, out var cachedAnimation))
                return cachedAnimation;

            List<Texture2D>? frames = LoadAnimationFrom(_assets, fileName);
            if (frames == null && !ReferenceEquals(_assets, _internalAssets))
                frames = LoadAnimationFrom(_internalAssets, fileName);

            _animationCache[fileName] = frames;
            return frames;
        }

        private List<Texture2D>? LoadAnimationFrom(SkinAssetResolver provider, string fileName)
        {
            var frames = new List<Texture2D>();
            for (int i = 0; i < 1000; i++)
            {
                ResolvedSkinAsset frame = provider.Resolve($"{fileName}-{i}");
                if (frame.Texture == null) break;
                RememberResolvedAsset(provider, frame);
                frames.Add(frame.Texture);
            }
            if (frames.Count > 0) return frames;

            ResolvedSkinAsset single = provider.Resolve(fileName);
            if (single.Texture == null) return null;
            RememberResolvedAsset(provider, single);
            return new List<Texture2D> { single.Texture };
        }

        private void RememberResolvedAsset(SkinAssetResolver provider, ResolvedSkinAsset asset)
        {
            if (asset.Texture == null) return;
            if (ReferenceEquals(provider, _internalAssets) && asset.Origin == SkinAssetOrigin.Skin)
            {
                asset = new ResolvedSkinAsset
                {
                    Texture = asset.Texture,
                    LogicalSize = asset.LogicalSize,
                    SourcePixelSize = asset.SourcePixelSize,
                    TexturePixelSize = asset.TexturePixelSize,
                    Origin = SkinAssetOrigin.Internal,
                    IsHighResolution = asset.IsHighResolution
                };
            }
            _assetInfo[asset.Texture] = asset;
        }

        public ManiaKeymodeDefinition GetManiaProfile(int keyCount)
        {
            if (_currentDefinition.Keymodes.TryGetValue(keyCount, out var active)) return active;
            if (_internalDefinition.Keymodes.TryGetValue(keyCount, out var fallback)) return fallback;
            return CreateFallbackMode(keyCount);
        }

        public ResolvedManiaKeymode GetResolvedKeymode(int keyCount)
        {
            if (_resolvedKeymodes.TryGetValue(keyCount, out var cached)) return cached;
            ManiaKeymodeDefinition profile;
            bool internalOnly = !_currentDefinition.Keymodes.TryGetValue(keyCount, out profile!);
            if (internalOnly)
                profile = _internalDefinition.Keymodes.TryGetValue(keyCount, out var fallback)
                    ? fallback : CreateFallbackMode(keyCount);
            ManiaSkinDefinition sourceDefinition = internalOnly ? _internalDefinition : _currentDefinition;
            int generalAnimationFramerate = sourceDefinition.AnimationFramerate ?? -1;

            var lanes = new List<ResolvedManiaLane>(profile.Lanes.Count);
            for (int i = 0; i < profile.Lanes.Count; i++)
            {
                ManiaLaneDefinition lane = profile.Lanes[i];
                string suffix = GetDefaultStyleSuffix(keyCount, i);
                bool legacy = !internalOnly && _currentDefinition.Provider == SkinProviderKind.OsuLegacy;
                string? noteName = First(lane.NoteAsset, profile.NoteStyle.Asset,
                    legacy ? $"mania-note{suffix}" : null);
                var note = ResolveAnimation(noteName, "bar-note", profile.NoteStyle.FramesPerSecond ?? 60, internalOnly);

                string? receptorName = First(lane.ReceptorAsset, profile.ReceptorStyle.Asset,
                    legacy ? $"mania-key{suffix}" : null);
                var receptor = ResolveAnimation(receptorName, "bar-key", profile.ReceptorStyle.FramesPerSecond ?? generalAnimationFramerate, internalOnly);
                string? pressedName = First(lane.PressedReceptorAsset, profile.ReceptorStyle.PressedAsset,
                    legacy ? $"mania-key{suffix}D" : null);
                var pressed = ResolveAnimation(pressedName, "bar-key-pressed", profile.ReceptorStyle.FramesPerSecond ?? generalAnimationFramerate, internalOnly)
                    ?? receptor;

                string? headName = First(lane.LongHeadAsset, profile.LongHeadStyle.Asset,
                    legacy ? $"mania-note{suffix}H" : null);
                var head = ResolveAnimation(headName, noteName, profile.LongHeadStyle.FramesPerSecond ?? 60, internalOnly)
                    ?? ResolveAnimation(headName, "bar-hold-head", profile.LongHeadStyle.FramesPerSecond ?? 60, internalOnly) ?? note;
                string? bodyName = First(lane.LongBodyAsset, profile.LongBodyStyle.Asset,
                    legacy ? $"mania-note{suffix}L" : null);
                var body = ResolveAnimation(bodyName, "bar-hold-body", profile.LongBodyStyle.FramesPerSecond ?? 30, internalOnly);
                string? tailName = First(lane.LongTailAsset, profile.LongTailStyle.Asset,
                    legacy ? $"mania-note{suffix}T" : null);
                var tail = ResolveAnimation(tailName, headName, profile.LongTailStyle.FramesPerSecond ?? 60, internalOnly)
                    ?? ResolveAnimation(tailName, "bar-hold-tail", profile.LongTailStyle.FramesPerSecond ?? 60, internalOnly) ?? head;

                string lightSuffix = First(profile.LightingNoteAsset, legacy ? $"lightingN-{i}" : null) ?? "bar-light-note";
                string holdLightSuffix = First(profile.LightingHoldAsset, legacy ? $"lightingL-{i}" : null) ?? "bar-light-hold";
                // LightFramePerSecond belongs to StageLight. Mania note and hold
                // lighting use their own fixed 60 FPS path, separate from General.
                var noteLight = ResolveAnimation(lightSuffix, "bar-light-note", 60, internalOnly);
                var holdLight = ResolveAnimation(holdLightSuffix, "bar-light-hold", 60, internalOnly);
                lanes.Add(new ResolvedManiaLane(lane, note, receptor, pressed, head, body, tail, noteLight, holdLight));
            }

            var stage = new Dictionary<string, ResolvedSkinAnimation>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in new[] { "left", "right", "bottom", "hint", "light" })
            {
                profile.StageAssets.TryGetValue(part, out string? configured);
                string conventional = $"mania-stage-{part}";
                string? name = First(configured, conventional);
                string? emergency = part == "hint" ? "bar-stage-hint" : null;
                int frameRate = part.Equals("light", StringComparison.OrdinalIgnoreCase)
                    ? profile.LightFramesPerSecond : generalAnimationFramerate;
                ResolvedSkinAnimation? animation = ResolveAnimation(name, emergency, frameRate, internalOnly);
                if (animation != null) stage[part] = animation;
            }

            var judgements = new Dictionary<string, ResolvedSkinAnimation>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in new[] { "Hit0", "Hit50", "Hit100", "Hit200", "Hit300", "Hit300g" })
            {
                string? configured = profile.JudgementAssets.TryGetValue(key, out string? hit) ? hit : null;
                string suffix = key[3..];
                var animation = ResolveAnimation(First(configured, $"mania-hit{suffix}", $"hit{suffix}"), null,
                    generalAnimationFramerate, internalOnly);
                if (animation != null) judgements[suffix] = animation;
            }

            var result = new ResolvedManiaKeymode(profile, lanes, stage, judgements);
            _resolvedKeymodes[keyCount] = result;
            return result;
        }

        public SkinSpriteMetrics GetSpriteMetrics(int keyCount, int columnIndex, string part)
        {
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            ManiaLaneDefinition lane = mode.Lanes[Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1)].Definition;
            SpriteStyleDefinition style = part switch
            {
                "receptor" => mode.Definition.ReceptorStyle,
                "head" => mode.Definition.LongHeadStyle,
                "body" => mode.Definition.LongBodyStyle,
                "tail" => mode.Definition.LongTailStyle,
                _ => mode.Definition.NoteStyle
            };
            float widthRatio = part switch
            {
                "note" => lane.NoteWidthRatio ?? style.LaneWidthRatio,
                "receptor" => lane.ReceptorWidthRatio ?? style.LaneWidthRatio,
                "body" => lane.HoldBodyWidthRatio ?? style.LaneWidthRatio,
                _ => style.LaneWidthRatio
            };
            return new SkinSpriteMetrics(widthRatio, style.HeightMode, style.HeightValue,
                mode.Definition.NoteHeightReferenceWidth ?? 0f, style.OffsetXRatio, style.OffsetY, style.FramesPerSecond);
        }

        private ResolvedSkinAnimation? ResolveAnimation(string? name, string? fallbackName, int fps, bool internalOnly)
        {
            if (string.Equals(name, "_blank", StringComparison.OrdinalIgnoreCase))
                return _blankTexture == null ? null : new ResolvedSkinAnimation(new[] { _blankTexture }, fps);

            if (!string.IsNullOrWhiteSpace(name))
            {
                List<Texture2D>? frames = internalOnly
                    ? LoadAnimationFrom(_internalAssets, name)
                    : GetAnimation(name);
                if (frames != null) return new ResolvedSkinAnimation(frames, fps);
            }

            if (string.IsNullOrWhiteSpace(fallbackName) || fallbackName.Equals(name, StringComparison.OrdinalIgnoreCase)) return null;
            List<Texture2D>? fallback = internalOnly
                ? LoadAnimationFrom(_internalAssets, fallbackName)
                : GetAnimation(fallbackName);
            if (fallback == null) return null;
            string key = $"{_currentDefinition.Name}:{name ?? "<missing>"}->{fallbackName}";
            string provider = internalOnly || !_assetInfo.TryGetValue(fallback[0], out ResolvedSkinAsset? info)
                || info.Origin != SkinAssetOrigin.Skin ? "fallback interno" : "skin activa";
            if (_reportedDiagnostics.Add(key)) Console.WriteLine($"[Skin] Fallback visual usado: {name ?? "(sin asset)"} -> {provider}/{fallbackName}.");
            return new ResolvedSkinAnimation(fallback, fps);
        }

        private ResolvedSkinAsset ResolveFromInternal(string name)
        {
            ResolvedSkinAsset asset = _internalAssets.Resolve(name);
            if (asset.Texture != null)
            {
                asset = new ResolvedSkinAsset
                {
                    Texture = asset.Texture, LogicalSize = asset.LogicalSize,
                    SourcePixelSize = asset.SourcePixelSize, TexturePixelSize = asset.TexturePixelSize,
                    Origin = SkinAssetOrigin.Internal, IsHighResolution = asset.IsHighResolution
                };
                _assetInfo[asset.Texture] = asset;
            }
            return asset;
        }

        private ManiaKeymodeDefinition CreateFallbackMode(int keyCount)
        {
            if (keyCount < 1 || keyCount > 18) throw new ArgumentOutOfRangeException(nameof(keyCount));
            var baseMode = _internalDefinition.Keymodes.TryGetValue(keyCount <= 5 ? 4 : 7, out var exact)
                ? exact : _internalDefinition.Keymodes.Values.FirstOrDefault();
            float baseWidth = baseMode?.Lanes.Count > 0
                ? baseMode.Lanes.Sum(l => l.Width + l.GapAfter) / baseMode.Lanes.Count : 48f;
            var lanes = new List<ManiaLaneDefinition>(keyCount);
            for (int i = 0; i < keyCount; i++)
            {
                ManiaLaneDefinition? source = baseMode?.Lanes.Count > 0 ? baseMode.Lanes[i % baseMode.Lanes.Count] : null;
                lanes.Add(new ManiaLaneDefinition
                {
                    Width = baseWidth * Math.Clamp((baseMode?.KeyCount ?? 4) / (float)keyCount, .65f, 1.35f),
                    GapAfter = 2f,
                    LaneColor = source?.LaneColor,
                    LightColor = source?.LightColor,
                    HoldColor = source?.HoldColor
                });
            }
            return new ManiaKeymodeDefinition
            {
                KeyCount = keyCount, Placement = "center", Lanes = lanes,
                DefaultHitPositionRatio = .84f, JudgementLine = true,
                ColumnLineWidths = new float[keyCount + 1], ColumnLines = new bool[keyCount + 1],
                NoteStyle = baseMode?.NoteStyle ?? new SpriteStyleDefinition { Asset = "bar-note", LaneWidthRatio = .94f },
                ReceptorStyle = baseMode?.ReceptorStyle ?? new SpriteStyleDefinition { Asset = "bar-key", PressedAsset = "bar-key-pressed", LaneWidthRatio = .9f, HeightMode = SpriteHeightMode.LaneWidthFraction, HeightValue = .17f },
                LongHeadStyle = baseMode?.LongHeadStyle ?? new SpriteStyleDefinition { Asset = "bar-hold-head", LaneWidthRatio = .94f },
                LongBodyStyle = baseMode?.LongBodyStyle ?? new SpriteStyleDefinition { Asset = "bar-hold-body", LaneWidthRatio = .88f },
                LongTailStyle = baseMode?.LongTailStyle ?? new SpriteStyleDefinition { Asset = "bar-hold-tail", LaneWidthRatio = .94f },
                BodyMode = baseMode?.BodyMode ?? NoteBodyRenderMode.Stretch,
                StageAssets = baseMode?.StageAssets ?? new Dictionary<string, string?> { ["hint"] = "bar-stage-hint" },
                LightFramesPerSecond = baseMode?.LightFramesPerSecond ?? 60
            };
        }

        private static string? First(params string?[] names) => names.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        private static ManiaSkinDefinition CreateEmergencyDefinition()
        {
            ManiaKeymodeDefinition Build(int keys)
            {
                var lanes = Enumerable.Range(0, keys).Select(_ => new ManiaLaneDefinition { Width = 48, GapAfter = 2 }).ToList();
                return new ManiaKeymodeDefinition
                {
                    KeyCount = keys, Lanes = lanes, ColumnLineWidths = new float[keys + 1], ColumnLines = new bool[keys + 1],
                    NoteStyle = new SpriteStyleDefinition { LaneWidthRatio = .9f },
                    ReceptorStyle = new SpriteStyleDefinition { LaneWidthRatio = .9f, HeightMode = SpriteHeightMode.LaneWidthFraction, HeightValue = .2f },
                    LongHeadStyle = new SpriteStyleDefinition { LaneWidthRatio = .9f },
                    LongBodyStyle = new SpriteStyleDefinition { LaneWidthRatio = .85f },
                    LongTailStyle = new SpriteStyleDefinition { LaneWidthRatio = .9f }
                };
            }
            return new ManiaSkinDefinition
            {
                Id = "internal:emergency", Name = "Nullscent Emergency",
                Provider = SkinProviderKind.Emergency,
                Keymodes = new Dictionary<int, ManiaKeymodeDefinition> { [4] = Build(4), [7] = Build(7) }
            };
        }

        /// <summary>
        /// Resuelve el frame activo de una animación basándose en el tiempo y el framerate de la skin.
        /// </summary>
        public Texture2D? GetAnimationFrame(List<Texture2D>? animation, double timeMs, int? framesPerSecond = null)
        {
            if (animation == null || animation.Count == 0) return null;
            if (animation.Count == 1) return animation[0];

            if (timeMs < 0) return animation[0];

            int fps = ResolvedSkinAnimation.ResolveFrameRate(
                framesPerSecond ?? _currentDefinition.AnimationFramerate ?? -1, animation.Count);
            return animation[Math.Clamp((int)(timeMs * fps / 1000d) % animation.Count, 0, animation.Count - 1)];
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
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            int index = Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1);
            return mode.Lanes[index].Note?.Textures;
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
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            int index = Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1);
            return mode.Lanes[index].LongHead?.Textures;
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
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            int index = Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1);
            return mode.Lanes[index].LongBody?.Textures;
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
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            int index = Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1);
            return mode.Lanes[index].LongTail?.Textures;
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
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            int index = Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1);
            return (pressed ? mode.Lanes[index].PressedReceptor : mode.Lanes[index].Receptor)?.Textures;
        }

        /// <summary>
        /// Obtiene la animación de lighting (haz de luz).
        /// </summary>
        public List<Texture2D>? GetLightingAnimation(int keyCount, int columnIndex)
        {
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            int index = Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1);
            return mode.Lanes[index].NoteLighting?.Textures;
        }

        public List<Texture2D>? GetLightingLAnimation(int keyCount, int columnIndex)
        {
            ResolvedManiaKeymode mode = GetResolvedKeymode(keyCount);
            int index = Math.Clamp(columnIndex, 0, mode.Lanes.Count - 1);
            return mode.Lanes[index].HoldLighting?.Textures;
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
            string key = part ?? "bottom";
            return GetResolvedKeymode(keyCount).StageAssets.TryGetValue(key, out ResolvedSkinAnimation? animation)
                ? animation.FirstFrame : null;
        }

        public List<Texture2D>? GetStageAnimation(int keyCount, string part)
        {
            return GetResolvedKeymode(keyCount).StageAssets.TryGetValue(part, out ResolvedSkinAnimation? animation)
                ? animation.Textures : null;
        }

        public int GetLightFramePerSecond(int keyCount) => GetManiaProfile(keyCount).LightFramesPerSecond;
        // v2.4 scales mania combo and hitbursts in the 768-high virtual space
        // instead of the 480-high stage space (480 / 768 = 0.625).
        public float GetLegacyHudScale(float layoutScale) => layoutScale * _currentDefinition.LegacyHudScale;

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
            string suffix = judgement switch
            {
                "Perfect" => "300g", "Great" => "300", "Good" => "200",
                "Ok" => "100", "Meh" => "50", "Miss" => "0", _ => ""
            };
            if (suffix.Length == 0) return null;
            return GetResolvedKeymode(keyCount).Judgements.TryGetValue(suffix, out ResolvedSkinAnimation? animation)
                ? animation.Textures : null;
        }

        public Texture2D? GetJudgementFrame(int keyCount, string judgement, double elapsedMs)
        {
            string suffix = judgement switch
            {
                "Perfect" => "300g", "Great" => "300", "Good" => "200",
                "Ok" => "100", "Meh" => "50", "Miss" => "0", _ => ""
            };
            return suffix.Length > 0 && GetResolvedKeymode(keyCount).Judgements.TryGetValue(suffix, out ResolvedSkinAnimation? animation)
                ? animation.FrameAtClamped(elapsedMs) : null;
        }

        /// <summary>
        /// Propiedades del layout expuestas desde la skin activa.
        /// </summary>
        private ManiaKeymodeDefinition Profile(int keyCount) => GetManiaProfile(keyCount);
        public float? GetColumnWidth(int keyCount, int columnIndex)
        {
            var lanes = Profile(keyCount).Lanes;
            return columnIndex >= 0 && columnIndex < lanes.Count ? lanes[columnIndex].Width : null;
        }
        public float[]? GetColumnSpacing(int keyCount) => Profile(keyCount).Lanes.Select(lane => lane.GapAfter).ToArray();
        public bool[]? GetColumnLines(int keyCount) => Profile(keyCount).ColumnLines;
        public float? GetColumnStart(int keyCount) => Profile(keyCount).ColumnStart;
        public float? GetWidthForNoteHeightScale(int keyCount) => Profile(keyCount).NoteHeightReferenceWidth;
        public float GetNoteHeightReferenceWidth(int keyCount)
        {
            var config = Profile(keyCount);
            if (config.NoteHeightReferenceWidth is float referenceWidth && referenceWidth > 0f && float.IsFinite(referenceWidth))
                return referenceWidth;
            return config.Lanes.Select(lane => lane.Width).Where(width => float.IsFinite(width) && width > 0)
                .DefaultIfEmpty(48f).Min();
        }
        public float[]? GetColumnLineWidths(int keyCount) => Profile(keyCount).ColumnLineWidths;
        public int? GetHitPosition(int keyCount) => (int?)Profile(keyCount).HitPosition;
        public int? GetLightPosition(int keyCount) => (int?)Profile(keyCount).LightPosition;
        public bool IsLightingEnabled(int keyCount) => Profile(keyCount).ColumnLighting;
        public bool HasJudgementLine(int keyCount) => Profile(keyCount).JudgementLine;
        public bool? GetUpsideDownPreference(int keyCount) => Profile(keyCount).UpsideDown;
        public bool AreKeysUnderNotes(int keyCount) => Profile(keyCount).KeysUnderNotes;
        public int? GetScorePosition(int keyCount) => (int?)Profile(keyCount).ScorePosition;
        public int? GetComboPosition(int keyCount) => (int?)Profile(keyCount).ComboPosition;
        public (byte R, byte G, byte B, byte A)? GetColourBarline(int keyCount) => ToTuple(Profile(keyCount).BarlineColor);
        public (byte R, byte G, byte B, byte A)? GetColourJudgementLine(int keyCount) => ToTuple(Profile(keyCount).JudgementLineColor);
        public (byte R, byte G, byte B, byte A)? GetColourColumnLine(int keyCount) => ToTuple(Profile(keyCount).ColumnLineColor);
        public (byte R, byte G, byte B, byte A)? GetColourBreak(int keyCount) => ToTuple(Profile(keyCount).BreakColor);
        public (byte R, byte G, byte B, byte A)? GetGlobalColourHold(int keyCount) => ToTuple(Profile(keyCount).HoldColor);
        public string GetComboPrefix() => _currentDefinition.ComboPrefix;
        public float GetComboOverlap() => _currentDefinition.ComboOverlap;
        public float[]? GetLightingNWidths(int keyCount) => Profile(keyCount).LightingNoteWidths;
        public float[]? GetLightingLWidths(int keyCount) => Profile(keyCount).LightingHoldWidths;
        public (byte R, byte G, byte B, byte A)? GetColourLight(int keyCount, int columnIndex)
        {
            var lanes = Profile(keyCount).Lanes;
            return columnIndex >= 0 && columnIndex < lanes.Count ? ToTuple(lanes[columnIndex].LightColor) : null;
        }
        public (byte R, byte G, byte B, byte A)? GetColourHold(int keyCount, int columnIndex)
        {
            var profile = Profile(keyCount);
            var lanes = profile.Lanes;
            return columnIndex >= 0 && columnIndex < lanes.Count
                ? ToTuple(lanes[columnIndex].HoldColor ?? profile.HoldColor) : ToTuple(profile.HoldColor);
        }
        public (byte R, byte G, byte B, byte A)? GetColumnColorMap(int keyCount, int columnIndex)
        {
            var lanes = Profile(keyCount).Lanes;
            return columnIndex >= 0 && columnIndex < lanes.Count ? ToTuple(lanes[columnIndex].LaneColor) : null;
        }
        public int GetNoteBodyStyle(int keyCount, int columnIndex)
        {
            var profile = Profile(keyCount);
            NoteBodyRenderMode mode = columnIndex >= 0 && columnIndex < profile.Lanes.Count
                ? profile.Lanes[columnIndex].BodyMode ?? profile.BodyMode : profile.BodyMode;
            return mode switch
            {
                NoteBodyRenderMode.Stretch => 0,
                NoteBodyRenderMode.TileFromHead => 2,
                _ => 1
            };
        }

        private static (byte R, byte G, byte B, byte A)? ToTuple(SkinColor? color) => color is { } value
            ? (value.R, value.G, value.B, value.A) : null;

        public bool FlipNote(int keyCount, int columnIndex, string part, bool downScroll)
        {
            return ManiaSkinRules.FlipNote(Profile(keyCount), columnIndex, part, downScroll);
        }

        public bool FlipKey(int keyCount, int columnIndex, bool pressed, bool downScroll)
        {
            return ManiaSkinRules.FlipKey(Profile(keyCount), columnIndex, pressed, downScroll);
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
            _internalAssets.Dispose();
            _blankTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
