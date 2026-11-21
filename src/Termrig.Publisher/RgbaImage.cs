namespace Termrig.Publisher
{
    using System;

    /// <summary>An 8-bit straight-alpha RGBA bitmap with the resampling and compositing installer artwork needs.</summary>
    public sealed class RgbaImage
    {
        /// <summary>Width in pixels. Minimum 1.</summary>
        public int Width { get; }

        /// <summary>Height in pixels. Minimum 1.</summary>
        public int Height { get; }

        /// <summary>Row-major pixels, four bytes (R, G, B, A) each.</summary>
        public byte[] Pixels { get; }

        /// <summary>Creates a transparent image.</summary>
        /// <param name="width">Width in pixels, 1 to 16384.</param>
        /// <param name="height">Height in pixels, 1 to 16384.</param>
        /// <exception cref="ArgumentOutOfRangeException">A dimension is outside 1 to 16384.</exception>
        public RgbaImage(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 16384);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 16384);
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];
        }

        /// <summary>Creates an image filled with one color.</summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="red">Red, 0 to 255.</param>
        /// <param name="green">Green, 0 to 255.</param>
        /// <param name="blue">Blue, 0 to 255.</param>
        /// <param name="alpha">Alpha, 0 (transparent) to 255 (opaque).</param>
        /// <returns>The filled image.</returns>
        public static RgbaImage Filled(int width, int height, byte red, byte green, byte blue, byte alpha)
        {
            RgbaImage image = new RgbaImage(width, height);
            for (int index = 0; index < image.Pixels.Length; index += 4)
            {
                image.Pixels[index] = red;
                image.Pixels[index + 1] = green;
                image.Pixels[index + 2] = blue;
                image.Pixels[index + 3] = alpha;
            }
            return image;
        }

        /// <summary>Returns a resized copy using area averaging in premultiplied alpha, which keeps edges clean when shrinking.</summary>
        /// <param name="width">New width in pixels.</param>
        /// <param name="height">New height in pixels.</param>
        /// <returns>The resized image.</returns>
        public RgbaImage Resize(int width, int height)
        {
            RgbaImage result = new RgbaImage(width, height);
            double scaleX = (double)Width / width;
            double scaleY = (double)Height / height;
            for (int y = 0; y < height; y++)
            {
                double top = y * scaleY;
                double bottom = top + scaleY;
                for (int x = 0; x < width; x++)
                {
                    double left = x * scaleX;
                    double right = left + scaleX;
                    double red = 0, green = 0, blue = 0, alpha = 0, total = 0;
                    if (scaleX <= 1 && scaleY <= 1)
                    {
                        Bilinear(left + scaleX / 2 - 0.5, top + scaleY / 2 - 0.5, out red, out green, out blue, out alpha);
                        total = 1;
                    }
                    else
                    {
                        for (int sy = (int)Math.Floor(top); sy < Math.Min(Height, (int)Math.Ceiling(bottom)); sy++)
                        {
                            double coverY = Math.Min(bottom, sy + 1) - Math.Max(top, sy);
                            for (int sx = (int)Math.Floor(left); sx < Math.Min(Width, (int)Math.Ceiling(right)); sx++)
                            {
                                double weight = coverY * (Math.Min(right, sx + 1) - Math.Max(left, sx));
                                int source = (sy * Width + sx) * 4;
                                double a = Pixels[source + 3] / 255.0;
                                red += Pixels[source] * a * weight;
                                green += Pixels[source + 1] * a * weight;
                                blue += Pixels[source + 2] * a * weight;
                                alpha += a * weight;
                                total += weight;
                            }
                        }
                    }
                    int target = (y * width + x) * 4;
                    if (alpha > 0)
                    {
                        result.Pixels[target] = Clamp(red / alpha);
                        result.Pixels[target + 1] = Clamp(green / alpha);
                        result.Pixels[target + 2] = Clamp(blue / alpha);
                    }
                    result.Pixels[target + 3] = Clamp(alpha / total * 255.0);
                }
            }
            return result;
        }

        /// <summary>Alpha-blends another image onto this one.</summary>
        /// <param name="image">The image to draw.</param>
        /// <param name="left">Destination x of the image's left edge. May be negative.</param>
        /// <param name="top">Destination y of the image's top edge. May be negative.</param>
        public void Draw(RgbaImage image, int left, int top)
        {
            ArgumentNullException.ThrowIfNull(image);
            for (int y = Math.Max(0, -top); y < image.Height && y + top < Height; y++)
            {
                for (int x = Math.Max(0, -left); x < image.Width && x + left < Width; x++)
                {
                    int source = (y * image.Width + x) * 4;
                    int target = ((y + top) * Width + x + left) * 4;
                    double sourceAlpha = image.Pixels[source + 3] / 255.0;
                    double targetAlpha = Pixels[target + 3] / 255.0;
                    double outAlpha = sourceAlpha + targetAlpha * (1 - sourceAlpha);
                    if (outAlpha <= 0) continue;
                    for (int channel = 0; channel < 3; channel++)
                        Pixels[target + channel] = Clamp((image.Pixels[source + channel] * sourceAlpha + Pixels[target + channel] * targetAlpha * (1 - sourceAlpha)) / outAlpha);
                    Pixels[target + 3] = Clamp(outAlpha * 255.0);
                }
            }
        }

        /// <summary>Returns a square canvas of the given size with this image scaled to fit inside a margin and centered.</summary>
        /// <param name="size">Canvas width and height in pixels.</param>
        /// <param name="margin">Fraction of the size left empty on each side, 0 to 0.45. Default 0.</param>
        /// <returns>The new image.</returns>
        public RgbaImage FitSquare(int size, double margin = 0) => Fit(new RgbaImage(size, size), margin);

        /// <summary>Scales this image to fit inside a canvas, keeping its aspect ratio, and centers it there.</summary>
        /// <param name="canvas">The background to draw on; modified and returned.</param>
        /// <param name="margin">Fraction of the canvas's shorter side left empty on each side, 0 to 0.45.</param>
        /// <returns>The canvas.</returns>
        public RgbaImage Fit(RgbaImage canvas, double margin)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentOutOfRangeException.ThrowIfLessThan(margin, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(margin, 0.45);
            int inset = (int)Math.Round(Math.Min(canvas.Width, canvas.Height) * margin);
            double scale = Math.Min((double)(canvas.Width - 2 * inset) / Width, (double)(canvas.Height - 2 * inset) / Height);
            int width = Math.Max(1, (int)Math.Round(Width * scale));
            int height = Math.Max(1, (int)Math.Round(Height * scale));
            canvas.Draw(Resize(width, height), (canvas.Width - width) / 2, (canvas.Height - height) / 2);
            return canvas;
        }

        private void Bilinear(double x, double y, out double red, out double green, out double blue, out double alpha)
        {
            x = Math.Clamp(x, 0, Width - 1);
            y = Math.Clamp(y, 0, Height - 1);
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            int x1 = Math.Min(Width - 1, x0 + 1), y1 = Math.Min(Height - 1, y0 + 1);
            double fx = x - x0, fy = y - y0;
            red = green = blue = alpha = 0;
            foreach (int corner in new[] { 0, 1, 2, 3 })
            {
                int sx = (corner & 1) == 0 ? x0 : x1;
                int sy = (corner & 2) == 0 ? y0 : y1;
                double weight = ((corner & 1) == 0 ? 1 - fx : fx) * ((corner & 2) == 0 ? 1 - fy : fy);
                int source = (sy * Width + sx) * 4;
                double a = Pixels[source + 3] / 255.0;
                red += Pixels[source] * a * weight;
                green += Pixels[source + 1] * a * weight;
                blue += Pixels[source + 2] * a * weight;
                alpha += a * weight;
            }
        }

        private static byte Clamp(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}
