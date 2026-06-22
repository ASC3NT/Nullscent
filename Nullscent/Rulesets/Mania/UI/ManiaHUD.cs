#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nullscent.Rulesets.Mania.Configuration;
using Nullscent.Rulesets.Mania.Mods;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.UI;
using System.Collections.Generic;

namespace Nullscent.Rulesets.Mania.UI
{
    public class ManiaHUD
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly TrueTypeFontRenderer _fontRenderer;
        private readonly ManiaConfig _config;

        public ManiaHUD(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, TrueTypeFontRenderer fontRenderer, ManiaConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _fontRenderer = fontRenderer;
            _config = config;
        }

        public void Draw(GameTime gameTime, ManiaScoreProcessor scoreProcessor, Beatmap.Beatmap beatmap, double currentTime, List<ManiaMod> activeMods)
        {
            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            _spriteBatch.Begin();

            _fontRenderer.SetFontSize(24);

            if (_config.ShowScore)
            {
                _fontRenderer.DrawTextRight($"Score: {scoreProcessor.Score:N0}", screenWidth - 20, 20, Color.White, 1.0f);
            }

            if (_config.ShowAccuracy)
            {
                _fontRenderer.DrawTextRight($"Accuracy: {scoreProcessor.AccuracyPercent:F2}%", screenWidth - 20, 50, Color.Cyan, 1.0f);
            }

            if (_config.ShowCombo)
            {
                _fontRenderer.DrawTextRight($"Combo: {scoreProcessor.Combo}x", screenWidth - 20, 80, Color.Yellow, 1.0f);
            }

            if (_config.ShowHealthBar)
            {
                DrawHealthBar(screenWidth, screenHeight, scoreProcessor.Health);
            }

            _spriteBatch.End();
        }

        private void DrawHealthBar(int screenWidth, int screenHeight, double health)
        {
            var pixel = new Texture2D(_graphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            int barWidth = 200;
            int barHeight = 20;
            int x = screenWidth - barWidth - 20;
            int y = screenHeight - 40;

            var bgRect = new Rectangle(x, y, barWidth, barHeight);
            _spriteBatch.Draw(pixel, bgRect, new Color(40, 40, 40));

            var healthRect = new Rectangle(x, y, (int)(barWidth * health), barHeight);
            Color healthColor = health > 0.5 ? Color.Green : (health > 0.2 ? Color.Yellow : Color.Red);
            _spriteBatch.Draw(pixel, healthRect, healthColor);
        }
    }
}
