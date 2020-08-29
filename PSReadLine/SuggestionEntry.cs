/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.PowerShell.Internal;

namespace Microsoft.PowerShell
{
    public partial class PSConsoleReadLine
    {
        private struct SuggestionEntry
        {
            internal readonly string Source;
            internal readonly string SuggestionText;
            internal readonly int InputMatchIndex;

            internal SuggestionEntry(string soruce, string suggestion, int matchIndex)
            {
                Source = soruce;
                SuggestionText = suggestion;
                InputMatchIndex = matchIndex;
            }

            private static int DivideAndRoundUp(int divisor, int dividend)
            {
                return (divisor + dividend - 1) / dividend;
            }

            internal string GetListItemText(int width, string input)
            {
                // '> ------list-item-text------ [History]'
                int textWidth = width - PredictionListView.SourceWidth - 5;

                StringBuilder line = new StringBuilder(capacity: width)
                    .Append(PredictionViewBase.TextMetadataFg)
                    .Append('>')
                    .Append(PredictionViewBase.DefaultFg)
                    .Append(' ');

                int textLengthInCells = LengthInBufferCells(SuggestionText);
                if (textLengthInCells <= textWidth)
                {
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
                                .Append(PredictionViewBase.DefaultFg)
                                .Append(SuggestionText, start, length);
                            break;
                        }
                    }

                    int spacesNeeded = textWidth - textLengthInCells;
                    if (spacesNeeded > 0)
                    {
                        line.Append(Spaces(spacesNeeded));
                    }
                }
                else
                {
                    switch (InputMatchIndex)
                    {
                        case -1: {
                            int length = SubstringLengthByCells(SuggestionText, textWidth - 3);
                            line.Append(SuggestionText, 0, length)
                                .Append("...");
                            break;
                        }

                        case 0: {
                            int inputLenInCells = LengthInBufferCells(input);
                            if (inputLenInCells < textWidth / 2)
                            {
                                int length = SubstringLengthByCells(SuggestionText, textWidth - 3);
                                line.Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, 0, input.Length)
                                    .Append(PredictionViewBase.DefaultFg)
                                    .Append(SuggestionText, input.Length, length - input.Length)
                                    .Append("...");
                            }
                            else
                            {
                                int rightLenInCells = LengthInBufferCells(SuggestionText, input.Length, SuggestionText.Length);
                                if (rightLenInCells <= textWidth - 3 - 5)
                                {
                                    int remainingLenInCells = textWidth - 3 - rightLenInCells;
                                    int length = SubstringLengthByCellsFromEnd(SuggestionText, input.Length - 1, remainingLenInCells);
                                    line.Append(_singleton._options.EmphasisColor)
                                        .Append("...")
                                        .Append(SuggestionText, input.Length - length, length)
                                        .Append(PredictionViewBase.DefaultFg)
                                        .Append(SuggestionText, input.Length, SuggestionText.Length - input.Length);
                                }
                                else
                                {
                                    int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, input.Length - 1, 5);
                                    int startIndex = input.Length - leftStrLen;
                                    int totalStrLen = SubstringLengthByCells(SuggestionText, startIndex, textWidth - 6);
                                    line.Append(_singleton._options.EmphasisColor)
                                        .Append("...")
                                        .Append(SuggestionText, startIndex, leftStrLen)
                                        .Append(PredictionViewBase.DefaultFg)
                                        .Append(SuggestionText, input.Length, totalStrLen - leftStrLen)
                                        .Append("...");
                                }
                            }

                            break;
                        }

                        default: {
                            int leftMidLenInCells = LengthInBufferCells(SuggestionText, 0, InputMatchIndex + input.Length);
                            int rightStartindex = InputMatchIndex + input.Length;
                            int threshold = DivideAndRoundUp(textWidth * 2, 3);
                            if (leftMidLenInCells <= threshold)
                            {
                                int rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex, textWidth - leftMidLenInCells - 3);
                                line.Append(SuggestionText, 0, InputMatchIndex)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, input.Length)
                                    .Append(PredictionViewBase.DefaultFg)
                                    .Append(SuggestionText, rightStartindex, rightStrLen)
                                    .Append("...");
                                break;
                            }

                            int midRightLenInCells = LengthInBufferCells(SuggestionText, InputMatchIndex, SuggestionText.Length);
                            if (midRightLenInCells <= threshold)
                            {
                                int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1, textWidth - midRightLenInCells - 3);
                                line.Append("...")
                                    .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, input.Length)
                                    .Append(PredictionViewBase.DefaultFg)
                                    .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                                break;
                            }

                            int midLenInCells = LengthInBufferCells(SuggestionText, InputMatchIndex, InputMatchIndex + input.Length);
                            threshold = DivideAndRoundUp(textWidth, 3);
                            if (midLenInCells <= threshold)
                            {
                                int leftCellLen = (textWidth - midLenInCells) / 2;
                                int rigthCellLen = textWidth - midLenInCells - leftCellLen;

                                int leftStrLen = SubstringLengthByCellsFromEnd(SuggestionText, InputMatchIndex - 1, leftCellLen - 3);
                                int rightStrLen = SubstringLengthByCells(SuggestionText, rightStartindex, rigthCellLen - 3);

                                line.Append("...")
                                    .Append(SuggestionText, InputMatchIndex - leftStrLen, leftStrLen)
                                    .Append(_singleton._options.EmphasisColor)
                                    .Append(SuggestionText, InputMatchIndex, input.Length)
                                    .Append(PredictionViewBase.DefaultFg)
                                    .Append(SuggestionText, rightStartindex, rightStrLen)
                                    .Append("...");
                                break;
                            }

                            int leftPlusRightLenInCells = leftMidLenInCells + midRightLenInCells - midLenInCells * 2;
                            if (leftPlusRightLenInCells <= textWidth - 7)
                            {
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
                                    .Append(PredictionViewBase.DefaultFg)
                                    .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                                break;
                            }

                            int leftLenInCells = leftMidLenInCells - midLenInCells;
                            int rightLenInCells = midRightLenInCells - midLenInCells;

                            if (leftLenInCells <= threshold)
                            {
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
                                    .Append(PredictionViewBase.DefaultFg)
                                    .Append(SuggestionText, rightStartindex, rightStrLen)
                                    .Append("...");
                                break;
                            }

                            if (rightLenInCells <= threshold)
                            {
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
                                    .Append(PredictionViewBase.DefaultFg)
                                    .Append(SuggestionText, rightStartindex, SuggestionText.Length - rightStartindex);
                                break;
                            }

                            {
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
                                    .Append(PredictionViewBase.DefaultFg)
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
                    .Append(PredictionViewBase.TextMetadataFg)
                    .Append(Source)
                    .Append(PredictionViewBase.DefaultFg)
                    .Append(']');

                return line.ToString();
            }
        }
    }
}
