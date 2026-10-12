#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nullscent.Skin
{
    public enum SkinAssetOrigin { Skin, Internal, IntentionalBlank }

    public sealed class ResolvedSkinAsset
    {
        public Texture2D? Texture { get; init; }
        public Vector2 LogicalSize { get; init; }
        public Vector2 SourcePixelSize { get; init; }
        public Vector2 TexturePixelSize { get; init; }
        public SkinAssetOrigin Origin { get; init; }
        public bool IsHighResolution { get; init; }
    }

    // Owns only textures loaded from one selected skin. Null means that the existing
    // geometric/text fallback in the renderer is the internal Nullscent asset.
    public sealed class SkinAssetResolver : IDisposable
    {
        private readonly GraphicsDevice graphics;
        private readonly string? root;
        private readonly Dictionary<string, ResolvedSkinAsset> cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Texture2D blank;
        private readonly bool preferHd;
        public List<string> Warnings { get; } = new();

        public SkinAssetResolver(GraphicsDevice graphics, string? directory, bool preferHd)
        {
            this.graphics = graphics;
            root = directory == null ? null : Path.GetFullPath(directory);
            this.preferHd = preferHd;
            blank = new Texture2D(graphics, 1, 1);
            blank.SetData(new[] { Color.Transparent });
        }

        public ResolvedSkinAsset Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new ResolvedSkinAsset { Origin = SkinAssetOrigin.Internal };
            if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                return new ResolvedSkinAsset
                {
                    Texture = blank, LogicalSize = Vector2.One,
                    SourcePixelSize = Vector2.One, TexturePixelSize = Vector2.One,
                    Origin = SkinAssetOrigin.IntentionalBlank
                };
            if (cache.TryGetValue(name, out var cached)) return cached;

            var result = new ResolvedSkinAsset { Origin = SkinAssetOrigin.Internal };
            if (root != null)
            {
                foreach (var candidate in CandidatePaths(name))
                {
                    try
                    {
                        if (!File.Exists(candidate.Path)) continue;
                        using var stream = File.OpenRead(candidate.Path);
                        var image = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                        if (image.Width <= 0 || image.Height <= 0) throw new InvalidDataException("Dimensiones de imagen inválidas.");
                        // osu!lazer's MaxDimensionLimitedTextureLoaderStore limits each axis
                        // independently before uploading. Keep the *original* dimensions as
                        // the logical size: resizing is a GPU representation detail.
                        // MonoGame's Reach profile enforces a 2048px limit even
                        // on GPUs that could accept lazer's 8192px texture.
                        int safeTextureLimit = graphics.GraphicsProfile == GraphicsProfile.Reach ? 2048 : 8192;
                        for (int limit = safeTextureLimit; limit >= 1024; limit /= 2)
                        {
                            // Scale both axes with one ratio. Clamping them independently
                            // distorts very wide/tall legacy LN textures.
                            float resizeScale = Math.Min(1f, limit / (float)Math.Max(image.Width, image.Height));
                            int uploadWidth = Math.Max(1, (int)MathF.Round(image.Width * resizeScale));
                            int uploadHeight = Math.Max(1, (int)MathF.Round(image.Height * resizeScale));
                            byte[] pixels = uploadWidth == image.Width && uploadHeight == image.Height
                                ? image.Data : ResizeBilinear(image.Data, image.Width, image.Height, uploadWidth, uploadHeight);
                            Texture2D? texture = null;
                            try
                            {
                                texture = new Texture2D(graphics, uploadWidth, uploadHeight, false, SurfaceFormat.Color);
                                texture.SetData(pixels);
                                if (pixels != image.Data)
                                    Console.WriteLine($"[SkinAssetResolver] {name}: textura {image.Width}x{image.Height} reducida a {uploadWidth}x{uploadHeight} para GPU.");
                                result = new ResolvedSkinAsset
                                {
                                    Texture = texture,
                                    LogicalSize = new Vector2(image.Width / (candidate.Hd ? 2f : 1f), image.Height / (candidate.Hd ? 2f : 1f)),
                                    SourcePixelSize = new Vector2(image.Width, image.Height),
                                    TexturePixelSize = new Vector2(uploadWidth, uploadHeight),
                                    Origin = SkinAssetOrigin.Skin,
                                    IsHighResolution = candidate.Hd
                                };
                                break;
                            }
                            catch
                            {
                                texture?.Dispose();
                                if (limit == 1024) throw;
                            }
                        }
                        if (result.Texture != null) break;
                    }
                    catch (Exception ex)
                    {
                        Warnings.Add($"No se pudo cargar {name}: {ex.Message}");
                        Console.WriteLine($"[SkinAssetResolver] {name}: {ex.Message}");
                    }
                }
            }
            cache[name] = result;
            return result;
        }

        private static byte[] ResizeBilinear(byte[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            var output = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                float sourceY = (y + 0.5f) * sourceHeight / height - 0.5f;
                int y0 = Math.Clamp((int)MathF.Floor(sourceY), 0, sourceHeight - 1);
                int y1 = Math.Min(y0 + 1, sourceHeight - 1);
                float fy = Math.Clamp(sourceY, 0, sourceHeight - 1) - y0;
                for (int x = 0; x < width; x++)
                {
                    float sourceX = (x + 0.5f) * sourceWidth / width - 0.5f;
                    int x0 = Math.Clamp((int)MathF.Floor(sourceX), 0, sourceWidth - 1);
                    int x1 = Math.Min(x0 + 1, sourceWidth - 1);
                    float fx = Math.Clamp(sourceX, 0, sourceWidth - 1) - x0;
                    int destination = (y * width + x) * 4;
                    int topLeft = (y0 * sourceWidth + x0) * 4;
                    int topRight = (y0 * sourceWidth + x1) * 4;
                    int bottomLeft = (y1 * sourceWidth + x0) * 4;
                    int bottomRight = (y1 * sourceWidth + x1) * 4;
                    for (int channel = 0; channel < 4; channel++)
                    {
                        float top = source[topLeft + channel] * (1 - fx) + source[topRight + channel] * fx;
                        float bottom = source[bottomLeft + channel] * (1 - fx) + source[bottomRight + channel] * fx;
                        output[destination + channel] = (byte)Math.Clamp((int)MathF.Round(top * (1 - fy) + bottom * fy), 0, 255);
                    }
                }
            }
            return output;
        }

        public bool HasFile(string name)
        {
            if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (var candidate in CandidatePaths(name))
                if (File.Exists(candidate.Path)) return true;
            return false;
        }

        private IEnumerable<(string Path, bool Hd)> CandidatePaths(string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name)) yield break;
            string normalized = name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string extension = Path.GetExtension(normalized);
            if (extension.Length > 0 && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) yield break;
            string stem = extension.Length > 0 ? normalized[..^extension.Length] : normalized;
            if (stem.EndsWith("@2x", StringComparison.OrdinalIgnoreCase)) stem = stem[..^3];
            string[] extensions = extension.Length > 0 ? new[] { extension } : new[] { ".png", ".jpg", ".jpeg" };
            foreach (bool hd in preferHd ? new[] { true, false } : new[] { false, true })
            foreach (string ext in extensions)
            {
                string path = Path.GetFullPath(Path.Combine(root, stem + (hd ? "@2x" : "") + ext));
                if (path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    yield return (path, hd);
            }
        }

        public void Dispose()
        {
            foreach (var asset in cache.Values) asset.Texture?.Dispose();
            cache.Clear();
            blank.Dispose();
        }
    }
}
