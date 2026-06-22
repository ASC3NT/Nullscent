#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace Nullscent.Core
{
    /// <summary>
    /// Manages keyboard input with high-frequency polling for minimal latency.
    /// Provides KeyDown/KeyUp events with accurate audio clock timestamps.
    /// Supports keybind configuration by key count (1K-18K) following osu!mania standards.
    /// </summary>
    public class InputManager
    {
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;

        /// <summary>
        /// Keybinds actuales: diccionario de key count → array de teclas.
        /// Cada entrada representa las teclas asignadas a cada columna para ese key count.
        /// </summary>
        public Dictionary<int, Keys[]> Keybinds { get; private set; } = new();

        /// <summary>
        /// Evento disparado cuando una tecla es presionada.
        /// Parámetros: (columnIndex, currentTimeMs)
        /// </summary>
        public event Action<int, double>? OnKeyDown;

        /// <summary>
        /// Evento disparado cuando una tecla es soltada.
        /// Parámetros: (columnIndex, currentTimeMs)
        /// </summary>
        public event Action<int, double>? OnKeyUp;

        /// <summary>
        /// Key count activo actualmente (determina qué keybind set usar).
        /// </summary>
        public int ActiveKeyCount { get; set; } = 4;

        /// <summary>
        /// Inicializa el InputManager con keybinds predeterminados de osu!mania.
        /// </summary>
        public InputManager()
        {
            InitializeDefaultKeybinds();
        }

        /// <summary>
        /// Actualiza el estado del teclado y dispara eventos de input (llamar cada frame).
        /// </summary>
        /// <param name="currentTimeMs">Timestamp actual del audio clock en milisegundos</param>
        public void Update(double currentTimeMs)
        {
            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();

            // Obtener keybinds para el key count activo
            if (!Keybinds.TryGetValue(ActiveKeyCount, out Keys[]? keys))
                return;

            // Detectar cambios de estado para cada columna
            for (int i = 0; i < keys.Length && i < ActiveKeyCount; i++)
            {
                Keys key = keys[i];
                bool isDown = _currentKeyboardState.IsKeyDown(key);
                bool wasDown = _previousKeyboardState.IsKeyDown(key);

                // KeyDown event
                if (isDown && !wasDown)
                    OnKeyDown?.Invoke(i, currentTimeMs);

                // KeyUp event
                if (!isDown && wasDown)
                    OnKeyUp?.Invoke(i, currentTimeMs);
            }
        }

        /// <summary>
        /// Verifica si una columna específica está siendo presionada actualmente.
        /// </summary>
        /// <param name="columnIndex">Índice de la columna (0-based)</param>
        /// <returns>True si la tecla de esa columna está presionada</returns>
        public bool IsColumnPressed(int columnIndex)
        {
            if (!Keybinds.TryGetValue(ActiveKeyCount, out Keys[]? keys))
                return false;

            if (columnIndex < 0 || columnIndex >= keys.Length)
                return false;

            return _currentKeyboardState.IsKeyDown(keys[columnIndex]);
        }

        /// <summary>
        /// Verifica si una tecla específica fue presionada este frame (single press).
        /// </summary>
        public bool IsKeyPressed(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        /// <summary>
        /// Verifica si una tecla está siendo sostenida.
        /// </summary>
        public bool IsKeyDown(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key);
        }

        /// <summary>
        /// Asigna una tecla a una columna específica para un key count dado.
        /// </summary>
        /// <param name="keyCount">Key count (1-18)</param>
        /// <param name="columnIndex">Índice de la columna (0-based)</param>
        /// <param name="key">Tecla a asignar</param>
        public void SetKeybind(int keyCount, int columnIndex, Keys key)
        {
            if (!Keybinds.ContainsKey(keyCount))
                Keybinds[keyCount] = new Keys[keyCount];

            if (columnIndex >= 0 && columnIndex < keyCount)
                Keybinds[keyCount][columnIndex] = key;
        }

        /// <summary>
        /// Inicializa los keybinds predeterminados de osu!mania para todos los key counts (1K-18K).
        /// </summary>
        private void InitializeDefaultKeybinds()
        {
            // 1K - centrado
            Keybinds[1] = new[] { Keys.Space };

            // 2K - simétrico
            Keybinds[2] = new[] { Keys.F, Keys.J };

            // 3K - con space en medio
            Keybinds[3] = new[] { Keys.F, Keys.Space, Keys.J };

            // 4K - estándar (DFJK)
            Keybinds[4] = new[] { Keys.D, Keys.F, Keys.J, Keys.K };

            // 5K - con space en medio
            Keybinds[5] = new[] { Keys.D, Keys.F, Keys.Space, Keys.J, Keys.K };

            // 6K - SDFJKL
            Keybinds[6] = new[] { Keys.S, Keys.D, Keys.F, Keys.J, Keys.K, Keys.L };

            // 7K - con space en medio
            Keybinds[7] = new[] { Keys.S, Keys.D, Keys.F, Keys.Space, Keys.J, Keys.K, Keys.L };

            // 8K - fila home completa
            Keybinds[8] = new[] { Keys.A, Keys.S, Keys.D, Keys.F, Keys.J, Keys.K, Keys.L, Keys.OemSemicolon };

            // 9K - agregar Space en medio
            Keybinds[9] = new[] { Keys.A, Keys.S, Keys.D, Keys.F, Keys.Space, Keys.J, Keys.K, Keys.L, Keys.OemSemicolon };

            // 10K - expandir a ambos lados
            Keybinds[10] = new[] { 
                Keys.Z, Keys.A, Keys.S, Keys.D, Keys.F, 
                Keys.J, Keys.K, Keys.L, Keys.OemSemicolon, Keys.OemQuotes 
            };

            // 11K - con space
            Keybinds[11] = new[] { 
                Keys.Z, Keys.A, Keys.S, Keys.D, Keys.F, Keys.Space,
                Keys.J, Keys.K, Keys.L, Keys.OemSemicolon, Keys.OemQuotes 
            };

            // 12K - fila completa ASDF + fila HJKL
            Keybinds[12] = new[] { 
                Keys.Z, Keys.X, Keys.A, Keys.S, Keys.D, Keys.F,
                Keys.H, Keys.J, Keys.K, Keys.L, Keys.OemSemicolon, Keys.OemQuotes 
            };

            // 13K - con space
            Keybinds[13] = new[] { 
                Keys.Z, Keys.X, Keys.A, Keys.S, Keys.D, Keys.F, Keys.Space,
                Keys.H, Keys.J, Keys.K, Keys.L, Keys.OemSemicolon, Keys.OemQuotes 
            };

            // 14K - agregar CVBN
            Keybinds[14] = new[] { 
                Keys.Z, Keys.X, Keys.C, Keys.V, Keys.A, Keys.S, Keys.D,
                Keys.F, Keys.J, Keys.K, Keys.L, Keys.OemSemicolon, Keys.B, Keys.N 
            };

            // 15K - con space
            Keybinds[15] = new[] { 
                Keys.Z, Keys.X, Keys.C, Keys.V, Keys.A, Keys.S, Keys.D, Keys.Space,
                Keys.F, Keys.J, Keys.K, Keys.L, Keys.OemSemicolon, Keys.B, Keys.N 
            };

            // 16K - fila numérica superior
            Keybinds[16] = new[] { 
                Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.Z, Keys.X, Keys.C, Keys.V,
                Keys.B, Keys.N, Keys.M, Keys.OemComma, Keys.D7, Keys.D8, Keys.D9, Keys.D0 
            };

            // 17K - con space
            Keybinds[17] = new[] { 
                Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.Z, Keys.X, Keys.C, Keys.V, Keys.Space,
                Keys.B, Keys.N, Keys.M, Keys.OemComma, Keys.D7, Keys.D8, Keys.D9, Keys.D0 
            };

            // 18K - máximo soportado
            Keybinds[18] = new[] { 
                Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.Z, Keys.X, Keys.C, Keys.V,
                Keys.B, Keys.N, Keys.M, Keys.OemComma, Keys.OemPeriod, Keys.D7, Keys.D8, Keys.D9, Keys.D0 
            };
        }

        /// <summary>
        /// Obtiene el keybind actual para una columna específica del key count activo.
        /// </summary>
        /// <param name="columnIndex">Índice de la columna</param>
        /// <returns>Tecla asignada, o null si no existe</returns>
        public Keys? GetKeybindForColumn(int columnIndex)
        {
            if (!Keybinds.TryGetValue(ActiveKeyCount, out Keys[]? keys))
                return null;

            if (columnIndex < 0 || columnIndex >= keys.Length)
                return null;

            return keys[columnIndex];
        }

        /// <summary>
        /// Limpia todos los listeners de eventos (útil al cambiar de pantalla).
        /// </summary>
        public void ClearEventListeners()
        {
            OnKeyDown = null;
            OnKeyUp = null;
        }
    }
}
