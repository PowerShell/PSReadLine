/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System.Text;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell
{
    internal sealed class Clipboard
    {
        private static StringBuilder _buffer = new StringBuilder();
        private Clipboard() { }

        public static void SetText(string text)
        {
            Clipboard._buffer.Clear();
            Clipboard._buffer.Append(text);
        }
        public static string GetText()
        {
            string text = Clipboard._buffer.ToString();
            return text;
        }
        public static bool ContainsText()
        {
            return Clipboard._buffer.Length > 0;
        }
    }
}
