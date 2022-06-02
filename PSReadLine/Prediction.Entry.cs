/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Text;

namespace Microsoft.PowerShell;

public partial class PSConsoleReadLine
{
    /// <summary>
    ///     This type represents an individual suggestion entry.
    /// </summary>
    internal struct SuggestionEntry
    {
        internal readonly Guid PredictorId;
        internal readonly uint? PredictorSession;
        internal readonly string Source;
        internal readonly string SuggestionText;
        internal readonly int InputMatchIndex;

        internal SuggestionEntry(string suggestion, int matchIndex)
            : this("History", Guid.Empty, null, suggestion, matchIndex)
        {
        }

        internal SuggestionEntry(string source, Guid predictorId, uint? predictorSession, string suggestion,
            int matchIndex)
        {
            Source = source;
            PredictorId = predictorId;
            PredictorSession = predictorSession;
            SuggestionText = suggestion;
            InputMatchIndex = matchIndex;
        }

        /// <summary>
        ///     Helper method to get the rounded-up result of a division.
        /// </summary>
        private static int DivideAndRoundUp(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }

        /// <summary>
        ///     Generate the list item text to be rendered for the list view.
        /// </summary>
        /// <remarks>
        ///     The list item text is in this format:
        ///     {> --------------------ITEM TEXT-------------------- [SOURCE]}
        ///     The leading character '>' and the 'SOURCE' portion are rendered with the configured metadata color
        ///     (ListPredictionColor).
        ///     When the 'ITEM TEXT' portion contains the user input, the matching part will be rendered with the configured
        ///     emphasis color.
        ///     When the current suggestion entry is selected in the list view, the whole line is rendered with the configured
        ///     highlighting color (ListPredictionSelectedColor).
        /// </remarks>
        /// <param name="width">The width of the list item.</param>
        /// <param name="input">The user input.</param>
        /// <param name="selectionHighlighting">The highlighting sequences for a selected list item.</param>
        internal string GetListItemText(int width, string input, string selectionHighlighting)
        {
            const string ellipsis = "...";
            const int ellipsisLength = 3;

            // Calculate the 'SOURCE' portion to be rendered.
            var sourceStrLen = Source.Length;
            var sourceWidth = _renderer.LengthInBufferCells(Source);
            if (sourceWidth > PredictionListView.SourceMaxWidth)
            {
                sourceWidth = PredictionListView.SourceMaxWidth;
                sourceStrLen = SubstringLengthByCells(Source, sourceWidth - ellipsisLength);
            }

            // Calculate the remaining width after deducting the ' [SOURCE]' portion and the leading '> ' part.
            // 5 is the length of the decoration characters: "> ", " [", and ']'.
            var textWidth = width - sourceWidth - 5;
            var textMetadataColor = Singleton.Options._listPredictionColor;

            var line = new StringBuilder(width)
                .Append(selectionHighlighting)
                .Append(textMetadataColor)
                .Append('>')
                .EndColorSection(selectionHighlighting)
                .Append(' ');

            var textLengthInCells = _renderer.LengthInBufferCells(SuggestionText);
            if (textLengthInCells <= textWidth)
            {
                // Things are easy when the suggestion text can fit in the text width.
                switch (InputMatchIndex)
                {
                    case -1:
                        line.Append(SuggestionText);
                        break;

                    default:
                    {
                        var start = InputMatchIndex + input.Length;
                        var length = SuggestionText.Length - start;
                        line.Append(SuggestionText, 0, InputMatchIndex)
                            .Append(Singleton.Options.EmphasisColor)
                            .Append(SuggestionText, InputMatchIndex, input.Length)
                            .EndColorSection(selectionHighlighting)
                            .Append(SuggestionText, start, length);
                        break;
                    }
                }

                // Do padding as necessary.
                var spacesNeeded = textWidth - textLengthInCells;
                if (spacesNeeded > 0) line.Append(Spaces(spacesNeeded));
            }
            else
            {
                // Things are more complicated when the suggestion text doesn't fit in the text width.
                switch (InputMatchIndex)
                {
                    case -1:
                    {
                        // The suggestion text doesn't contain the user input.
                        var length = SubstringLengthByCells(SuggestionText, textWidth - ellipsisLength);
                        line.Append(SuggestionText, 0, length)
                            .Append(ellipsis);
                        break;
                    }

                    case 0:
                    {
                        // The suggestion text starts with the user input, so we can divide the suggestion text into
                        // the user input (left portion) and the prediction text (right portion).
                        var inputLenInCells = _renderer.LengthInBufferCells(input);
                        if (inputLenInCells < textWidth / 2)
                        {
                            // If the user input portion takes less than half of the text width,
                            // then we just truncate the suggestion text at the end.
                            var length = SubstringLengthByCells(SuggestionText, textWidth - ellipsisLength);
                            line.Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, 0, input.Length)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, input.Length, length - input.Length)
                                .Append(ellipsis);
                        }
                        else
                        {
                            // We want to reserve 5 cells at least to display the trailing characters of the user input (the left portion).
                            var rightLenInCells = _renderer.LengthInBufferCells(SuggestionText, input.Length,
                                SuggestionText.Length);
                            if (rightLenInCells <= textWidth - ellipsisLength - 5)
                            {
                                // If the prediction text (the right portion) can fit in the rest width, the list item
                                // will be rendered as '...LLLLLRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR'
                                var remainingLenInCells = textWidth - ellipsisLength - rightLenInCells;
                                var length = SubstringLengthByCellsFromEnd(SuggestionText, input.Length - 1,
                                    remainingLenInCells);
                                line.Append(Singleton.Options.EmphasisColor)
                                    .Append(ellipsis)
                                    .Append(SuggestionText, input.Length - length, length)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, input.Length, SuggestionText.Length - input.Length);
                            }
                            else
                            {
                                // If the prediction text (the right portion) cannot fit in the rest width, the list
                                // item is rendered as '...LLLLLRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR...'
                                var leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, input.Length - 1, 5);
                                var startIndex = input.Length - leftStrLen;
                                var totalStrLen = SubstringLengthByCells(SuggestionText, startIndex,
                                    textWidth - ellipsisLength * 2);
                                line.Append(Singleton.Options.EmphasisColor)
                                    .Append(ellipsis)
                                    .Append(SuggestionText, startIndex, leftStrLen)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, input.Length, totalStrLen - leftStrLen)
                                    .Append(ellipsis);
                            }
                        }

                        break;
                    }

                    default:
                    {
                        // The user input is contained in the middle or at the end of the suggestion text, so we can
                        // divide the suggestion text into three portions:
                        //  - prediction text on the left of the user input (left portion);
                        //  - user input (mid portion);
                        //  - prediction text on the right of the user input (right portion).
                        var end = InputMatchIndex + input.Length;
                        var leftMidLenInCells = _renderer.LengthInBufferCells(SuggestionText, 0, end);
                        var rightStartindex = InputMatchIndex + input.Length;
                        // Round up the 2/3 of the text width and use that as the threshold.
                        var threshold = DivideAndRoundUp(textWidth * 2, 3);
                        if (leftMidLenInCells <= threshold)
                        {
                            // If the (left+mid) portions take up to 2/3 of the text width, we just truncate the suggestion text at the end.
                            var rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex,
                                textWidth - leftMidLenInCells - ellipsisLength);
                            line.Append(SuggestionText, 0, InputMatchIndex)
                                .Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, input.Length)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, rightStartindex, rightStrLen)
                                .Append(ellipsis);
                            break;
                        }

                        var midRightLenInCells = _renderer.LengthInBufferCells(SuggestionText, InputMatchIndex,
                            SuggestionText.Length);
                        if (midRightLenInCells <= threshold)
                        {
                            // Otherwise, if the (mid+right) portions take up to 2/3 of the text width, we just truncate the suggestion text at the beginning.
                            var leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1,
                                textWidth - midRightLenInCells - ellipsisLength);
                            line.Append(ellipsis)
                                .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                .Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, input.Length)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                            break;
                        }

                        var end1 = InputMatchIndex + input.Length;
                        var midLenInCells = _renderer.LengthInBufferCells(SuggestionText, InputMatchIndex, end1);
                        // Round up the 1/3 of the text width and use that as the threshold.
                        threshold = DivideAndRoundUp(textWidth, 3);
                        if (midLenInCells <= threshold)
                        {
                            // Otherwise, if the mid portion takes up to 1/3 of the text width, we truncate the suggestion text
                            // at both the beginning and the end.
                            var leftCellLen = (textWidth - midLenInCells) / 2;
                            var rigthCellLen = textWidth - midLenInCells - leftCellLen;

                            var leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1,
                                leftCellLen - ellipsisLength);
                            var rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex,
                                rigthCellLen - ellipsisLength);

                            line.Append(ellipsis)
                                .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                .Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, input.Length)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, rightStartindex, rightStrLen)
                                .Append(ellipsis);
                            break;
                        }

                        var leftPlusRightLenInCells = leftMidLenInCells + midRightLenInCells - midLenInCells * 2;
                        if (leftPlusRightLenInCells <= textWidth - 7)
                        {
                            // Otherwise, the mid portion is relatively too long. In this case, if the (left+right) portions are not
                            // too long -- namely we can reserve 7 cells at least for the mid portion, including '...' -- then let's
                            // render the list item text as: 'LLLLLLLLLLLLLLLLLLMM...MMRRRRRRRRRRRRRRRRRR'
                            var midRemainingLenInCells = textWidth - leftPlusRightLenInCells - ellipsisLength;
                            var midLeftCellLen = midRemainingLenInCells / 2;
                            var midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                            var midLeftStrLen =
                                SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                            var midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1,
                                midRightCellLen);

                            line.Append(SuggestionText, 0, InputMatchIndex)
                                .Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                .Append(ellipsis)
                                .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                            break;
                        }

                        // If we reach here, then we know that the mid portion takes longer than 1/3 of the text width,
                        // and the (left+right) portions takes pretty long too, very likely close to the text width.
                        var leftLenInCells = leftMidLenInCells - midLenInCells;
                        var rightLenInCells = midRightLenInCells - midLenInCells;

                        if (leftLenInCells <= threshold)
                        {
                            // If the left portion is less than or equal to 1/3 of the text width, then we display the whole left portion,
                            // reserve 1/3 of the text width to the mid portion, and the rest width is for the right portion. So the list
                            // item text looks like: 'LLLLLLLLLMMMMMMMM...MMMMMMMMRRRRRRRRRRRRRRRRRRRRR...'
                            var midRemainingLenInCells = textWidth / 3 - ellipsisLength;
                            var midLeftCellLen = midRemainingLenInCells / 2;
                            var midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                            var midLeftStrLen =
                                SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                            var midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1,
                                midRightCellLen);
                            var rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex,
                                midRemainingLenInCells);

                            line.Append(SuggestionText, 0, InputMatchIndex)
                                .Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                .Append(ellipsis)
                                .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, rightStartindex, rightStrLen)
                                .Append(ellipsis);
                            break;
                        }

                        if (rightLenInCells <= threshold)
                        {
                            // Similarly, if the right portion is less than or equal to 1/3 of the text width, then we display the whole
                            // right portion, reserve 1/3 of the text width to the mid portion, and the rest width is for the left portion.
                            // So the list item text looks like: '...LLLLLLLLLLLLLLLLMMMMMMMM...MMMMMMMMRRRRRRRRRRRRRR'
                            var midRemainingLenInCells = textWidth / 3 - ellipsisLength;
                            var midLeftCellLen = midRemainingLenInCells / 2;
                            var midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                            var midLeftStrLen =
                                SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                            var midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1,
                                midRightCellLen);
                            var leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1,
                                midRemainingLenInCells);

                            line.Append(ellipsis)
                                .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                .Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                .Append(ellipsis)
                                .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                            break;
                        }

                        {
                            // All left, mid, and right portions take longer than 1/3 of the text width. We assign 1/3 of the text width
                            // to each of them equally in this case, so the list item text looks like:
                            //   '...LLLLLLLLLLLLLLMMMMMMMM...MMMMMMMMRRRRRRRRRRRRRRRR...'
                            var midRemainingLenInCells = textWidth / 3 - ellipsisLength;
                            var midLeftCellLen = midRemainingLenInCells / 2;
                            var midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                            var midLeftStrLen =
                                SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                            var midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1,
                                midRightCellLen);
                            var leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1,
                                midRemainingLenInCells);
                            var rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex,
                                midRemainingLenInCells);
                            var spacesNeeded = textWidth - midRemainingLenInCells * 3 - ellipsisLength * 3;
                            var spaces = spacesNeeded > 0 ? Spaces(spacesNeeded) : string.Empty;

                            line.Append(ellipsis)
                                .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                .Append(Singleton.Options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                .Append(ellipsis)
                                .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, rightStartindex, rightStrLen)
                                .Append(ellipsis)
                                .Append(spaces);
                            break;
                        }
                    }
                }
            }

            line.Append(' ')
                .Append('[')
                .Append(textMetadataColor);

            if (sourceStrLen == Source.Length)
                line.Append(Source);
            else
                line.Append(Source, 0, sourceStrLen)
                    .Append(ellipsis);

            line.EndColorSection(selectionHighlighting)
                .Append(']');

            return line.ToString();
        }
    }
}