using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PathCompress
{
    static class Extensions
    {
        public static void Add(this Dictionary<string, int> table, string key)
        {
            if (table.ContainsKey(key))
                table[key]++;
            else
                table.Add(key, 1);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
     (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> knownKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (knownKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        private const int FILE_SHARE_READ = 1;
        private const int FILE_SHARE_WRITE = 2;

        private const int CREATION_DISPOSITION_OPEN_EXISTING = 3;

        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        // http://msdn.microsoft.com/en-us/library/aa364962%28VS.85%29.aspx
        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetFinalPathNameByHandle(IntPtr handle, [In, Out] StringBuilder path, int bufLen, int flags);

        // http://msdn.microsoft.com/en-us/library/aa363858(VS.85).aspx
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode,
        IntPtr SecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        public static string GetSymbolicLinkTarget(this DirectoryInfo symlink)
        {
            using (SafeFileHandle fileHandle = CreateFile(symlink.FullName, 0, 2, System.IntPtr.Zero, CREATION_DISPOSITION_OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, System.IntPtr.Zero))
            {
                if (fileHandle.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                StringBuilder path = new StringBuilder(512);
                int size = GetFinalPathNameByHandle(fileHandle.DangerousGetHandle(), path, path.Capacity, 0);
                if (size < 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                // The remarks section of GetFinalPathNameByHandle mentions the return being prefixed with "\\?\"
                // More information about "\\?\" here -> http://msdn.microsoft.com/en-us/library/aa365247(v=VS.85).aspx
                if (path[0] == '\\' && path[1] == '\\' && path[2] == '?' && path[3] == '\\')
                    return path.ToString().Substring(4);
                else
                    return path.ToString();
            }
        }

        [DllImport("kernel32.dll")]
        public static extern bool CreateSymbolicLink(
        string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        public enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }
    }
}
