namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using XTerm;
    using XTerm.Buffer;
    using XTerm.Options;
    using XTerm.Parser;

    /// <summary>
    /// Touchstone descriptors for Termrig-owned XTerm.NET regression coverage.
    /// </summary>
    public static class XTermSuites
    {
        /// <summary>
        /// XTerm.NET regression suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor XTermNetRegressionSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "XTermNET",
                displayName: "XTerm.NET",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "TextPresentationCheckmarkIsSingleWidth",
                        displayName: "Text-presentation checkmark is single width",
                        executeAsync: TextPresentationCheckmarkIsSingleWidthAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "EmojiPresentationCheckmarkIsDoubleWidth",
                        displayName: "Emoji-presentation checkmark is double width",
                        executeAsync: EmojiPresentationCheckmarkIsDoubleWidthAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "JoinedEmojiSequenceIsSingleWideCell",
                        displayName: "Joined emoji sequence is single wide cell",
                        executeAsync: JoinedEmojiSequenceIsSingleWideCellAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "ColonTrueColorForegroundIsParsed",
                        displayName: "Colon truecolor foreground is parsed",
                        executeAsync: ColonTrueColorForegroundIsParsedAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "ColonTrueColorBackgroundIsParsed",
                        displayName: "Colon truecolor background is parsed",
                        executeAsync: ColonTrueColorBackgroundIsParsedAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "ColonPaletteForegroundIsParsed",
                        displayName: "Colon 256-color foreground is parsed",
                        executeAsync: ColonPaletteForegroundIsParsedAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "DockerNetworkStatusColumnIsPreserved",
                        displayName: "Docker network status column is preserved",
                        executeAsync: DockerNetworkStatusColumnIsPreservedAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "CursorForwardClampsAtRightMargin",
                        displayName: "Cursor forward clamps at right margin",
                        executeAsync: CursorForwardClampsAtRightMarginAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "EraseCharactersDoesNotMoveCursor",
                        displayName: "Erase characters does not move cursor",
                        executeAsync: EraseCharactersDoesNotMoveCursorAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "OriginModeCursorPositionUsesScrollRegion",
                        displayName: "Origin mode cursor position uses scroll region",
                        executeAsync: OriginModeCursorPositionUsesScrollRegionAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "OriginModeLargeCursorPositionClampsToScrollBottom",
                        displayName: "Origin mode large cursor position clamps to scroll bottom",
                        executeAsync: OriginModeLargeCursorPositionClampsToScrollBottomAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "OriginModeLinePositionUsesScrollRegion",
                        displayName: "Origin mode line position uses scroll region",
                        executeAsync: OriginModeLinePositionUsesScrollRegionAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "SettingScrollRegionHomesCursor",
                        displayName: "Setting scroll region homes cursor",
                        executeAsync: SettingScrollRegionHomesCursorAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "SelectionTextUsesLineFeedLineEndings",
                        displayName: "Selection text uses LF line endings",
                        executeAsync: SelectionTextUsesLineFeedLineEndingsAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "SelectionTextClampsBounds",
                        displayName: "Selection text clamps bounds",
                        executeAsync: SelectionTextClampsBoundsAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "HyperlinkChangedFiresForStartAndClear",
                        displayName: "Hyperlink changed fires for start and clear",
                        executeAsync: HyperlinkChangedFiresForStartAndClearAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "PartialScrollRegionPreservesRowsBelow",
                        displayName: "Partial scroll region preserves rows below",
                        executeAsync: PartialScrollRegionPreservesRowsBelowAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "InsertLinesUsesActiveBufferCoordinates",
                        displayName: "Insert lines uses active buffer coordinates",
                        executeAsync: InsertLinesUsesActiveBufferCoordinatesAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "DeleteLinesUsesActiveBufferCoordinates",
                        displayName: "Delete lines uses active buffer coordinates",
                        executeAsync: DeleteLinesUsesActiveBufferCoordinatesAsync),

                    new TestCaseDescriptor(
                        suiteId: "XTermNET",
                        caseId: "DeleteLinesOutsideScrollRegionIsIgnored",
                        displayName: "Delete lines outside scroll region is ignored",
                        executeAsync: DeleteLinesOutsideScrollRegionIsIgnoredAsync)
                });
        }

        private static Task TextPresentationCheckmarkIsSingleWidthAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 20, rows: 3);

            terminal.Write("\u2714X");

            AssertEqual(2, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual("\u2714X", terminal.Buffer.Lines[0]?.TranslateToString(true), "Line text mismatch.");
            AssertEqual(1, terminal.Buffer.Lines[0]?[0].Width, "Checkmark width mismatch.");
            AssertEqual(1, terminal.Buffer.Lines[0]?[1].Width, "Following character width mismatch.");
            return Task.CompletedTask;
        }

        private static Task EmojiPresentationCheckmarkIsDoubleWidthAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 20, rows: 3);

            terminal.Write("\u2714\uFE0FX");

            AssertEqual(3, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual("\u2714\uFE0FX", terminal.Buffer.Lines[0]?.TranslateToString(true), "Line text mismatch.");
            AssertEqual(2, terminal.Buffer.Lines[0]?[0].Width, "Emoji checkmark width mismatch.");
            AssertEqual(0, terminal.Buffer.Lines[0]?[1].Width, "Emoji spacer width mismatch.");
            AssertEqual(1, terminal.Buffer.Lines[0]?[2].Width, "Following character width mismatch.");
            return Task.CompletedTask;
        }

        private static Task JoinedEmojiSequenceIsSingleWideCellAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 20, rows: 3);
            const string sequence = "\U0001F468\u200D\U0001F4BB";

            terminal.Write(sequence + "X");

            AssertEqual(3, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual(sequence + "X", terminal.Buffer.Lines[0]?.TranslateToString(true), "Line text mismatch.");
            AssertEqual(sequence, terminal.Buffer.Lines[0]?[0].Content, "Joined emoji cell content mismatch.");
            AssertEqual(2, terminal.Buffer.Lines[0]?[0].Width, "Joined emoji width mismatch.");
            AssertEqual(0, terminal.Buffer.Lines[0]?[1].Width, "Joined emoji spacer width mismatch.");
            AssertEqual("X", terminal.Buffer.Lines[0]?[2].Content, "Following character mismatch.");
            AssertEqual(1, terminal.Buffer.Lines[0]?[2].Width, "Following character width mismatch.");
            return Task.CompletedTask;
        }

        private static Task ColonTrueColorForegroundIsParsedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 20, rows: 3);

            terminal.Write("\x1B[1;38:2::12:34:56mX");

            BufferCell cell = terminal.Buffer.Lines[0]?[0] ?? throw new InvalidOperationException("Expected first cell.");
            AssertEqual(1, cell.Attributes.GetFgColorMode(), "Foreground color mode mismatch.");
            AssertEqual((12 << 16) | (34 << 8) | 56, cell.Attributes.GetFgColor(), "Foreground RGB mismatch.");
            AssertTrue(cell.Attributes.IsBold(), "Bold attribute should be preserved.");
            return Task.CompletedTask;
        }

        private static Task ColonTrueColorBackgroundIsParsedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 20, rows: 3);

            terminal.Write("\x1B[48:2::98:76:54mX");

            BufferCell cell = terminal.Buffer.Lines[0]?[0] ?? throw new InvalidOperationException("Expected first cell.");
            AssertEqual(1, cell.Attributes.GetBgColorMode(), "Background color mode mismatch.");
            AssertEqual((98 << 16) | (76 << 8) | 54, cell.Attributes.GetBgColor(), "Background RGB mismatch.");
            return Task.CompletedTask;
        }

        private static Task ColonPaletteForegroundIsParsedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 20, rows: 3);

            terminal.Write("\x1B[38:5:203mX");

            BufferCell cell = terminal.Buffer.Lines[0]?[0] ?? throw new InvalidOperationException("Expected first cell.");
            AssertEqual(0, cell.Attributes.GetFgColorMode(), "Foreground color mode mismatch.");
            AssertEqual(203, cell.Attributes.GetFgColor(), "Foreground palette index mismatch.");
            return Task.CompletedTask;
        }

        private static Task DockerNetworkStatusColumnIsPreservedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 80, rows: 3);
            const string prefix = " \u2714 Network docker_default";

            terminal.Write(prefix);
            terminal.Write("\x1B[28C");
            terminal.Write("Created");

            BufferLine? line = terminal.Buffer.Lines[0];
            if (line == null) throw new InvalidOperationException("Expected first terminal line.");
            int statusColumn = prefix.Length + 28;
            AssertEqual("Created", line.TranslateToString(false, statusColumn, statusColumn + 7), "Docker status column mismatch.");
            return Task.CompletedTask;
        }

        private static Task CursorForwardClampsAtRightMarginAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 10, rows: 3);

            terminal.Write("\x1B[1;8H\x1B[20C");

            AssertEqual(9, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual(0, terminal.Buffer.Y, "Cursor row mismatch.");
            return Task.CompletedTask;
        }

        private static Task EraseCharactersDoesNotMoveCursorAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal(cols: 20, rows: 3);

            terminal.Write("abcdef");
            terminal.Write("\x1B[1;3H\x1B[3X");

            AssertEqual(2, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual(0, terminal.Buffer.Y, "Cursor row mismatch.");
            AssertEqual("ab   f", terminal.Buffer.Lines[0]?.TranslateToString(true), "Erased line text mismatch.");
            return Task.CompletedTask;
        }

        private static Task OriginModeCursorPositionUsesScrollRegionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal();

            terminal.Write("\x1B[5;20r\x1B[?6h\x1B[3;20H");

            AssertEqual(19, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual(6, terminal.Buffer.Y, "Cursor row mismatch.");
            return Task.CompletedTask;
        }

        private static Task OriginModeLargeCursorPositionClampsToScrollBottomAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal();
            InputHandler handler = new InputHandler(terminal);
            terminal.Buffer.SetScrollRegion(4, 19);
            terminal.OriginMode = true;
            Params parameters = new Params();
            parameters.AddParam(Int32.MaxValue);
            parameters.AddParam(20);

            handler.HandleCsi("H", parameters);

            AssertEqual(19, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual(19, terminal.Buffer.Y, "Cursor row mismatch.");
            return Task.CompletedTask;
        }

        private static Task OriginModeLinePositionUsesScrollRegionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal();

            terminal.Write("\x1B[5;20r\x1B[?6h\x1B[6;16H\x1B[3d");

            AssertEqual(15, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual(6, terminal.Buffer.Y, "Cursor row mismatch.");
            return Task.CompletedTask;
        }

        private static Task SettingScrollRegionHomesCursorAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal();

            terminal.Write("\x1B[11;11H\x1B[5;20r");

            AssertEqual(0, terminal.Buffer.X, "Cursor column mismatch.");
            AssertEqual(0, terminal.Buffer.Y, "Cursor row mismatch.");
            return Task.CompletedTask;
        }

        private static Task SelectionTextUsesLineFeedLineEndingsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = new Terminal(new TerminalOptions { Rows = 3, Cols = 80, Scrollback = 20 });
            terminal.Write("alpha\r\nbeta\r\ngamma");

            terminal.Selection.StartSelection(0, 0);
            terminal.Selection.UpdateSelection(4, 2);
            terminal.Selection.EndSelection();

            string selectedText = terminal.Selection.GetSelectionText();
            AssertFalse(selectedText.Contains("\r", StringComparison.Ordinal), "Selection text should not contain carriage returns.");
            AssertEqual(2, Count(selectedText, '\n'), "Selection text line-feed count mismatch.");
            AssertStartsWith("alpha", selectedText, "Selection text start mismatch.");
            AssertContains("\nbeta", selectedText, "Selection text middle mismatch.");
            AssertEndsWith("gamma", selectedText, "Selection text end mismatch.");
            return Task.CompletedTask;
        }

        private static Task SelectionTextClampsBoundsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = new Terminal(new TerminalOptions { Rows = 3, Cols = 10, Scrollback = 20 });
            terminal.Write("alpha");

            terminal.Selection.StartSelection(-3, 0);
            terminal.Selection.UpdateSelection(4, 0);
            terminal.Selection.EndSelection();
            AssertEqual("alpha", terminal.Selection.GetSelectionText(), "Negative selection column was not clamped.");

            terminal.Selection.StartSelection(0, 0);
            terminal.Selection.UpdateSelection(30, 0);
            terminal.Selection.EndSelection();
            AssertStartsWith("alpha", terminal.Selection.GetSelectionText(), "Past-edge selection column was not clamped.");

            Terminal zeroColumnTerminal = new Terminal(new TerminalOptions { Rows = 3, Cols = 0, Scrollback = 20 });
            zeroColumnTerminal.Selection.StartSelection(0, 0);
            zeroColumnTerminal.Selection.UpdateSelection(0, 0);
            zeroColumnTerminal.Selection.EndSelection();
            AssertEqual(String.Empty, zeroColumnTerminal.Selection.GetSelectionText(), "Zero-column selection text should be empty.");
            return Task.CompletedTask;
        }

        private static Task HyperlinkChangedFiresForStartAndClearAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateTerminal();
            var urls = new List<string?>();
            terminal.HyperlinkChanged += (_, e) => urls.Add(e.Url);

            terminal.Write("\x1B]8;;http://example.com\x07");
            terminal.Write("\x1B]8;;\x07");

            AssertEqual(2, urls.Count, "Hyperlink change event count mismatch.");
            AssertEqual("http://example.com", urls[0], "Hyperlink start URL mismatch.");
            AssertEqual<string?>(null, urls[1], "Hyperlink clear URL mismatch.");
            AssertEqual<string?>(null, terminal.CurrentHyperlink, "Current hyperlink should be cleared.");
            return Task.CompletedTask;
        }

        private static Task PartialScrollRegionPreservesRowsBelowAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var buffer = new TerminalBuffer(10, 5, 100);

            SetCell(buffer, 0, "A");
            SetCell(buffer, 1, "B");
            SetCell(buffer, 2, "C");
            SetCell(buffer, 3, "D");
            SetCell(buffer, 4, ">");

            buffer.SetScrollRegion(0, 3);
            buffer.ScrollUp(1);

            AssertEqual(0, buffer.YBase, "Partial scroll region should not promote into scrollback.");
            AssertLineCell(buffer, 0, "B");
            AssertLineCell(buffer, 1, "C");
            AssertLineCell(buffer, 2, "D");
            AssertSpace(buffer, 3);
            AssertLineCell(buffer, 4, ">");
            return Task.CompletedTask;
        }

        private static Task InsertLinesUsesActiveBufferCoordinatesAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateScrolledTerminalWithActiveRows();
            int yBase = terminal.Buffer.YBase;

            terminal.Write("\x1B[1;4r\x1B[1;1H\x1B[1L");

            AssertEqual(yBase, terminal.Buffer.YBase, "YBase should be preserved.");
            AssertSpace(terminal.Buffer, yBase + 0);
            AssertLineCell(terminal.Buffer, yBase + 1, "A");
            AssertLineCell(terminal.Buffer, yBase + 2, "B");
            AssertLineCell(terminal.Buffer, yBase + 3, "C");
            AssertLineCell(terminal.Buffer, yBase + 4, ">");
            return Task.CompletedTask;
        }

        private static Task DeleteLinesUsesActiveBufferCoordinatesAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateScrolledTerminalWithActiveRows();
            int yBase = terminal.Buffer.YBase;

            terminal.Write("\x1B[1;4r\x1B[1;1H\x1B[1M");

            AssertEqual(yBase, terminal.Buffer.YBase, "YBase should be preserved.");
            AssertLineCell(terminal.Buffer, yBase + 0, "B");
            AssertLineCell(terminal.Buffer, yBase + 1, "C");
            AssertLineCell(terminal.Buffer, yBase + 2, "D");
            AssertSpace(terminal.Buffer, yBase + 3);
            AssertLineCell(terminal.Buffer, yBase + 4, ">");
            return Task.CompletedTask;
        }

        private static Task DeleteLinesOutsideScrollRegionIsIgnoredAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Terminal terminal = CreateScrolledTerminalWithActiveRows();
            int yBase = terminal.Buffer.YBase;

            terminal.Write("\x1B[1;4r\x1B[5;1H\x1B[1M");

            AssertEqual(yBase, terminal.Buffer.YBase, "YBase should be preserved.");
            AssertLineCell(terminal.Buffer, yBase + 0, "A");
            AssertLineCell(terminal.Buffer, yBase + 1, "B");
            AssertLineCell(terminal.Buffer, yBase + 2, "C");
            AssertLineCell(terminal.Buffer, yBase + 3, "D");
            AssertLineCell(terminal.Buffer, yBase + 4, ">");
            return Task.CompletedTask;
        }

        private static Terminal CreateTerminal(int cols = 80, int rows = 24)
        {
            return new Terminal(new TerminalOptions
            {
                Cols = cols,
                Rows = rows,
                Scrollback = 1000,
                TermName = "xterm-256color"
            });
        }

        private static Terminal CreateScrolledTerminalWithActiveRows()
        {
            Terminal terminal = new Terminal(new TerminalOptions { Cols = 10, Rows = 5, Scrollback = 100 });
            terminal.Write("s1\r\ns2\r\ns3\r\ns4\r\ns5\r\n");
            int yBase = terminal.Buffer.YBase;

            SetCell(terminal.Buffer, yBase + 0, "A");
            SetCell(terminal.Buffer, yBase + 1, "B");
            SetCell(terminal.Buffer, yBase + 2, "C");
            SetCell(terminal.Buffer, yBase + 3, "D");
            SetCell(terminal.Buffer, yBase + 4, ">");

            return terminal;
        }

        private static void SetCell(TerminalBuffer buffer, int row, string content)
        {
            BufferLine? line = buffer.GetLine(row);
            if (line == null) throw new InvalidOperationException("Expected buffer line " + row + ".");
            var cell = new BufferCell { Content = content, Width = content.Length == 0 ? 0 : 1 };
            line.SetCell(0, ref cell);
        }

        private static void AssertLineCell(TerminalBuffer buffer, int row, string expected)
        {
            BufferLine? line = buffer.GetLine(row);
            if (line == null) throw new InvalidOperationException("Expected buffer line " + row + ".");
            AssertEqual(expected, line[0].Content, "Cell content mismatch at row " + row + ".");
        }

        private static void AssertSpace(TerminalBuffer buffer, int row)
        {
            BufferLine? line = buffer.GetLine(row);
            if (line == null) throw new InvalidOperationException("Expected buffer line " + row + ".");
            AssertTrue(line[0].IsSpace(), "Expected space cell at row " + row + ".");
        }

        private static int Count(string text, char expected)
        {
            int count = 0;
            foreach (char ch in text)
            {
                if (ch == expected) count++;
            }

            return count;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition) throw new InvalidOperationException(message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + "; Actual: " + actual + ".");
            }
        }

        private static void AssertContains(string expected, string actual, string message)
        {
            if (!actual.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(message + " Expected to contain: " + expected + "; Actual: " + actual + ".");
            }
        }

        private static void AssertStartsWith(string expected, string actual, string message)
        {
            if (!actual.StartsWith(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(message + " Expected prefix: " + expected + "; Actual: " + actual + ".");
            }
        }

        private static void AssertEndsWith(string expected, string actual, string message)
        {
            if (!actual.EndsWith(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(message + " Expected suffix: " + expected + "; Actual: " + actual + ".");
            }
        }
    }
}
