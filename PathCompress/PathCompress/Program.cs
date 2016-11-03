using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;

/// <summary>
/// Remove duplicate paths.
/// Remove nonexistant paths.
/// Use symlinks to shorten paths.
/// </summary>
namespace PathCompress
{
    class Program
    {
        static void Main(string[] args)
        {
            string pathVar = Environment.GetEnvironmentVariable("path", EnvironmentVariableTarget.Machine);
            string newPathVar = PathCompress.Compress(pathVar, 30);

            Console.WriteLine(pathVar);
            Console.WriteLine(newPathVar);
            Console.WriteLine(pathVar.Length.ToString() + " -> " + newPathVar.Length.ToString());

            Console.WriteLine("Write to path?(y/n)");
            string input = Console.ReadLine();
            if(input.Equals("y")) Environment.SetEnvironmentVariable("path", newPathVar, EnvironmentVariableTarget.Machine);

        }

        //static Dictionary<string, int> getSubDirectories
    }

    public static class PathCompress
    {
        private const string WORK_DIR = @"C:\l";

        /// <summary>
        /// This function removes duplicate entries and nonexistant entries from the path.
        /// </summary>
        /// <param name="pathVar">The current contents of the PATH</param>
        /// <returns>Cleaned up path.</returns>
        public static string CleanPath(string pathVar)
        {
            string retval = pathVar.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                                   .Where(path => new DirectoryInfo(path).Exists)
                                   .Select(path => new DirectoryInfo(path).GetSymbolicLinkTarget())
                                   .Distinct()
                                   .OrderBy(change => change)
                                   .Aggregate(new StringBuilder(), (current, next) => current.Append(current.Length > 0 ? ";" : "").Append(next))
                                   .ToString();

            return retval;
        }

        /// <summary>
        /// Get a list containing all parent directories.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<string> GetAllParentDirs(string path)
        {
            List<string> retval = new List<string>(path.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Count());

            DirectoryInfo di = new DirectoryInfo(path);

            while (di != null)
            {
                retval.Add(di.FullName);
                di = di.Parent;
            }

            return retval;
        }

        public static Dictionary<string, int> CountDirectories(string pathVar)
        {
            Dictionary<string, int> table = new Dictionary<string, int>();

            foreach(string path in pathVar.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach(string parentPath in GetAllParentDirs(path))
                {
                    table.Add(parentPath);
                }
            }

            return table;
        }

        public static string Compress(string pathVar, int threshold)
        {
            PrepareForCompression();

            string retval;
            int count = 0;
            string newPath = CleanPath(pathVar);

            do
            {
                retval = newPath;

                count++;
                string nextSymLink = makeSymPath(count);

                KeyValuePair<string, int> max = CountDirectories(newPath).Where(pair => pair.Key.StartsWith("C:"))
                                                                         .OrderByDescending(pair => pair.Value * (pair.Key.Length - nextSymLink.Length))
                                                                         .First();
                string symLinkTarget = new DirectoryInfo(max.Key).GetSymbolicLinkTarget();

                Extensions.CreateSymbolicLink(nextSymLink, symLinkTarget, Extensions.SymbolicLink.Directory);
                Console.WriteLine(nextSymLink + " -> " + symLinkTarget);

                newPath = newPath.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(path => !path.StartsWith(max.Key) ? path : path.Replace(max.Key, nextSymLink))
                                 .Aggregate(new StringBuilder(), (current, next) => current.Append(current.Length > 0 ? ";" : "").Append(next))
                                 .ToString();

            } while (retval.Length - newPath.Length > threshold);

            return retval;
        }

        private static void PrepareForCompression()
        {
            if (!Directory.Exists(WORK_DIR))
                Directory.CreateDirectory(WORK_DIR);
            int count = 1;
            while (Directory.Exists(makeSymPath(count)))
            {
                Directory.Delete(makeSymPath(count));
                count++;
            }
            File.WriteAllText(WORK_DIR + Path.DirectorySeparatorChar + "backup" + DateTime.Now.Ticks.ToString() + ".txt", Environment.GetEnvironmentVariable("path", EnvironmentVariableTarget.Machine));
        }

        private static string makeSymPath(int count)
        {
            return WORK_DIR + Path.DirectorySeparatorChar.ToString() + count.ToString();
        }
            
    }

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
