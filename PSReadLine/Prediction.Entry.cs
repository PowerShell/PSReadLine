/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Text;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        /// <summary>
        /// This type represents an individual suggestion entry.
        /// </summary>
        private struct SuggestionEntry
        {
            internal readonly Guid PredictorId;
            internal readonly string Source;
            internal readonly string SuggestionText;
            internal readonly int InputMatchIndex;

            internal SuggestionEntry(string soruce, Guid id, string suggestion, int matchIndex)
            {
                Source = soruce;
                PredictorId = id;
                SuggestionText = suggestion;
                InputMatchIndex = matchIndex;
            }

            /// <summary>
            /// Helper method to get the rounded-up result of a division.
            /// </summary>
            private static int DivideAndRoundUp(int divisor, int dividend)
            {
                return (divisor + dividend - 1) / dividend;
            }

            /// <summary>
            /// Generate the list item text to be rendered for the list view.
            /// </summary>
            /// <remarks>
            /// The list item text is in this format:
            ///  {> --------------------ITEM TEXT-------------------- [SOURCE]}
            /// The leading character '>' and the 'SOURCE' portion are rendered with the configured metadata color.
            /// When the 'ITEM TEXT' portion contains the user input, the matching part will be rendered with the configured emphasis color.
            /// When the current suggestion entry is selected in the list view, the whole line is rendered with the configured highlighting color.
            /// </remarks>
            /// <param name="width">The width of the list item.</param>
            /// <param name="input">The user input.</param>
            /// <param name="selectionHighlighting">The highlighting sequences for a selected list item.</param>
            internal string GetListItemText(int width, string input, string selectionHighlighting)
            {
                // Calculate the 'SOURCE' portion to be rendered.
                int sourceStrLen = Source.Length;
                int sourceWidth = LengthInBufferCells(Source);
                if (sourceWidth > PredictionListView.SourceMaxWidth)
                {
                    sourceWidth = PredictionListView.SourceMaxWidth;
                    sourceStrLen = SubstringLengthByCells(Source, sourceWidth - 3);
                }

                // Calculate the remaining width after deducting the ' [SOURCE]' portion and the leading '> ' part.
                int textWidth = width - sourceWidth - 5;

                StringBuilder line = new StringBuilder(capacity: width)
                    .Append(selectionHighlighting)
                    .Append(PredictionViewBase.TextMetadataFg)
                    .Append('>')
                    .EndColorSection(selectionHighlighting)
                    .Append(' ');

                int textLengthInCells = LengthInBufferCells(SuggestionText);
                if (textLengthInCells <= textWidth)
                {
                    // Things are easy when the suggestion text can fit in the text width.
                    switch (InputMatchIndex)
                    {
                        case -1:
                            line.Append(SuggestionText);
                            break;

                        default: {
                            int start = InputMatchIndex + input.Length;
                            int length = SuggestionText.Length - start;
                            line.Append(SuggestionText, 0, InputMatchIndex)
                                .Append(_singleton._options.EmphasisColor)
                                .Append(SuggestionText, InputMatchIndex, input.Length)
                                .EndColorSection(selectionHighlighting)
                                .Append(SuggestionText, start, length);
                            break;
                        }
                    }

                    // Do padding as necessary.
                    int spacesNeeded = textWidth - textLengthInCells;
                    if (spacesNeeded > 0)
                    {
                        line.Append(Spaces(spacesNeeded));
                    }
                }
                else
                {
                    // Things are more complicated when the suggestion text doesn't fit in the text width.
                    switch (InputMatchIndex)
                    {
                        case -1: {
                            // The suggestion text doesn't contain the user input.
                            int length = SubstringLengthByCells(SuggestionText, textWidth - 3);
                            line.Append(SuggestionText, 0, length)
                                .Append("...");
                            break;
                        }

                        case 0: {
                            // The suggestion text starts with the user input, so we can divide the suggestion text into
                            // the user input (left portion) and the prediction text (right portion).
                            int inputLenInCells = LengthInBufferCells(input);
                            if (inputLenInCells < textWidth / 2)
                            {
                                // If the user input portion takes less than half of the text width,
                                // then we just truncate the suggestion text at the end.
                                int length = SubstringLengthByCells(SuggestionText, textWidth - 3);
                                line.Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, 0, input.Length)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, input.Length, length - input.Length)
                                    .Append("...");
                            }
                            else
                            {
                                // We want to reserve 5 cells at least to display the trailing characters of the user input (the left portion).
                                int rightLenInCells = LengthInBufferCells(SuggestionText, input.Length, SuggestionText.Length);
                                if (rightLenInCells <= textWidth - 3 - 5)
                                {
                                    // If the prediction text (the right portion) can fit in the rest width, the list item
                                    // will be rendered as '...LLLLLRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR'
                                    int remainingLenInCells = textWidth - 3 - rightLenInCells;
                                    int length = SubstringLengthByCellsFromEnd(SuggestionText, input.Length - 1, remainingLenInCells);
                                    line.Append(_singleton._options.EmphasisColor)
                                        .Append("...")
                                        .Append(SuggestionText, input.Length - length, length)
                                        .EndColorSection(selectionHighlighting)
                                        .Append(SuggestionText, input.Length, SuggestionText.Length - input.Length);
                                }
                                else
                                {
                                    // If the prediction text (the right portion) cannot fit in the rest width, the list
                                    // item is rendered as '...LLLLLRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR...'
                                    int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, input.Length - 1, 5);
                                    int startIndex = input.Length - leftStrLen;
                                    int totalStrLen = SubstringLengthByCells(SuggestionText, startIndex, textWidth - 6);
                                    line.Append(_singleton._options.EmphasisColor)
                                        .Append("...")
                                        .Append(SuggestionText, startIndex, leftStrLen)
                                        .EndColorSection(selectionHighlighting)
                                        .Append(SuggestionText, input.Length, totalStrLen - leftStrLen)
                                        .Append("...");
                                }
                            }

                            break;
                        }

                        default: {
                            // The user input is contained in the middle or at the end of the suggestion text, so we can
                            // divide the suggestion text into three portions:
                            //  - prediction text on the left of the user input (left portion);
                            //  - user input (mid portion);
                            //  - prediction text on the right of the user input (right portion).
                            int leftMidLenInCells = LengthInBufferCells(SuggestionText, 0, InputMatchIndex + input.Length);
                            int rightStartindex = InputMatchIndex + input.Length;
                            int threshold = DivideAndRoundUp(textWidth * 2, 3);
                            if (leftMidLenInCells <= threshold)
                            {
                                // If the (left+mid) portions take up to 2/3 of the text width, we just truncate the suggestion text at the end.
                                int rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex, textWidth - leftMidLenInCells - 3);
                                line.Append(SuggestionText, 0, InputMatchIndex)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, input.Length)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, rightStartindex, rightStrLen)
                                    .Append("...");
                                break;
                            }

                            int midRightLenInCells = LengthInBufferCells(SuggestionText, InputMatchIndex, SuggestionText.Length);
                            if (midRightLenInCells <= threshold)
                            {
                                // Otherwise, if the (mid+right) portions take up to 2/3 of the text width, we just truncate the suggestion text at the beginning.
                                int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1, textWidth - midRightLenInCells - 3);
                                line.Append("...")
                                    .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, input.Length)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                                break;
                            }

                            int midLenInCells = LengthInBufferCells(SuggestionText, InputMatchIndex, InputMatchIndex + input.Length);
                            threshold = DivideAndRoundUp(textWidth, 3);
                            if (midLenInCells <= threshold)
                            {
                                // Otherwise, if the mid portion takes up to 1/3 of the text width, we truncate the suggestion text
                                // at both the beginning and the end.
                                int leftCellLen = (textWidth - midLenInCells) / 2;
                                int rigthCellLen = textWidth - midLenInCells - leftCellLen;

                                int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1, leftCellLen - 3);
                                int rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex, rigthCellLen - 3);

                                line.Append("...")
                                    .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, input.Length)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, rightStartindex, rightStrLen)
                                    .Append("...");
                                break;
                            }

                            int leftPlusRightLenInCells = leftMidLenInCells + midRightLenInCells - midLenInCells * 2;
                            if (leftPlusRightLenInCells <= textWidth - 7)
                            {
                                // Otherwise, the mid portion is relatively too long. In this case, if the (left+right) portions are not
                                // too long -- namely we can reserve 7 cells at least for the mid portion, including '...' -- then let's
                                // render the list item text as: 'LLLLLLLLLLLLLLLLLLMM...MMRRRRRRRRRRRRRRRRRR'
                                int midRemainingLenInCells = textWidth - leftPlusRightLenInCells - 3;
                                int midLeftCellLen = midRemainingLenInCells / 2;
                                int midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                                int midLeftStrLen = SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                                int midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1, midRightCellLen);

                                line.Append(SuggestionText, 0, InputMatchIndex)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                    .Append("...")
                                    .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                                break;
                            }

                            // If we reach here, then we know that the mid portion takes longer than 1/3 of the text width,
                            // and the (left+right) portions takes pretty long too, very likely close to the text width.
                            int leftLenInCells = leftMidLenInCells - midLenInCells;
                            int rightLenInCells = midRightLenInCells - midLenInCells;

                            if (leftLenInCells <= threshold)
                            {
                                // If the left portion is less than or equal to 1/3 of the text width, then we display the whole left portion,
                                // reserve 1/3 of the text width to the mid portion, and the rest width is for the right portion. So the list
                                // item text looks like: 'LLLLLLLLLMMMMMMMM...MMMMMMMMRRRRRRRRRRRRRRRRRRRRR...'
                                int midRemainingLenInCells = textWidth / 3 - 3;
                                int midLeftCellLen = midRemainingLenInCells / 2;
                                int midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                                int midLeftStrLen = SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                                int midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1, midRightCellLen);
                                int rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex, midRemainingLenInCells);

                                line.Append(SuggestionText, 0, InputMatchIndex)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                    .Append("...")
                                    .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, rightStartindex, rightStrLen)
                                    .Append("...");
                                break;
                            }

                            if (rightLenInCells <= threshold)
                            {
                                // Similarly, if the right portion is less than or equal to 1/3 of the text width, then we display the whole
                                // right portion, reserve 1/3 of the text width to the mid portion, and the rest width is for the left portion.
                                // So the list item text looks like: '...LLLLLLLLLLLLLLLLMMMMMMMM...MMMMMMMMRRRRRRRRRRRRRR'
                                int midRemainingLenInCells = textWidth / 3 - 3;
                                int midLeftCellLen = midRemainingLenInCells / 2;
                                int midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                                int midLeftStrLen = SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                                int midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1, midRightCellLen);
                                int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1, midRemainingLenInCells);

                                line.Append("...")
                                    .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                    .Append("...")
                                    .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                                break;
                            }

                            {
                                // All left, mid, and right portions take longer than 1/3 of the text width. We assign 1/3 of the text width
                                // to each of them equally in this case, so the list item text looks like:
                                //   '...LLLLLLLLLLLLLLMMMMMMMM...MMMMMMMMRRRRRRRRRRRRRRRR...'
                                int midRemainingLenInCells = textWidth / 3 - 3;
                                int midLeftCellLen = midRemainingLenInCells / 2;
                                int midRightCellLen = midRemainingLenInCells - midLeftCellLen;

                                int midLeftStrLen = SubstringLengthByCells(SuggestionText, InputMatchIndex, midLeftCellLen);
                                int midRightStrLen = SubstringLengthByCellsFromEnd(SuggestionText, rightStartindex - 1, midRightCellLen);
                                int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1, midRemainingLenInCells);
                                int rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex, midRemainingLenInCells);
                                int spacesNeeded = textWidth - midRemainingLenInCells * 3 - 9;
                                string spaces = spacesNeeded > 0 ? Spaces(spacesNeeded) : string.Empty;

                                line.Append("...")
                                    .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, midLeftStrLen)
                                    .Append("...")
                                    .Append(SuggestionText, rightStartindex - midRightStrLen, midRightStrLen)
                                    .EndColorSection(selectionHighlighting)
                                    .Append(SuggestionText, rightStartindex, rightStrLen)
                                    .Append("...")
                                    .Append(spaces);
                                break;
                            }
                        }
                    }
                }

                line.Append(' ')
                    .Append('[')
                    .Append(PredictionViewBase.TextMetadataFg);

                if (sourceStrLen == Source.Length)
                {
                    line.Append(Source);
                }
                else
                {
                    line.Append(Source, 0, sourceStrLen)
                        .Append("...");
                }

                line.EndColorSection(selectionHighlighting)
                    .Append(']');

                return line.ToString();
            }
        }
    }
}
