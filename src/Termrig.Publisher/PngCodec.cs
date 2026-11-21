namespace Termrig.Publisher
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    /// <summary>Reads and writes non-interlaced 8-bit PNG files without native imaging libraries.</summary>
    public static class PngCodec
    {
        private static readonly byte[] _Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>Decodes a PNG. Supports 8-bit grayscale, RGB, palette (with tRNS), gray-alpha, and RGBA, non-interlaced.</summary>
        /// <param name="data">The PNG file contents.</param>
        /// <returns>The decoded image.</returns>
        /// <exception cref="PublisherException">The data is not a supported PNG.</exception>
        public static RgbaImage Decode(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length < 8 || !data.AsSpan(0, 8).SequenceEqual(_Signature)) throw new PublisherException("The logo is not a PNG file.");
            int width = 0, height = 0, colorType = -1;
            byte[] palette = Array.Empty<byte>();
            byte[] transparency = Array.Empty<byte>();
            using MemoryStream compressed = new MemoryStream();
            int offset = 8;
            while (offset + 8 <= data.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
                string type = Encoding.ASCII.GetString(data, offset + 4, 4);
                if (length < 0 || offset + 12 + length > data.Length) throw new PublisherException("The PNG is truncated.");
                ReadOnlySpan<byte> body = data.AsSpan(offset + 8, length);
                switch (type)
                {
                    case "IHDR":
                        width = BinaryPrimitives.ReadInt32BigEndian(body);
                        height = BinaryPrimitives.ReadInt32BigEndian(body.Slice(4));
                        int bitDepth = body[8];
                        colorType = body[9];
                        if (bitDepth != 8) throw new PublisherException("Only 8-bit PNG logos are supported; re-export the logo as 8-bit RGBA.");
                        if (body[12] != 0) throw new PublisherException("Interlaced PNG logos are not supported; re-export without interlacing.");
                        break;
                    case "PLTE": palette = body.ToArray(); break;
                    case "tRNS": transparency = body.ToArray(); break;
                    case "IDAT": compressed.Write(body); break;
                }
                offset += 12 + length;
                if (type == "IEND") break;
            }
            int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new PublisherException("Unsupported PNG color type " + colorType + ".") };
            compressed.Position = 0;
            using ZLibStream inflater = new ZLibStream(compressed, CompressionMode.Decompress);
            int stride = width * channels;
            byte[] raw = new byte[(stride + 1) * height];
            inflater.ReadExactly(raw);
            RgbaImage image = new RgbaImage(width, height);
            byte[] previous = new byte[stride];
            byte[] current = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                int filter = raw[y * (stride + 1)];
                Array.Copy(raw, y * (stride + 1) + 1, current, 0, stride);
                Unfilter(filter, current, previous, channels);
                for (int x = 0; x < width; x++)
                {
                    int target = (y * width + x) * 4;
                    int source = x * channels;
                    switch (colorType)
                    {
                        case 0: image.Pixels[target] = image.Pixels[target + 1] = image.Pixels[target + 2] = current[source]; image.Pixels[target + 3] = 255; break;
                        case 2: image.Pixels[target] = current[source]; image.Pixels[target + 1] = current[source + 1]; image.Pixels[target + 2] = current[source + 2]; image.Pixels[target + 3] = 255; break;
                        case 3:
                            int entry = current[source];
                            if (entry * 3 + 2 >= palette.Length) throw new PublisherException("The PNG palette is incomplete.");
                            image.Pixels[target] = palette[entry * 3]; image.Pixels[target + 1] = palette[entry * 3 + 1]; image.Pixels[target + 2] = palette[entry * 3 + 2];
                            image.Pixels[target + 3] = entry < transparency.Length ? transparency[entry] : (byte)255;
                            break;
                        case 4: image.Pixels[target] = image.Pixels[target + 1] = image.Pixels[target + 2] = current[source]; image.Pixels[target + 3] = current[source + 1]; break;
                        default: Array.Copy(current, source, image.Pixels, target, 4); break;
                    }
                }
                byte[] swap = previous;
                previous = current;
                current = swap;
            }
            return image;
        }

        /// <summary>Encodes an image as an 8-bit RGBA PNG.</summary>
        /// <param name="image">The image to encode.</param>
        /// <returns>The PNG file contents.</returns>
        public static byte[] Encode(RgbaImage image)
        {
            ArgumentNullException.ThrowIfNull(image);
            int stride = image.Width * 4;
            byte[] raw = new byte[(stride + 1) * image.Height];
            for (int y = 0; y < image.Height; y++)
            {
                raw[y * (stride + 1)] = 0;
                Array.Copy(image.Pixels, y * stride, raw, y * (stride + 1) + 1, stride);
            }
            using MemoryStream deflated = new MemoryStream();
            using (ZLibStream deflater = new ZLibStream(deflated, CompressionLevel.SmallestSize, leaveOpen: true)) deflater.Write(raw);
            byte[] header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
            header[8] = 8;
            header[9] = 6;
            using MemoryStream output = new MemoryStream();
            output.Write(_Signature);
            WriteChunk(output, "IHDR", header);
            WriteChunk(output, "IDAT", deflated.ToArray());
            WriteChunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        private static void Unfilter(int filter, byte[] current, byte[] previous, int bytesPerPixel)
        {
            for (int index = 0; index < current.Length; index++)
            {
                int left = index >= bytesPerPixel ? current[index - bytesPerPixel] : 0;
                int up = previous[index];
                int upLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : 0;
                int predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new PublisherException("The PNG uses unknown filter " + filter + ".")
                };
                current[index] = (byte)(current[index] + predictor);
            }
        }

        private static int Paeth(int left, int up, int upLeft)
        {
            int estimate = left + up - upLeft;
            int distanceLeft = Math.Abs(estimate - left), distanceUp = Math.Abs(estimate - up), distanceUpLeft = Math.Abs(estimate - upLeft);
            if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft) return left;
            return distanceUp <= distanceUpLeft ? up : upLeft;
        }

        private static void WriteChunk(Stream output, string type, byte[] body)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(buffer, body.Length);
            output.Write(buffer);
            byte[] typeAndBody = new byte[4 + body.Length];
            Encoding.ASCII.GetBytes(type, typeAndBody);
            Array.Copy(body, 0, typeAndBody, 4, body.Length);
            output.Write(typeAndBody);
            BinaryPrimitives.WriteUInt32BigEndian(buffer, Crc32.Compute(typeAndBody));
            output.Write(buffer);
        }
    }
}
