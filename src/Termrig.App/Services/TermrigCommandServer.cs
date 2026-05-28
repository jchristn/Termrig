namespace Termrig.App.Services
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Local command server for tr open/close commands.
    /// </summary>
    public sealed class TermrigCommandServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Local named pipe used by Termrig command processes.
        /// </summary>
        public static string PipeName
        {
            get
            {
                string user = Environment.UserName;
                if (String.IsNullOrWhiteSpace(user)) user = "default";
                foreach (char character in Path.GetInvalidFileNameChars())
                {
                    user = user.Replace(character, '_');
                }

                return "termrig-" + user;
            }
        }

        #endregion

        #region Private-Members

        private readonly Func<CommandLineCommand, Task<bool>> _Handler;
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private Task? _LoopTask = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the command server.
        /// </summary>
        /// <param name="handler">Command handler.</param>
        public TermrigCommandServer(Func<CommandLineCommand, Task<bool>> handler)
        {
            _Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start listening for commands.
        /// </summary>
        public void Start()
        {
            if (_LoopTask != null) return;
            _LoopTask = Task.Run(ListenAsync);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Cancellation.Cancel();
            _Cancellation.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task ListenAsync()
        {
            while (!_Cancellation.IsCancellationRequested)
            {
                try
                {
                    using (NamedPipeServerStream pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        await pipe.WaitForConnectionAsync(_Cancellation.Token).ConfigureAwait(false);
                        await HandlePipeAsync(pipe).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                }
            }
        }

        private async Task HandlePipeAsync(Stream pipe)
        {
            using (StreamReader reader = new StreamReader(pipe))
            using (StreamWriter writer = new StreamWriter(pipe) { AutoFlush = true })
            {
                string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (!CommandLineCommand.TryDeserialize(line, out CommandLineCommand? command) || command == null)
                {
                    await writer.WriteLineAsync("ERROR").ConfigureAwait(false);
                    return;
                }

                bool handled = await _Handler(command).ConfigureAwait(false);
                await writer.WriteLineAsync(handled ? "OK" : "ERROR").ConfigureAwait(false);
            }
        }

        #endregion
    }
}
