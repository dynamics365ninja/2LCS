using LCS.JsonObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace LCS
{
    // Credentials for mstsc in the Credential Manager. Written synchronously before mstsc starts and removed
    // when it exits - cmdkey with a fixed 10 s delete raced with mstsc and made it ask for the password.
    internal sealed class RdpCredentials : IDisposable
    {
        private const uint CRED_TYPE_GENERIC = 1;
        private const uint CRED_PERSIST_SESSION = 1;

        // Per-host reference count: removed when the last connection closes.
        private static readonly Dictionary<string, int> Users = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private string _target;

        private RdpCredentials(string host, string userName, string password)
        {
            _target = $"TERMSRV/{host}";
            lock (Users)
            {
                Write(_target, userName, password);
                Users[_target] = Users.TryGetValue(_target, out var count) ? count + 1 : 1;
            }
        }

        public static Process Connect(RDPConnectionDetails rdpEntry)
        {
            var credentials = new RdpCredentials(rdpEntry.Address, $"{rdpEntry.Domain}\\{rdpEntry.Username}", rdpEntry.Password);
            try
            {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32\mstsc.exe"),
                        Arguments = string.IsNullOrEmpty(rdpEntry.Port) ? $"/v:{rdpEntry.Address}" : $"/v:{rdpEntry.Address}:{rdpEntry.Port}"
                    },
                    EnableRaisingEvents = true
                };
                process.Exited += (s, e) => credentials.Dispose();
                process.Start();
                return process;
            }
            catch
            {
                credentials.Dispose();
                throw;
            }
        }

        // Removes the credentials of all connections started by 2LCS (sign out).
        public static void RemoveAll()
        {
            lock (Users)
            {
                foreach (var target in Users.Keys.ToList())
                {
                    CredDelete(target, CRED_TYPE_GENERIC, 0);
                }
                Users.Clear();
            }
        }

        public void Dispose()
        {
            lock (Users)
            {
                if (_target == null) return;
                var remaining = Users.TryGetValue(_target, out var count) ? count - 1 : 0;
                if (remaining <= 0)
                {
                    Users.Remove(_target);
                    CredDelete(_target, CRED_TYPE_GENERIC, 0);
                }
                else
                {
                    Users[_target] = remaining;
                }
                _target = null;
            }
        }

        private static void Write(string target, string userName, string password)
        {
            var secret = password ?? string.Empty;
            var blob = Marshal.StringToCoTaskMemUni(secret);
            try
            {
                var credential = new CREDENTIAL
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = target,
                    UserName = userName,
                    CredentialBlob = blob,
                    CredentialBlobSize = (uint)(secret.Length * 2),
                    Persist = CRED_PERSIST_SESSION
                };
                if (!CredWrite(ref credential, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Storing the Remote Desktop credentials for {target} failed.");
                }
            }
            finally
            {
                // Do not leave the password in memory longer than needed.
                Marshal.ZeroFreeCoTaskMemUnicode(blob);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);
    }
}
