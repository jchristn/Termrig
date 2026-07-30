namespace Test.Terminal
{
    using XTerm.Buffer;
    using Xunit;

    /// <summary>
    /// Covers <see cref="BufferLine.Version"/>, the O(1) change token that replaced per-cell content
    /// hashing in the terminal render cache. Correctness of the render cache depends on the version
    /// advancing on every content mutation and staying put on pure reads.
    /// </summary>
    public class BufferLineVersionTests
    {
        private static BufferCell Cell(string content)
        {
            return new BufferCell(content, 1, AttributeData.Default);
        }

        [Fact]
        public void PristineLineStartsAtVersionZero()
        {
            var line = new BufferLine(10);
            Assert.Equal(0, line.Version);
        }

        [Fact]
        public void MutationsAdvanceTheVersion()
        {
            var line = new BufferLine(10);
            var cell = Cell("X");

            line.SetCell(0, ref cell);
            long afterSetCell = line.Version;
            Assert.True(afterSetCell > 0);

            line[1] = cell;
            Assert.True(line.Version > afterSetCell);
            long afterIndexer = line.Version;

            line.Fill(BufferCell.Space);
            Assert.True(line.Version > afterIndexer);
            long afterFill = line.Version;

            line.LineAttribute = LineAttribute.DoubleWidth;
            Assert.True(line.Version > afterFill);
            long afterAttr = line.Version;

            line.Resize(20, BufferCell.Space);
            Assert.True(line.Version > afterAttr);
            long afterResize = line.Version;

            line.Invalidate();
            Assert.Equal(afterResize + 1, line.Version);
        }

        [Fact]
        public void ReadingCellsDoesNotAdvanceTheVersion()
        {
            var line = new BufferLine(10);
            var cell = Cell("X");
            line.SetCell(0, ref cell);
            long version = line.Version;

            // Negative: pure reads must not invalidate a cached render.
            BufferCell read = line[0];
            _ = line.GetTrimmedLength();
            _ = line.TranslateToString();

            Assert.Equal("X", read.Content);
            Assert.Equal(version, line.Version);
        }

        [Fact]
        public void CloneCopiesVersionButIsIndependentAfterwards()
        {
            var line = new BufferLine(10);
            var cell = Cell("X");
            line.SetCell(0, ref cell);

            BufferLine clone = line.Clone();
            Assert.Equal(line.Version, clone.Version);

            // Mutating the clone must not disturb the source line's version (identity is what the
            // render cache uses to tell them apart).
            var other = Cell("Y");
            clone.SetCell(1, ref other);
            Assert.True(clone.Version > line.Version);
        }

        [Fact]
        public void CopyFromAdvancesVersionSoReusedSlotsInvalidateCaches()
        {
            var source = new BufferLine(10);
            var cell = Cell("Z");
            source.SetCell(0, ref cell);

            var destination = new BufferLine(10);
            long before = destination.Version;
            destination.CopyFrom(source);

            // A buffer line object can be reused (via the circular list) with new content; the version
            // must change so any render cache keyed to its previous version is treated as stale.
            Assert.True(destination.Version > before);
            Assert.Equal("Z", destination[0].Content);
        }
    }
}
