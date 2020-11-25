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
        private static StringBuilder actualPagerContent = new StringBuilder();
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

        private static readonly string paramHelp = @"

-Date <System.DateTime>

DESC: Specifies a date and time. Time is optional and if not specified, returns 00:00:00.
Required: false, Position: 0, Default Value: None, Pipeline Input: True (ByPropertyName, ByValue), WildCard: false
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
                descDetails[0].Members.Add(new PSNoteProperty("Text", descText));

                paramHelp.Members.Add(new PSNoteProperty("Description", descDetails));
                paramHelp.Members.Add(new PSNoteProperty("Name", "Date"));
                paramHelp.Members.Add(new PSNoteProperty("type", new PSNoteProperty("name", "System.Datetime")));
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
            actualPagerContent.Clear();
            actualPagerContent.Append(content);
        }

        [SkippableFact]
        public void DynHelp_GetFullHelp()
        {
            TestSetup(KeyMode.Cmd);
            Test("Get-Date", Keys(
                "Get-Date",
                _.F1,
                CheckThat(() => AssertScreenIs(18, TokenClassification.String, fullHelp))
            ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelp()
        {
            TestSetup(KeyMode.Cmd);
            Test("Get-Date", Keys(
                "Get-Date -Date",
                _.Alt_h,
                CheckThat(() => AssertScreenIs(6, TokenClassification.String, paramHelp))
                ));
        }
    }
}