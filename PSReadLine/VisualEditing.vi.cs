/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// Edit the command line in a text editor specified by $env:EDITOR or $env:VISUAL.
        /// </summary>
        public static void ViEditVisually(ConsoleKeyInfo? key = null, object arg = null)
        {
            // EDITOR can be a cmd with args such as `emacsclient -c`
            // each component of the cmd may only be quoted by " due to internal design, see doc for `ProcessStartInfo.Arguments`
            string cmd = GetPreferredEditor().Trim();

            if (string.IsNullOrEmpty(cmd))
            {
                Ding();
                return;
            }

            string exe = string.Empty;
            string arguments = string.Empty;
            bool exeQuoteUnmatched = false;

            if (File.Exists(cmd)) // path/might contain space/foo.exe
            {
                exe = cmd;
            }
            else if (cmd.StartsWith('"')) // "path/with space/foo.exe" --args ...
            {
                int nextQuote = cmd.IndexOf('"', 1);

                if (nextQuote != -1)
                    (exe, arguments) = (cmd[1..nextQuote], cmd[(nextQuote + 1)..]);
                else
                    exeQuoteUnmatched = true;
            }
            else
            {
                // foo.exe
                // foo.exe --args
                // path/to/foo.exe --args
                int firstSpace = cmd.IndexOf(' ');
                (exe, arguments) = firstSpace != -1
                    ? (cmd[..firstSpace], cmd[(firstSpace + 1)..])
                    : (cmd, string.Empty);
            }

            if (string.IsNullOrEmpty(exe) || exeQuoteUnmatched)
            {
                Ding();
                return;
            }

            if (_singleton._engineIntrinsics?.InvokeCommand.GetCommand(exe, CommandTypes.Application) is not ApplicationInfo editorCommand)
            {
                Ding();
                return;
            }

            var tempPowerShellFile = GetTemporaryPowerShellFile();
            using (FileStream fs = File.OpenWrite(tempPowerShellFile))
            {
                using (TextWriter tw = new StreamWriter(fs))
                {
                    tw.Write(_singleton._buffer.ToString());
                }
            }

            exe = editorCommand.Path;
            var si = new ProcessStartInfo(exe, $"{arguments} \"{tempPowerShellFile}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false
            };
            var pi = _singleton.CallPossibleExternalApplication(() => Process.Start(si));
            if (pi != null)
            {
                pi.WaitForExit();
                InvokePrompt();
                _singleton.ProcessViVisualEditing(tempPowerShellFile);
            }
            else
            {
                Ding();
            }
        }

        private static string GetTemporaryPowerShellFile()
        {
            string filename;
            do
            {
                filename = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ps1");
            } while (File.Exists(filename) || Directory.Exists(filename));

            return filename;
        }

        private void ProcessViVisualEditing(string tempFileName)
        {
            string editedCommand;
            using (TextReader tr = File.OpenText(tempFileName))
            {
                editedCommand = tr.ReadToEnd();
            }
            File.Delete(tempFileName);

            if (!string.IsNullOrWhiteSpace(editedCommand))
            {
                while (editedCommand.Length > 0 && char.IsWhiteSpace(editedCommand[editedCommand.Length - 1]))
                {
                    editedCommand = editedCommand.Substring(0, editedCommand.Length - 1);
                }
                editedCommand = editedCommand.Replace(Environment.NewLine, "\n");
                Replace(0, _buffer.Length, editedCommand);
                _current = _buffer.Length;
                if (_options.EditMode == EditMode.Vi) _current -= 1;
                Render();
            }
        }

        private static string GetPreferredEditor()
        {
            var editor = Environment.GetEnvironmentVariable("VISUAL");
            return !string.IsNullOrWhiteSpace(editor)
                ? editor
                : Environment.GetEnvironmentVariable("EDITOR");
        }
    }
}
