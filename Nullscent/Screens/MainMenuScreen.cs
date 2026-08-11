#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nullscent.Config;
using Nullscent.Core;
using Nullscent.UI;
using System;
using System.Collections.Generic;

namespace Nullscent.Screens
{
    /// <summary>
    /// Menú principal del juego con navegación por botones en arco,
    /// fondo de partículas neon interactivas y pulsación musical.
    /// Opciones: Play, Settings, Exit
    /// </summary>
    public class MainMenuScreen : IGameScreen
    {
        private readonly Game1 _game;
        private readonly InputManager _inputManager;
        private readonly TrueTypeFontRenderer _fontRenderer;

        private readonly List<MenuButton> _buttons = new();
        private readonly List<float> _buttonHoverProgress = new();
        private int _selectedButtonIndex = 0;
        private readonly Color _dimTextColor = new(180, 180, 200);

        private KeyboardState _previousKeyState;
        private MouseState _prevMouseState;

        // Estructura de partícula para fondo premium
        private struct MenuParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Size;
            public float Alpha;
            public Color Color;
        }
        private readonly List<MenuParticle> _particles = new();
        private readonly Random _random = new();

        private bool _showExitConfirmation = false;
        private float _exitOverlayAlpha = 0f;
        private float _entranceProgress = 0f;
        private float EaseOutCubic(float x) => 1f - (float)Math.Pow(1f - x, 3);

        public MainMenuScreen(Game1 game, InputManager inputManager, TrueTypeFontRenderer fontRenderer)
        {
            _game = game;
            _inputManager = inputManager;
            _fontRenderer = fontRenderer;
        }

        public void Initialize()
        {
            Console.WriteLine("[MainMenuScreen] Initializing main menu...");

            _buttons.Clear();
            _buttons.Add(new MenuButton("PLAY", Rectangle.Empty, OnPlayClicked));
            _buttons.Add(new MenuButton("SETTINGS", Rectangle.Empty, OnSettingsClicked));
            _buttons.Add(new MenuButton("EXIT", Rectangle.Empty, OnExitClicked));

            _buttonHoverProgress.Clear();
            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttonHoverProgress.Add(0f);
            }

            _selectedButtonIndex = 0;
            _previousKeyState = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();
            _entranceProgress = 0f;

            // Inicializar partículas de fondo flotantes
            _particles.Clear();
            int particleCount = 45;
            Color[] neonColors = new[] {
                new Color(255, 102, 171), // Hot pink
                new Color(186, 85, 211),  // Medium orchid
                new Color(138, 43, 226),  // Blue violet
                new Color(0, 191, 255)    // Deep sky blue
            };

            for (int i = 0; i < particleCount; i++)
            {
                _particles.Add(new MenuParticle
                {
                    Position = new Vector2(_random.Next(0, _game.WindowWidth), _random.Next(0, _game.WindowHeight)),
                    Velocity = new Vector2((float)(_random.NextDouble() * 30 - 15), (float)(_random.NextDouble() * 30 - 15)),
                    Size = _random.Next(6, 22),
                    Alpha = (float)(_random.NextDouble() * 0.12f + 0.04f),
                    Color = neonColors[_random.Next(neonColors.Length)]
                });
            }

            Console.WriteLine("[MainMenuScreen] Main menu initialized");
        }

        public void Update(GameTime gameTime)
        {
            var currentKeyState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();

            if (!_game.IsActive)
            {
                _previousKeyState = currentKeyState;
                _prevMouseState = currentMouseState;
                return;
            }

            if (_showExitConfirmation)
            {
                _exitOverlayAlpha += (1.0f - _exitOverlayAlpha) * 0.15f;
                
                // Atajos de teclado para confirmación
                if (IsKeyPressed(currentKeyState, Keys.Escape) || IsKeyPressed(currentKeyState, Keys.N))
                {
                    _showExitConfirmation = false;
                }
                else if (IsKeyPressed(currentKeyState, Keys.Enter) || IsKeyPressed(currentKeyState, Keys.Y))
                {
                    _game.Exit();
                }
                
                // Detección de clics en YES / NO
                int centerX = _game.WindowWidth / 2;
                int centerY = _game.WindowHeight / 2;
                var yesRect = new Rectangle(centerX - 130, centerY + 15, 105, 34);
                var noRect = new Rectangle(centerX + 25, centerY + 15, 105, 34);
                
                if (currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                {
                    if (yesRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        _game.Exit();
                        return;
                    }
                    else if (noRect.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        _showExitConfirmation = false;
                    }
                }
                
                _previousKeyState = currentKeyState;
                _prevMouseState = currentMouseState;
                return;
            }

            _exitOverlayAlpha += (0f - _exitOverlayAlpha) * 0.15f;

            _entranceProgress = Math.Min(1.0f, _entranceProgress + (float)gameTime.ElapsedGameTime.TotalSeconds * 2.5f);

            // Actualizar partículas
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                p.Position += p.Velocity * dt;

                // Envolver partículas al salir de pantalla
                if (p.Position.X < -50) p.Position.X = _game.WindowWidth + 50;
                if (p.Position.X > _game.WindowWidth + 50) p.Position.X = -50;
                if (p.Position.Y < -50) p.Position.Y = _game.WindowHeight + 50;
                if (p.Position.Y > _game.WindowHeight + 50) p.Position.Y = -50;

                _particles[i] = p;
            }

            float ease = EaseOutCubic(_entranceProgress);
            int rightOffset = (int)(200f * (1f - ease));

            // Actualizar posiciones de los botones en base a resolución y arco
            int panelCenterX = (int)(_game.WindowWidth * 0.72f) + rightOffset;
            int baseHeight = 70;
            int baseWidth = 320;

            for (int i = 0; i < _buttons.Count; i++)
            {
                float angle = (i - 1) * 0.35f;
                int arcOffsetX = (int)(Math.Cos(angle) * 45.0f);
                int x = panelCenterX - baseWidth / 2 + arcOffsetX;
                int y = _game.WindowHeight / 2 - 120 + i * 90;

                _buttons[i].Bounds = new Rectangle(x, y, baseWidth, baseHeight);
            }

            // Interactividad de mouse
            if (!_game.SettingsOverlay.IsOpen)
            {
                for (int i = 0; i < _buttons.Count; i++)
                {
                    var button = _buttons[i];
                    float progress = _buttonHoverProgress[i];
                    // Ajustar rectángulo de click considerando la animación de hover (desplazamiento a la izquierda)
                    int xAnim = button.Bounds.X - (int)(progress * 25);
                    var activeBounds = new Rectangle(xAnim, button.Bounds.Y, button.Bounds.Width, button.Bounds.Height);

                    if (activeBounds.Contains(currentMouseState.X, currentMouseState.Y))
                    {
                        if (_selectedButtonIndex != i)
                        {
                            _selectedButtonIndex = i;
                            // Opcional: Sonido de hover
                        }

                        if (currentMouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
                        {
                            button.OnClick?.Invoke();
                            _previousKeyState = currentKeyState;
                            _prevMouseState = currentMouseState;
                            return;
                        }
                    }
                }

                // Navegación con teclado
                if (IsKeyPressed(currentKeyState, Keys.Down) || IsKeyPressed(currentKeyState, Keys.S))
                {
                    _selectedButtonIndex = (_selectedButtonIndex + 1) % _buttons.Count;
                }

                if (IsKeyPressed(currentKeyState, Keys.Up) || IsKeyPressed(currentKeyState, Keys.W))
                {
                    _selectedButtonIndex--;
                    if (_selectedButtonIndex < 0)
                        _selectedButtonIndex = _buttons.Count - 1;
                }

                if (IsKeyPressed(currentKeyState, Keys.Enter) || IsKeyPressed(currentKeyState, Keys.Space))
                {
                    _buttons[_selectedButtonIndex].OnClick?.Invoke();
                }

                if (IsKeyPressed(currentKeyState, Keys.Escape))
                {
                    _showExitConfirmation = true;
                }
            }

            // Interpolación de animación hover suave
            for (int i = 0; i < _buttons.Count; i++)
            {
                float target = (i == _selectedButtonIndex) ? 1f : 0f;
                _buttonHoverProgress[i] += (target - _buttonHoverProgress[i]) * 0.15f;
            }

            _previousKeyState = currentKeyState;
            _prevMouseState = currentMouseState;

            _inputManager.Update(gameTime.TotalGameTime.TotalMilliseconds);
        }

        public void Draw(GameTime gameTime)
        {
            var spriteBatch = _game.SpriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // Fondo negro / azul profundo
            _fontRenderer.DrawBox(new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight), new Color(10, 10, 15));

            // Dibujar partículas neon flotantes
            foreach (var p in _particles)
            {
                var rect = new Rectangle((int)p.Position.X, (int)p.Position.Y, (int)p.Size, (int)p.Size);
                _fontRenderer.DrawBox(rect, p.Color * p.Alpha);
                _fontRenderer.DrawBoxBorder(rect, p.Color * (p.Alpha * 1.5f), 1);
            }

            // Calcular pulsación basada en tiempo (ritmo del menú)
            float pulse = 1.0f + 0.04f * (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 3.5 * Math.PI);
            float ease = EaseOutCubic(_entranceProgress);
            int leftOffset = (int)(-200f * (1f - ease));

            // LOGO CENTRAL (LADO IZQUIERDO)
            Vector2 logoCenter = new Vector2(_game.WindowWidth * 0.28f + leftOffset, _game.WindowHeight / 2 - 20);

            // Cajas neón decorativas concéntricas en el logo
            var ring1 = new Rectangle((int)logoCenter.X - 160, (int)logoCenter.Y - 160, 320, 320);
            var ring2 = new Rectangle((int)logoCenter.X - 120, (int)logoCenter.Y - 120, 240, 240);
            
            _fontRenderer.DrawBoxBorder(ring1, Color.MediumVioletRed * 0.12f * pulse, 2);
            _fontRenderer.DrawBoxBorder(ring2, Color.DeepSkyBlue * 0.22f * (2.0f - pulse), 3);

            // Título principal con brillo neón
            _fontRenderer.SetFontSize(80);
            _fontRenderer.DrawTextCentered("NULLSCENT", logoCenter, Color.DeepSkyBlue * 0.15f, 1.0f); // Sombra de brillo
            _fontRenderer.DrawTextCentered("NULLSCENT", logoCenter - new Vector2(2, 2), Color.HotPink, pulse);

            // Subtítulo
            _fontRenderer.SetFontSize(22);
            _fontRenderer.DrawTextCentered("osu!mania Practice Client", logoCenter + new Vector2(0, 75), Color.Gray * 0.8f, 1.0f);

            // BOTONES EN ARCO (LADO DERECHO)
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                float progress = _buttonHoverProgress[i];

                // El botón se desliza hacia la izquierda y se expande en hover
                int xOffset = (int)(progress * -30);
                int padding = (int)(progress * 4);
                var animBounds = new Rectangle(
                    button.Bounds.X + xOffset - padding,
                    button.Bounds.Y - padding,
                    button.Bounds.Width + padding * 2,
                    button.Bounds.Height + padding * 2
                );

                // Colores estilizados
                Color bgNormal = new Color(25, 25, 35, 180);
                Color bgHover = new Color(255, 102, 171, 55); // Magenta translúcido en hover
                Color bgColor = Color.Lerp(bgNormal, bgHover, progress);

                Color borderNormal = Color.White * 0.08f;
                Color borderHover = Color.HotPink;
                Color borderColor = Color.Lerp(borderNormal, borderHover, progress);

                Color textNormal = _dimTextColor;
                Color textHover = Color.White;
                Color textColor = Color.Lerp(textNormal, textHover, progress);

                // Fondo y borde del botón
                _fontRenderer.DrawBox(animBounds, bgColor);
                _fontRenderer.DrawBoxBorder(animBounds, borderColor, progress > 0.5f ? 3 : 1);

                // Dibujar texto
                _fontRenderer.SetFontSize(28);
                var textSize = _fontRenderer.MeasureText(button.Text, 1.0f);
                var textPos = new Vector2(
                    animBounds.X + (animBounds.Width - textSize.X) / 2,
                    animBounds.Y + (animBounds.Height - textSize.Y) / 2
                );

                if (progress > 0.1f)
                {
                    // Pequeña sombra neón de fondo para el texto hovered
                    _fontRenderer.DrawText(button.Text, textPos + new Vector2(1, 1), Color.HotPink * (progress * 0.5f), 1.0f);
                }
                _fontRenderer.DrawText(button.Text, textPos, textColor, 1.0f);
            }

            // Pie de página de controles
            _fontRenderer.SetFontSize(16);
            string footer = "UP/DOWN/WS: Navigate  |  ENTER: Select  |  ESC: Exit";
            _fontRenderer.DrawTextCentered(footer, new Vector2(_game.WindowWidth / 2, _game.WindowHeight - 35), new Color(100, 100, 110), 1.0f);

            // Dibujar diálogo de confirmación de salida
            if (_exitOverlayAlpha > 0.01f)
            {
                // Fondo atenuado
                _fontRenderer.DrawBox(new Rectangle(0, 0, _game.WindowWidth, _game.WindowHeight), Color.Black * 0.6f * _exitOverlayAlpha);
                
                int dialogW = 380;
                int dialogH = 150;
                int centerX = _game.WindowWidth / 2;
                int centerY = _game.WindowHeight / 2;
                var dialogRect = new Rectangle(centerX - dialogW / 2, centerY - dialogH / 2, dialogW, dialogH);
                
                // Marco
                _fontRenderer.DrawBox(dialogRect, new Color(20, 20, 25, 240) * _exitOverlayAlpha);
                _fontRenderer.DrawBoxBorder(dialogRect, Color.HotPink * _exitOverlayAlpha, 2);
                
                // Texto
                _fontRenderer.SetFontSize(16);
                _fontRenderer.DrawTextCentered("REALLY EXIT NULLSCENT?", centerX, centerY - 30, Color.White * _exitOverlayAlpha);
                
                // Botón YES
                var yesRect = new Rectangle(centerX - 130, centerY + 15, 105, 34);
                var mouseState = Mouse.GetState();
                bool yesHover = yesRect.Contains(mouseState.X, mouseState.Y);
                Color yesBg = yesHover ? Color.HotPink * 0.3f : new Color(35, 35, 45) * 0.7f;
                _fontRenderer.DrawBox(yesRect, yesBg * _exitOverlayAlpha);
                _fontRenderer.DrawBoxBorder(yesRect, (yesHover ? Color.HotPink : Color.Gray) * _exitOverlayAlpha, 1);
                _fontRenderer.DrawTextCentered("YES (Y)", centerX - 77, centerY + 30, Color.White * _exitOverlayAlpha);
                
                // Botón NO
                var noRect = new Rectangle(centerX + 25, centerY + 15, 105, 34);
                bool noHover = noRect.Contains(mouseState.X, mouseState.Y);
                Color noBg = noHover ? Color.HotPink * 0.3f : new Color(35, 35, 45) * 0.7f;
                _fontRenderer.DrawBox(noRect, noBg * _exitOverlayAlpha);
                _fontRenderer.DrawBoxBorder(noRect, (noHover ? Color.HotPink : Color.Gray) * _exitOverlayAlpha, 1);
                _fontRenderer.DrawTextCentered("NO (N)", centerX + 77, centerY + 30, Color.White * _exitOverlayAlpha);
            }

            spriteBatch.End();
        }

        public void Cleanup()
        {
            Console.WriteLine("[MainMenuScreen] Cleaning up main menu");
            _buttons.Clear();
            _particles.Clear();
        }

        private bool IsKeyPressed(KeyboardState currentState, Keys key)
        {
            return currentState.IsKeyDown(key) && _previousKeyState.IsKeyUp(key);
        }

        private void OnPlayClicked()
        {
            Console.WriteLine("[MainMenuScreen] Play button clicked");
            var songSelect = new Nullscent.UI.SongSelectScreen(
                _game,
                _game.GraphicsDevice,
                _game.SpriteBatch,
                _game.StateManager,
                _game.AudioEngine,
                _inputManager,
                _game.HitSoundPlayer,
                _game.Settings
            );
            _game.StateManager.ChangeState(GameState.SongSelect, songSelect);
        }

        private void OnSettingsClicked()
        {
            Console.WriteLine("[MainMenuScreen] Settings button clicked");
            _game.ToggleSettingsOverlay();
        }

        private void OnExitClicked()
        {
            Console.WriteLine("[MainMenuScreen] Exit button clicked");
            _showExitConfirmation = true;
        }
    }

    /// <summary>
    /// Clase auxiliar para representar un botón del menú.
    /// </summary>
    internal class MenuButton
    {
        public string Text { get; }
        public Rectangle Bounds { get; set; }
        public Action? OnClick { get; }

        public MenuButton(string text, Rectangle bounds, Action? onClick = null)
        {
            Text = text;
            Bounds = bounds;
            OnClick = onClick;
        }
    }
}
