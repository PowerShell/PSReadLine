/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.PowerShell.PSReadLine;

namespace Microsoft.PowerShell
{

#pragma warning disable 1591

    public enum EditMode
    {
        Windows,
        Emacs,
        Vi,
    }

    public enum BellStyle
    {
        None,
        Visual,
        Audible
    }

    public enum ViModeStyle
    {
        None,
        Prompt,
        Cursor,
        Script
    }

    public enum ViMode
    {
        Insert,
        Command
    }

    public enum HistorySaveStyle
    {
        SaveIncrementally,
        SaveAtExit,
        SaveNothing
    }

    public enum AddToHistoryOption
    {
        SkipAdding,
        MemoryOnly,
        MemoryAndFile
    }

    public enum PredictionSource
    {
        None,
        History,
    }

    public class PSConsoleReadLineOptions
    {
        public const ConsoleColor DefaultCommentColor   = ConsoleColor.DarkGreen;
        public const ConsoleColor DefaultKeywordColor   = ConsoleColor.Green;
        public const ConsoleColor DefaultStringColor    = ConsoleColor.DarkCyan;
        public const ConsoleColor DefaultOperatorColor  = ConsoleColor.DarkGray;
        public const ConsoleColor DefaultVariableColor  = ConsoleColor.Green;
        public const ConsoleColor DefaultCommandColor   = ConsoleColor.Yellow;
        public const ConsoleColor DefaultParameterColor = ConsoleColor.DarkGray;
        public const ConsoleColor DefaultTypeColor      = ConsoleColor.Gray;
        public const ConsoleColor DefaultNumberColor    = ConsoleColor.White;
        public const ConsoleColor DefaultMemberColor    = ConsoleColor.Gray;
        public const ConsoleColor DefaultEmphasisColor  = ConsoleColor.Cyan;
        public const ConsoleColor DefaultErrorColor     = ConsoleColor.Red;

        // Use dark black by default for the suggestion text.
        // Find the most suitable color using https://stackoverflow.com/a/33206814
        public const string DefaultPredictionColor = "\x1b[38;5;238m";

        public static EditMode DefaultEditMode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? EditMode.Windows
            : EditMode.Emacs;

        public const string DefaultContinuationPrompt = ">> ";

        /// <summary>
        /// The maximum number of commands to store in the history.
        /// </summary>
        public const int DefaultMaximumHistoryCount = 4096;

        /// <summary>
        /// The maximum number of items to store in the kill ring.
        /// </summary>
        public const int DefaultMaximumKillRingCount = 10;

        /// <summary>
        /// In Emacs, when searching history, the cursor doesn't move.
        /// In 4NT, the cursor moves to the end.  This option allows
        /// for either behavior.
        /// </summary>
        public const bool DefaultHistorySearchCursorMovesToEnd = false;

        /// <summary>
        /// When displaying possible completions, either display
        /// tooltips or display just the completions.
        /// </summary>
        public const bool DefaultShowToolTips = true;

        /// <summary>
        /// When ringing the bell, what frequency do we use?
        /// </summary>
        public const int DefaultDingTone = 1221;

        public const int DefaultDingDuration = 50;

        public const int DefaultCompletionQueryItems = 100;

        // Default includes all characters PowerShell treats like a dash - em dash, en dash, horizontal bar
        public const string DefaultWordDelimiters = @";:,.[]{}()/\|^&*-=+'""" + "\u2013\u2014\u2015";

        /// <summary>
        /// When ringing the bell, what should be done?
        /// </summary>
        public const BellStyle DefaultBellStyle = BellStyle.Audible;

        public const bool DefaultHistorySearchCaseSensitive = false;

        public const HistorySaveStyle DefaultHistorySaveStyle = HistorySaveStyle.SaveIncrementally;

        /// <summary>
        /// The predictive suggestion feature is disabled by default.
        /// </summary>
        public const PredictionSource DefaultPredictionSource = PredictionSource.None;

        /// <summary>
        /// How long in milliseconds should we wait before concluding
        /// the input is not an escape sequence?
        /// </summary>
        public const int DefaultAnsiEscapeTimeout = 100;

        public PSConsoleReadLineOptions(string hostName)
        {
            ResetColors();
            EditMode = DefaultEditMode;
            ContinuationPrompt = DefaultContinuationPrompt;
            ContinuationPromptColor = Console.ForegroundColor;
            ExtraPromptLineCount = DefaultExtraPromptLineCount;
            AddToHistoryHandler = DefaultAddToHistoryHandler;
            HistoryNoDuplicates = DefaultHistoryNoDuplicates;
            MaximumHistoryCount = DefaultMaximumHistoryCount;
            MaximumKillRingCount = DefaultMaximumKillRingCount;
            HistorySearchCursorMovesToEnd = DefaultHistorySearchCursorMovesToEnd;
            ShowToolTips = DefaultShowToolTips;
            DingDuration = DefaultDingDuration;
            DingTone = DefaultDingTone;
            BellStyle = DefaultBellStyle;
            CompletionQueryItems = DefaultCompletionQueryItems;
            WordDelimiters = DefaultWordDelimiters;
            HistorySearchCaseSensitive = DefaultHistorySearchCaseSensitive;
            HistorySaveStyle = DefaultHistorySaveStyle;
            AnsiEscapeTimeout = DefaultAnsiEscapeTimeout;
            PredictionSource = DefaultPredictionSource;

            var historyFileName = hostName + "_history.txt";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                HistorySavePath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft",
                    "Windows",
                    "PowerShell",
                    "PSReadLine",
                    historyFileName);
            }
            else
            {
                // PSReadLine can't use Utils.CorePSPlatform (6.0+ only), so do the equivalent:
                string historyPath = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

                if (!String.IsNullOrEmpty(historyPath))
                {
                    HistorySavePath = System.IO.Path.Combine(
                        historyPath,
                        "powershell",
                        "PSReadLine",
                        historyFileName);
                }
                else
                {
                    // History is data, so it goes into .local/share/powershell folder
                    var home = Environment.GetEnvironmentVariable("HOME");

                    if (!String.IsNullOrEmpty(home))
                    {
                        HistorySavePath = System.IO.Path.Combine(
                            home,
                            ".local",
                            "share",
                            "powershell",
                            "PSReadLine",
                            historyFileName);
                    }
                    else
                    {
                        // No HOME, then don't save anything
                        HistorySavePath = "/dev/null";
                    }
                }
            }

            CommandValidationHandler = null;
            CommandsToValidateScriptBlockArguments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ForEach-Object", "%",
                "Invoke-Command", "icm",
                "Measure-Command",
                "New-Module", "nmo",
                "Register-EngineEvent",
                "Register-ObjectEvent",
                "Register-WMIEvent",
                "Set-PSBreakpoint", "sbp",
                "Start-Job", "sajb",
                "Trace-Command", "trcm",
                "Use-Transaction",
                "Where-Object", "?", "where",
            };
        }

        public EditMode EditMode { get; set; }

        public string ContinuationPrompt { get; set; }

        public object ContinuationPromptColor
        {
            get => _continuationPromptColor;
            set => _continuationPromptColor = VTColorUtils.AsEscapeSequence(value);
        }

        internal string _continuationPromptColor;

        /// <summary>
        /// Prompts are typically 1 line, but sometimes they may span lines.  This
        /// count is used to make sure we can display the full prompt after showing
        /// ambiguous completions
        /// </summary>
        public int ExtraPromptLineCount { get; set; }
        public const int DefaultExtraPromptLineCount = 0;

        /// <summary>
        /// This handler is called before adding a command line to history.
        /// The return value indicates if the command line should be skipped,
        /// or added to memory only, or added to both memory and history file.
        /// </summary>
        public Func<string, object> AddToHistoryHandler { get; set; }
        public static readonly Func<string, object> DefaultAddToHistoryHandler =
            s => PSConsoleReadLine.GetDefaultAddToHistoryOption(s);

        /// <summary>
        /// This handler is called from ValidateAndAcceptLine.
        /// If an exception is thrown, validation fails and the error is reported.
        /// </summary>
        public Action<CommandAst> CommandValidationHandler { get; set; }

        /// <summary>
        /// Most commands do not accept script blocks, but for those that do,
        /// we want to validate commands in the script block arguments.
        /// Unfortunately, we can't know how the argument is used.  In the worst
        /// case, for commands like Get-ADUser, the script block actually
        /// specifies a different language.
        ///
        /// Because we can't know ahead of time all of the commands that do
        /// odd things with script blocks, we create a white-list of commands
        /// that do invoke the script block - this covers the most useful cases.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public HashSet<string> CommandsToValidateScriptBlockArguments { get; set; }

        /// <summary>
        /// When true, duplicates will not be recalled from history more than once.
        /// </summary>
        public bool HistoryNoDuplicates { get; set; }
        public const bool DefaultHistoryNoDuplicates = true;

        public int MaximumHistoryCount { get; set; }
        public int MaximumKillRingCount { get; set; }
        public bool HistorySearchCursorMovesToEnd { get; set; }
        public bool ShowToolTips { get; set; }
        public int DingTone { get; set; }
        public int CompletionQueryItems { get; set; }
        public string WordDelimiters { get; set; }

        /// <summary>
        /// When ringing the bell, how long (in ms)?
        /// </summary>
        public int DingDuration { get; set; }
        public BellStyle BellStyle { get; set; }

        public bool HistorySearchCaseSensitive { get; set; }
        internal StringComparison HistoryStringComparison => HistorySearchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// How are command and insert modes indicated when in vi edit mode?
        /// </summary>
        public ViModeStyle ViModeIndicator { get; set; }

        /// <summary>
        /// The script block to execute when the indicator mode is set to Script.
        /// </summary>
        public ScriptBlock ViModeChangeHandler { get; set; }

        /// <summary>
        /// The path to the saved history.
        /// </summary>
        public string HistorySavePath { get; set; }
        public HistorySaveStyle HistorySaveStyle { get; set; }

        /// <summary>
        /// Sets the source to get predictive suggestions.
        /// </summary>
        public PredictionSource PredictionSource { get; set; }

        /// <summary>
        /// How long in milliseconds should we wait before concluding
        /// the input is not an escape sequence?
        /// </summary>
        public int AnsiEscapeTimeout { get; set; }

        /// <summary>
        /// This is the text you want turned red on parse errors, but must
        /// occur immediately before the cursor when readline starts.
        /// If the prompt function is pure, this value can be inferred, e.g.
        /// the default prompt will use "> " for this value.
        /// </summary>
        public string[] PromptText
        {
            get => _promptText;
            set
            {
                _promptText = value;
                if (_promptText == null || _promptText.Length == 0)
                    return;

                // For texts with VT sequences, reset all attributes if not already.
                // We only handle the first 2 elements because that's all will potentially be used.
                int minLength = _promptText.Length == 1 ? 1 : 2;
                for (int i = 0; i < minLength; i ++)
                {
                    var text = _promptText[i];
                    if (text.Contains('\x1b') && !text.EndsWith("\x1b[0m", StringComparison.Ordinal))
                    {
                        _promptText[i] = string.Concat(text, "\x1b[0m");
                    }
                }
            }
        }
        private string[] _promptText;

        public object DefaultTokenColor
        {
            get => _defaultTokenColor;
            set => _defaultTokenColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object CommentColor
        {
            get => _commentColor;
            set => _commentColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object KeywordColor
        {
            get => _keywordColor;
            set => _keywordColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object StringColor
        {
            get => _stringColor;
            set => _stringColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object OperatorColor
        {
            get => _operatorColor;
            set => _operatorColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object VariableColor
        {
            get => _variableColor;
            set => _variableColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object CommandColor
        {
            get => _commandColor;
            set => _commandColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object ParameterColor
        {
            get => _parameterColor;
            set => _parameterColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object TypeColor
        {
            get => _typeColor;
            set => _typeColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object NumberColor
        {
            get => _numberColor;
            set => _numberColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object MemberColor
        {
            get => _memberColor;
            set => _memberColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object EmphasisColor
        {
            get => _emphasisColor;
            set => _emphasisColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object ErrorColor
        {
            get => _errorColor;
            set => _errorColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object SelectionColor
        {
            get => _selectionColor;
            set => _selectionColor = VTColorUtils.AsEscapeSequence(value);
        }

        public object PredictionColor
        {
            get => _predictionColor;
            set => _predictionColor = VTColorUtils.AsEscapeSequence(value);
        }

        internal string _defaultTokenColor;
        internal string _commentColor;
        internal string _keywordColor;
        internal string _stringColor;
        internal string _operatorColor;
        internal string _variableColor;
        internal string _commandColor;
        internal string _parameterColor;
        internal string _typeColor;
        internal string _numberColor;
        internal string _memberColor;
        internal string _emphasisColor;
        internal string _errorColor;
        internal string _selectionColor;
        internal string _predictionColor;

        internal void ResetColors()
        {
            var fg = Console.ForegroundColor;
            DefaultTokenColor = fg;
            CommentColor      = DefaultCommentColor;
            KeywordColor      = DefaultKeywordColor;
            StringColor       = DefaultStringColor;
            OperatorColor     = DefaultOperatorColor;
            VariableColor     = DefaultVariableColor;
            CommandColor      = DefaultCommandColor;
            ParameterColor    = DefaultParameterColor;
            TypeColor         = DefaultTypeColor;
            NumberColor       = DefaultNumberColor;
            MemberColor       = DefaultNumberColor;
            EmphasisColor     = DefaultEmphasisColor;
            ErrorColor        = DefaultErrorColor;
            PredictionColor   = DefaultPredictionColor;

            var bg = Console.BackgroundColor;
            if (fg == VTColorUtils.UnknownColor || bg == VTColorUtils.UnknownColor)
            {
                // TODO: light vs. dark
                fg = ConsoleColor.Gray;
                bg = ConsoleColor.Black;
            }

            SelectionColor = VTColorUtils.AsEscapeSequence(bg, fg);
        }

        private static Dictionary<string, Action<PSConsoleReadLineOptions, object>> ColorSetters = null;

        internal void SetColor(string property, object value)
        {
            if (ColorSetters == null)
            {
                var setters =
                    new Dictionary<string, Action<PSConsoleReadLineOptions, object>>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"ContinuationPrompt", (o, v) => o.ContinuationPromptColor = v},
                        {"Emphasis", (o, v) => o.EmphasisColor = v},
                        {"Error", (o, v) => o.ErrorColor = v},
                        {"Default", (o, v) => o.DefaultTokenColor = v},
                        {"Comment", (o, v) => o.CommentColor = v},
                        {"Keyword", (o, v) => o.KeywordColor = v},
                        {"String", (o, v) => o.StringColor = v},
                        {"Operator", (o, v) => o.OperatorColor = v},
                        {"Variable", (o, v) => o.VariableColor = v},
                        {"Command", (o, v) => o.CommandColor = v},
                        {"Parameter", (o, v) => o.ParameterColor = v},
                        {"Type", (o, v) => o.TypeColor = v},
                        {"Number", (o, v) => o.NumberColor = v},
                        {"Member", (o, v) => o.MemberColor = v},
                        {"Selection", (o, v) => o.SelectionColor = v},
                        {"Prediction", (o, v) => o.PredictionColor = v},
                    };

                Interlocked.CompareExchange(ref ColorSetters, setters, null);
            }

            if (ColorSetters.TryGetValue(property, out var setter))
            {
                setter(this, value);
            }
            else
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, PSReadLineResources.InvalidColorProperty, property));
            }
        }
    }

    [Cmdlet("Get", "PSReadLineOption", HelpUri = "https://go.microsoft.com/fwlink/?LinkId=528808")]
    [OutputType(typeof(PSConsoleReadLineOptions))]
    public class GetPSReadLineOption : PSCmdlet
    {
        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            WriteObject(PSConsoleReadLine.GetOptions());
        }
    }

    [Cmdlet("Set", "PSReadLineOption", HelpUri = "https://go.microsoft.com/fwlink/?LinkId=528811")]
    public class SetPSReadLineOption : PSCmdlet
    {
        [Parameter]
        public EditMode EditMode
        {
            get => _editMode.GetValueOrDefault();
            set => _editMode = value;
        }
        internal EditMode? _editMode;

        [Parameter]
        [AllowEmptyString]
        public string ContinuationPrompt { get; set; }

        [Parameter]
        public SwitchParameter HistoryNoDuplicates
        {
            get => _historyNoDuplicates.GetValueOrDefault();
            set => _historyNoDuplicates = value;
        }
        internal SwitchParameter? _historyNoDuplicates;

        [Parameter]
        [AllowNull]
        public Func<string, object> AddToHistoryHandler
        {
            get => _addToHistoryHandler;
            set
            {
                _addToHistoryHandler = value;
                _addToHistoryHandlerSpecified = true;
            }
        }
        private Func<string, object> _addToHistoryHandler;
        internal bool _addToHistoryHandlerSpecified;

        [Parameter]
        [AllowNull]
        public Action<CommandAst> CommandValidationHandler
        {
            get => _commandValidationHandler;
            set
            {
                _commandValidationHandler = value;
                _commandValidationHandlerSpecified = true;
            }
        }
        private Action<CommandAst> _commandValidationHandler;
        internal bool _commandValidationHandlerSpecified;

        [Parameter]
        public SwitchParameter HistorySearchCursorMovesToEnd
        {
            get => _historySearchCursorMovesToEnd.GetValueOrDefault();
            set => _historySearchCursorMovesToEnd = value;
        }
        internal SwitchParameter? _historySearchCursorMovesToEnd;

        [Parameter]
        [ValidateRange(1, int.MaxValue)]
        public int MaximumHistoryCount
        {
            get => _maximumHistoryCount.GetValueOrDefault();
            set => _maximumHistoryCount = value;
        }
        internal int? _maximumHistoryCount;

        [Parameter]
        public int MaximumKillRingCount
        {
            get => _maximumKillRingCount.GetValueOrDefault();
            set => _maximumKillRingCount = value;
        }
        internal int? _maximumKillRingCount;

        [Parameter]
        public SwitchParameter ShowToolTips
        {
            get => _showToolTips.GetValueOrDefault();
            set => _showToolTips = value;
        }
        internal SwitchParameter? _showToolTips;

        [Parameter]
        public int ExtraPromptLineCount
        {
            get => _extraPromptLineCount.GetValueOrDefault();
            set => _extraPromptLineCount = value;
        }
        internal int? _extraPromptLineCount;

        [Parameter]
        public int DingTone
        {
            get => _dingTone.GetValueOrDefault();
            set => _dingTone = value;
        }
        internal int? _dingTone;

        [Parameter]
        public int DingDuration
        {
            get => _dingDuration.GetValueOrDefault();
            set => _dingDuration = value;
        }
        internal int? _dingDuration;

        [Parameter]
        public BellStyle BellStyle
        {
            get => _bellStyle.GetValueOrDefault();
            set => _bellStyle = value;
        }
        internal BellStyle? _bellStyle;

        [Parameter]
        public int CompletionQueryItems
        {
            get => _completionQueryItems.GetValueOrDefault();
            set => _completionQueryItems = value;
        }
        internal int? _completionQueryItems;

        [Parameter]
        public string WordDelimiters { get; set; }

        [Parameter]
        public SwitchParameter HistorySearchCaseSensitive
        {
            get => _historySearchCaseSensitive.GetValueOrDefault();
            set => _historySearchCaseSensitive = value;
        }
        internal SwitchParameter? _historySearchCaseSensitive;

        [Parameter]
        public HistorySaveStyle HistorySaveStyle
        {
            get => _historySaveStyle.GetValueOrDefault();
            set => _historySaveStyle = value;
        }
        internal HistorySaveStyle? _historySaveStyle;

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string HistorySavePath
        {
            get => _historySavePath;
            set
            {
                _historySavePath = GetUnresolvedProviderPathFromPSPath(value);
            }
        }
        private string _historySavePath;

        [Parameter]
        [ValidateRange(25, 1000)]
        public int AnsiEscapeTimeout
        {
            get => _ansiEscapeTimeout.GetValueOrDefault();
            set => _ansiEscapeTimeout = value;
        }
        internal int? _ansiEscapeTimeout;

        [Parameter]
        [ValidateNotNull]
        public string[] PromptText { get; set; }

        [Parameter]
        public ViModeStyle ViModeIndicator
        {
            get => _viModeIndicator.GetValueOrDefault();
            set => _viModeIndicator = value;
        }
        internal ViModeStyle? _viModeIndicator;

        [Parameter]
        public ScriptBlock ViModeChangeHandler { get; set; }

        [Parameter]
        public PredictionSource PredictionSource
        {
            get => _predictionSource.GetValueOrDefault();
            set => _predictionSource = value;
        }
        internal PredictionSource? _predictionSource;

        [Parameter]
        public Hashtable Colors { get; set; }

        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            PSConsoleReadLine.SetOptions(this);
        }
    }

    public class ChangePSReadLineKeyHandlerCommandBase : PSCmdlet
    {
        [Parameter(Position = 0, Mandatory = true)]
        [Alias("Key")]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        public string[] Chord { get; set; }

        [Parameter]
        public ViMode ViMode { get; set; }

        [ExcludeFromCodeCoverage]
        protected IDisposable UseRequestedDispatchTables()
        {
            bool inViMode = PSConsoleReadLine.GetOptions().EditMode == EditMode.Vi;
            bool viModeParamPresent = MyInvocation.BoundParameters.ContainsKey("ViMode");

            if (inViMode || viModeParamPresent)
            {
                if (!inViMode)
                {
                    // "-ViMode" must have been specified explicitly. Well, okay... we can
                    // modify the Vi tables... but isn't that an odd thing to do from
                    // not-vi mode?
                    WriteWarning(PSReadLineResources.NotInViMode);
                }

                if (ViMode == ViMode.Command)
                    return PSConsoleReadLine.UseViCommandModeTables();
                else // default if -ViMode not specified, invalid, or "Insert"
                    return PSConsoleReadLine.UseViInsertModeTables();
            }

            return null;
        }
    }

    [Cmdlet("Set", "PSReadLineKeyHandler", HelpUri = "https://go.microsoft.com/fwlink/?LinkId=528810")]
    public class SetPSReadLineKeyHandlerCommand : ChangePSReadLineKeyHandlerCommandBase, IDynamicParameters
    {
        [Parameter(Position = 1, Mandatory = true, ParameterSetName = "ScriptBlock")]
        [ValidateNotNull]
        public ScriptBlock ScriptBlock { get; set; }

        [Parameter(ParameterSetName = "ScriptBlock")]
        public string BriefDescription { get; set; }

        [Parameter(ParameterSetName = "ScriptBlock")]
        [Alias("LongDescription")]  // Alias to stay comptible with previous releases
        public string Description { get; set; }

        private const string FunctionParameter = "Function";
        private const string FunctionParameterSet = "Function";

        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            using (UseRequestedDispatchTables())
            {
                if (ParameterSetName.Equals(FunctionParameterSet))
                {
                    var function = (string)_dynamicParameters.Value[FunctionParameter].Value;
                    var mi = typeof (PSConsoleReadLine).GetMethod(function,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                    var keyHandler = (Action<ConsoleKeyInfo?, object>)
                         mi.CreateDelegate(typeof (Action<ConsoleKeyInfo?, object>));
                    var functionName = mi.Name;
                    var longDescription = PSReadLineResources.ResourceManager.GetString(functionName + "Description");

                    PSConsoleReadLine.SetKeyHandler(Chord, keyHandler, functionName, longDescription);
                }
                else
                {
                    PSConsoleReadLine.SetKeyHandler(Chord, ScriptBlock, BriefDescription, Description);
                }
            }
        }

        private readonly Lazy<RuntimeDefinedParameterDictionary> _dynamicParameters =
            new Lazy<RuntimeDefinedParameterDictionary>(CreateDynamicParametersResult);

        private static RuntimeDefinedParameterDictionary CreateDynamicParametersResult()
        {
            var bindableFunctions = (typeof(PSConsoleReadLine).GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(method =>
                    {
                        var parameters = method.GetParameters();
                        return parameters.Length == 2
                               && parameters[0].ParameterType == typeof(ConsoleKeyInfo?)
                               && parameters[1].ParameterType == typeof(object);
                    })
                .Select(method => method.Name)
                .OrderBy(name => name);

            var attributes = new Collection<Attribute>
            {
                new ParameterAttribute
                {
                    Position = 1,
                    Mandatory = true,
                    ParameterSetName = FunctionParameterSet
                },
                new ValidateSetAttribute(bindableFunctions.ToArray())
            };
            var parameter = new RuntimeDefinedParameter(FunctionParameter, typeof(string), attributes);
            var result = new RuntimeDefinedParameterDictionary {{FunctionParameter, parameter}};
            return result;
        }

        public object GetDynamicParameters()
        {
            return _dynamicParameters.Value;
        }
    }

    [Cmdlet("Get", "PSReadLineKeyHandler", DefaultParameterSetName = "FullListing", 
        HelpUri = "https://go.microsoft.com/fwlink/?LinkId=528807")]
    [OutputType(typeof(KeyHandler))]
    public class GetKeyHandlerCommand : PSCmdlet
    {
        [Parameter(ParameterSetName = "FullListing")]
        public SwitchParameter Bound
        {
            get => _bound.GetValueOrDefault();
            set => _bound = value;
        }
        private SwitchParameter? _bound;

        [Parameter(ParameterSetName = "FullListing")]
        public SwitchParameter Unbound
        {
            get => _unbound.GetValueOrDefault();
            set => _unbound = value;
        }
        private SwitchParameter? _unbound;

        [Parameter(ParameterSetName = "SpecificBindings", Position = 0, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Key")]
        public string[] Chord { get; set; }

        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            bool bound = true;
            bool unbound = false;
            if (_bound.HasValue && _unbound.HasValue)
            {
                bound = _bound.Value.IsPresent;
                unbound = _unbound.Value.IsPresent;
            }
            else if (_bound.HasValue)
            {
                bound = _bound.Value.IsPresent;
                unbound = false;
            }
            else if (_unbound.HasValue)
            {
                bound = false;
                unbound = _unbound.Value.IsPresent;
            }

            IEnumerable<PowerShell.KeyHandler> handlers;
            if (ParameterSetName.Equals("FullListing", StringComparison.OrdinalIgnoreCase))
            {
                 handlers = PSConsoleReadLine.GetKeyHandlers(bound, unbound);
            }
            else
            {
                 handlers = PSConsoleReadLine.GetKeyHandlers(Chord);
            }
            var groups = handlers.GroupBy(k => k.Group).OrderBy(g => g.Key);

            foreach (var bindings in groups)
            {
                WriteObject(bindings.OrderBy(k => k.Function), true);
            }
        }
    }

    [Cmdlet("Remove", "PSReadLineKeyHandler", HelpUri = "https://go.microsoft.com/fwlink/?LinkId=528809")]
    public class RemoveKeyHandlerCommand : ChangePSReadLineKeyHandlerCommandBase
    {
        [ExcludeFromCodeCoverage]
        protected override void EndProcessing()
        {
            using (UseRequestedDispatchTables())
            {
                PSConsoleReadLine.RemoveKeyHandler(Chord);
            }
        }
    }

    public static class VTColorUtils
    {
        public const ConsoleColor UnknownColor = (ConsoleColor) (-1);
        private static readonly Dictionary<string, ConsoleColor> ConsoleColors =
            new Dictionary<string, ConsoleColor>(StringComparer.OrdinalIgnoreCase)
            {
                {"Black", ConsoleColor.Black},
                {"DarkBlue", ConsoleColor.DarkBlue},
                {"DarkGreen", ConsoleColor.DarkGreen},
                {"DarkCyan", ConsoleColor.DarkCyan},
                {"DarkRed", ConsoleColor.DarkRed},
                {"DarkMagenta", ConsoleColor.DarkMagenta},
                {"DarkYellow", ConsoleColor.DarkYellow},
                {"Gray", ConsoleColor.Gray},
                {"DarkGray", ConsoleColor.DarkGray},
                {"Blue", ConsoleColor.Blue},
                {"Green", ConsoleColor.Green},
                {"Cyan", ConsoleColor.Cyan},
                {"Red", ConsoleColor.Red},
                {"Magenta", ConsoleColor.Magenta},
                {"Yellow", ConsoleColor.Yellow},
                {"White", ConsoleColor.White},
            };

        public static bool IsValidColor(object o)
        {
            switch (o)
            {
                case ConsoleColor c:
                    return true;

                case string s:
                    if (s.Length > 0)
                    {
                        // String can be converted to ConsoleColor, so is it a ConsoleColor?
                        if (ConsoleColors.ContainsKey(s))
                            return true;

                        // Escape sequence - assume it's fine as is
                        if (s[0] == '\x1b')
                            return true;

                        // RGB format with possible '#'
                        if (s[0] == '#')
                            s = s.Substring(1);

                        if (int.TryParse(s, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out int rgb) &&
                            rgb >= 0 && rgb <= 0x00ffffff)
                            return true;
                    }
                    break;
            }

            return false;
        }

        public static string AsEscapeSequence(object o)
        {
            return AsEscapeSequence(o, isBackground: false);
        }

        public static string AsEscapeSequence(object o, bool isBackground)
        {
            switch (o)
            {
                case ConsoleColor c:
                    return MapColorToEscapeSequence(c, isBackground);

                case string s:
                    if (s.Length > 0)
                    {
                        // String can be converted to ConsoleColor, so it is a ConsoleColor
                        if (ConsoleColors.TryGetValue(s, out ConsoleColor c))
                            return MapColorToEscapeSequence(c, isBackground);

                        // Escape sequence - assume it's fine as is
                        if (s[0] == '\x1b')
                            return s;

                        // RGB format with possible '#'
                        if (s[0] == '#')
                            s = s.Substring(1);

                        if (s.Length == 6 &&
                            int.TryParse(s, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out int rgb) &&
                            rgb >= 0 && rgb <= 0x00ffffff)
                        {
                            if (rgb < 256)
                            {
                                return "\x1b[" + (isBackground ? "4" : "3") + "8;5;" + rgb + "m";
                            }

                            var r = (rgb >> 16) & 0xff;
                            var g = (rgb >> 8) & 0xff;
                            var b = rgb & 0xff;

                            return "\x1b[" + (isBackground ? "4" : "3") + "8;2;" + r + ";" + g + ";" + b + "m";
                        }
                    }
                    break;
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, PSReadLineResources.InvalidColorValue, o.ToString()));
        }

        public static string AsEscapeSequence(ConsoleColor fg, ConsoleColor bg)
        {
            if (fg < 0 || fg >= (ConsoleColor) ForegroundColorMap.Length)
                throw new ArgumentOutOfRangeException(nameof(fg));
            if (bg < 0 || bg >= (ConsoleColor) ForegroundColorMap.Length)
                throw new ArgumentOutOfRangeException(nameof(bg));

            string ExtractCode(string s)
            {
                return s.Substring(2).TrimEnd(new[] {'m'});
            }
            return "\x1b[" + ExtractCode(ForegroundColorMap[(int)fg]) + ";" + ExtractCode(BackgroundColorMap[(int)bg]) + "m";
        }

        private static readonly string[] BackgroundColorMap = {
            "\x1b[40m", // Black
            "\x1b[44m", // DarkBlue
            "\x1b[42m", // DarkGreen
            "\x1b[46m", // DarkCyan
            "\x1b[41m", // DarkRed
            "\x1b[45m", // DarkMagenta
            "\x1b[43m", // DarkYellow
            "\x1b[47m", // Gray
            "\x1b[100m", // DarkGray
            "\x1b[104m", // Blue
            "\x1b[102m", // Green
            "\x1b[106m", // Cyan
            "\x1b[101m", // Red
            "\x1b[105m", // Magenta
            "\x1b[103m", // Yellow
            "\x1b[107m", // White
        };

        private static readonly string[] ForegroundColorMap = {
            "\x1b[30m", // Black
            "\x1b[34m", // DarkBlue
            "\x1b[32m", // DarkGreen
            "\x1b[36m", // DarkCyan
            "\x1b[31m", // DarkRed
            "\x1b[35m", // DarkMagenta
            "\x1b[33m", // DarkYellow
            "\x1b[37m", // Gray
            "\x1b[90m", // DarkGray
            "\x1b[94m", // Blue
            "\x1b[92m", // Green
            "\x1b[96m", // Cyan
            "\x1b[91m", // Red
            "\x1b[95m", // Magenta
            "\x1b[93m", // Yellow
            "\x1b[97m", // White
        };

        internal static string MapColorToEscapeSequence(ConsoleColor color, bool isBackground)
        {
            int index = (int) color;
            if (index < 0)
            {
                // TODO: light vs. dark
                if (isBackground)
                {
                    // Don't change the background - the default (unknown) background
                    // might be subtly or completely different than what we choose and
                    // look weird.
                    return "";
                }

                return ForegroundColorMap[(int) ConsoleColor.Gray];
            }

            if (index > ForegroundColorMap.Length)
            {
                return "";
            }
            return (isBackground ? BackgroundColorMap : ForegroundColorMap)[index];
        }

        public static string FormatEscape(string esc)
        {
            var replacement = (typeof(PSObject).Assembly.GetName().Version.Major < 6)
                ? "$([char]0x1b)"
                : "`e";
            return esc.Replace("\x1b", replacement);
        }

        public static string FormatColor(object seq)
        {
            var result = seq.ToString();
            if (seq is ConsoleColor) return result;

            result = result + "\"" + FormatEscape(result) + "\"" + "\x1b[0m";
            return result;
        }
    }
#pragma warning restore 1591

}
