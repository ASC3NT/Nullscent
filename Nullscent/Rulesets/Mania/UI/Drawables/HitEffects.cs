#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Judgements;
using Nullscent.UI; // Import TrueTypeFontRenderer
using Nullscent.Skin;
using System;

namespace Nullscent.Rulesets.Mania.UI.Drawables
{
    /// <summary>
    /// Visual explosion effect when hitting a note.
    /// Inspired by osu!mania's hit lighting and explosions.
    /// </summary>
    public class HitExplosion
    {
        private HitResult _result;
        private double _startTime;
        private readonly double _duration;
        private bool _isActive;

        private const double EXPLOSION_DURATION = 300; // ms
        private const double SCALE_UP_DURATION = 80;   // ms

        public bool IsActive => _isActive;

        public HitExplosion(HitResult result, double currentTime)
        {
            _result = result;
            _startTime = currentTime;
            _duration = EXPLOSION_DURATION;
            _isActive = true;
        }

        public void Reset(HitResult result, double currentTime)
        {
            _result = result;
            _startTime = currentTime;
            _isActive = true;
        }

        public void Update(double currentTime)
        {
            double elapsed = currentTime - _startTime;
            if (elapsed >= _duration)
            {
                _isActive = false;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D glowTexture, int columnX, int columnWidth, int receptorY, double currentTime, System.Collections.Generic.List<Texture2D>? skinnedExplosionFrames = null,
            SkinManager? skinManager = null, float legacyScale = 1f, float? configuredWidth = null)
        {
            if (!_isActive) return;

            double elapsed = currentTime - _startTime;

            // Calculate animation progress
            double progress = Math.Clamp(elapsed / _duration, 0, 1);

            // Scale animation (quick scale up, then fade)
            float scale = 1.0f;
            if (elapsed < SCALE_UP_DURATION)
            {
                scale = (float)(0.5 + (elapsed / SCALE_UP_DURATION) * 0.5);
            }

            // Alpha fade out
            float alpha = (float)(1.0 - progress);

            // Get color based on result
            Color explosionColor = GetColorForResult(_result) * alpha;

            // Draw explosion radial glow (wider for beautiful bloom)
            int width = (int)((columnWidth * 1.8f) * scale);
            int height = (int)(width * scale);
            int x = columnX + (columnWidth - width) / 2;
            int y = receptorY - height / 2;

            var explosionRect = new Rectangle(x, y, width, height);
            
            Texture2D activeTexture = glowTexture;
            bool isSkinned = skinnedExplosionFrames != null && skinnedExplosionFrames.Count > 0;
            if (isSkinned)
            {
                double frameDuration = Math.Max(1000d / 60d, 170d / skinnedExplosionFrames!.Count);
                int frameIdx = (int)(elapsed / frameDuration);
                frameIdx = Math.Clamp(frameIdx, 0, skinnedExplosionFrames.Count - 1);
                activeTexture = skinnedExplosionFrames[frameIdx];
                Vector2 logicalSize = skinManager?.GetLogicalSize(activeTexture)
                    ?? new Vector2(activeTexture.Width, activeTexture.Height);
                float assetScale = configuredWidth is > 0 ? configuredWidth.Value / 30f : 1f;
                width = Math.Max(1, (int)MathF.Round(logicalSize.X * legacyScale * assetScale));
                height = Math.Max(1, (int)MathF.Round(logicalSize.Y * legacyScale * assetScale));
                explosionRect = new Rectangle(columnX + (columnWidth - width) / 2, receptorY - height / 2, width, height);
                alpha = elapsed < 80 ? (float)Math.Clamp(elapsed / 80d, 0, 1)
                    : (float)Math.Clamp((200d - elapsed) / 120d, 0, 1);
            }
            
            // Draw texture
            spriteBatch.Draw(activeTexture, explosionRect, isSkinned ? Color.White * alpha : explosionColor);

            // Draw bright core only if not using skinned texture
            if (!isSkinned && elapsed < SCALE_UP_DURATION * 2)
            {
                int coreSize = (int)((columnWidth * 0.6f) * scale);
                var coreRect = new Rectangle(
                    columnX + (columnWidth - coreSize) / 2,
                    receptorY - coreSize / 2,
                    coreSize,
                    coreSize
                );
                spriteBatch.Draw(glowTexture, coreRect, Color.White * alpha * 1.5f);
            }
        }

        private Color GetColorForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => new Color(255, 224, 0),    // Gold/Yellow MAX
                HitResult.Great => new Color(255, 180, 0),      // Orange/Yellow 300
                HitResult.Good => new Color(0, 230, 100),       // Green 200
                HitResult.Ok => new Color(50, 150, 255),        // Blue 100
                HitResult.Meh => new Color(180, 180, 180),      // Gray 50
                _ => new Color(100, 100, 100)
            };
        }
    }

    /// <summary>
    /// Displays judgement text (Perfect, Great, etc) above the hit location.
    /// </summary>
    public class DrawableJudgement
    {
        private readonly HitResult _result;
        private readonly double _startTime;
        private readonly string _text;
        private bool _isActive;

        private const double DISPLAY_DURATION = 500; // ms
        private const double SCALE_DURATION = 100;   // ms
        private const float MOVE_SPEED = 50;         // pixels per second

        public bool IsActive => _isActive;

        public DrawableJudgement(HitResult result, double currentTime)
        {
            _result = result;
            _startTime = currentTime;
            _text = GetTextForResult(result);
            _isActive = true;
        }

        public void Update(double currentTime)
        {
            double elapsed = currentTime - _startTime;
            if (elapsed >= DISPLAY_DURATION)
            {
                _isActive = false;
            }
        }

        public void Draw(SpriteBatch spriteBatch, TrueTypeFontRenderer fontRenderer, 
            int columnX, int columnWidth, int receptorY, double currentTime)
        {
            if (!_isActive || fontRenderer == null) return;

            double elapsed = currentTime - _startTime;
            double progress = Math.Clamp(elapsed / DISPLAY_DURATION, 0, 1);

            // Scale animation
            float scale = 1.0f;
            if (elapsed < SCALE_DURATION)
            {
                scale = (float)(0.5 + (elapsed / SCALE_DURATION) * 0.5);
            }

            // Alpha fade
            float alpha = (float)(1.0 - Math.Pow(progress, 2));

            // Move upward
            float yOffset = (float)(elapsed / 1000.0 * MOVE_SPEED);

            // Get color
            Color color = GetColorForResult(_result) * alpha;

            // Draw text centered above receptor (usar tamaño de fuente fijo de 24 y escala de SpriteBatch para evitar re-generar el atlas de texturas)
            fontRenderer.SetFontSize(24);

            int textX = columnX + columnWidth / 2;
            int textY = (int)(receptorY - 60 - yOffset);

            fontRenderer.DrawTextCentered(_text, textX, textY, color, scale);
        }

        private string GetTextForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => "Perfect",
                HitResult.Great => "Great",
                HitResult.Good => "Good",
                HitResult.Ok => "Ok",
                HitResult.Meh => "Meh",
                HitResult.Miss => "Miss",
                _ => ""
            };
        }

        private Color GetColorForResult(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => new Color(255, 255, 100),
                HitResult.Great => new Color(100, 255, 100),
                HitResult.Good => new Color(100, 200, 255),
                HitResult.Ok => new Color(200, 150, 255),
                HitResult.Meh => new Color(200, 200, 200),
                HitResult.Miss => new Color(255, 100, 100),
                _ => Color.White
            };
        }
    }
}
