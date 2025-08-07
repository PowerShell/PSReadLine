using System;
using System.Collections.Generic;
using System.Dynamic;
using Microsoft.PowerShell;

namespace Test
{
    public class KeyboardLayout : DynamicObject
    {
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            // Map the property name and try to compose ConsoleKeyInfo
            var mappedName = MapPropertyNameToChord(binder.Name);
            
            try
            {
                // Use PSReadLine's ConsoleKeyChordConverter to parse it
                var keys = ConsoleKeyChordConverter.Convert(mappedName);
                
                // We expect a single key for this use case
                if (keys.Length == 1)
                {
                    result = keys[0];
                    return true;
                }
            }
            catch
            {
                // If the conversion fails, fall through to special cases
            }

            // Handle special volume keys that aren't in ConsoleKeyChordConverter
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

        private string MapPropertyNameToChord(string propertyName)
        {
            // Define symbol mappings
            var symbolMap = new Dictionary<string, string>
            {
                ["DQuote"] = "\"",
                ["SQuote"] = "'",
                ["Slash"] = "/",
                ["Backslash"] = "\\",
                ["Percent"] = "%",
                ["Dollar"] = "$",
                ["Comma"] = ",",
                ["Period"] = ".",
                ["Tilde"] = "~",
                ["Question"] = "?",
                ["Uphat"] = "^",
                ["Underbar"] = "_",
                ["LBracket"] = "[",
                ["RBracket"] = "]",
                ["Greater"] = ">",
                ["Less"] = "<",
                ["Minus"] = "-",
                ["Equals"] = "=",
                ["At"] = "@"
            };

            // Split on underscore, map symbols, and join with plus
            var parts = propertyName.Split('_');
            var result = new List<string>();
            
            foreach (var part in parts)
            {
                result.Add(symbolMap.TryGetValue(part, out var mapped) ? mapped : part);
            }
            
            return string.Join("+", result);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            result = null;
            if (indexes.Length == 1 && indexes[0] is char c)
            {
                // Try to get the key using the character directly
                try
                {
                    var keys = ConsoleKeyChordConverter.Convert(c.ToString());
                    if (keys.Length == 1)
                    {
                        result = keys[0];
                        return true;
                    }
                }
                catch
                {
                    // If conversion fails, we can't handle this character
                }
            }
            return false;
        }
    }
}
