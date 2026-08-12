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

        [Fact]
        public void RecomputeInitialCoords_ShouldRecoverInitialXWhenBufferGetsWider()
        {
            // The column of the initial coordinates is the width of the prompt reduced modulo the
            // buffer width, so a prompt of 36 cells walked through the buffer widths below has to
            // give 36, 1, 36, 11 and 36 in turn. Reducing the column in place on each change gives
            // the right answer only for the first one, because it discards how many physical lines
            // the prompt spans and the column can then no longer be recovered.
            //
            // Only the column is checked here. Recovering the row relies on the terminal having
            // reflowed the screen buffer, which the test console does not do.
            const int promptCells = 36;
            const int bufferHeight = 100;
            int[] bufferWidths = { 100, 35, 60, 25, 100 };

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
            object savedPreviousRender = previousRenderField.GetValue(instance);

            try
            {
                // An empty input keeps the initial row at 0 throughout, so a plain test console is
                // all that is needed to report each new buffer width.
                bufferField.SetValue(instance, new StringBuilder());
                currentField.SetValue(instance, 0);
                initialPromptCellsField.SetValue(instance, promptCells);
                initialXField.SetValue(instance, promptCells % bufferWidths[0]);
                initialYField.SetValue(instance, 0);

                RenderData previousRender = new()
                {
                    lines = new[] { new RenderedLineData(line: "", isFirstLogicalLine: true) }
                };

                foreach (int bufferWidth in bufferWidths)
                {
                    TestConsole console = new(_, bufferWidth, bufferHeight);
                    consoleField.SetValue(instance, console);

                    previousRender.initialY = (int)initialYField.GetValue(instance);
                    previousRenderField.SetValue(instance, previousRender);
                    handlePotentialResizingField.SetValue(instance, true);

                    recomputeInitialCoords.Invoke(instance, new object[] { true });

                    int initialX = (int)initialXField.GetValue(instance);
                    Assert.True(
                        promptCells % bufferWidth == initialX,
                        $"buffer width {bufferWidth}: initial column is {initialX} but should be {promptCells % bufferWidth}");

                    // The render data now describes the buffer as it was before the next change.
                    previousRender.UpdateConsoleInfo(console);
                }
            }
            finally
            {
                consoleField.SetValue(instance, savedConsole);
                bufferField.SetValue(instance, savedBuffer);
                currentField.SetValue(instance, savedCurrent);
                previousRenderField.SetValue(instance, savedPreviousRender);
            }
        }
    }
}
