namespace Termrig.Publisher
{
    using System;

    /// <summary>The CRC-32 (IEEE 802.3) checksum used by PNG chunks.</summary>
    public static class Crc32
    {
        private static readonly uint[] _Table = BuildTable();

        /// <summary>Computes the CRC of a byte range.</summary>
        /// <param name="data">The bytes to checksum.</param>
        /// <returns>The CRC-32 value.</returns>
        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;
            foreach (byte value in data) crc = _Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildTable()
        {
            uint[] table = new uint[256];
            for (uint index = 0; index < 256; index++)
            {
                uint value = index;
                for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                table[index] = value;
            }
            return table;
        }
    }
}
