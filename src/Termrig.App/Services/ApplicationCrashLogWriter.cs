namespace Termrig.App.Services
{
    using System;
    using System.Threading;
    using Termrig.Core.Models;
    using Termrig.Core.Services;

    internal static class ApplicationCrashLogWriter
    {
        #region Private-Members

        private static int _CrashLogWritten = 0;

        #endregion

        #region Public-Methods

        internal static void TryWrite(string summary, string details)
        {
            if (Interlocked.Exchange(ref _CrashLogWritten, 1) != 0) return;

            try
            {
                WorkspaceRecoveryState? recoveryState = null;
                try
                {
                    WorkspaceRecoveryStore recoveryStore = new WorkspaceRecoveryStore();
                    recoveryState = recoveryStore.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
                catch
                {
                    recoveryState = null;
                }

                CrashLogStore crashLogStore = new CrashLogStore();
                crashLogStore.WriteApplicationCrash(summary, details, recoveryState);
            }
            catch
            {
            }
        }

        #endregion
    }
}
