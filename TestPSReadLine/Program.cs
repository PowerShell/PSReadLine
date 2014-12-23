using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using PSConsoleUtilities;

namespace TestPSReadLine
{
    class Program
    {
        static void Box(List<string> list)
        {
            int internalBoxWidth = Math.Min(Console.BufferWidth - 2, list.Max(e => e.Length));
            int boxWidth = internalBoxWidth + 2;
            int internalBoxHeight = list.Count;
            int boxHeight = internalBoxHeight + 2;

            var buffer = new CHAR_INFO[boxWidth * boxHeight];
            buffer[0].UnicodeChar = '+';
            buffer[boxWidth - 1].UnicodeChar = '+';
            for (int i = 1; i < boxWidth - 1; i++)
            {
                buffer[i].UnicodeChar = '-';
            }
            for (int i = 0; i < list.Count; i++)
            {
                int rowStart = (i + 1) * boxWidth;
                buffer[rowStart++].UnicodeChar = '|';
                buffer[rowStart + internalBoxWidth].UnicodeChar = '|';

                string s = list[i];
                int j;
                for (j = 0; j < s.Length; j++)
                {
                    buffer[rowStart + j].UnicodeChar = s[j];
                }
                for (; j < internalBoxWidth; j++)
                {
                    buffer[rowStart + j].UnicodeChar = ' ';
                }
            }
            int lastRowStart = (boxHeight - 1) * boxWidth;
            buffer[lastRowStart].UnicodeChar = '+';
            for (int i = 1; i < boxWidth - 1; i++)
            {
                buffer[i + lastRowStart].UnicodeChar = '-';
            }
            buffer[lastRowStart + boxWidth - 1].UnicodeChar = '+';

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i].Attributes = (ushort)ConsoleColor.Blue |
                    ((ushort)(ConsoleColor.DarkGreen) << 4);
                if (i % 2 != 0)
                {
                    buffer[i].Attributes |= 0xfff0;
                }
            }

            var handle = NativeMethods.GetStdHandle((uint) StandardHandleId.Output);
            var bufferSize = new COORD {X = (short)boxWidth, Y = (short)boxHeight};
            var bufferCoord = new COORD {X = 0, Y = 0};
            var writeRegion = new SMALL_RECT {
                Top = 1,
                Left = 1,
                Bottom = (short)(1 + boxHeight),
                Right = (short)(1 + boxWidth)};

            Console.WriteLine("some random stuff");
            Console.WriteLine("and more some random stuff");
            Console.WriteLine("lorem ipsum blah blah");
            Console.ReadKey();
            var saveBuffer = new CHAR_INFO[buffer.Length];
            NativeMethods.ReadConsoleOutput(handle, saveBuffer,
                bufferSize, bufferCoord, ref writeRegion);
            unsafe
            {
                fixed (CHAR_INFO* p = &buffer[0])
                fixed (CHAR_INFO* sp = &saveBuffer[0])
                {
                    NativeMethods.WriteConsoleOutput(handle, buffer,
                                                                bufferSize, bufferCoord, ref writeRegion);
                    Console.ReadKey();
                    NativeMethods.WriteConsoleOutput(handle, saveBuffer,
                                                                bufferSize, bufferCoord, ref writeRegion);
                }
            }
            Console.ReadKey();
        }

        static void CauseCrash(ConsoleKeyInfo? key = null, object arg = null)
        {
            throw new Exception("intentional crash for test purposes");
        }

        [STAThread]
        static void Main()
        {
            //Box(new List<string> {"abc", "  def", "this is something coo"});

            var iss = InitialSessionState.CreateDefault2();
            var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();
            Runspace.DefaultRunspace = rs;

            PSConsoleReadLine.SetOptions(new SetPSReadlineOption
            {
                EditMode = EditMode.Emacs,
                HistoryNoDuplicates = true,
            });
            PSConsoleReadLine.SetKeyHandler(new[] {"Ctrl+LeftArrow"}, PSConsoleReadLine.ShellBackwardWord, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"Ctrl+RightArrow"}, PSConsoleReadLine.ShellNextWord, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"F4"}, PSConsoleReadLine.HistorySearchBackward, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"F5"}, PSConsoleReadLine.HistorySearchForward, "", "");
            //PSConsoleReadLine.SetKeyHandler(new[] {"Ctrl+D,Ctrl+E"}, PSConsoleReadLine.EnableDemoMode, "", "");
            //PSConsoleReadLine.SetKeyHandler(new[] {"Ctrl+D,Ctrl+D"}, PSConsoleReadLine.DisableDemoMode, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"Ctrl+D,Ctrl+C"}, PSConsoleReadLine.CaptureScreen, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"Ctrl+D,Ctrl+P"}, PSConsoleReadLine.InvokePrompt, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"Ctrl+D,Ctrl+X"}, CauseCrash, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"F6"}, PSConsoleReadLine.PreviousLine, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"F7"}, PSConsoleReadLine.NextLine, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"F2"}, PSConsoleReadLine.ValidateAndAcceptLine, "", "");
            PSConsoleReadLine.SetKeyHandler(new[] {"Enter"}, PSConsoleReadLine.AcceptLine, "", "");


            EngineIntrinsics executionContext;
            using (var ps = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                executionContext =
                    ps.AddScript("$ExecutionContext").Invoke<EngineIntrinsics>().FirstOrDefault();

                // This is a workaround to ensure the command analysis cache has been created before
                // we enter into ReadLine.  It's a little slow and infrequently needed, so just
                // uncomment if you hit a hang, run it once, then comment it out again.
                //ps.Commands.Clear();
                //ps.AddCommand("Get-Command").Invoke();
            }

            while (true)
            {
                Console.Write("TestHostPS> ");

                var line = PSConsoleReadLine.ReadLine(null, executionContext);
                Console.WriteLine(line);
                line = line.Trim();
                if (line.Equals("exit"))
                    Environment.Exit(0);
                if (line.Equals("cmd"))
                    PSConsoleReadLine.SetOptions(new SetPSReadlineOption {EditMode = EditMode.Windows});
                if (line.Equals("emacs"))
                    PSConsoleReadLine.SetOptions(new SetPSReadlineOption {EditMode = EditMode.Emacs});
                if (line.Equals("nodupes"))
                    PSConsoleReadLine.SetOptions(new SetPSReadlineOption {HistoryNoDuplicates = true});
            }
        }
    }
}
