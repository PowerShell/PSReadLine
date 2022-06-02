using System.Collections.Generic;
using System.IO;
using Microsoft.PowerShell;
using Newtonsoft.Json;
using Test.Resizing;
using Xunit;

namespace Test.Resizing
{
#pragma warning disable 0649

    /// <summary>
    ///     This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class LogicalToPhysicalLineTestData
    {
        public List<LogicalToPhysicalLineTestContext> Context;
        public bool IsFirstLogicalLine;
        public string Line;
        public string Name;
    }

    /// <summary>
    ///     This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class LogicalToPhysicalLineTestContext
    {
        public int BufferWidth;
        public int InitialX;
        public int LastLineLen;
        public int LineCount;
    }

    /// <summary>
    ///     This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class ResizingTestData
    {
        public List<ResizingTestContext> Context;
        public List<string> Lines;
        public string Name;
        public int NewBufferWidth;
        public int OldBufferWidth;
    }

    /// <summary>
    ///     This class is initialized by JSON deserialization.
    /// </summary>
    internal sealed class ResizingTestContext
    {
        public Point NewCursor;
        public Point NewInitial;
        public RenderOffset Offset;
        public Point OldCursor;
        public Point OldInitial;

        internal sealed class RenderOffset
        {
            public int CharIndex;
            public int LineIndex;
        }
    }

#pragma warning restore 0649
}

namespace Test
{
    public partial class ReadLine
    {
        private static List<ResizingTestData> s_resizingTestData;

        private void InitializeTestData()
        {
            if (s_resizingTestData is null)
            {
                var path = Path.Combine("assets", "resizing", "renderdata-to-cursor-point.json");
                var text = File.ReadAllText(path);
                s_resizingTestData = JsonConvert.DeserializeObject<List<ResizingTestData>>(text);
            }
        }

        private PSConsoleReadLine GetPSConsoleReadLineSingleton()
        {
            return _rl;
        }

        [Fact]
        public void ConvertPointToRenderDataOffset_ShouldWork()
        {
            InitializeTestData();
            var instance = GetPSConsoleReadLineSingleton();

            foreach (var test in s_resizingTestData)
            {
                Renderer.RenderData renderData = new()
                {
                    lines = new RenderedLineData[test.Lines.Count],
                    bufferWidth = test.OldBufferWidth
                };

                for (var i = 0; i < test.Lines.Count; i++)
                    renderData.lines[i] = new RenderedLineData(test.Lines[i], i == 0);

                for (var j = 0; j < test.Context.Count; j++)
                {
                    var context = test.Context[j];
                    renderData.cursorLeft = context.OldCursor.X;
                    renderData.cursorTop = context.OldCursor.Y;

                    var offset =
                        _renderer.ConvertPointToRenderDataOffset(context.OldInitial.X, context.OldInitial.Y,
                            renderData);
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
            var instance = GetPSConsoleReadLineSingleton();

            foreach (var test in s_resizingTestData)
            {
                Renderer.RenderData renderData = new()
                {
                    lines = new RenderedLineData[test.Lines.Count],
                    bufferWidth = test.OldBufferWidth
                };

                for (var i = 0; i < test.Lines.Count; i++)
                    renderData.lines[i] = new RenderedLineData(test.Lines[i], i == 0);

                for (var j = 0; j < test.Context.Count; j++)
                {
                    var context = test.Context[j];
                    if (context.Offset.LineIndex != -1)
                    {
                        renderData.cursorLeft = context.OldCursor.X;
                        renderData.cursorTop = context.OldCursor.Y;

                        var offset = new Renderer.RenderDataOffset(context.Offset.LineIndex, context.Offset.CharIndex);
                        var newCursor = _renderer.ConvertRenderDataOffsetToPoint(context.NewInitial.X,
                            context.NewInitial.Y, test.NewBufferWidth, renderData, offset);
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

            foreach (var test in testDataList)
            {
                RenderedLineData lineData = new(test.Line, test.IsFirstLogicalLine);
                for (var i = 0; i < test.Context.Count; i++)
                {
                    var context = test.Context[i];
                    var lineCount =
                        lineData.PhysicalLineCount(context.BufferWidth, context.InitialX, out var lastLinelen);
                    Assert.True(
                        context.LineCount == lineCount &&
                        context.LastLineLen == lastLinelen,
                        $"{test.Name}-context_{i}: calculated physical line count or length of last physical line is not what's expected [count: {lineCount}, lastLen: {lastLinelen}]");
                }
            }
        }
    }
}