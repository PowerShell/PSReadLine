﻿using System;
using System.Management.Automation.Language;
using Microsoft.PowerShell;
using Xunit;

namespace UnitTestPSReadLine
{
    // Disgusting language hack to make it easier to read a sequence of keys.
    using _ = Keys;

    public partial class UnitTest
    {
        [Fact]
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

        [Fact]
        public void TestDeleteAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "echo zzz",
                CheckThat(() => PSConsoleReadLine.Delete(4, 4))));
        }

        [Fact]
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
                    Assert.True(throws, "Negative start should throw");

                    try { PSConsoleReadLine.Replace(11, 6, "zzz"); }
                    catch (ArgumentException) { throws = true; }
                    Assert.True(throws, "Start beyond end of buffer should throw");

                    try { PSConsoleReadLine.Replace(0, 12, "zzz"); }
                    catch (ArgumentException) { throws = true; }
                    Assert.True(throws, "Length too long should throw");

                    try { PSConsoleReadLine.Replace(0, -1, "zzz"); }
                    catch (ArgumentException) { throws = true; }
                    Assert.True(throws, "Negative length should throw");
                })));
        }

        [Fact]
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
                    Assert.Equal("echo", input);
                    Assert.Equal(4, cursor);

                    Ast ast;
                    Token[] tokens;
                    ParseError[] parseErrors;
                    PSConsoleReadLine.GetBufferState(out ast, out tokens, out parseErrors, out cursor);
                    Assert.NotNull(ast);
                    Assert.True(ast is ScriptBlockAst && ((ScriptBlockAst)ast).EndBlock.Statements.Count == 1);
                    Assert.Equal((tokens[0].TokenFlags & TokenFlags.CommandName), TokenFlags.CommandName);
                    Assert.Equal(0, parseErrors.Length);
                    Assert.Equal(4, cursor);
                })));
        }

        [Fact]
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
                    Assert.Equal(start, -1);
                    Assert.Equal(length, -1);
                }),
                _.ShiftHome,
                CheckThat(() =>
                {
                    int start;
                    int length;
                    PSConsoleReadLine.GetSelectionState(out start, out length);
                    Assert.Equal(start, 0);
                    Assert.Equal(length, 4);
                }),
                _.ShiftRightArrow,
                CheckThat(() =>
                {
                    int start;
                    int length;
                    PSConsoleReadLine.GetSelectionState(out start, out length);
                    Assert.Equal(start, 1);
                    Assert.Equal(length, 3);
                })));
        }

        [Fact]
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
