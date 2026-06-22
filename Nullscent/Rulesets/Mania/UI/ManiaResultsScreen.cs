#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.Gameplay;
using Nullscent.Rulesets.Mania.Beatmaps;
using Nullscent.Rulesets.Mania.Mods;
using Nullscent.Rulesets.Mania.Scoring;
using Nullscent.Screens;
using Nullscent.UI;
using System.Collections.Generic;

namespace Nullscent.Rulesets.Mania.UI
{
    public class ManiaResultsScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly GameStateManager _stateManager;
        private readonly AudioEngine _audioEngine;
        private readonly InputManager _inputManager;
        private readonly GameSettings _settings;

        private readonly ManiaScoreProcessor _scoreProcessor;
        private readonly Beatmap.Beatmap _beatmap;
        private readonly ManiaBeatmap _maniaBeatmap;
        private readonly List<ManiaMod> _activeMods;

        private TrueTypeFontRenderer? _fontRenderer;
        private KeyboardState _previousKeyboardState;

        public ManiaResultsScreen(
            Game1 game,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            GameStateManager stateManager,
            AudioEngine audioEngine,
            InputManager inputManager,
            GameSettings settings,
            ManiaScoreProcessor scoreProcessor,
            Beatmap.Beatmap beatmap,
            ManiaBeatmap maniaBeatmap,
            List<ManiaMod> activeMods)
        {
            _game = game;
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _stateManager = stateManager;
            _audioEngine = audioEngine;
            _inputManager = inputManager;
            _settings = settings;
            _scoreProcessor = scoreProcessor;
            _beatmap = beatmap;
            _maniaBeatmap = maniaBeatmap;
            _activeMods = activeMods;
        }

        public void Initialize()
        {
            _fontRenderer = _game.FontRenderer;
            _previousKeyboardState = Keyboard.GetState();
        }

        public void Update(GameTime gameTime)
        {
            var currentKeyboardState = Keyboard.GetState();

            if (IsKeyPressed(currentKeyboardState, Keys.Escape) || IsKeyPressed(currentKeyboardState, Keys.Enter))
            {
                ReturnToSongSelect();
            }

            if (IsKeyPressed(currentKeyboardState, Keys.R))
            {
                RetryBeatmap();
            }

            _previousKeyboardState = currentKeyboardState;
        }

        public void Draw(GameTime gameTime)
        {
            if (_fontRenderer == null) return;

            _graphicsDevice.Clear(new Color(15, 15, 20));

            _spriteBatch.Begin();

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            _fontRenderer.SetFontSize(48);
            _fontRenderer.DrawTextCentered("RESULTS", screenWidth / 2, 40, new Color(255, 102, 171), 1.0f);

            _fontRenderer.SetFontSize(64);
            _fontRenderer.DrawTextCentered($"{_scoreProcessor.Score:N0}", screenWidth / 2, screenHeight / 2 - 80, Color.White, 1.0f);

            _fontRenderer.SetFontSize(36);
            _fontRenderer.DrawTextCentered($"{_scoreProcessor.AccuracyPercent:F2}%", screenWidth / 2, screenHeight / 2, new Color(255, 102, 171), 1.0f);

            _fontRenderer.SetFontSize(24);
            _fontRenderer.DrawTextCentered($"Max Combo: {_scoreProcessor.MaxCombo}x", screenWidth / 2, screenHeight / 2 + 60, Color.Cyan, 1.0f);

            _fontRenderer.SetFontSize(128);
            _fontRenderer.DrawText(_scoreProcessor.Rank.ToString(), screenWidth - 200, screenHeight / 2 - 64, Color.Gold, 1.0f);

            _fontRenderer.SetFontSize(18);
            _fontRenderer.DrawTextCentered($"{_beatmap.Metadata.Title} - {_beatmap.Metadata.Artist}", screenWidth / 2, screenHeight - 100, Color.White, 1.0f);
            _fontRenderer.DrawTextCentered($"[{_beatmap.Metadata.Version}] {_maniaBeatmap.KeyCount}K", screenWidth / 2, screenHeight - 75, new Color(180, 180, 200), 1.0f);

            _fontRenderer.SetFontSize(16);
            _fontRenderer.DrawTextCentered("Enter/Esc: Return to Song Select  |  R: Retry", screenWidth / 2, screenHeight - 30, new Color(180, 180, 200), 1.0f);

            _spriteBatch.End();
        }

        private void ReturnToSongSelect()
        {
            var songSelect = new SongSelectScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                new HitSoundPlayer(),
                _settings
            );

            _stateManager.ChangeState(GameState.SongSelect, songSelect);
        }

        private void RetryBeatmap()
        {
            var gameplay = new ManiaGameplayScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                new HitSoundPlayer(),
                _settings,
                _beatmap,
                _activeMods
            );

            _stateManager.ChangeState(GameState.Gameplay, gameplay);
        }

        private bool IsKeyPressed(KeyboardState currentState, Keys key)
        {
            return currentState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        public void Cleanup()
        {
            _audioEngine.Stop();
        }
    }
}
