namespace Termrig.Publisher
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>Writes Windows .ico, macOS .icns, and 24-bit .bmp files from an RGBA source.</summary>
    public static class IconEncoder
    {
        /// <summary>Sizes written into a Windows icon.</summary>
        public static IReadOnlyList<int> IcoSizes { get; } = new[] { 16, 24, 32, 48, 64, 128, 256 };

        /// <summary>Encodes a Windows icon. Sizes below 256 use 32-bit DIBs; 256 uses PNG, as Windows expects.</summary>
        /// <param name="source">A square source image, ideally at least 256 pixels.</param>
        /// <returns>The .ico file contents.</returns>
        public static byte[] Ico(RgbaImage source)
        {
            ArgumentNullException.ThrowIfNull(source);
            List<byte[]> images = new List<byte[]>();
            foreach (int size in IcoSizes)
            {
                RgbaImage scaled = source.FitSquare(size);
                images.Add(size >= 256 ? PngCodec.Encode(scaled) : Dib(scaled));
            }
            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)images.Count);
            int offset = 6 + 16 * images.Count;
            for (int index = 0; index < images.Count; index++)
            {
                int size = IcoSizes[index];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(images[index].Length);
                writer.Write(offset);
                offset += images[index].Length;
            }
            foreach (byte[] image in images) writer.Write(image);
            writer.Flush();
            return output.ToArray();
        }

        /// <summary>Encodes a macOS icon family with PNG entries from 16 to 1024 pixels.</summary>
        /// <param name="source">A square source image.</param>
        /// <returns>The .icns file contents.</returns>
        public static byte[] Icns(RgbaImage source)
        {
            ArgumentNullException.ThrowIfNull(source);
            string[] types = { "icp4", "icp5", "icp6", "ic07", "ic08", "ic09", "ic10", "ic11", "ic12", "ic13", "ic14" };
            int[] sizes = { 16, 32, 64, 128, 256, 512, 1024, 32, 64, 256, 512 };
            using MemoryStream body = new MemoryStream();
            byte[] buffer = new byte[4];
            for (int index = 0; index < types.Length; index++)
            {
                byte[] png = PngCodec.Encode(source.FitSquare(sizes[index]));
                body.Write(Encoding.ASCII.GetBytes(types[index]));
                BinaryPrimitives.WriteInt32BigEndian(buffer, png.Length + 8);
                body.Write(buffer);
                body.Write(png);
            }
            using MemoryStream output = new MemoryStream();
            output.Write(Encoding.ASCII.GetBytes("icns"));
            BinaryPrimitives.WriteInt32BigEndian(buffer, (int)body.Length + 8);
            output.Write(buffer);
            body.Position = 0;
            body.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>Encodes an opaque 24-bit bottom-up BMP, compositing transparency onto white.</summary>
        /// <param name="source">The image to encode.</param>
        /// <returns>The .bmp file contents.</returns>
        public static byte[] Bmp(RgbaImage source)
        {
            ArgumentNullException.ThrowIfNull(source);
            RgbaImage opaque = RgbaImage.Filled(source.Width, source.Height, 255, 255, 255, 255);
            opaque.Draw(source, 0, 0);
            int stride = (source.Width * 3 + 3) & ~3;
            int imageSize = stride * source.Height;
            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(14 + 40 + imageSize);
            writer.Write(0);
            writer.Write(14 + 40);
            writer.Write(40);
            writer.Write(source.Width);
            writer.Write(source.Height);
            writer.Write((ushort)1);
            writer.Write((ushort)24);
            writer.Write(0);
            writer.Write(imageSize);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);
            byte[] row = new byte[stride];
            for (int y = source.Height - 1; y >= 0; y--)
            {
                Array.Clear(row);
                for (int x = 0; x < source.Width; x++)
                {
                    int pixel = (y * source.Width + x) * 4;
                    row[x * 3] = opaque.Pixels[pixel + 2];
                    row[x * 3 + 1] = opaque.Pixels[pixel + 1];
                    row[x * 3 + 2] = opaque.Pixels[pixel];
                }
                writer.Write(row);
            }
            writer.Flush();
            return output.ToArray();
        }

        private static byte[] Dib(RgbaImage image)
        {
            int maskStride = ((image.Width + 31) / 32) * 4;
            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output);
            writer.Write(40);
            writer.Write(image.Width);
            writer.Write(image.Height * 2);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(0);
            writer.Write(image.Width * image.Height * 4 + maskStride * image.Height);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            for (int y = image.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int pixel = (y * image.Width + x) * 4;
                    writer.Write(image.Pixels[pixel + 2]);
                    writer.Write(image.Pixels[pixel + 1]);
                    writer.Write(image.Pixels[pixel]);
                    writer.Write(image.Pixels[pixel + 3]);
                }
            }
            writer.Write(new byte[maskStride * image.Height]);
            writer.Flush();
            return output.ToArray();
        }
    }
}
