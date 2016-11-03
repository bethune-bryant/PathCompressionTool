using Microsoft.VisualStudio.TestTools.UnitTesting;
using PathCompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathCompress.Tests
{
    [TestClass()]
    public class PathCompressTests
    {
        [TestMethod()]
        public void CleanPathTest()
        {
            string initialPath = @"C:\;C:\.\.\;;C:\;C:\somepaththatdoesntexist\";

            string newPath = PathCompress.CleanPath(initialPath);

            Assert.AreEqual(newPath, @"C:\");
        }

        [TestMethod()]
        public void GetAllParentDirsTest()
        {
            string path = @"C:\a\b\c";

            List<string> expected = new List<string>(new string[] { @"C:\a\b\c", @"C:\a\b", @"C:\a", @"C:\" });

            List<string> subPaths = PathCompress.GetAllParentDirs(path);

            Assert.AreEqual(expected.Count, subPaths.Count);

            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i], subPaths[i]);
        }

        [TestMethod()]
        public void CountDirectoriesTest()
        {
            string pathVar = @"C:\a\b\c;C:\a\b\d;C:\a\d\c";

            Dictionary<string, int> expected = new Dictionary<string, int>();
            expected.Add(@"C:\", 3);
            expected.Add(@"C:\a", 3);
            expected.Add(@"C:\a\b", 2);
            expected.Add(@"C:\a\d", 1);
            expected.Add(@"C:\a\d\c", 1);
            expected.Add(@"C:\a\b\c", 1);
            expected.Add(@"C:\a\b\d", 1);

            Dictionary<string, int> result = PathCompress.CountDirectories(pathVar);

            Assert.AreEqual(expected.Count, result.Count);
            foreach(string key in expected.Keys)
            {
                Assert.IsTrue(result.ContainsKey(key));
                Assert.AreEqual(expected[key], result[key]);
            }
        }
    }
}