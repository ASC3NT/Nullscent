#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Audio;
using Nullscent.Beatmap;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.Gameplay;
using System;

namespace Nullscent.UI
{
    /// <summary>
    /// Pantalla de resultados post-gameplay.
    /// Muestra score, accuracy, combo, juicios y grade con animaciones estilo osu!lazer.
    /// </summary>
    public class ResultsScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly GameStateManager _stateManager;
        private readonly AudioEngine _audioEngine;
        private readonly InputManager _inputManager;
        private readonly HitSoundPlayer _hitSoundPlayer;
        private readonly GameSettings _settings;

        private readonly ScoreEngine? _scoreEngine;
        private readonly Beatmap.Beatmap? _beatmap;

        private TrueTypeFontRenderer? _fontRenderer;
        private KeyboardState _previousKeyboardState;
        private int _selectedOption;

        // Colors
        private readonly Color _backgroundColor = new(15, 15, 25);
        private readonly Color _accentColor = new(255, 102, 171);
        private readonly Color _textColor = Color.White;
        private readonly Color _dimTextColor = new(180, 180, 200);

        // Animation
        private double _animationTime;

        public ResultsScreen(
            Game game,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            GameStateManager stateManager,
            AudioEngine audioEngine,
            InputManager inputManager,
            HitSoundPlayer hitSoundPlayer,
            GameSettings settings,
            ScoreEngine? scoreEngine,
            Beatmap.Beatmap? beatmap)
        {
            _game = (Game1)game;
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _stateManager = stateManager;
            _audioEngine = audioEngine;
            _inputManager = inputManager;
            _hitSoundPlayer = hitSoundPlayer;
            _settings = settings;
            _scoreEngine = scoreEngine;
            _beatmap = beatmap;
        }

        public void Initialize()
        {
            Console.WriteLine("[ResultsScreen] Initialized");

            _fontRenderer = _game.FontRenderer;
            _animationTime = 0;
            _selectedOption = 0;

            if (_scoreEngine != null)
            {
                Console.WriteLine("[ResultsScreen] Results:");
                Console.WriteLine(_scoreEngine.GetScoreSummary());
            }
        }

        public void Update(GameTime gameTime)
        {
            _animationTime += gameTime.ElapsedGameTime.TotalSeconds;

            var currentKeyboardState = Keyboard.GetState();

            // Navegación
            if (IsKeyPressed(currentKeyboardState, Keys.Left))
                _selectedOption = 0; // Retry

            if (IsKeyPressed(currentKeyboardState, Keys.Right))
                _selectedOption = 1; // Back

            // Confirmar
            if (IsKeyPressed(currentKeyboardState, Keys.Enter))
            {
                if (_selectedOption == 0)
                    RetryBeatmap();
                else
                    BackToSongSelect();
            }

            // Escape para volver
            if (IsKeyPressed(currentKeyboardState, Keys.Escape))
                BackToSongSelect();

            _previousKeyboardState = currentKeyboardState;
        }

        private void RetryBeatmap()
        {
            if (_beatmap == null)
            {
                Console.WriteLine("[ResultsScreen] Cannot retry: no beatmap data");
                BackToSongSelect();
                return;
            }

            Console.WriteLine("[ResultsScreen] Retrying beatmap...");

            var gameplayScreen = new GameplayScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _hitSoundPlayer,
                _settings,
                _beatmap
            );

            _stateManager.ChangeState(GameState.Gameplay, gameplayScreen);
        }

        private void BackToSongSelect()
        {
            Console.WriteLine("[ResultsScreen] Returning to song select...");

            var songSelectScreen = new SongSelectScreen(
                _game,
                _graphicsDevice,
                _spriteBatch,
                _stateManager,
                _audioEngine,
                _inputManager,
                _hitSoundPlayer,
                _settings
            );

            _stateManager.ChangeState(GameState.SongSelect, songSelectScreen);
        }

        public void Draw(GameTime gameTime)
        {
            _graphicsDevice.Clear(_backgroundColor);

            if (_fontRenderer == null || _scoreEngine == null)
                return;

            _spriteBatch.Begin();

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;
            int centerX = screenWidth / 2;

            // Header: Beatmap info
            DrawHeader(centerX, screenHeight);

            // Main content: Score, accuracy, grade
            DrawMainStats(centerX, screenHeight);

            // Judgement breakdown
            DrawJudgementBreakdown(centerX, screenHeight);

            // Footer: Buttons
            DrawFooter(centerX, screenHeight);

            _spriteBatch.End();
        }

        private void DrawHeader(int centerX, int screenHeight)
        {
            if (_fontRenderer == null || _beatmap == null)
                return;

            int headerY = 30;

            // Beatmap title
            string title = $"{_beatmap.Metadata.Artist} - {_beatmap.Metadata.Title}";
            _fontRenderer.DrawTextCentered(title, centerX, headerY, _textColor);

            // Difficulty
            string difficulty = $"[{_beatmap.Metadata.Version}] - {_beatmap.KeyCount}K";
            _fontRenderer.DrawTextCentered(difficulty, centerX, headerY + 30, _dimTextColor);
        }

        private void DrawMainStats(int centerX, int screenHeight)
        {
            if (_fontRenderer == null || _scoreEngine == null)
                return;

            int statsY = 120;

            // Grade (grande, centrado)
            string grade = _scoreEngine.Grade;
            Color gradeColor = GetGradeColor(grade);
            _fontRenderer.DrawTextCentered(grade, centerX, statsY, gradeColor);

            // Score
            statsY += 120;
            _fontRenderer.DrawTextCentered($"{_scoreEngine.Score:N0}", centerX, statsY, _textColor);

            // Accuracy
            statsY += 60;
            _fontRenderer.DrawTextCentered($"{_scoreEngine.AccuracyPercent:F2}%", centerX, statsY, _accentColor);

            // Combo
            statsY += 50;
            string comboText = $"{_scoreEngine.MaxCombo}x / {_scoreEngine.MaxPossibleCombo}x";
            _fontRenderer.DrawTextCentered(comboText, centerX, statsY, _dimTextColor);
        }

        private void DrawJudgementBreakdown(int centerX, int screenHeight)
        {
            if (_fontRenderer == null || _scoreEngine == null)
                return;

            int breakdownY = screenHeight - 280;

            _fontRenderer.DrawTextCentered("Judgements", centerX, breakdownY, _textColor);
            breakdownY += 35;

            // Panel de juicios
            int panelWidth = 600;
            int panelHeight = 160;
            int panelX = centerX - panelWidth / 2;

            var panelRect = new Rectangle(panelX, breakdownY, panelWidth, panelHeight);
            _fontRenderer.DrawBox(panelRect, new Color(30, 30, 40, 200));
            _fontRenderer.DrawBoxBorder(panelRect, _accentColor * 0.5f, 2);

            // Dibujar cada juicio
            int col1X = panelX + 30;
            int col2X = panelX + 320;
            int rowY = breakdownY + 20;
            int rowSpacing = 28;

            DrawJudgementRow("MAX", _scoreEngine.JudgementCounts[Judgement.Max], new Color(255, 215, 0), col1X, rowY);
            DrawJudgementRow("300", _scoreEngine.JudgementCounts[Judgement.Perfect], new Color(100, 200, 255), col2X, rowY);

            rowY += rowSpacing;
            DrawJudgementRow("200", _scoreEngine.JudgementCounts[Judgement.Great], new Color(100, 255, 100), col1X, rowY);
            DrawJudgementRow("100", _scoreEngine.JudgementCounts[Judgement.Ok], new Color(150, 255, 150), col2X, rowY);

            rowY += rowSpacing;
            DrawJudgementRow("50", _scoreEngine.JudgementCounts[Judgement.Meh], new Color(255, 200, 100), col1X, rowY);
            DrawJudgementRow("MISS", _scoreEngine.JudgementCounts[Judgement.Miss], new Color(255, 100, 100), col2X, rowY);
        }

        private void DrawJudgementRow(string name, int count, Color color, int x, int y)
        {
            if (_fontRenderer == null)
                return;

            _fontRenderer.DrawText($"{name}:", x, y, color);
            _fontRenderer.DrawText($"{count}", x + 100, y, _textColor);
        }

        private void DrawFooter(int centerX, int screenHeight)
        {
            if (_fontRenderer == null)
                return;

            int footerY = screenHeight - 80;

            // Botones
            int buttonSpacing = 200;
            int retryX = centerX - buttonSpacing / 2;
            int backX = centerX + buttonSpacing / 2;

            // Retry
            Color retryColor = _selectedOption == 0 ? _accentColor : _dimTextColor;
            _fontRenderer.DrawTextCentered("Retry", retryX, footerY, retryColor);

            // Back
            Color backColor = _selectedOption == 1 ? _accentColor : _dimTextColor;
            _fontRenderer.DrawTextCentered("Back", backX, footerY, backColor);

            // Hint
            _fontRenderer.DrawTextCentered("Arrow keys to select  |  Enter to confirm  |  Esc to exit", 
                centerX, footerY + 40, _dimTextColor * 0.6f);
        }

        private Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "SS" => new Color(255, 215, 0), // Gold
                "S" => new Color(255, 215, 0),
                "A" => new Color(100, 255, 100), // Green
                "B" => new Color(100, 200, 255), // Blue
                "C" => new Color(200, 100, 255), // Purple
                "D" => new Color(255, 100, 100), // Red
                _ => Color.White
            };
        }

        private bool IsKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        public void Cleanup()
        {
            Console.WriteLine("[ResultsScreen] Cleanup");
        }
    }
}
