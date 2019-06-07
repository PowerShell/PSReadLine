using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;

namespace Test
{
    public class KeyboardLayout : DynamicObject
    {
        public class KeyInfo
        {
            public string Key { get; set; }
            public string KeyChar { get; set; }
            public string ConsoleKey { get; set; }
            public string Modifiers { get; set; }

            public static string CharAsPropertyName(char c)
            {
                switch (c)
                {
                    case ' ': return "Spacebar";
                    case '\r': return "Enter";
                    case '\n': return "Enter";
                    case '`': return "Backtick";
                    case '~': return "Tilde";
                    case '!': return "Bang";
                    case '@': return "At";
                    case '#': return "Pound";
                    case '$': return "Dollar";
                    case '%': return "Percent";
                    case '^': return "Uphat";
                    case '&': return "Ampersand";
                    case '*': return "Star";
                    case '(': return "LParen";
                    case ')': return "RParen";
                    case '_': return "Underbar";
                    case '=': return "Equals";
                    case '-': return "Minus";
                    case '+': return "Plus";
                    case '[': return "LBracket";
                    case ']': return "RBracket";
                    case '{': return "LBrace";
                    case '}': return "RBrace";
                    case '\\': return "Backslash";
                    case '|': return "Pipe";
                    case ';': return "Semicolon";
                    case '\'': return "SQuote";
                    case ':': return "Colon";
                    case '"': return "DQuote";
                    case ',': return "Comma";
                    case '.': return "Period";
                    case '/': return "Slash";
                    case '<': return "Less";
                    case '>': return "Greater";
                    case '?': return "Question";
                    case '0': return "D0";
                    case '1': return "D1";
                    case '2': return "D2";
                    case '3': return "D3";
                    case '4': return "D4";
                    case '5': return "D5";
                    case '6': return "D6";
                    case '7': return "D7";
                    case '8': return "D8";
                    case '9': return "D9";
                }

                return null;
            }

            public string KeyAsPropertyName()
            {
                string alt = null;
                char lastChar = Key[Key.Length - 1];
                switch (lastChar) {
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                        if (Key.Length == 1)
                        {
                            alt = CharAsPropertyName(lastChar);
                        }
                        break;

                    default:
                        alt = CharAsPropertyName(lastChar);
                        break;
                }
                var key = (alt != null)
                    ? Key.Substring(0, Key.Length - 1) + alt
                    : Key;
                return key.Replace('+', '_');
            }

            public ConsoleKeyInfo AsConsoleKeyInfo()
            {
                if (!Enum.TryParse<ConsoleKey>(ConsoleKey, out var consoleKey)) {
                    throw new InvalidCastException();
                }
                return new ConsoleKeyInfo(KeyChar[0], consoleKey,
                    shift: Modifiers.Contains("Shift"),
                    alt: Modifiers.Contains("Alt"),
                    control: Modifiers.Contains("Control"));
            }
        }

        public override string ToString()
        {
            return _layout;
        }

        private readonly string _layout;
        private readonly Dictionary<string, ConsoleKeyInfo> _keyMap = new Dictionary<string, ConsoleKeyInfo>();

        public KeyboardLayout(string lang, string os)
        {
            var keyInfos = File.ReadAllText($"KeyInfo-{lang}-{os}.json");
            foreach (var keyInfo in Newtonsoft.Json.JsonConvert.DeserializeObject<List<Test.KeyboardLayout.KeyInfo>>(keyInfos))
            {
                var propName = keyInfo.KeyAsPropertyName();
                var consoleKeyInfo = keyInfo.AsConsoleKeyInfo();
                _keyMap.Add(propName, consoleKeyInfo);
            }

            _layout = lang;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (_keyMap.TryGetValue(binder.Name, out var keyInfo)) {
                result = keyInfo;
                return true;
            }
            switch (binder.Name) {
                case "VolumeUp":
                    result = new ConsoleKeyInfo('\0', ConsoleKey.VolumeUp, false, false, false);
                    return true;
                case "VolumeDown":
                    result = new ConsoleKeyInfo('\0', ConsoleKey.VolumeDown, false, false, false);
                    return true;
                case "VolumeMute":
                    result = new ConsoleKeyInfo('\0', ConsoleKey.VolumeMute, false, false, false);
                    return true;
            }

            result = null;
            return false;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            result = null;
            if (indexes.Length == 1)
            {
                if (indexes[0] is char c)
                {
                    var propName = KeyInfo.CharAsPropertyName(c) ?? c.ToString();
                    if (_keyMap.TryGetValue(propName, out var keyInfo))
                    {
                        result = keyInfo;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}