using System;
using System.Management.Automation.Language;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        [SkippableFact]
        public void InsertAPI()
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

        [SkippableFact]
        public void DeleteAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "echo zzz",
                CheckThat(() => PSConsoleReadLine.Delete(4, 4))));
        }

        [SkippableFact]
        public void ReplaceAPI()
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

        [SkippableFact]
        public void GetBufferStateAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "echo",
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetBufferState(out var input, out var cursor);
                    Assert.Equal("echo", input);
                    Assert.Equal(4, cursor);

                    PSConsoleReadLine.GetBufferState(out var ast, out var tokens, out var parseErrors, out cursor);
                    Assert.NotNull(ast);
                    Assert.True(ast is ScriptBlockAst sbast && sbast.EndBlock.Statements.Count == 1);
                    Assert.Equal(TokenFlags.CommandName, (tokens[0].TokenFlags & TokenFlags.CommandName));
                    Assert.Empty(parseErrors);
                    Assert.Equal(4, cursor);
                })));
        }

        [SkippableFact]
        public void GetSelectionStateAPI()
        {
            TestSetup(KeyMode.Cmd);

            Test("echo", Keys(
                "echo",
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetSelectionState(out var start, out var length);
                    Assert.Equal(-1, start);
                    Assert.Equal(-1, length);
                }),
                _.Shift_Home,
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetSelectionState(out var start, out var length);
                    Assert.Equal(0, start);
                    Assert.Equal(4, length);
                }),
                _.Shift_RightArrow,
                CheckThat(() =>
                {
                    PSConsoleReadLine.GetSelectionState(out var start, out var length);
                    Assert.Equal(1, start);
                    Assert.Equal(3, length);
                })));
        }

        [SkippableFact]
        public void SetCursorPositionAPI()
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
