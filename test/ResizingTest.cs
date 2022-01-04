using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    }
}
