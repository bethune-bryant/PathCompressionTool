using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PathCompress
{
    /// <summary>
    /// The goal of this program is to reduce the size of the windows path variable by:
    /// -Removing duplicate paths.
    /// -Removing nonexistent paths.
    /// -Creating and using symlinks to shorten paths.
    /// </summary>
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
    }

    public static class PathCompress
    {
        private const string WORK_DIR = @"C:\l";

        /// <summary>
        /// Cleans the path and then compresses it with symlinks until making a new sym link
        /// does not shorten th epath by at least <paramref name="threshold"/>.
        /// </summary>
        /// <param name="pathVar"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
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

                //Find the sub-path which, when replaced, would save the most characters.
                KeyValuePair<string, int> max = CountDirectories(newPath).Where(pair => pair.Key.StartsWith("C:"))
                                                                         .OrderByDescending(pair => pair.Value * (pair.Key.Length - nextSymLink.Length))
                                                                         .First();

                //Make a symbolic link to the max sub-path.
                string symLinkTarget = new DirectoryInfo(max.Key).GetSymbolicLinkTarget();
                Extensions.CreateSymbolicLink(nextSymLink, symLinkTarget, Extensions.SymbolicLink.Directory);
                Console.WriteLine(nextSymLink + " -> " + symLinkTarget);

                //Use the new symlink to shorten all the paths that used the target path.
                newPath = newPath.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(path => !path.StartsWith(max.Key) ? path : path.Replace(max.Key, nextSymLink))
                                 .Aggregate(new StringBuilder(), (current, next) => current.Append(current.Length > 0 ? ";" : "").Append(next))
                                 .ToString();

            } while (retval.Length - newPath.Length > threshold);

            return retval;
        }

        /// <summary>
        /// Does everything that needs to be done before compression can begin.
        /// -Makes the working directory if it doesn't already exist.
        /// -Cleans up the working directory by removing all the old symlinks.
        /// -Makes a backup of the path.
        /// </summary>
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

        /// <summary>
        /// Counts the number of unique directories that occur in the <paramref name="pathVar"/>.
        /// </summary>
        /// <param name="pathVar"></param>
        /// <returns></returns>
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

        private static string makeSymPath(int count)
        {
            return WORK_DIR + Path.DirectorySeparatorChar.ToString() + count.ToString();
        }
            
    }
}
