using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSConsoleUtilities;

namespace UnitTestPSReadLine
{
    [TestClass]
    public class KeyInfoConverterTest
    {
        [TestMethod]
        public void TestKeyInfoConverterSimpleCharLiteral()
        {
            var result = ConsoleKeyChordConverter.Convert("x");
            Assert.IsNotNull(result);            
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, 'x');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, (ConsoleModifiers)0);            
        }

        [TestMethod]
        public void TestKeyInfoConverterSimpleCharLiteralWithModifiers()
        {
            var result = ConsoleKeyChordConverter.Convert("alt+shift+x");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, 'X');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift | ConsoleModifiers.Alt);
        }

        [TestMethod]
        public void TestKeyInfoConverterSymbolLiteral()
        {
            var result = ConsoleKeyChordConverter.Convert("}");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterShiftedSymbolLiteral()
        {
            // } => shift+]  / shift+oem6
            var result = ConsoleKeyChordConverter.Convert("shift+]");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterWellKnownConsoleKey()
        {
            // oem6
            var result = ConsoleKeyChordConverter.Convert("shift+oem6");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 1);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, '}');
            Assert.AreEqual(key.Key, ConsoleKey.Oem6);
            Assert.AreEqual(key.Modifiers, ConsoleModifiers.Shift);
        }

        [TestMethod]
        public void TestKeyInfoConverterSequence()
        {
            // oem6
            var result = ConsoleKeyChordConverter.Convert("Escape,X");
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Length, 2);

            var key = result[0];

            Assert.AreEqual(key.KeyChar, (char)27);
            Assert.AreEqual(key.Key, ConsoleKey.Escape);
            Assert.AreEqual(key.Modifiers, (ConsoleModifiers)0);

            key = result[1];

            Assert.AreEqual(key.KeyChar, 'x');
            Assert.AreEqual(key.Key, ConsoleKey.X);
            Assert.AreEqual(key.Modifiers, (ConsoleModifiers)0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]        
        public void TestKeyInfoConverterInvalidKey()
        {
            var result = ConsoleKeyChordConverter.Convert("escrape");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestKeyInfoConverterInvalidModifierTypo()
        {
            var result = ConsoleKeyChordConverter.Convert("alt+shuft+x");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestKeyInfoConverterInvalidModifierInapplicable()
        {
            var result = ConsoleKeyChordConverter.Convert("shift+}");
        }

        [TestMethod]
        [ExpectedException(typeof (ArgumentException))]
        public void TestKeyInfoConverterInvalidSubsequence1()
        {
            var result = ConsoleKeyChordConverter.Convert("x,");
        }

        [TestMethod]
        [ExpectedException(typeof (ArgumentException))]
        public void TestKeyInfoConverterInvalidSubsequence2()
        {
            var result = ConsoleKeyChordConverter.Convert(",x");
        }
    }
}
