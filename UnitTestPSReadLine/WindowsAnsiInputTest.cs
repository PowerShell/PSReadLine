using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        private ConsoleKeyInfo[] StringToCKI(string str)
        {
            return str.Select(c => new ConsoleKeyInfo(c, 0, false, false, false)).ToArray();
        }

        [TestMethod]
        public void TestMapControlChars()
        {
            var map = new WindowsAnsiCharMap();
            ConsoleKeyInfo processedKey;
            // Enter (Ctrl+J)
            map.ProcessKey(new ConsoleKeyInfo('\x0D', 0, false, false, false));
            Assert.AreEqual(true, map.KeyAvailable);
            processedKey = map.ReadKey();
            Assert.AreEqual(processedKey.Key, ConsoleKey.Enter);
            Assert.AreEqual(processedKey.Modifiers, (ConsoleModifiers)0);
            Assert.AreEqual(false, map.KeyAvailable);

            // Ctrl+C
            map.ProcessKey(new ConsoleKeyInfo('\x03', 0, false, false, false));
            processedKey = map.ReadKey();
            Assert.AreEqual(processedKey.Key, ConsoleKey.C);
            Assert.AreEqual(processedKey.Modifiers, ConsoleModifiers.Control);
        }

        private void CheckEscapeInput(ICharMap map, ConsoleKeyInfo intended, ConsoleKeyInfo[] keys, bool inputOnly = false)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                map.ProcessKey(keys[i]);
                if (i < keys.Length - 1)
                {
                    Assert.AreEqual(false, map.KeyAvailable);
                }
            }
            if (inputOnly)
            {
                // Hack to make the map process escapes now.
                var escapeTimeout = map.EscapeTimeout;
                map.EscapeTimeout = 0;
                bool unused = map.KeyAvailable;
                map.EscapeTimeout = escapeTimeout;
                return;
            }
            Assert.AreEqual(true, map.KeyAvailable);
            var processedKey = map.ReadKey();
            Assert.AreEqual(false, map.KeyAvailable);
            Assert.AreEqual(intended, processedKey);
        }

        [TestMethod]
        public void TestValidEscapeSequences()
        {
            // Use a high timeout value so there's no way it will try to convert
            // part of a sequence to Alt+something.
            var map = new WindowsAnsiCharMap(1000);

            // ^[[A = UpArrow
            CheckEscapeInput(map, _.UpArrow, StringToCKI("\x1b[A"));

            // ^[OA = UpArrow (alternate form)
            CheckEscapeInput(map, _.UpArrow, StringToCKI("\x1bOA"));

            // ^[[1;2B = Shift+DownArrow
            CheckEscapeInput(map,
                new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift: true, alt: false, control: false),
                StringToCKI("\x1b[1;2B")
            );

            // ^[[1;8C = Ctrl+Alt+Shift+RightArrow
            CheckEscapeInput(map,
                new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, shift: true, alt: true, control: true),
                StringToCKI("\x1b[1;8C")
            );

            // ^[[1;6D = Ctrl+Shift+LeftArrow
            CheckEscapeInput(map,
                new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, shift: true, alt: false, control: true),
                StringToCKI("\x1b[1;6D")
            );

            // ^[[3~ = Delete
            CheckEscapeInput(map,
                new ConsoleKeyInfo('\0', ConsoleKey.Delete, shift: false, alt: false, control: false),
                StringToCKI("\x1b[3~")
            );

            // ^[[15;5~ = Control+F5
            CheckEscapeInput(map,
                new ConsoleKeyInfo('\0', ConsoleKey.F5, shift: false, alt: false, control: true),
                StringToCKI("\x1b[15;5~")
            );

        }

        [TestMethod]
        public void TestAltSequences()
        {
            var map = new WindowsAnsiCharMap(1000);
            ConsoleKeyInfo processedKey;

            // ^[[ = Alt+[
            CheckEscapeInput(map, default(ConsoleKeyInfo), StringToCKI("\x1b["), true);
            processedKey = map.ReadKey();
            Assert.AreEqual('[', processedKey.KeyChar);
            Assert.AreEqual(ConsoleModifiers.Alt, processedKey.Modifiers);
            Assert.AreEqual(false, map.KeyAvailable);

            // ^[j = Alt+j
            CheckEscapeInput(map, default(ConsoleKeyInfo), StringToCKI("\x1bj"), true);
            processedKey = map.ReadKey();
            Assert.AreEqual('j', processedKey.KeyChar);
            Assert.AreEqual(ConsoleModifiers.Alt, processedKey.Modifiers);
            Assert.AreEqual(false, map.KeyAvailable);

            // ^[X = Alt+X
            // Currently shift is not set for capitals, so just check the alt
            // parts to allow that behavior to change without breaking this test.
            CheckEscapeInput(map, default(ConsoleKeyInfo), StringToCKI("\x1bX"), true);
            processedKey = map.ReadKey();
            Assert.AreEqual('X', processedKey.KeyChar);
            Assert.AreEqual(ConsoleModifiers.Alt, processedKey.Modifiers & ConsoleModifiers.Alt);
            Assert.AreEqual(false, map.KeyAvailable);

            // ^[^A = Alt+Ctrl+A
            CheckEscapeInput(map, default(ConsoleKeyInfo), StringToCKI("\x1b\x01"), true);
            processedKey = map.ReadKey();
            Assert.AreEqual('\x01', processedKey.KeyChar);
            Assert.AreEqual(ConsoleModifiers.Alt | ConsoleModifiers.Control, processedKey.Modifiers);
            Assert.AreEqual(false, map.KeyAvailable);

            // This is a "tricky" one since ^[O can start a sequence and the second
            // escape needs to cancel sequence processing, make an Alt+O available,
            // and after that has been read make Esc available.
            // ^[O^[ = Alt+O Esc
            var consoleKeys = StringToCKI("\x1bO\x1b");
            foreach (var ck in consoleKeys)
            {
                map.ProcessKey(ck);
            }
            Assert.AreEqual(true, map.KeyAvailable);
            processedKey = map.ReadKey();
            // Alt+O
            Assert.AreEqual('O', processedKey.KeyChar);
            Assert.AreEqual(ConsoleModifiers.Alt, processedKey.Modifiers);
            // Make the map stop looking for an escape sequence.
            map.EscapeTimeout = 0;
            // Esc
            Assert.AreEqual(true, map.KeyAvailable);
            Assert.AreEqual('\x1b', map.ReadKey().KeyChar);
            Assert.AreEqual(false, map.KeyAvailable);
            map.EscapeTimeout = 1000;

            // ^[^[ = Esc Esc, not Alt+Esc.
            consoleKeys = StringToCKI("\x1b\x1b");
            foreach (var ck in consoleKeys)
            {
                map.ProcessKey(ck);
            }
            Assert.AreEqual(true, map.KeyAvailable);
            map.ReadKey();
            map.EscapeTimeout = 0;
            Assert.AreEqual(true, map.KeyAvailable);
            map.ReadKey();
        }

        private void CheckPartialEscapeInput(ICharMap map, int expectedCount, ConsoleKeyInfo[] keys)
        {
            foreach (var key in keys)
            {
                map.ProcessKey(key);
                Assert.AreEqual(false, map.KeyAvailable);
            }
            int keyCount = 0;
            // Hack to make the map think the timeout is up.
            var escapeTimeout = map.EscapeTimeout;
            map.EscapeTimeout = 0;
            while (map.KeyAvailable)
            {
                map.ReadKey();
                keyCount++;
            }
            Assert.AreEqual(expectedCount, keyCount);
            map.EscapeTimeout = escapeTimeout;
        }

        [TestMethod]
        public void TestPartialEscapeSequences()
        {
            var map = new WindowsAnsiCharMap(1000);

            // Just escape
            CheckPartialEscapeInput(map, 1, StringToCKI("\x1b"));

            // ^[[1;2
            CheckPartialEscapeInput(map, 5, StringToCKI("\x1b[1;2"));

            // ^[[11
            CheckPartialEscapeInput(map, 4, StringToCKI("\x1b[11"));

            // ^[O -> Alt+O
            CheckPartialEscapeInput(map, 1, StringToCKI("\x1bO"));
        }
    }
}
