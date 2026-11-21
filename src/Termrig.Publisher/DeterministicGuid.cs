namespace Termrig.Publisher
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>Name-based (RFC 4122 version 5) GUIDs, so an identifier always yields the same Windows AppId.</summary>
    public static class DeterministicGuid
    {
        private static readonly Guid _UrlNamespace = new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>Creates a version 5 GUID from a name in the RFC 4122 URL namespace.</summary>
        /// <param name="name">The stable name, for example <c>ai.latticetech.termrig</c>.</param>
        /// <returns>The same GUID for the same name on every machine.</returns>
        public static Guid Create(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            byte[] namespaceBytes = _UrlNamespace.ToByteArray(bigEndian: true);
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte[] input = new byte[namespaceBytes.Length + nameBytes.Length];
            Buffer.BlockCopy(namespaceBytes, 0, input, 0, namespaceBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, input, namespaceBytes.Length, nameBytes.Length);
#pragma warning disable CA5350 // RFC 4122 version 5 is defined over SHA-1; this is an identifier, not a security boundary.
            byte[] hash = SHA1.HashData(input);
#pragma warning restore CA5350
            byte[] result = new byte[16];
            Array.Copy(hash, result, 16);
            result[6] = (byte)((result[6] & 0x0F) | 0x50);
            result[8] = (byte)((result[8] & 0x3F) | 0x80);
            return new Guid(result, bigEndian: true);
        }
    }
}
