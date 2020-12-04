using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using Microsoft.PowerShell;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
        private static string actualContent;        
        
        private static readonly string fullHelp = @"

NAME
    Get-Date

SYNOPSIS
    Gets the current date and time.

PARAMETERS
    -AsUTC <System.Management.Automation.SwitchParameter>
        Converts the date value to the equivalent time in UTC.

        This parameter was introduced in PowerShell 7.1.

        Required?                    false
        Position?                    named
        Default value                False
        Accept pipeline input?       False
        Accept wildcard characters?  false
";
                
        internal static object GetDynamicHelpContent(string commandName, string parameterName, bool isFullHelp)
        {
            string descText = @"Specifies a date and time. Time is optional and if not specified, returns 00:00:00.";

            if (!String.IsNullOrEmpty(commandName) && isFullHelp)
            {
                return fullHelp;
            }
            else if (!String.IsNullOrEmpty(commandName) && !String.IsNullOrEmpty(parameterName))
            {
                PSObject paramHelp = new PSObject();

                PSObject[] descDetails = new PSObject[1];
                descDetails[0] = new PSObject();
                descDetails[0].Members.Add(new PSNoteProperty("Text", descText));

                var np = new PSNoteProperty("name", "System.Datetime");
                np.Value = "System.Datetime";

                var typeName = new PSObject(np);

                paramHelp.Members.Add(new PSNoteProperty("Description", descDetails));
                paramHelp.Members.Add(new PSNoteProperty("Name", "Date"));
                paramHelp.Members.Add(new PSNoteProperty("type", typeName));
                paramHelp.Members.Add(new PSNoteProperty("required", "false"));
                paramHelp.Members.Add(new PSNoteProperty("position", "0"));
                paramHelp.Members.Add(new PSNoteProperty("defaultValue", "None"));
                paramHelp.Members.Add(new PSNoteProperty("pipelineInput", "True (ByPropertyName, ByValue)"));
                paramHelp.Members.Add(new PSNoteProperty("globbing", "false"));

                return paramHelp;
            }

            return null;
        }

        internal static void WriteToPager(string content, string regexPatternToScrollTo)
        {
            actualContent = content;
            PSConsoleReadLine.ReadKey();
        }

        [SkippableFact]
        public void DynHelp_GetFullHelp()
        {
            TestSetup(KeyMode.Cmd);

            _console.Clear();

            Test("Get-Date", Keys(
                "Get-Date", _.F1,
                CheckThat(() => Assert.Equal(fullHelp, actualContent)),
                _.Enter,
                _.Enter
                ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelp()
        {
            PSConsoleReadLine.EnableDynHelpTestHook = true;

            try
            {
                TestSetup(KeyMode.Cmd, new KeyHandler("Alt+h", PSConsoleReadLine.DynamicHelpParameter));

                _console.Clear();
                string emptyLine = new string(' ', _console.BufferWidth);

                Test("Get-Date -Date", Keys(
                    "Get-Date -Date", _.Alt_h,
                    CheckThat(() => AssertScreenIs(9,
                    TokenClassification.Command, "Get-Date",
                    TokenClassification.None, " ",
                    TokenClassification.Parameter, "-Date", NextLine,
                    emptyLine,
                    TokenClassification.None, $"-Date <name>", NextLine,
                    emptyLine,
                    TokenClassification.None, "DESC: Specifies a date and time. Time is optional and if not", NextLine, " specified, returns 00:00:00.", NextLine,                    
                    TokenClassification.None, "Required: false, Position: 0, Default Value: None, Pipeline ", NextLine, "Input: True (ByPropertyName, ByValue), WildCard: false")),
                    _.Enter,
                    _.Enter
                    ));
            }
            finally
            {
                PSConsoleReadLine.EnableDynHelpTestHook = false;
            }
        }
    }
}
