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
        private SkinConfig? _defaultSkinConfig;
        private readonly Dictionary<(string FileName, bool HighResolution), Texture2D?> _textureCache = new();
        private readonly Dictionary<Texture2D, bool> _isHDTexture = new();
        private readonly Dictionary<string, List<Texture2D>?> _animationCache = new();
        private Texture2D? _blankTexture;

        public SkinConfig? CurrentSkinConfig => _currentSkinConfig;
        public string CurrentSkinDirectory { get; private set; } = string.Empty;
        public string DefaultSkinDirectory { get; set; } = Path.Combine("Skin", "--] Eliminate skin + new zealand[--");

        public SkinManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _blankTexture = new Texture2D(graphicsDevice, 1, 1);
            _blankTexture.SetData(new[] { Color.Transparent });
        }

        private void LoadDefaultConfig()
        {
            if (_defaultSkinConfig == null && !string.IsNullOrWhiteSpace(DefaultSkinDirectory) && Directory.Exists(DefaultSkinDirectory))
            {
                // Redirigir DefaultSkinDirectory si hay un subdirectorio que contenga skin.ini
                string resolvedDefaultDir = DefaultSkinDirectory;
                string iniPath = Path.Combine(resolvedDefaultDir, "skin.ini");
                if (!File.Exists(iniPath))
                {
                    try
                    {
                        var subDirs = Directory.GetDirectories(resolvedDefaultDir);
                        foreach (var subDir in subDirs)
                        {
                            if (File.Exists(Path.Combine(subDir, "skin.ini")))
                            {
                                resolvedDefaultDir = subDir;
                                break;
                            }
                        }
                    }
                    catch { }
                }
                DefaultSkinDirectory = resolvedDefaultDir;
                _defaultSkinConfig = SkinConfigParser.Parse(DefaultSkinDirectory);
            }
        }

        /// <summary>
        /// Carga una skin desde un directorio.
        /// </summary>
        public bool LoadSkin(string skinDirectory)
        {
            if (!Directory.Exists(skinDirectory))
            {
                Console.WriteLine($"[SkinManager] Skin directory not found: {skinDirectory}");
                return false;
            }

            // Buscar si hay un subdirectorio con skin.ini
            string resolvedSkinDir = skinDirectory;
            string iniPath = Path.Combine(resolvedSkinDir, "skin.ini");
            if (!File.Exists(iniPath))
            {
                try
                {
                    var subDirs = Directory.GetDirectories(resolvedSkinDir);
                    foreach (var subDir in subDirs)
                    {
                        if (File.Exists(Path.Combine(subDir, "skin.ini")))
                        {
                            resolvedSkinDir = subDir;
                            Console.WriteLine($"[SkinManager] Redirecting skin directory to subdirectory: {resolvedSkinDir}");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SkinManager] Error searching subdirectories: {ex.Message}");
                }
            }

            try
            {
                UnloadSkin();
                _currentSkinConfig = SkinConfigParser.Parse(resolvedSkinDir);
                CurrentSkinDirectory = resolvedSkinDir;

                Console.WriteLine($"[SkinManager] Loaded skin: {_currentSkinConfig.Name} by {_currentSkinConfig.Author}");
                
                // Asegurar que default config está cargada
                LoadDefaultConfig();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkinManager] Failed to load skin: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Descarga la skin actual y libera recursos.
        /// </summary>
        public void UnloadSkin()
        {
            foreach (var texture in _textureCache.Values)
                texture?.Dispose();

            _textureCache.Clear();
            _isHDTexture.Clear();
            _animationCache.Clear();
            _currentSkinConfig = null;
            CurrentSkinDirectory = string.Empty;
        }

        /// <summary>
        /// Determina si un archivo de textura existe en la skin activa.
        /// </summary>
        public bool HasTextureFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(CurrentSkinDirectory))
                return false;

            // 1. Buscar en la carpeta actual
            if (HasTextureFileInDirectory(CurrentSkinDirectory, fileName))
                return true;

            // 2. Buscar en el directorio padre (si la actual es un subdirectorio)
            try
            {
                string? parentDir = Path.GetDirectoryName(CurrentSkinDirectory);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir) &&
                    !parentDir.Equals(CurrentSkinDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    if (HasTextureFileInDirectory(parentDir, fileName))
                        return true;
                }
            }
            catch { }

            // 3. Buscar en la default
            if (!string.IsNullOrWhiteSpace(DefaultSkinDirectory) &&
                !CurrentSkinDirectory.Equals(DefaultSkinDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (HasTextureFileInDirectory(DefaultSkinDirectory, fileName))
                    return true;
            }

            return false;
        }

        private bool HasTextureFileInDirectory(string directory, string fileName)
        {
            string normalizedName = fileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            var searchNames = new List<string>();
            if (Core.SettingsService.Instance.HighResolutionSkins)
            {
                searchNames.Add(normalizedName + "@2x");
                searchNames.Add(normalizedName);
            }
            else
            {
                searchNames.Add(normalizedName);
                searchNames.Add(normalizedName + "@2x");
            }

            string[] extensions = { ".png", ".jpg", ".jpeg" };

            foreach (var name in searchNames)
            {
                string fullPath = Path.Combine(directory, name);
                if (Path.HasExtension(name))
                {
                    if (File.Exists(fullPath)) return true;
                }
                else
                {
                    foreach (var ext in extensions)
                    {
                        if (File.Exists(fullPath + ext)) return true;
                    }
                }
            }

            return false;
        }

        private Texture2D? LoadTextureFromDirectory(string directory, string fileName, out bool isHD)
        {
            isHD = false;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return null;

            bool highRes = Core.SettingsService.Instance.HighResolutionSkins;
            string extension = "";
            string baseName = fileName;
            if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".png";
                baseName = fileName[..^4];
            }
            else if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".jpg";
                baseName = fileName[..^4];
            }
            else if (fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".jpeg";
                baseName = fileName[..^5];
            }

            string normalizedBase = baseName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            var candidates = new List<(string Name, bool IsHD)>();
            if (highRes)
            {
                candidates.Add((normalizedBase + "@2x", true));
                candidates.Add((normalizedBase, false));
            }
            else
            {
                candidates.Add((normalizedBase, false));
                candidates.Add((normalizedBase + "@2x", true));
            }

            string? resolvedPath = null;

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(extension))
                {
                    string fullPath = Path.Combine(directory, candidate.Name + extension);
                    if (File.Exists(fullPath))
                    {
                        resolvedPath = fullPath;
                        isHD = candidate.IsHD;
                        break;
                    }
                }
                else
                {
                    string[] extensions = { ".png", ".jpg", ".jpeg" };
                    foreach (var ext in extensions)
                    {
                        string fullPath = Path.Combine(directory, candidate.Name + ext);
                        if (File.Exists(fullPath))
                        {
                            resolvedPath = fullPath;
                            isHD = candidate.IsHD;
                            break;
                        }
                    }
                }
                if (resolvedPath != null) break;
            }

            if (resolvedPath == null)
            {
                return null;
            }

            try
            {
                return LoadTextureWithHeightLimit(resolvedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkinManager] Failed to load texture {fileName} from {directory}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene una textura de la skin. Retorna null si no está disponible.
        /// </summary>
        public Texture2D? GetTexture(string fileName)
        {
            if (string.IsNullOrWhiteSpace(CurrentSkinDirectory))
                return null;

            bool highRes = Core.SettingsService.Instance.HighResolutionSkins;
            var cacheKey = (fileName, highRes);
            if (_textureCache.TryGetValue(cacheKey, out Texture2D? cachedTexture))
                return cachedTexture;

            // 1. Intentar cargar de la skin actual
            var texture = LoadTextureFromDirectory(CurrentSkinDirectory, fileName, out bool isHD);

            // 1.5. Si no se encuentra y la actual es subdirectorio, buscar en carpeta padre
            if (texture == null)
            {
                try
                {
                    string? parentDir = Path.GetDirectoryName(CurrentSkinDirectory);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir) &&
                        !parentDir.Equals(CurrentSkinDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        texture = LoadTextureFromDirectory(parentDir, fileName, out isHD);
                    }
                }
                catch { }
            }

            // 2. Si no se encuentra, intentar cargar de la skin default
            if (texture == null && !string.IsNullOrWhiteSpace(DefaultSkinDirectory) &&
                !CurrentSkinDirectory.Equals(DefaultSkinDirectory, StringComparison.OrdinalIgnoreCase))
            {
                texture = LoadTextureFromDirectory(DefaultSkinDirectory, fileName, out isHD);
            }

            _textureCache[cacheKey] = texture;
            if (texture != null)
            {
                _isHDTexture[texture] = isHD;
            }
            return texture;
        }

        private Texture2D LoadTextureWithHeightLimit(string resolvedPath)
        {
            using (var fileStream = File.OpenRead(resolvedPath))
            {
                var image = StbImageSharp.ImageResult.FromStream(fileStream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                int targetHeight = image.Height;
                byte[] pixelData = image.Data;

                if (targetHeight > 16384)
                {
                    targetHeight = 16384;
                    pixelData = new byte[image.Width * targetHeight * 4];
                    Array.Copy(image.Data, pixelData, pixelData.Length);
                    Console.WriteLine($"[SkinManager] Warning: Cropped texture {Path.GetFileName(resolvedPath)} from {image.Height}px to 16384px to meet GPU limits.");
                }

                Texture2D texture = new Texture2D(_graphicsDevice, image.Width, targetHeight, false, SurfaceFormat.Color);
                texture.SetData(pixelData);
                return texture;
            }
        }

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
        public Texture2D? GetAnimationFrame(List<Texture2D>? animation, double timeMs)
        {
            if (animation == null || animation.Count == 0) return null;
            if (animation.Count == 1) return animation[0];

            if (timeMs < 0) return animation[0];

            int fps = _currentSkinConfig?.AnimationFramerate ?? -1;
            if (fps <= 0)
                fps = 15; // default en osu!mania

            double frameDuration = 1000.0 / fps;
            if (frameDuration <= 0) return animation[0];

            int frameIdx = (int)(timeMs / frameDuration) % animation.Count;
            return animation[Math.Clamp(frameIdx, 0, animation.Count - 1)];
        }

        /// <summary>
        /// Obtiene la escala base de la textura para compatibilidad con assets @2x.
        /// Retorna 0.5f si es @2x, o 1.0f de lo contrario.
        /// </summary>
        public float GetTextureScale(Texture2D? texture)
        {
            if (texture == null) return 1.0f;
            return _isHDTexture.TryGetValue(texture, out bool isHD) && isHD ? 0.5f : 1.0f;
        }

        private string GetDefaultStyleSuffix(int keyCount, int columnIndex)
        {
            if (keyCount == 1) return "S";
            if (keyCount == 2) return (columnIndex == 0) ? "1" : "2";
            if (keyCount == 3) return (columnIndex == 1) ? "S" : "1";
            if (keyCount == 4)
            {
                if (columnIndex == 0 || columnIndex == 3) return "1";
                return "2";
            }
            if (keyCount == 5)
            {
                if (columnIndex == 2) return "S";
                if (columnIndex == 0 || columnIndex == 4) return "1";
                return "2";
            }
            if (keyCount == 6)
            {
                if (columnIndex == 1 || columnIndex == 4) return "2";
                return "1";
            }
            if (keyCount == 7)
            {
                if (columnIndex == 3) return "S";
                if (columnIndex == 1 || columnIndex == 5) return "2";
                return "1";
            }
            if (keyCount == 8)
            {
                if (columnIndex == 0 || columnIndex == 7) return "S";
                if (columnIndex == 2 || columnIndex == 5) return "2";
                return "1";
            }
            return (columnIndex % 2 == 0) ? "1" : "2";
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
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            
            // Buscar en orden de prioridad estándar de osu! (ej. mania-note1)
            var anim = GetAnimation($"mania-note{style}");
            if (anim != null) return anim;

            anim = GetAnimation($"mania-note-{style}");
            if (anim != null) return anim;

            anim = GetAnimation($"mania-note{keyCount}-{columnIndex}");
            if (anim != null) return anim;

            return null;
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
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteHeadImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            // 1. Keycount-specific LN head
            var anim = GetAnimation($"mania-note{keyCount}-{columnIndex}H");
            if (anim != null) return anim;

            // 2. Style-specific LN head
            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            anim = GetAnimation($"mania-note{style}H");
            if (anim != null) return anim;

            anim = GetAnimation($"mania-note-{style}H");
            if (anim != null) return anim;

            // 3. Fallback to normal note
            anim = GetNoteAnimation(keyCount, columnIndex);
            if (anim != null) return anim;

            // 4. Default game asset fallback
            return GetAnimation("mania-note-hold-head");
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
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteBodyImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            // 1. Keycount-specific LN body
            var anim = GetAnimation($"mania-note{keyCount}-{columnIndex}L");
            if (anim != null) return anim;

            // 2. Style-specific LN body
            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            anim = GetAnimation($"mania-note{style}L");
            if (anim != null) return anim;

            anim = GetAnimation($"mania-note-{style}L");
            if (anim != null) return anim;

            // 3. Default game asset fallback
            return GetAnimation("mania-note-hold-body");
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
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteTailImages.TryGetValue(columnIndex, out string? customPath))
                return GetAnimation(customPath);

            // 1. Keycount-specific LN tail
            var anim = GetAnimation($"mania-note{keyCount}-{columnIndex}T");
            if (anim != null) return anim;

            // 2. Style-specific LN tail
            string style = GetDefaultStyleSuffix(keyCount, columnIndex);
            anim = GetAnimation($"mania-note{style}T");
            if (anim != null) return anim;

            anim = GetAnimation($"mania-note-{style}T");
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
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

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

            string fileName = $"mania-key{keyCount}-{columnIndex}{suffix}";
            return GetAnimation(fileName);
        }

        /// <summary>
        /// Obtiene la animación de lighting (haz de luz).
        /// </summary>
        public List<Texture2D>? GetLightingAnimation(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            if (config != null && !string.IsNullOrWhiteSpace(config.LightingN))
            {
                if (config.LightingN.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                    return _blankTexture != null ? new List<Texture2D> { _blankTexture } : null;
                var anim = GetAnimation(config.LightingN);
                if (anim != null) return anim;
            }

            string fileName = $"lightingN-{columnIndex}";
            return GetAnimation(fileName) ?? GetAnimation("lightingN");
        }

        public List<Texture2D>? GetLightingLAnimation(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            if (config != null && !string.IsNullOrWhiteSpace(config.LightingL))
            {
                if (config.LightingL.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                    return _blankTexture != null ? new List<Texture2D> { _blankTexture } : null;
                var anim = GetAnimation(config.LightingL);
                if (anim != null) return anim;
            }

            string fileName = $"lightingL-{columnIndex}";
            return GetAnimation(fileName) ?? GetAnimation("lightingL") ?? GetLightingAnimation(keyCount, columnIndex);
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
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

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

        public List<Texture2D>? GetHitExplosionAnimation(int keyCount, int columnIndex)
        {
            var anim = GetAnimation("hitlighting");
            if (anim != null && anim.Count > 0) return anim;

            var stageHint = GetStageTexture(keyCount, "hint");
            if (stageHint != null) return new List<Texture2D> { stageHint };

            return null;
        }

        public Texture2D? GetHitExplosionTexture()
        {
            return GetTexture("hitlighting") ?? GetStageTexture(4, "hint");
        }

        /// <summary>
        /// Traduce los juicios y obtiene su textura mapeada correctamente a los sprites de osu!.
        /// </summary>
        public Texture2D? GetJudgementTexture(string judgement)
        {
            string? primary = null;
            string? secondary = null;

            switch (judgement)
            {
                case "Perfect":
                    primary = "mania-hit300g";
                    secondary = "hit300g";
                    break;
                case "Great":
                    primary = "mania-hit300";
                    secondary = "hit300";
                    break;
                case "Good":
                    primary = "mania-hit200";
                    secondary = "hit200";
                    break;
                case "Ok":
                    primary = "mania-hit100";
                    secondary = "hit100";
                    break;
                case "Meh":
                    primary = "mania-hit50";
                    secondary = "hit50";
                    break;
                case "Miss":
                    primary = "mania-hit0";
                    secondary = "hit0";
                    break;
            }

            if (primary != null)
            {
                var tex = GetTexture(primary) ?? GetTexture(primary + ".png");
                if (tex != null) return tex;
            }
            if (secondary != null)
            {
                var tex = GetTexture(secondary) ?? GetTexture(secondary + ".png");
                if (tex != null) return tex;
            }

            string fallbackName = $"hit{judgement.ToLowerInvariant()}";
            return GetTexture(fallbackName) ?? GetTexture(fallbackName + ".png");
        }

        /// <summary>
        /// Propiedades del layout expuestas desde la skin activa.
        /// </summary>
        public float? GetColumnWidth(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            if (config?.ColumnWidths != null && config.ColumnWidths.Length > columnIndex)
                return config.ColumnWidths[columnIndex];

            LoadDefaultConfig();
            var defConfig = _defaultSkinConfig?.GetConfigForKeyCount(keyCount);
            if (defConfig?.ColumnWidths != null && defConfig.ColumnWidths.Length > columnIndex)
                return defConfig.ColumnWidths[columnIndex];

            return null;
        }

        public float[]? GetColumnSpacing(int keyCount)
        {
            var spacing = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColumnSpacing;
            if (spacing != null) return spacing;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColumnSpacing;
        }

        public bool[]? GetColumnLines(int keyCount)
        {
            var lines = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColumnLines;
            if (lines != null) return lines;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColumnLines;
        }

        public float? GetColumnStart(int keyCount)
        {
            var start = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColumnStart;
            if (start.HasValue) return start.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColumnStart;
        }

        public float? GetWidthForNoteHeightScale(int keyCount)
        {
            var val = _currentSkinConfig?.GetConfigForKeyCount(keyCount).WidthForNoteHeightScale;
            if (val.HasValue) return val.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).WidthForNoteHeightScale;
        }

        public int? GetHitPosition(int keyCount)
        {
            var pos = _currentSkinConfig?.GetConfigForKeyCount(keyCount).HitPosition;
            if (pos.HasValue) return pos.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).HitPosition;
        }

        public int? GetLightPosition(int keyCount)
        {
            var pos = _currentSkinConfig?.GetConfigForKeyCount(keyCount).LightPosition;
            if (pos.HasValue) return pos.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).LightPosition;
        }

        public bool IsLightingEnabled(int keyCount)
        {
            if (_currentSkinConfig != null)
                return _currentSkinConfig.GetConfigForKeyCount(keyCount).ColumnLighting;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColumnLighting ?? true;
        }

        public bool HasJudgementLine(int keyCount)
        {
            if (_currentSkinConfig != null)
                return _currentSkinConfig.GetConfigForKeyCount(keyCount).JudgementLine;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).JudgementLine ?? true;
        }

        public bool IsUpsideDown(int keyCount)
        {
            if (_currentSkinConfig != null)
                return _currentSkinConfig.GetConfigForKeyCount(keyCount).UpsideDown;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).UpsideDown ?? false;
        }

        public bool AreKeysUnderNotes(int keyCount)
        {
            if (_currentSkinConfig != null)
                return _currentSkinConfig.GetConfigForKeyCount(keyCount).KeysUnderNotes;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).KeysUnderNotes ?? false;
        }

        public int? GetScorePosition(int keyCount)
        {
            var pos = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ScorePosition;
            if (pos.HasValue) return pos.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ScorePosition;
        }

        public int? GetComboPosition(int keyCount)
        {
            var pos = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ComboPosition;
            if (pos.HasValue) return pos.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ComboPosition;
        }

        public (byte R, byte G, byte B, byte A)? GetColourBarline(int keyCount)
        {
            var color = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColourBarline;
            if (color.HasValue) return color.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColourBarline;
        }

        public (byte R, byte G, byte B, byte A)? GetColourJudgementLine(int keyCount)
        {
            var color = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColourJudgementLine;
            if (color.HasValue) return color.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColourJudgementLine;
        }

        public (byte R, byte G, byte B, byte A)? GetColourColumnLine(int keyCount)
        {
            var color = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColourColumnLine;
            if (color.HasValue) return color.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColourColumnLine;
        }

        public (byte R, byte G, byte B, byte A)? GetColourBreak(int keyCount)
        {
            var color = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColourBreak;
            if (color.HasValue) return color.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColourBreak;
        }

        public (byte R, byte G, byte B, byte A)? GetGlobalColourHold(int keyCount)
        {
            var color = _currentSkinConfig?.GetConfigForKeyCount(keyCount).ColourHold;
            if (color.HasValue) return color.Value;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).ColourHold;
        }

        public float[]? GetLightingNWidths(int keyCount)
        {
            var widths = _currentSkinConfig?.GetConfigForKeyCount(keyCount).LightingNWidths;
            if (widths != null) return widths;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).LightingNWidths;
        }

        public float[]? GetLightingLWidths(int keyCount)
        {
            var widths = _currentSkinConfig?.GetConfigForKeyCount(keyCount).LightingLWidths;
            if (widths != null) return widths;

            LoadDefaultConfig();
            return _defaultSkinConfig?.GetConfigForKeyCount(keyCount).LightingLWidths;
        }

        public (byte R, byte G, byte B, byte A)? GetColourLight(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            if (config?.ColourLights != null && config.ColourLights.TryGetValue(columnIndex, out var color))
                return color;

            LoadDefaultConfig();
            var defConfig = _defaultSkinConfig?.GetConfigForKeyCount(keyCount);
            if (defConfig?.ColourLights != null && defConfig.ColourLights.TryGetValue(columnIndex, out var defColor))
                return defColor;

            return null;
        }

        public (byte R, byte G, byte B, byte A)? GetColourHold(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            if (config?.ColourHolds != null && config.ColourHolds.TryGetValue(columnIndex, out var color))
                return color;
            if (config?.ColourHold != null)
                return config.ColourHold.Value;

            LoadDefaultConfig();
            var defConfig = _defaultSkinConfig?.GetConfigForKeyCount(keyCount);
            if (defConfig?.ColourHolds != null && defConfig.ColourHolds.TryGetValue(columnIndex, out var defColor))
                return defColor;
            if (defConfig?.ColourHold != null)
                return defConfig.ColourHold.Value;

            return null;
        }

        public (byte R, byte G, byte B, byte A)? GetColumnColorMap(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            if (config?.ColumnColorsMap != null && config.ColumnColorsMap.TryGetValue(columnIndex, out var color))
                return color;

            LoadDefaultConfig();
            var defConfig = _defaultSkinConfig?.GetConfigForKeyCount(keyCount);
            if (defConfig?.ColumnColorsMap != null && defConfig.ColumnColorsMap.TryGetValue(columnIndex, out var defColor))
                return defColor;

            return null;
        }

        public int GetNoteBodyStyle(int keyCount, int columnIndex)
        {
            if (_currentSkinConfig != null)
            {
                var configCurrent = _currentSkinConfig.GetConfigForKeyCount(keyCount);
                if (configCurrent.NoteBodyStyles.TryGetValue(columnIndex, out int style))
                    return style;
                return configCurrent.NoteBodyStyle;
            }

            LoadDefaultConfig();
            var defConfig = _defaultSkinConfig?.GetConfigForKeyCount(keyCount);
            if (defConfig != null)
            {
                if (defConfig.NoteBodyStyles.TryGetValue(columnIndex, out int style))
                    return style;
                return defConfig.NoteBodyStyle;
            }

            return 1;
        }

        public Texture2D? GetScorebarBg() => GetTexture("scorebar-bg");
        public Texture2D? GetScorebarColour() => GetTexture("scorebar-colour");
        public Texture2D? GetCursorTexture() => GetTexture("cursor");

        /// <summary>
        /// Dibuja un número en pantalla usando texturas del skin con un prefijo dado (ej: "default" para combo).
        /// </summary>
        public bool DrawNumber(SpriteBatch spriteBatch, string text, Vector2 position, string prefix, float scale, float alpha, bool centered = true, float spacing = -2f)
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
                    float texScale = GetTextureScale(tex);
                    float height = tex.Height * texScale;
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
                    float logicalWidth = tex.Width * GetTextureScale(tex);
                    float logicalHeight = tex.Height * GetTextureScale(tex);
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
                    spriteBatch.Draw(tex, new Rectangle((int)currentX, (int)(position.Y - uniformH / 2f), (int)w, (int)uniformH), Color.White * alpha);
                }
                currentX += w + spacing;
            }

            return true;
        }

        public void Dispose()
        {
            UnloadSkin();
            _blankTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
