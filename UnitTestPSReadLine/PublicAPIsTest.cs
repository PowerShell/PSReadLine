using System;
using System.Management.Automation.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerShell;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [TestMethod]
        public void TestInsertAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo foo", Keys(
                CheckThat(() =>
                {
                    PSConsoleReadLine.Insert('e');
                    PSConsoleReadLine.Insert(" foo");
                }),
                _.Home, _.RightArrow,
                CheckThat(() =>
                {
                    PSConsoleReadLine.Insert('c');
                    PSConsoleReadLine.Insert("ho");
                })));
        }

        [TestMethod]
        public void TestDeleteAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "echo zzz",
                CheckThat(() => PSConsoleReadLine.Delete(4, 4))));
        }

        [TestMethod]
        public void TestReplaceAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo zzz", Keys(
                "echo foobar",
                CheckThat(() => PSConsoleReadLine.Replace(5, 6, "zzz"))));

            bool throws = false;
            Test("echo", Keys(
                "echo",
                CheckThat(() =>
                {
                    try { PSConsoleReadLine.Replace(-1, 6, "zzz"); }
                    catch (ArgumentException) { throws = true; }
                    Assert.IsTrue(throws, "Negative start should throw");

                    try { PSConsoleReadLine.Replace(11, 6, "zzz"); }
                    catch (ArgumentException) { throws = true; }
                    Assert.IsTrue(throws, "Start beyond end of buffer should throw");

                    try { PSConsoleReadLine.Replace(0, 12, "zzz"); }
                    catch (ArgumentException) { throws = true; }
                    Assert.IsTrue(throws, "Length too long should throw");
                })));
        }

        [TestMethod]
        public void TestGetBufferStateAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "echo",
                CheckThat(() =>
                {
                    string input;
                    int cursor;
                    PSConsoleReadLine.GetBufferState(out input, out cursor);
                    Assert.AreEqual("echo", input);
                    Assert.AreEqual(4, cursor);

                    Ast ast;
                    Token[] tokens;
                    ParseError[] parseErrors;
                    PSConsoleReadLine.GetBufferState(out ast, out tokens, out parseErrors, out cursor);
                    Assert.IsNotNull(ast);
                    Assert.IsTrue(ast is ScriptBlockAst && ((ScriptBlockAst)ast).EndBlock.Statements.Count == 1);
                    Assert.IsTrue((tokens[0].TokenFlags & TokenFlags.CommandName) == TokenFlags.CommandName);
                    Assert.AreEqual(0, parseErrors.Length);
                    Assert.AreEqual(4, cursor);
                })));
        }

        [TestMethod]
        public void TestGetSelectionStateAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "echo",
                CheckThat(() =>
                {
                    int start;
                    int length;
                    PSConsoleReadLine.GetSelectionState(out start, out length);
                    Assert.AreEqual(start, -1);
                    Assert.AreEqual(length, -1);
                }),
                _.ShiftHome,
                CheckThat(() =>
                {
                    int start;
                    int length;
                    PSConsoleReadLine.GetSelectionState(out start, out length);
                    Assert.AreEqual(start, 0);
                    Assert.AreEqual(length, 4);
                }),
                _.ShiftRightArrow,
                CheckThat(() =>
                {
                    int start;
                    int length;
                    PSConsoleReadLine.GetSelectionState(out start, out length);
                    Assert.AreEqual(start, 1);
                    Assert.AreEqual(length, 3);
                })));
        }

        [TestMethod]
        public void TestSetCursorPositionAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo abc", Keys(
                "ec a",
                CheckThat(() => PSConsoleReadLine.SetCursorPosition(2)),
                "ho",
                CheckThat(() => PSConsoleReadLine.SetCursorPosition(100)),
                "bc"
                ));
        }
    }
}
