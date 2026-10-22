using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RyanXuWei.HybridLivePreview
{
    // Maps KeyboardEvent.code (physical key) to Input System keys.
    internal static class WebKeyMap
    {
        static readonly Dictionary<string, Key> Map = Build();

        public static bool TryGet(string code, out Key key)
        {
            key = Key.None;
            return !string.IsNullOrEmpty(code) && Map.TryGetValue(code, out key);
        }

        static Dictionary<string, Key> Build()
        {
            var map = new Dictionary<string, Key>
            {
                ["Space"] = Key.Space,
                ["Enter"] = Key.Enter,
                ["Tab"] = Key.Tab,
                ["Backquote"] = Key.Backquote,
                ["Quote"] = Key.Quote,
                ["Semicolon"] = Key.Semicolon,
                ["Comma"] = Key.Comma,
                ["Period"] = Key.Period,
                ["Slash"] = Key.Slash,
                ["Backslash"] = Key.Backslash,
                ["IntlBackslash"] = Key.OEM1,
                ["BracketLeft"] = Key.LeftBracket,
                ["BracketRight"] = Key.RightBracket,
                ["Minus"] = Key.Minus,
                ["Equal"] = Key.Equals,
                ["ShiftLeft"] = Key.LeftShift,
                ["ShiftRight"] = Key.RightShift,
                ["AltLeft"] = Key.LeftAlt,
                ["AltRight"] = Key.RightAlt,
                ["ControlLeft"] = Key.LeftCtrl,
                ["ControlRight"] = Key.RightCtrl,
                ["MetaLeft"] = Key.LeftMeta,
                ["MetaRight"] = Key.RightMeta,
                ["ContextMenu"] = Key.ContextMenu,
                ["Escape"] = Key.Escape,
                ["ArrowLeft"] = Key.LeftArrow,
                ["ArrowRight"] = Key.RightArrow,
                ["ArrowUp"] = Key.UpArrow,
                ["ArrowDown"] = Key.DownArrow,
                ["Backspace"] = Key.Backspace,
                ["PageDown"] = Key.PageDown,
                ["PageUp"] = Key.PageUp,
                ["Home"] = Key.Home,
                ["End"] = Key.End,
                ["Insert"] = Key.Insert,
                ["Delete"] = Key.Delete,
                ["CapsLock"] = Key.CapsLock,
                ["NumLock"] = Key.NumLock,
                ["PrintScreen"] = Key.PrintScreen,
                ["ScrollLock"] = Key.ScrollLock,
                ["Pause"] = Key.Pause,
                ["NumpadEnter"] = Key.NumpadEnter,
                ["NumpadDivide"] = Key.NumpadDivide,
                ["NumpadMultiply"] = Key.NumpadMultiply,
                ["NumpadAdd"] = Key.NumpadPlus,
                ["NumpadSubtract"] = Key.NumpadMinus,
                ["NumpadDecimal"] = Key.NumpadPeriod,
                ["NumpadEqual"] = Key.NumpadEquals,
            };
            for (var c = 'A'; c <= 'Z'; c++)
                map["Key" + c] = (Key)Enum.Parse(typeof(Key), c.ToString());
            for (var d = 0; d <= 9; d++)
            {
                map["Digit" + d] = (Key)Enum.Parse(typeof(Key), "Digit" + d);
                map["Numpad" + d] = (Key)Enum.Parse(typeof(Key), "Numpad" + d);
            }
            for (var f = 1; f <= 12; f++)
                map["F" + f] = (Key)Enum.Parse(typeof(Key), "F" + f);
            return map;
        }
    }
}
