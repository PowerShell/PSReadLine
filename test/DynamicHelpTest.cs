using System;
using System.Management.Automation;
using Xunit;

namespace Test
{
    public partial class ReadLine
    {
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

            PSObject paramHelp = new PSObject();

            if (string.Equals(commandName, "Get-FakeHelp", StringComparison.OrdinalIgnoreCase) && string.Equals(parameterName, "Fake", StringComparison.OrdinalIgnoreCase))
            {
                PSObject[] descDetails = new PSObject[1];
                descDetails[0] = new PSObject();
                descDetails[0].Members.Add(new PSNoteProperty("Text", null));
            }
            else if (string.Equals(commandName, "Get-Date", StringComparison.OrdinalIgnoreCase) && string.Equals(parameterName, "Date", StringComparison.OrdinalIgnoreCase))
            {
                return GetParameterHelpObject(description: "Specifies a date and time.");
            }
            else if (string.Equals(commandName, "Get-MultiLineHelp", StringComparison.OrdinalIgnoreCase))
            {
                if(string.Equals(parameterName, "OneAndHalf", StringComparison.OrdinalIgnoreCase))
                {
                    string multiLineDesc = "Some very long description that is over the buffer width of 60 characters but shorter than 120.";
                    return GetParameterHelpObject(multiLineDesc);
                }
                else if(string.Equals(parameterName, "ExactlyTwo", StringComparison.OrdinalIgnoreCase))
                {
                    string multiLineDesc = "Some very long description that is over the buffer width of 60 characters and exactly the length of 120 characters";
                    return GetParameterHelpObject(multiLineDesc);
                }
            }

            return paramHelp;
        }

        private static PSObject GetParameterHelpObject(string description)
        {
            PSObject paramHelp = new PSObject();
            PSObject[] descDetails = new PSObject[1];
            descDetails[0] = new PSObject();
            descDetails[0].Members.Add(new PSNoteProperty("Text", description));

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

        [SkippableFact]
        public void DynHelp_GetFullHelp_OnEmptyLine()
        {
            TestSetup(KeyMode.Cmd);
            Test("", Keys(_.F1, _.Enter));
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
        public void DynHelp_GetParameterHelp_OnEmptyLine()
        {
            TestSetup(KeyMode.Cmd);
            Test("", Keys(_.Alt_h, _.Enter));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelp_And_Clear()
        {
            TestSetup(KeyMode.Cmd);

            Test("Get-Date -Date", Keys(
                "Get-Date -Date", _.Alt_h,
                    CheckThat(() => AssertScreenIs(9,
                        TokenClassification.Command, "Get-Date",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-Date",
                        NextLine,
                        NextLine,
                        TokenClassification.None, $"-Date <name>",
                        NextLine,
                        NextLine,
                        TokenClassification.None, "DESC: Specifies a date and time.",
                        NextLine,
                        TokenClassification.None, "Required: false, Position: 0, Default Value: None, Pipeline ",
                        NextLine,
                        "Input: True (ByPropertyName, ByValue), WildCard: false")),
                _.Escape,
                    CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "Get-Date",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-Date")),
                _.Enter
                ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelpMultiLine_And_Clear()
        {
            TestSetup(KeyMode.Cmd);

            Test("Get-MultiLineHelp -OneAndHalf", Keys(
                "Get-MultiLineHelp -OneAndHalf", _.Alt_h,
                    CheckThat(() => AssertScreenIs(9,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-OneAndHalf",
                        NextLine,
                        NextLine,
                        TokenClassification.None, $"-Date <name>",
                        NextLine,
                        NextLine,
                        TokenClassification.None, "DESC: Some very long description that is over the buffer width of ",
                        TokenClassification.None, "60 characters but shorter than 120.",
                        NextLine,
                        TokenClassification.None, "Required: false, Position: 0, Default Value: None, Pipeline ",
                        NextLine,
                        "Input: True (ByPropertyName, ByValue), WildCard: false")),
                _.LeftArrow,
                    CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-OneAndHalf")),
                _.Enter
                ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelpTwoLines_And_Clear()
        {
            TestSetup(KeyMode.Cmd);

            Test("Get-MultiLineHelp -ExactlyTwo", Keys(
                "Get-MultiLineHelp -ExactlyTwo", _.Alt_h,
                    CheckThat(() => AssertScreenIs(9,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-ExactlyTwo",
                        NextLine,
                        NextLine,
                        TokenClassification.None, $"-Date <name>",
                        NextLine,
                        NextLine,
                        TokenClassification.None, "DESC: Some very long description that is over the buffer wid",
                        TokenClassification.None, "th of 60 characters and exactly the length of 120 characters",
                        NextLine,
                        TokenClassification.None, "Required: false, Position: 0, Default Value: None, Pipeline ",
                        NextLine,
                        "Input: True (ByPropertyName, ByValue), WildCard: false")),
                _.RightArrow,
                    CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-ExactlyTwo")),
                _.Enter
                ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelpTwoLines_And_Clear_Emacs()
        {
            TestSetup(KeyMode.Emacs);

            Test("Get-MultiLineHelp -ExactlyTwo", Keys(
                "Get-MultiLineHelp -ExactlyTwo", _.Alt_h,
                    CheckThat(() => AssertScreenIs(9,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-ExactlyTwo",
                        NextLine,
                        NextLine,
                        TokenClassification.None, $"-Date <name>",
                        NextLine,
                        NextLine,
                        TokenClassification.None, "DESC: Some very long description that is over the buffer wid",
                        TokenClassification.None, "th of 60 characters and exactly the length of 120 characters",
                        NextLine,
                        TokenClassification.None, "Required: false, Position: 0, Default Value: None, Pipeline ",
                        NextLine,
                        "Input: True (ByPropertyName, ByValue), WildCard: false")),
                _.RightArrow,
                    CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-ExactlyTwo")),
                _.Enter
                ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelpTwoLines_And_Clear_Vi()
        {
            TestSetup(KeyMode.Vi);

            Test("Get-MultiLineHelp -ExactlyTwo", Keys(
                "Get-MultiLineHelp -ExactlyTwo", _.Alt_h,
                    CheckThat(() => AssertScreenIs(9,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-ExactlyTwo",
                        NextLine,
                        NextLine,
                        TokenClassification.None, $"-Date <name>",
                        NextLine,
                        NextLine,
                        TokenClassification.None, "DESC: Some very long description that is over the buffer wid",
                        TokenClassification.None, "th of 60 characters and exactly the length of 120 characters",
                        NextLine,
                        TokenClassification.None, "Required: false, Position: 0, Default Value: None, Pipeline ",
                        NextLine,
                        "Input: True (ByPropertyName, ByValue), WildCard: false")),
                _.RightArrow,
                    CheckThat(() => AssertScreenIs(1,
                        TokenClassification.Command, "Get-MultiLineHelp",
                        TokenClassification.None, " ",
                        TokenClassification.Parameter, "-ExactlyTwo")),
                _.Enter
                ));
        }

        [SkippableFact]
        public void DynHelp_GetParameterHelpErrorMessage()
        {
            TestSetup(KeyMode.Cmd);

            Test("Get-FakeHelp -Fake", Keys(
                "Get-FakeHelp -Fake", _.Alt_h,
                    CheckThat(() => AssertScreenIs(4,
                    TokenClassification.Command, "Get-FakeHelp",
                    TokenClassification.None, " ",
                    TokenClassification.Parameter, "-Fake",
                    NextLine,
                    NextLine,
                    TokenClassification.None, "No help content available. Please use Update-Help to downloa",
                    NextLine,
                    "d the latest help content.")),
                _.RightArrow,
                _.Enter
                ));
        }
    }
}
