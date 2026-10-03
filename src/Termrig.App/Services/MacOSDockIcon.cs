namespace Termrig.App.Services
{
    using Avalonia.Platform;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Sets the macOS Dock icon for unbundled launches (dotnet run, global tool), where there is no
    /// .app bundle Info.plist to supply one. Avalonia does not map Window.Icon to the Dock icon.
    /// </summary>
    internal static class MacOSDockIcon
    {
        #region Private-Members

        private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";
        private const string IconUri = "avares://Termrig/Assets/termrig-logo.png";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply the Termrig logo as the application Dock icon. Must be called on the UI thread after
        /// the platform has initialized NSApplication. No-op on other platforms.
        /// </summary>
        internal static void TryApply()
        {
            if (!OperatingSystem.IsMacOS()) return;

            // Inside Termrig.app the bundle's Termrig.icns already supplies the Dock icon.
            string? processPath = Environment.ProcessPath;
            if (processPath != null && processPath.Contains(".app/Contents/MacOS/", StringComparison.Ordinal)) return;

            try
            {
                byte[] bytes;
                using (Stream stream = AssetLoader.Open(new Uri(IconUri)))
                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    bytes = buffer.ToArray();
                }

                IntPtr nsApp = SendIntPtr(GetClass("NSApplication"), GetSelector("sharedApplication"));
                if (nsApp == IntPtr.Zero) return;

                GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    IntPtr data = SendIntPtr(
                        GetClass("NSData"),
                        GetSelector("dataWithBytes:length:"),
                        pinned.AddrOfPinnedObject(),
                        (IntPtr)bytes.Length);
                    if (data == IntPtr.Zero) return;

                    IntPtr image = SendIntPtr(SendIntPtr(GetClass("NSImage"), GetSelector("alloc")), GetSelector("initWithData:"), data);
                    if (image == IntPtr.Zero) return;

                    SendVoid(nsApp, GetSelector("setApplicationIconImage:"), image);
                    SendVoid(image, GetSelector("release"));
                }
                finally
                {
                    pinned.Free();
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Private-Methods

        private static IntPtr GetClass(string name)
        {
            return objc_getClass(name);
        }

        private static IntPtr GetSelector(string name)
        {
            return sel_registerName(name);
        }

        [DllImport(ObjCLibrary)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(ObjCLibrary)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern void SendVoid(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern void SendVoid(IntPtr receiver, IntPtr selector, IntPtr arg1);

        #endregion
    }
}
