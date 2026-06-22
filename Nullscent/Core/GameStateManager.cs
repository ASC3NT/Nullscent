#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace Nullscent.Core
{
    /// <summary>
    /// Tipo de estado/pantalla del juego.
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// Menú principal.
        /// </summary>
        MainMenu,

        /// <summary>
        /// Pantalla de configuración.
        /// </summary>
        Settings,

        /// <summary>
        /// Pantalla de song select (selección de beatmap).
        /// </summary>
        SongSelect,

        /// <summary>
        /// Pantalla de gameplay (jugando un beatmap).
        /// </summary>
        Gameplay,

        /// <summary>
        /// Pantalla de resultados (post-gameplay).
        /// </summary>
        Results,

        /// <summary>
        /// Menú de pausa (overlay durante gameplay).
        /// </summary>
        Paused
    }

    /// <summary>
    /// Interfaz base para todas las pantallas/estados del juego.
    /// Cada pantalla implementa su propia lógica de Update y Draw.
    /// </summary>
    public interface IGameScreen
    {
        /// <summary>
        /// Inicializa la pantalla (llamado al entrar en este estado).
        /// </summary>
        void Initialize();

        /// <summary>
        /// Actualiza la lógica de la pantalla.
        /// </summary>
        /// <param name="gameTime">Información de timing del frame</param>
        void Update(GameTime gameTime);

        /// <summary>
        /// Dibuja la pantalla.
        /// </summary>
        /// <param name="gameTime">Información de timing del frame</param>
        void Draw(GameTime gameTime);

        /// <summary>
        /// Limpia recursos al salir de la pantalla.
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// Gestiona las transiciones entre estados/pantallas del juego.
    /// Mantiene referencia al estado actual y facilita cambios de pantalla.
    /// Soporta stack de pantallas para overlays (como pause menu).
    /// </summary>
    public class GameStateManager
    {
        private IGameScreen? _currentScreen;
        private GameState _currentState;
        private readonly System.Collections.Generic.Stack<IGameScreen> _screenStack = new();

        /// <summary>
        /// Estado actual del juego.
        /// </summary>
        public GameState CurrentState
        {
            get => _currentState;
            private set => _currentState = value;
        }

        /// <summary>
        /// Pantalla actual activa.
        /// </summary>
        public IGameScreen? CurrentScreen => _currentScreen;

        /// <summary>
        /// Evento disparado cuando el estado cambia.
        /// Parámetros: (previousState, newState)
        /// </summary>
        public event Action<GameState, GameState>? OnStateChanged;

        /// <summary>
        /// Cambia al estado especificado con una nueva pantalla.
        /// </summary>
        /// <param name="newState">Nuevo estado</param>
        /// <param name="newScreen">Nueva pantalla a activar</param>
        public void ChangeState(GameState newState, IGameScreen newScreen)
        {
            GameState previousState = _currentState;

            // Limpiar pantalla anterior
            _currentScreen?.Cleanup();

            // Cambiar a nueva pantalla
            _currentScreen = newScreen;
            _currentState = newState;
            _currentScreen.Initialize();

            OnStateChanged?.Invoke(previousState, newState);

            Console.WriteLine($"[GameStateManager] State changed: {previousState} -> {newState}");
        }

        /// <summary>
        /// Push una pantalla al stack (para overlays como settings, skin select).
        /// </summary>
        public void PushState(IGameScreen newScreen)
        {
            if (_currentScreen != null)
                _screenStack.Push(_currentScreen);

            _currentScreen = newScreen;
            _currentScreen.Initialize();

            Console.WriteLine($"[GameStateManager] Pushed new screen to stack (depth: {_screenStack.Count})");
        }

        /// <summary>
        /// Pop la pantalla actual y vuelve a la anterior.
        /// </summary>
        public void PopState()
        {
            _currentScreen?.Cleanup();

            if (_screenStack.Count > 0)
            {
                _currentScreen = _screenStack.Pop();
                Console.WriteLine($"[GameStateManager] Popped screen from stack (depth: {_screenStack.Count})");
            }
            else
            {
                _currentScreen = null;
                Console.WriteLine("[GameStateManager] Stack empty, no screen to return to");
            }
        }

        /// <summary>
        /// Actualiza la pantalla actual.
        /// </summary>
        /// <param name="gameTime">Información de timing del frame</param>
        public void Update(GameTime gameTime)
        {
            _currentScreen?.Update(gameTime);
        }

        /// <summary>
        /// Dibuja la pantalla actual.
        /// </summary>
        /// <param name="gameTime">Información de timing del frame</param>
        public void Draw(GameTime gameTime)
        {
            _currentScreen?.Draw(gameTime);
        }

        /// <summary>
        /// Limpia recursos del estado actual.
        /// </summary>
        public void Cleanup()
        {
            while (_screenStack.Count > 0)
            {
                var screen = _screenStack.Pop();
                screen?.Cleanup();
            }

            _currentScreen?.Cleanup();
            _currentScreen = null;
        }
    }
}
