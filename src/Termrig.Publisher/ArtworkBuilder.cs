namespace Termrig.Publisher
{
    using System;
    using System.IO;

    /// <summary>Generates every icon and installer image from the manifest's logo, so no one maintains per-format artwork.</summary>
    public static class ArtworkBuilder
    {
        /// <summary>Windows icon file name.</summary>
        public const string IcoFile = "app.ico";

        /// <summary>macOS icon family file name.</summary>
        public const string IcnsFile = "app.icns";

        /// <summary>Inno Setup wizard image sizes (tall welcome image) at 100%, 150%, and 200% scaling.</summary>
        public static readonly int[] WizardImageWidths = { 164, 246, 328 };

        /// <summary>Inno Setup small wizard image sizes at 100%, 150%, and 200% scaling.</summary>
        public static readonly int[] WizardSmallSizes = { 55, 83, 110 };

        /// <summary>Linux hicolor icon sizes.</summary>
        public static readonly int[] LinuxIconSizes = { 48, 64, 128, 256, 512 };

        /// <summary>Writes every artwork file into the context's artwork directory.</summary>
        /// <param name="context">The publisher context.</param>
        /// <exception cref="PublisherException">The logo is missing or not a supported PNG.</exception>
        public static void Build(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            RgbaImage logo = Load(context, context.Manifest.Project.Logo);
            RgbaImage darkLogo = string.IsNullOrWhiteSpace(context.Manifest.Project.LogoDark) ? logo : Load(context, context.Manifest.Project.LogoDark);
            string directory = context.ArtworkDirectory;
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, IcoFile), IconEncoder.Ico(logo));
            RgbaImage macIcon = string.IsNullOrWhiteSpace(context.Manifest.Project.MacosIcon) ? logo : Load(context, context.Manifest.Project.MacosIcon);
            File.WriteAllBytes(Path.Combine(directory, IcnsFile), IconEncoder.Icns(macIcon));
            foreach (int size in LinuxIconSizes) File.WriteAllBytes(Path.Combine(directory, "icon-" + size + ".png"), PngCodec.Encode(logo.FitSquare(size)));
            foreach (int width in WizardImageWidths)
            {
                int height = (int)Math.Round(width * 314.0 / 164.0);
                RgbaImage canvas = RgbaImage.Filled(width, height, 255, 255, 255, 255);
                File.WriteAllBytes(Path.Combine(directory, "wizard-" + width + ".bmp"), IconEncoder.Bmp(logo.Fit(canvas, 0.1)));
            }
            foreach (int size in WizardSmallSizes)
                File.WriteAllBytes(Path.Combine(directory, "wizard-small-" + size + ".bmp"), IconEncoder.Bmp(logo.Fit(RgbaImage.Filled(size, size, 255, 255, 255, 255), 0.04)));
            File.WriteAllBytes(Path.Combine(directory, "pkg-background.png"), PngCodec.Encode(logo.FitSquare(200, 0.08)));
            File.WriteAllBytes(Path.Combine(directory, "pkg-background-dark.png"), PngCodec.Encode(darkLogo.FitSquare(200, 0.08)));
        }

        private static RgbaImage Load(PublisherContext context, string relative)
        {
            string path = context.Resolve(relative);
            if (!File.Exists(path)) throw new PublisherException("Logo not found: " + path + ". Set project.logo in publisher.json to a square transparent PNG.");
            return PngCodec.Decode(File.ReadAllBytes(path));
        }
    }
}
