using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.PowerShell;
using Newtonsoft.Json;
using Xunit;

namespace Test.Resizing
{
#pragma warning disable 0649

    /// <summary>
    /// This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class LogicalToPhysicalLineTestData
    {
        public string Name;
        public string Line;
        public bool IsFirstLogicalLine;
        public List<LogicalToPhysicalLineTestContext> Context;
    }

    /// <summary>
    /// This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class LogicalToPhysicalLineTestContext
    {
        public int BufferWidth;
        public int InitialX;
        public int LineCount;
        public int LastLineLen;
    }

    /// <summary>
    /// This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class ResizingTestData
    {
        public string Name;
        public List<string> Lines;
        public int OldBufferWidth;
        public int NewBufferWidth;
        public List<ResizingTestContext> Context;
    }

    /// <summary>
    /// This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class ResizingTestContext
    {
        public Point OldInitial;
        public Point OldCursor;
        public Point NewInitial;
        public Point NewCursor;
        public RenderOffset Offset;

        internal sealed class RenderOffset
        {
            public int LineIndex;
            public int CharIndex;
        }
    }

#pragma warning restore 0649
}

namespace Test
{
    using Test.Resizing;

    public partial class ReadLine
    {
        private static List<ResizingTestData> s_resizingTestData;

        private void InitializeTestData()
        {
            if (s_resizingTestData is null)
            {
                string path = Path.Combine("assets", "resizing", "renderdata-to-cursor-point.json");
                string text = File.ReadAllText(path);
                s_resizingTestData = JsonConvert.DeserializeObject<List<ResizingTestData>>(text);
            }
        }

        private PSConsoleReadLine GetPSConsoleReadLineSingleton()
        {
            return (PSConsoleReadLine)typeof(PSConsoleReadLine)
                .GetField("_singleton", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        [Fact]
        public void ConvertPointToRenderDataOffset_ShouldWork()
        {
            InitializeTestData();
            PSConsoleReadLine instance = GetPSConsoleReadLineSingleton();

            foreach (ResizingTestData test in s_resizingTestData)
            {
                RenderData renderData = new()
                {
                    lines = new RenderedLineData[test.Lines.Count],
                    bufferWidth = test.OldBufferWidth
                };

                for (int i = 0; i < test.Lines.Count; i++)
                {
                    renderData.lines[i] = new RenderedLineData(test.Lines[i], isFirstLogicalLine: i == 0);
                }

                for (int j = 0; j < test.Context.Count; j++)
                {
                    ResizingTestContext context = test.Context[j];
                    renderData.cursorLeft = context.OldCursor.X;
                    renderData.cursorTop = context.OldCursor.Y;

                    RenderDataOffset offset = instance.ConvertPointToRenderDataOffset(context.OldInitial.X, context.OldInitial.Y, renderData);
                    Assert.True(
                        context.Offset.LineIndex == offset.LogicalLineIndex &&
                        context.Offset.CharIndex == offset.VisibleCharIndex,
                        $"{test.Name}-context_{j}: calculated offset is not what's expected [line: {offset.LogicalLineIndex}, char: {offset.VisibleCharIndex}]");
                }
            }
        }

        [Fact]
        public void ConvertRenderDataOffsetToPoint_ShouldWork()
        {
            InitializeTestData();
            PSConsoleReadLine instance = GetPSConsoleReadLineSingleton();

            foreach (ResizingTestData test in s_resizingTestData)
            {
                RenderData renderData = new()
                {
                    lines = new RenderedLineData[test.Lines.Count],
                    bufferWidth = test.OldBufferWidth
                };

                for (int i = 0; i < test.Lines.Count; i++)
                {
                    renderData.lines[i] = new RenderedLineData(test.Lines[i], isFirstLogicalLine: i == 0);
                }

                for (int j = 0; j < test.Context.Count; j++)
                {
                    ResizingTestContext context = test.Context[j];
                    if (context.Offset.LineIndex != -1)
                    {
                        renderData.cursorLeft = context.OldCursor.X;
                        renderData.cursorTop = context.OldCursor.Y;

                        var offset = new RenderDataOffset(context.Offset.LineIndex, context.Offset.CharIndex);
                        Point newCursor = instance.ConvertRenderDataOffsetToPoint(context.NewInitial.X, context.NewInitial.Y, test.NewBufferWidth, renderData, offset);
                        Assert.True(
                            context.NewCursor.X == newCursor.X &&
                            context.NewCursor.Y == newCursor.Y,
                            $"{test.Name}-context_{j}: calculated new cursor is not what's expected [X: {newCursor.X}, Y: {newCursor.Y}]");
                    }
                }
            }
        }

        [Fact]
        public void PhysicalLineCountMethod_ShouldWork()
        {
            var path = Path.Combine("assets", "resizing", "physical-line-count.json");
            var text = File.ReadAllText(path);
            var testDataList = JsonConvert.DeserializeObject<List<LogicalToPhysicalLineTestData>>(text);

            foreach (LogicalToPhysicalLineTestData test in testDataList)
            {
                RenderedLineData lineData = new(test.Line, test.IsFirstLogicalLine);
                for (int i = 0; i < test.Context.Count; i++)
                {
                    LogicalToPhysicalLineTestContext context = test.Context[i];
                    int lineCount = lineData.PhysicalLineCount(context.BufferWidth, context.InitialX, out int lastLinelen);
                    Assert.True(
                        context.LineCount == lineCount &&
                        context.LastLineLen == lastLinelen,
                        $"{test.Name}-context_{i}: calculated physical line count or length of last physical line is not what's expected [count: {lineCount}, lastLen: {lastLinelen}]");
                }
            }
        }

        private static FieldInfo GetInstanceField(string name)
        {
            return typeof(PSConsoleReadLine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        /// <summary>
        /// Drive 'RecomputeInitialCoords' through a chain of buffer widths and check that the anchor
        /// it recovers at each width is the one the terminal's reflow put on the screen.
        /// </summary>
        /// <param name="promptCells">
        /// The cell width of the prompt's last logical line. The prompt is taken to start at column 0
        /// of a physical line, so at every buffer width the anchor is at row 'promptCells / width' and
        /// column 'promptCells % width'. That is the only property of the prompt that matters here.
        /// </param>
        /// <param name="input">The input as it stands on the screen when the buffer width changes.</param>
        /// <param name="cursorOffset">Where '_current' points into that input.</param>
        /// <param name="bufferWidths">
        /// The width 'ReadLine' was entered at, followed by the widths the buffer is changed to.
        /// </param>
        /// <remarks>
        /// The cursor the test console reports at each width is the one a terminal that reflowed the
        /// screen would report: the anchor plus the rendering of the input up to '_current'. The
        /// rendering is the forward direction of the very calculation under test, which is what makes
        /// this a test of the inverse - given a rendering and where it ended up on the screen, where
        /// does the anchor have to be - and not a restatement of it.
        /// </remarks>
        private void AssertAnchorIsRecoveredAcrossWidths(
            int promptCells,
            string input,
            int cursorOffset,
            int[] bufferWidths)
        {
            const int bufferHeight = 200;

            PSConsoleReadLine instance = GetPSConsoleReadLineSingleton();
            FieldInfo consoleField = GetInstanceField("_console");
            FieldInfo bufferField = GetInstanceField("_buffer");
            FieldInfo currentField = GetInstanceField("_current");
            FieldInfo initialXField = GetInstanceField("_initialX");
            FieldInfo initialYField = GetInstanceField("_initialY");
            FieldInfo initialPromptCellsField = GetInstanceField("_initialPromptCells");
            FieldInfo previousRenderField = GetInstanceField("_previousRender");
            FieldInfo handlePotentialResizingField = GetInstanceField("_handlePotentialResizing");
            MethodInfo recomputeInitialCoords = typeof(PSConsoleReadLine)
                .GetMethod("RecomputeInitialCoords", BindingFlags.Instance | BindingFlags.NonPublic);

            object savedConsole = consoleField.GetValue(instance);
            object savedBuffer = bufferField.GetValue(instance);
            object savedCurrent = currentField.GetValue(instance);
            object savedInitialX = initialXField.GetValue(instance);
            object savedInitialY = initialYField.GetValue(instance);
            object savedInitialPromptCells = initialPromptCellsField.GetValue(instance);
            object savedPreviousRender = previousRenderField.GetValue(instance);

            try
            {
                bufferField.SetValue(instance, new StringBuilder(input));
                currentField.SetValue(instance, cursorOffset);

                // What 'ReadLine' captured at the width it was entered at. '_initialX' is the column
                // the console reported, which is the prompt's cell width reduced modulo that width,
                // and '_initialPromptCells' is seeded from it - so the seed is the prompt's true cell
                // width only when the prompt fitted the buffer it started on.
                int entryWidth = bufferWidths[0];
                int heldX = promptCells % entryWidth;
                int heldY = promptCells / entryWidth;
                initialXField.SetValue(instance, heldX);
                initialYField.SetValue(instance, heldY);
                initialPromptCellsField.SetValue(instance, heldX);

                RenderData previousRender = new()
                {
                    lines = new[] { new RenderedLineData(line: input, isFirstLogicalLine: true) }
                };

                for (int i = 1; i < bufferWidths.Length; i++)
                {
                    int bufferWidth = bufferWidths[i];
                    TestConsole console = new(_, bufferWidth, bufferHeight);
                    consoleField.SetValue(instance, console);

                    // Where the reflow left the anchor, and hence where it left the cursor.
                    int expectedX = promptCells % bufferWidth;
                    int expectedY = promptCells / bufferWidth;
                    initialXField.SetValue(instance, expectedX);
                    initialYField.SetValue(instance, expectedY);
                    Point cursor = instance.ConvertOffsetToPoint(cursorOffset);
                    console.CursorLeft = cursor.X;
                    console.CursorTop = cursor.Y;

                    // The coordinates PSReadLine actually holds are the ones from the previous width.
                    initialXField.SetValue(instance, heldX);
                    initialYField.SetValue(instance, heldY);
                    previousRender.initialY = heldY;
                    previousRenderField.SetValue(instance, previousRender);
                    handlePotentialResizingField.SetValue(instance, true);

                    recomputeInitialCoords.Invoke(instance, new object[] { true });

                    heldX = (int)initialXField.GetValue(instance);
                    heldY = (int)initialYField.GetValue(instance);
                    Assert.True(
                        expectedX == heldX && expectedY == heldY,
                        $"prompt of {promptCells} cells, buffer width {bufferWidth}: the anchor was " +
                        $"recovered at ({heldX}, {heldY}) but the reflow left it at ({expectedX}, {expectedY})");

                    // The render data now describes the buffer as it was before the next change.
                    previousRender.UpdateConsoleInfo(console);
                }
            }
            finally
            {
                consoleField.SetValue(instance, savedConsole);
                bufferField.SetValue(instance, savedBuffer);
                currentField.SetValue(instance, savedCurrent);
                initialXField.SetValue(instance, savedInitialX);
                initialYField.SetValue(instance, savedInitialY);
                initialPromptCellsField.SetValue(instance, savedInitialPromptCells);
                previousRenderField.SetValue(instance, savedPreviousRender);
            }
        }

        [Fact]
        public void RecomputeInitialCoords_ShouldRecoverInitialXWhenBufferGetsWider()
        {
            // The column of the anchor is the width of the prompt reduced modulo the buffer width, so
            // a prompt of 36 cells walked through the buffer widths below has to give 36, 1, 36, 11
            // and 36 in turn. Reducing the column in place on each change gives the right answer only
            // for the first one, because it discards how many physical lines the prompt spans and the
            // column can then no longer be recovered.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 36,
                input: "",
                cursorOffset: 0,
                bufferWidths: new[] { 100, 35, 60, 25, 100 });
        }

        [Fact]
        public void RecomputeInitialCoords_ShouldRecoverInitialXWhenThePromptDidNotFitTheInitialBuffer()
        {
            // 'ReadLine' is entered on a buffer narrower than the prompt, so the column the console
            // reports - and therefore everything derived from it - is already reduced: 82 % 62 == 20.
            // No arithmetic on that 20 can produce 82 again. The cursor can, because the terminal
            // reflowed the prompt and the cursor together and the input is empty, which puts the
            // cursor on the anchor itself.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 82,
                input: "",
                cursorOffset: 0,
                bufferWidths: new[] { 62, 82 });

            // The same, then on to widths the prompt does and does not fit, in both directions.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 82,
                input: "",
                cursorOffset: 0,
                bufferWidths: new[] { 62, 100, 70, 120, 62 });
        }

        [Fact]
        public void RecomputeInitialCoords_ShouldRecoverInitialXWithTextOnTheInputLine()
        {
            // The cursor is no longer on the anchor, so recovering the anchor means subtracting the
            // rendering of the input from it.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 82,
                input: "Get-ChildItem",
                cursorOffset: 13,
                bufferWidths: new[] { 62, 82 });

            // Narrow to wide, with an input long enough to wrap at every width in the chain.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 82,
                input: "Get-ChildItem -Path . -Recurse | Where-Object { $_.Length -gt 1024 }",
                cursorOffset: 67,
                bufferWidths: new[] { 62, 100, 130 });

            // Wide to narrow, and with the cursor inside the input rather than at its end.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 36,
                input: "Get-ChildItem -Path . -Recurse | Where-Object { $_.Length -gt 1024 }",
                cursorOffset: 30,
                bufferWidths: new[] { 120, 60, 40 });
        }

        [Fact]
        public void RecomputeInitialCoords_ShouldKeepWorkingWhenThePromptFitsTheBuffer()
        {
            // The prompt fitted the buffer 'ReadLine' was entered on, so the belief about its cell
            // width was never damaged and the cursor has to agree with it at every width.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 12,
                input: "",
                cursorOffset: 0,
                bufferWidths: new[] { 80, 40, 120, 20, 80 });

            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 12,
                input: "Get-ChildItem -Path .",
                cursorOffset: 21,
                bufferWidths: new[] { 80, 40, 120, 20, 80 });
        }

        [Fact]
        public void RecomputeInitialCoords_ShouldRecoverInitialXWithAMultiLineInput()
        {
            // A newline moves the rendering to the continuation prompt's column no matter where the
            // anchor is, so past one the cursor says nothing about the anchor's column: taking the
            // cursor's own column as the offset to subtract would put the anchor at column 0 and draw
            // the first logical line over the prompt. The belief about the prompt's cell width is the
            // answer here, and the cursor must be read as agreeing with it rather than replacing it.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 30,
                input: "Get-ChildItem |\nForEach-Object { $_.Name }",
                cursorOffset: 41,
                bufferWidths: new[] { 80, 60, 25, 100 });

            // With the cursor still on the first logical line, where the column is observable again.
            AssertAnchorIsRecoveredAcrossWidths(
                promptCells: 30,
                input: "Get-ChildItem |\nForEach-Object { $_.Name }",
                cursorOffset: 10,
                bufferWidths: new[] { 80, 60, 25, 100 });
        }

        [Fact]
        public void RecomputeInitialCoords_ShouldRecoverInitialXFromRenderDataWhenTheInputChanged()
        {
            // The other half of 'RecomputeInitialCoords': the input has changed since it was last
            // rendered - 'Escape' cleared it after the resize, say - so the anchor has to be recovered
            // from the previous rendering rather than from '_buffer' and '_current'.
            const int promptCells = 82;
            const int entryWidth = 62;
            const int newWidth = 82;
            const int bufferHeight = 200;
            const string rendered = "Get-ChildItem -Path . -Recurse";

            PSConsoleReadLine instance = GetPSConsoleReadLineSingleton();
            FieldInfo consoleField = GetInstanceField("_console");
            FieldInfo bufferField = GetInstanceField("_buffer");
            FieldInfo currentField = GetInstanceField("_current");
            FieldInfo initialXField = GetInstanceField("_initialX");
            FieldInfo initialYField = GetInstanceField("_initialY");
            FieldInfo initialPromptCellsField = GetInstanceField("_initialPromptCells");
            FieldInfo previousRenderField = GetInstanceField("_previousRender");
            FieldInfo handlePotentialResizingField = GetInstanceField("_handlePotentialResizing");
            MethodInfo recomputeInitialCoords = typeof(PSConsoleReadLine)
                .GetMethod("RecomputeInitialCoords", BindingFlags.Instance | BindingFlags.NonPublic);

            object savedConsole = consoleField.GetValue(instance);
            object savedBuffer = bufferField.GetValue(instance);
            object savedCurrent = currentField.GetValue(instance);
            object savedInitialX = initialXField.GetValue(instance);
            object savedInitialY = initialYField.GetValue(instance);
            object savedInitialPromptCells = initialPromptCellsField.GetValue(instance);
            object savedPreviousRender = previousRenderField.GetValue(instance);

            try
            {
                int heldX = promptCells % entryWidth;
                int heldY = promptCells / entryWidth;

                // The input was cleared after the resize, which is what makes this the other branch.
                bufferField.SetValue(instance, new StringBuilder());
                currentField.SetValue(instance, 0);
                initialXField.SetValue(instance, heldX);
                initialYField.SetValue(instance, heldY);
                initialPromptCellsField.SetValue(instance, heldX);

                // The rendering as it stood at the entry width, with the cursor at the end of it.
                RenderData previousRender = new()
                {
                    lines = new[] { new RenderedLineData(rendered, isFirstLogicalLine: true) },
                    bufferWidth = entryWidth,
                    bufferHeight = bufferHeight,
                    initialY = heldY,
                };
                Point oldCursor = instance.ConvertRenderDataOffsetToPoint(
                    heldX, heldY, entryWidth, previousRender, new RenderDataOffset(0, int.MaxValue));
                previousRender.cursorLeft = oldCursor.X;
                previousRender.cursorTop = oldCursor.Y;
                previousRenderField.SetValue(instance, previousRender);

                // Where the reflow left the anchor, and the cursor that follows from it.
                int expectedX = promptCells % newWidth;
                int expectedY = promptCells / newWidth;
                RenderDataOffset offset = instance.ConvertPointToRenderDataOffset(heldX, heldY, previousRender);
                Assert.NotEqual(-1, offset.LogicalLineIndex);
                Point newCursor = instance.ConvertRenderDataOffsetToPoint(
                    expectedX, expectedY, newWidth, previousRender, offset);

                TestConsole console = new(_, newWidth, bufferHeight)
                {
                    CursorLeft = newCursor.X,
                    CursorTop = newCursor.Y,
                };
                consoleField.SetValue(instance, console);
                handlePotentialResizingField.SetValue(instance, true);

                recomputeInitialCoords.Invoke(instance, new object[] { false });

                int initialX = (int)initialXField.GetValue(instance);
                int initialY = (int)initialYField.GetValue(instance);
                Assert.True(
                    expectedX == initialX && expectedY == initialY,
                    $"buffer width {newWidth}: the anchor was recovered at ({initialX}, {initialY}) " +
                    $"but the reflow left it at ({expectedX}, {expectedY})");
            }
            finally
            {
                consoleField.SetValue(instance, savedConsole);
                bufferField.SetValue(instance, savedBuffer);
                currentField.SetValue(instance, savedCurrent);
                initialXField.SetValue(instance, savedInitialX);
                initialYField.SetValue(instance, savedInitialY);
                initialPromptCellsField.SetValue(instance, savedInitialPromptCells);
                previousRenderField.SetValue(instance, savedPreviousRender);
            }
        }
    }
}
