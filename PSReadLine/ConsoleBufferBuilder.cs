/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell
{
    internal class ConsoleBufferBuilder
    {
        private List<CHAR_INFO> buffer;

        public ConsoleBufferBuilder()
        {
            buffer = new List<CHAR_INFO>();
        }

        public ConsoleBufferBuilder(int capacity)
        {
            buffer = new List<CHAR_INFO>(capacity);
        }

        CHAR_INFO NewCharInfo(char c)
        {
            return new CHAR_INFO
            {
                UnicodeChar = c,
                BackgroundColor = Console.BackgroundColor,
                ForegroundColor = Console.ForegroundColor
            };
        }

        public void Append(string s)
        {
            foreach (char c in s)
            {
                buffer.Add(NewCharInfo(c));
            }
        }

        public void Append(char c, int count)
        {
            while (count-- > 0)
            {
                buffer.Add(NewCharInfo(c));
            }
        }

        public CHAR_INFO[] ToArray()
        {
            return buffer.ToArray();
        }

        public int Length
        {
            get { return buffer.Count; }
        }
    }
}

