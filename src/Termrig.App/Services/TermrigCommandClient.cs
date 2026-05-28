namespace Termrig.App.Services
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Sends commands to an already-running Termrig process.
    /// </summary>
    public static class TermrigCommandClient
    {
        #region Public-Methods

        /// <summary>
        /// Try to send a command to the local Termrig command server.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a running process accepted the command.</returns>
        public static async Task<bool> TrySendAsync(CommandLineCommand command, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            try
            {
                using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", TermrigCommandServer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        timeout.CancelAfter(TimeSpan.FromMilliseconds(350));
                        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
                    }

                    using (StreamWriter writer = new StreamWriter(pipe) { AutoFlush = true })
                    using (StreamReader reader = new StreamReader(pipe))
                    {
                        await writer.WriteLineAsync(command.Serialize()).ConfigureAwait(false);
                        await reader.ReadLineAsync().ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        #endregion
    }
}
