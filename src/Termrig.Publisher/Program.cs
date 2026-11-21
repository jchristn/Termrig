namespace Termrig.Publisher
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            try
            {
                PublisherOptions options = PublisherOptions.Parse(args);
                if (options.Help)
                {
                    Help();
                    return 0;
                }
                await ChannelRunner.RunAsync(options, cancellation.Token).ConfigureAwait(false);
                return 0;
            }
            catch (PublisherException error)
            {
                Console.Error.WriteLine("Publisher: " + error.Message);
                return 1;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Publisher: canceled.");
                return 130;
            }
        }

        private static void Help()
        {
            Console.WriteLine("Termrig.Publisher builds installers from publisher.json.");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run --project src/Termrig.Publisher -c Release -- --channel <name>[,<name>...] [options]");
            Console.WriteLine("       dotnet run --project src/Termrig.Publisher -c Release -- --all [options]");
            Console.WriteLine();
            Console.WriteLine("Packaging channels (in build order): " + string.Join(", ", ChannelCatalog.All));
            Console.WriteLine("  publish   self-contained single-file builds (run first)");
            Console.WriteLine("  zip, inno                Windows .zip and Inno Setup .exe (Windows host)");
            Console.WriteLine("  app, tarball, pkg, dmg   macOS bundle, .tar.gz, .pkg, and .dmg (macOS host; tarball also on Linux)");
            Console.WriteLine("  deb, rpm, appimage       Linux packages (Linux host)");
            Console.WriteLine("Utility channels: " + string.Join(", ", ChannelCatalog.Utility));
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --all                 every enabled channel this host supports, then checksums");
            Console.WriteLine("  --version <v>         override the version read from the version file");
            Console.WriteLine("  --rids <a,b>          runtime identifiers (default: the manifest's for this OS)");
            Console.WriteLine("  --output <dir>        release directory (default: installers/<version>)");
            Console.WriteLine("  --root <dir>          repository root (default: nearest directory with publisher.json)");
            Console.WriteLine("  --tag <v1.2.3>        tag checked by verify-release");
            Console.WriteLine("  --notes-output <file> destination for the notes channel");
        }
    }
}
