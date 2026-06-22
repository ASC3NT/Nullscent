#nullable enable

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nullscent.Skin
{
    /// <summary>
    /// Gestiona carga y acceso a elementos de skin (texturas, configuración).
    /// Proporciona fallbacks cuando elementos de skin no están disponibles.
    /// Soporta formato de skin osu!stable/lazer para mania.
    /// Compatible con archivos .osk estándar de osu!
    /// </summary>
    public class SkinManager : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private SkinConfig? _currentSkinConfig;
        private readonly Dictionary<string, Texture2D> _textureCache = new();

        public SkinConfig? CurrentSkinConfig => _currentSkinConfig;
        public string CurrentSkinDirectory { get; private set; } = string.Empty;

        public SkinManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
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

            try
            {
                UnloadSkin();
                _currentSkinConfig = SkinConfigParser.Parse(skinDirectory);
                CurrentSkinDirectory = skinDirectory;

                Console.WriteLine($"[SkinManager] Loaded skin: {_currentSkinConfig.Name} by {_currentSkinConfig.Author}");
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
            _currentSkinConfig = null;
            CurrentSkinDirectory = string.Empty;
        }

        /// <summary>
        /// Obtiene una textura de la skin. Retorna null si no está disponible.
        /// </summary>
        public Texture2D? GetTexture(string fileName)
        {
            if (string.IsNullOrWhiteSpace(CurrentSkinDirectory))
                return null;

            if (_textureCache.TryGetValue(fileName, out Texture2D? cachedTexture))
                return cachedTexture;

            string fullPath = Path.Combine(CurrentSkinDirectory, fileName);

            if (!File.Exists(fullPath) && !Path.HasExtension(fileName))
            {
                string[] extensions = { ".png", ".jpg", ".jpeg" };
                foreach (var ext in extensions)
                {
                    string pathWithExt = fullPath + ext;
                    if (File.Exists(pathWithExt))
                    {
                        fullPath = pathWithExt;
                        break;
                    }
                }
            }

            if (!File.Exists(fullPath))
                return null;

            try
            {
                using var fileStream = File.OpenRead(fullPath);
                var texture = Texture2D.FromStream(_graphicsDevice, fileStream);
                _textureCache[fileName] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkinManager] Failed to load texture {fileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtiene la textura de nota para una columna específica.
        /// </summary>
        public Texture2D? GetNoteTexture(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteImages.TryGetValue(columnIndex, out string? customPath))
                return GetTexture(customPath);

            string fileName = $"mania-note{keyCount}-{columnIndex}.png";
            var texture = GetTexture(fileName);

            if (texture == null)
            {
                fileName = $"mania-note-{columnIndex % 4 + 1}.png";
                texture = GetTexture(fileName);
            }

            return texture;
        }

        /// <summary>
        /// Obtiene la textura de head de LN.
        /// </summary>
        public Texture2D? GetLNHeadTexture(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteHeadImages.TryGetValue(columnIndex, out string? customPath))
                return GetTexture(customPath);

            return GetTexture("mania-note-hold-head.png");
        }

        /// <summary>
        /// Obtiene la textura de body de LN.
        /// </summary>
        public Texture2D? GetLNBodyTexture(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteBodyImages.TryGetValue(columnIndex, out string? customPath))
                return GetTexture(customPath);

            return GetTexture("mania-note-hold-body.png");
        }

        /// <summary>
        /// Obtiene la textura de tail de LN.
        /// </summary>
        public Texture2D? GetLNTailTexture(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null && config.NoteTailImages.TryGetValue(columnIndex, out string? customPath))
                return GetTexture(customPath);

            return GetTexture("mania-note-hold-tail.png");
        }

        /// <summary>
        /// Obtiene la textura de key (receptor).
        /// </summary>
        public Texture2D? GetKeyTexture(int keyCount, int columnIndex, bool pressed = false)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);

            if (config != null)
            {
                var imageDict = pressed ? config.KeyPressedImages : config.KeyImages;
                if (imageDict.TryGetValue(columnIndex, out string? customPath))
                    return GetTexture(customPath);
            }

            string suffix = pressed ? "D" : "";
            string fileName = $"mania-key{keyCount}-{columnIndex}{suffix}.png";
            var texture = GetTexture(fileName);

            if (texture == null)
            {
                fileName = $"mania-key-{columnIndex % 4 + 1}{suffix}.png";
                texture = GetTexture(fileName);
            }

            return texture;
        }

        /// <summary>
        /// Obtiene la textura de lighting (efecto de iluminación).
        /// </summary>
        public Texture2D? GetLightingTexture(int keyCount, int columnIndex)
        {
            string fileName = $"lightingN-{columnIndex}.png";
            return GetTexture(fileName) ?? GetTexture("lightingN.png");
        }

        /// <summary>
        /// Obtiene la textura del stage (fondo de columnas).
        /// </summary>
        public Texture2D? GetStageTexture(int keyCount, string? part = null)
        {
            if (part != null)
            {
                string fileName = $"mania-stage-{part}.png";
                return GetTexture(fileName);
            }

            return GetTexture("mania-stage-bottom.png");
        }

        /// <summary>
        /// Obtiene la textura de judgement popup.
        /// </summary>
        public Texture2D? GetJudgementTexture(string judgement)
        {
            string fileName = $"hit{judgement.ToLowerInvariant()}.png";
            return GetTexture(fileName);
        }

        /// <summary>
        /// Obtiene ancho de columna configurado en la skin.
        /// </summary>
        public float? GetColumnWidth(int keyCount, int columnIndex)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            if (config?.ColumnWidths != null && config.ColumnWidths.Length > columnIndex)
                return config.ColumnWidths[columnIndex];

            return null;
        }

        /// <summary>
        /// Obtiene la posición de hit position configurada.
        /// </summary>
        public float? GetHitPosition(int keyCount)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            return config?.HitPosition;
        }

        /// <summary>
        /// Verifica si el lighting está habilitado.
        /// </summary>
        public bool IsLightingEnabled(int keyCount)
        {
            var config = _currentSkinConfig?.GetConfigForKeyCount(keyCount);
            return config?.ColumnLighting ?? true;
        }

        public void Dispose()
        {
            UnloadSkin();
            GC.SuppressFinalize(this);
        }
    }
}
