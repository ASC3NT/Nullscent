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
        public string? ResolvedPath { get; init; }
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
                    ResolvedPath = "_blank",
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
                        Vector2? pairedStandardSize = null;
                        if (candidate.Hd && TryGetStandardLogicalSize(name, out Vector2 standardSize))
                        {
                            var highResolutionInfo = StbImageSharp.ImageInfo.FromStream(stream);
                            if (highResolutionInfo is { Width: > 0, Height: > 0 } dimensions)
                            {
                                var highResolutionSize = new Vector2(dimensions.Width, dimensions.Height);
                                if (!HasExpectedHighResolutionDimensions(standardSize, highResolutionSize))
                                {
                                    Warnings.Add($"@2x variant for {name} is {highResolutionSize.X}x{highResolutionSize.Y}, while its 1x pair is {standardSize.X}x{standardSize.Y}; the malformed @2x variant was ignored.");
                                    continue;
                                }

                                pairedStandardSize = standardSize;
                            }

                            stream.Position = 0;
                        }
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
                                    ResolvedPath = candidate.Path,
                                    LogicalSize = ResolveLogicalSize(new Vector2(image.Width, image.Height), candidate.Hd, pairedStandardSize),
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

        internal static byte[] ResizeBilinear(byte[] source, int sourceWidth, int sourceHeight, int width, int height)
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
                    float topWeight = 1 - fy;
                    float bottomWeight = fy;
                    float leftWeight = 1 - fx;
                    float rightWeight = fx;
                    float topLeftWeight = topWeight * leftWeight;
                    float topRightWeight = topWeight * rightWeight;
                    float bottomLeftWeight = bottomWeight * leftWeight;
                    float bottomRightWeight = bottomWeight * rightWeight;

                    float topLeftAlpha = source[topLeft + 3] / 255f;
                    float topRightAlpha = source[topRight + 3] / 255f;
                    float bottomLeftAlpha = source[bottomLeft + 3] / 255f;
                    float bottomRightAlpha = source[bottomRight + 3] / 255f;
                    float alpha = topLeftAlpha * topLeftWeight + topRightAlpha * topRightWeight
                        + bottomLeftAlpha * bottomLeftWeight + bottomRightAlpha * bottomRightWeight;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        float premultiplied = source[topLeft + channel] * topLeftAlpha * topLeftWeight
                            + source[topRight + channel] * topRightAlpha * topRightWeight
                            + source[bottomLeft + channel] * bottomLeftAlpha * bottomLeftWeight
                            + source[bottomRight + channel] * bottomRightAlpha * bottomRightWeight;
                        float straight = alpha > 0 ? premultiplied / alpha : 0;
                        output[destination + channel] = (byte)Math.Clamp((int)MathF.Round(straight), 0, 255);
                    }
                    output[destination + 3] = (byte)Math.Clamp((int)MathF.Round(alpha * 255), 0, 255);
                }
            }
            return output;
        }

        internal static Vector2 ResolveLogicalSize(Vector2 sourcePixelSize, bool isHighResolution, Vector2? pairedStandardSize)
        {
            if (!isHighResolution) return sourcePixelSize;
            return pairedStandardSize ?? sourcePixelSize / 2f;
        }

        internal static bool HasExpectedHighResolutionDimensions(Vector2 standardSize, Vector2 highResolutionSize)
            => highResolutionSize.X == standardSize.X * 2f && highResolutionSize.Y == standardSize.Y * 2f;

        private bool TryGetStandardLogicalSize(string name, out Vector2 logicalSize)
        {
            foreach (var candidate in CandidatePaths(name, preferHighResolution: false))
            {
                if (candidate.Hd || !File.Exists(candidate.Path)) continue;
                try
                {
                    using var stream = File.OpenRead(candidate.Path);
                    var info = StbImageSharp.ImageInfo.FromStream(stream);
                    if (info is { Width: > 0, Height: > 0 } dimensions)
                    {
                        logicalSize = new Vector2(dimensions.Width, dimensions.Height);
                        return true;
                    }
                }
                catch
                {
                    // A broken 1x companion must not prevent a valid @2x asset from loading.
                }
            }

            logicalSize = default;
            return false;
        }

        public bool HasFile(string name)
        {
            if (name.Equals("_blank", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (var candidate in CandidatePaths(name))
                if (File.Exists(candidate.Path)) return true;
            return false;
        }

        private IEnumerable<(string Path, bool Hd)> CandidatePaths(string name, bool? preferHighResolution = null)
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
            bool highResolutionFirst = preferHighResolution ?? preferHd;
            foreach (bool hd in highResolutionFirst ? new[] { true, false } : new[] { false, true })
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
