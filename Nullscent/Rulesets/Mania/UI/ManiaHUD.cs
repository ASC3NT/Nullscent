#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Configuration;
using Nullscent.Rulesets.Mania.Mods;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.UI;
using Nullscent.Skin;
using System;
using System.Collections.Generic;

namespace Nullscent.Rulesets.Mania.UI
{
    public class ManiaHUD : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly TrueTypeFontRenderer _fontRenderer;
        private readonly ManiaConfig _config;
        private readonly Texture2D _pixel;

        public ManiaHUD(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, TrueTypeFontRenderer fontRenderer, ManiaConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _fontRenderer = fontRenderer;
            _config = config;

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void Draw(ManiaScoreProcessor scoreProcessor, Beatmap.Beatmap beatmap, double currentTime, List<ManiaMod> activeMods, SkinManager? skinManager)
        {
            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            _fontRenderer.SetFontSize(24);

            if (_config.ShowScore)
            {
                string scoreStr = scoreProcessor.Score.ToString("D8");
                // Position it at top right: screenWidth - textureWidth
                bool drawn = skinManager != null && skinManager.DrawNumber(_spriteBatch, scoreStr, new Vector2(screenWidth - 170, 30), "score", 0.75f, 1.0f, centered: false, spacing: -2f);
                if (!drawn)
                {
                    _fontRenderer.DrawTextRight($"Score: {scoreProcessor.Score:N0}", screenWidth - 20, 20, Color.White, 1.0f);
                }
            }

            if (_config.ShowAccuracy)
            {
                string accStr = $"{scoreProcessor.AccuracyPercent:F2}%";
                bool drawn = skinManager != null && skinManager.DrawNumber(_spriteBatch, accStr, new Vector2(screenWidth - 140, 60), "score", 0.6f, 1.0f, centered: false, spacing: -2f);
                if (!drawn)
                {
                    _fontRenderer.DrawTextRight($"Accuracy: {scoreProcessor.AccuracyPercent:F2}%", screenWidth - 20, 50, Color.Cyan, 1.0f);
                }
            }

            if (_config.ShowCombo)
            {
                string comboStr = $"{scoreProcessor.Combo}x";
                bool drawn = skinManager != null && skinManager.DrawNumber(_spriteBatch, comboStr, new Vector2(screenWidth - 140, 90), "score", 0.6f, 1.0f, centered: false, spacing: -2f);
                if (!drawn)
                {
                    _fontRenderer.DrawTextRight($"Combo: {scoreProcessor.Combo}x", screenWidth - 20, 80, Color.Yellow, 1.0f);
                }
            }

            if (_config.ShowHealthBar)
            {
                DrawHealthBar(screenWidth, screenHeight, scoreProcessor.Health, skinManager);
            }

            _spriteBatch.End();
        }

        private void DrawHealthBar(int screenWidth, int screenHeight, double health, SkinManager? skinManager)
        {
            Texture2D? bgTex = skinManager?.GetScorebarBg();
            Texture2D? colTex = skinManager?.GetScorebarColour();

            if (bgTex != null && colTex != null)
            {
                // Top-left corner positioning (standard osu! look)
                int x = 20;
                int y = 20;

                Vector2 bgSize = skinManager!.GetLogicalSize(bgTex);
                Vector2 colSize = skinManager.GetLogicalSize(colTex);

                // Draw background
                _spriteBatch.Draw(bgTex, new Rectangle(x, y, (int)bgSize.X, (int)bgSize.Y), Color.White);

                // Draw color fill cropped by health percentage (from left to right)
                int fillWidth = (int)(colTex.Width * Math.Clamp(health, 0.0, 1.0));
                if (fillWidth > 0)
                {
                    var srcRect = new Rectangle(0, 0, fillWidth, colTex.Height);
                    _spriteBatch.Draw(colTex, new Rectangle(x, y, (int)(colSize.X * health), (int)colSize.Y), srcRect, Color.White);
                }
            }
            else
            {
                int barWidth = 200;
                int barHeight = 20;
                int x = screenWidth - barWidth - 20;
                int y = screenHeight - 40;

                var bgRect = new Rectangle(x, y, barWidth, barHeight);
                _spriteBatch.Draw(_pixel, bgRect, new Color(40, 40, 40));

                var healthRect = new Rectangle(x, y, (int)(barWidth * health), barHeight);
                Color healthColor = health > 0.5 ? Color.Green : (health > 0.2 ? Color.Yellow : Color.Red);
                _spriteBatch.Draw(_pixel, healthRect, healthColor);
            }
        }

        public void Dispose()
        {
            _pixel?.Dispose();
        }
    }
}
