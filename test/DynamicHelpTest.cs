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

            if (string.IsNullOrEmpty(commandName))
            {
                return null;
            }

            if (isFullHelp)
            {
                return fullHelp;
            }

            if (string.IsNullOrEmpty(parameterName))
            {
                return null;
            }

            if (string.IsNullOrEmpty(parameterName))
            {
                return null;
            }

            PSObject paramHelp = new PSObject();

            if (string.Equals(commandName, "Get-FakeHelp", StringComparison.OrdinalIgnoreCase) && string.Equals(parameterName, "Fake", StringComparison.OrdinalIgnoreCase))
            {
                PSObject[] descDetails = new PSObject[1];
                descDetails[0] = new PSObject();
                descDetails[0].Members.Add(new PSNoteProperty("Text", null));
            }
            else if (string.Equals(commandName, "Get-Date", StringComparison.OrdinalIgnoreCase) && string.Equals(parameterName, "Date", StringComparison.OrdinalIgnoreCase))
            {
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
            }

            return paramHelp;
        }

        [SkippableFact]
        public void DynHelp_GetFullHelp()
        {
            TestSetup(KeyMode.Cmd);
            Test("Get-Date", Keys(
                "Get-Date", _.F1,
                CheckThat(() => Assert.Equal(fullHelp, _mockedMethods.helpContentRendered)),
                _.Enter
                ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelp()
        {
            TestSetup(KeyMode.Cmd);
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

        [SkippableFact]
        public void DynHelp_GetParameterHelpErrorMessage()
        {
            TestSetup(KeyMode.Cmd);
            string emptyLine = new string(' ', _console.BufferWidth);

            Test("Get-FakeHelp -Fake", Keys(
                "Get-FakeHelp -Fake", _.Alt_h,
                CheckThat(() => AssertScreenIs(4,
                TokenClassification.Command, "Get-FakeHelp",
                TokenClassification.None, " ",
                TokenClassification.Parameter, "-Fake", NextLine,
                emptyLine,
                TokenClassification.None, "No help content available. Please use Update-Help to downloa", NextLine, "d the latest help content.")),
                _.Enter,
                _.Enter
                ));
        }
    }
}
