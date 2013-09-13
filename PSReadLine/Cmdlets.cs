using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace PSConsoleUtilities
{
#if NEVER
    public class PSConsoleUtilitiesInit : IModuleAssemblyInitializer
    {
        [ExcludeFromCodeCoverage]
        public void OnImport()
        {
            var ps = PowerShell.Create(RunspaceMode.CurrentRunspace);
            ps.AddScript(@"
function global:PSConsoleHostReadline
{
    [PSConsoleUtilities.PSConsoleReadLine]::ReadLine()
}
");
            ps.Invoke();
        }
    }
#endif

    [Cmdlet("Set", "PSReadlineOption")]
    public class SetPSReadlineOption : PSCmdlet
    {
        [Parameter(ParameterSetName = "OptionsSet")]
        public EditMode EditMode
        {
            get { return _editMode.GetValueOrDefault(); }
            set { _editMode = value; }
        }
        internal EditMode? _editMode;

        [Parameter(ParameterSetName = "OptionsSet")]
        [AllowEmptyString]
        public string ContinuationPrompt { get; set; }

        [Parameter(ParameterSetName = "OptionsSet")]
        public ConsoleColor ContinuationPromptForegroundColor
        {
            get { return _continuationPromptForegroundColor.GetValueOrDefault(); }
            set { _continuationPromptForegroundColor = value; }
        }
        internal ConsoleColor? _continuationPromptForegroundColor;

        [Parameter(ParameterSetName = "OptionsSet")]
        public ConsoleColor ContinuationPromptBackgroundColor
        {
            get { return _continuationPromptBackgroundColor.GetValueOrDefault(); }
            set { _continuationPromptBackgroundColor = value; }
        }
        internal ConsoleColor? _continuationPromptBackgroundColor;

        [Parameter(ParameterSetName = "OptionsSet")]
        public SwitchParameter HistoryNoDuplicates
        {
            get { return _historyNoDuplicates.GetValueOrDefault(); }
            set { _historyNoDuplicates = value; }
        }
        internal SwitchParameter? _historyNoDuplicates;

        [Parameter(ParameterSetName = "OptionsSet")]
        [AllowNull]
        public Func<string, bool> AddToHistoryHandler
        {
            get { return _addToHistoryHandler; }
            set
            {
                _addToHistoryHandler = value;
                _addToHistoryHandlerSpecified = true;
            }
        }
        private Func<string, bool> _addToHistoryHandler;
        internal bool _addToHistoryHandlerSpecified;

        [Parameter(ParameterSetName = "OptionsSet")]
        public SwitchParameter HistorySearchCursorMovesToEnd
        {
            get { return _historySearchCursorMovesToEnd.GetValueOrDefault(); }
            set { _historySearchCursorMovesToEnd = value; }
        }
        internal SwitchParameter? _historySearchCursorMovesToEnd;

        [Parameter(ParameterSetName = "OptionsSet")]
        public int MaximumHistoryCount
        {
            get { return _maximumHistoryCount.GetValueOrDefault(); }
            set { _maximumHistoryCount = value; }
        }
        internal int? _maximumHistoryCount;

        [Parameter(ParameterSetName = "OptionsSet")]
        public int MaximumKillRingCount
        {
            get { return _maximumKillRingCount.GetValueOrDefault(); }
            set { _maximumKillRingCount = value; }
        }
        internal int? _maximumKillRingCount;

        [Parameter(ParameterSetName = "OptionsSet")]
        public SwitchParameter ResetTokenColors
        {
            get { return _resetTokenColors.GetValueOrDefault(); }
            set { _resetTokenColors = value; }
        }
        internal SwitchParameter? _resetTokenColors;

        [Parameter(ParameterSetName = "OptionsSet")]
        public SwitchParameter ShowToolTips
        {
            get { return _showToolTips.GetValueOrDefault(); }
            set { _showToolTips = value; }
        }
        internal SwitchParameter? _showToolTips;

        [Parameter(ParameterSetName = "OptionsSet")]
        public int ExtraPromptLineCount
        {
            get { return _extraPromptLineCount.GetValueOrDefault(); }
            set { _extraPromptLineCount = value; }
        }
        internal int? _extraPromptLineCount;

        [Parameter(ParameterSetName = "OptionsSet")]
        public int DingTone
        {
            get { return _dingTone.GetValueOrDefault(); }
            set { _dingTone = value; }
        }
        internal int? _dingTone;

        [Parameter(ParameterSetName = "OptionsSet")]
        public int DingDuration
        {
            get { return _dingDuration.GetValueOrDefault(); }
            set { _dingDuration = value; }
        }
        internal int? _dingDuration;

        [Parameter(ParameterSetName = "OptionsSet")]
        public BellStyle BellStyle
        {
            get { return _bellStyle.GetValueOrDefault(); }
            set { _bellStyle = value; }
        }
        internal BellStyle? _bellStyle;

        [Parameter(ParameterSetName = "ColorSet", Position = 0, Mandatory = true)]
        public TokenClassification TokenKind
        {
            get { return _tokenKind.GetValueOrDefault(); }
            set { _tokenKind = value; }
        }
        internal TokenClassification? _tokenKind;

        [Parameter(ParameterSetName = "ColorSet", Position = 1)]
        public ConsoleColor ForegroundColor
        {
            get { return _foregroundColor.GetValueOrDefault(); }
            set { _foregroundColor = value; }
        }
        internal ConsoleColor? _foregroundColor;

        [Parameter(ParameterSetName = "ColorSet", Position = 2)]
        public ConsoleColor BackgroundColor
        {
            get { return _backgroundColor.GetValueOrDefault(); }
            set { _backgroundColor = value; }
        }
        internal ConsoleColor? _backgroundColor;

        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            PSConsoleReadLine.SetOptions(this);
        }
    }

    [Cmdlet("Set", "PSReadlineKeyHandler")]
    public class SetKeyHandlerCommand : PSCmdlet
    {
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Key")]
        [ValidateNotNullOrEmpty]
        public string[] Chord { get; set; }

        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNull]
        public Action<ConsoleKeyInfo?, object> Handler { get; set; }

        [Parameter(Mandatory = true)]
        public string BriefDescription { get; set; }

        [Parameter]
        public string LongDescription { get; set; }

        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            PSConsoleReadLine.SetKeyHandler(Chord, Handler, BriefDescription, LongDescription);
        }
    }

    [Cmdlet("Get", "PSReadlineKeyHandler")]
    public class GetKeyHandlerCommand : PSCmdlet
    {
        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            WriteObject(PSConsoleReadLine.GetKeyHandlers(), true);
        }
    }
}
