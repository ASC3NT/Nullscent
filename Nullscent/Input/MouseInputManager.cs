#nullable enable

using Microsoft.Xna.Framework.Input;
using System;

namespace Nullscent.Input
{
    /// <summary>
    /// Global mouse input tracking for UI interactions.
    /// Tracks position, clicks, and wheel scrolling.
    /// </summary>
    public class MouseInputManager
    {
        private MouseState _previousMouseState;
        private MouseState _currentMouseState;

        // Properties
        public int X => _currentMouseState.X;
        public int Y => _currentMouseState.Y;
        public bool IsLeftPressed => _currentMouseState.LeftButton == ButtonState.Pressed;
        public bool IsRightPressed => _currentMouseState.RightButton == ButtonState.Pressed;
        public int ScrollWheelValue => _currentMouseState.ScrollWheelValue;

        // Events
        public event Action<int, int>? LeftClicked;
        public event Action<int, int>? RightClicked;
        public event Action<int>? ScrollWheel;

        public MouseInputManager()
        {
            _previousMouseState = Mouse.GetState();
            _currentMouseState = _previousMouseState;
        }

        public void Update()
        {
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Detect left click (rising edge)
            if (IsLeftButtonJustPressed())
            {
                LeftClicked?.Invoke(X, Y);
            }

            // Detect right click (rising edge)
            if (IsRightButtonJustPressed())
            {
                RightClicked?.Invoke(X, Y);
            }

            // Detect scroll wheel change
            if (_currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int delta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                ScrollWheel?.Invoke(delta);
            }
        }

        public bool IsLeftButtonJustPressed()
        {
            return _currentMouseState.LeftButton == ButtonState.Pressed &&
                   _previousMouseState.LeftButton == ButtonState.Released;
        }

        public bool IsRightButtonJustPressed()
        {
            return _currentMouseState.RightButton == ButtonState.Pressed &&
                   _previousMouseState.RightButton == ButtonState.Released;
        }

        public bool IsLeftButtonJustReleased()
        {
            return _currentMouseState.LeftButton == ButtonState.Released &&
                   _previousMouseState.LeftButton == ButtonState.Pressed;
        }

        public bool IsRightButtonJustReleased()
        {
            return _currentMouseState.RightButton == ButtonState.Released &&
                   _previousMouseState.RightButton == ButtonState.Pressed;
        }

        public bool IsPointInside(int x, int y, int width, int height)
        {
            return X >= x && X < x + width && Y >= y && Y < y + height;
        }
    }
}
