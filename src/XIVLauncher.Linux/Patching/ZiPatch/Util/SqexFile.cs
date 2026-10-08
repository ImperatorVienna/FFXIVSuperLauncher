using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace XIVLauncher.Linux.Patching.ZiPatch.Util
{
    public class SqexFile
    {
        public string RelativePath { get; set; }

        protected SqexFile() {}

        public SqexFile(string relativePath)
        {
            RelativePath = relativePath;
        }

        public SqexFileStream? OpenStream(string basePath, FileMode mode, int tries = 5, int sleeptime = 1) =>
            SqexFileStream.WaitForStream(Resolve(basePath, RelativePath), mode, tries, sleeptime);

        public SqexFileStream OpenStream(SqexFileStreamStore store, string basePath, FileMode mode,
                                         int tries = 5, int sleeptime = 1) =>
            store.GetStream(Resolve(basePath, RelativePath), mode, tries, sleeptime);

        public void Delete(SqexFileStreamStore? store, string basePath, int tries = 5, int sleeptime = 1)
        {
            var path = Resolve(basePath, this.RelativePath);

            while (File.Exists(path))
            {
                store?.CloseStream(Resolve(basePath, this.RelativePath));

                try
                {
                    File.Delete(path);
                }
                catch (IOException ioe)
                {
                    if (ioe is FileNotFoundException or DirectoryNotFoundException)
                        break;

                    if (tries-- <= 0)
                        throw;

                    Thread.Sleep(sleeptime * 1000);
                }
            }
        }

        public void CreateDirectoryTree(string basePath)
        {
            var dirName = Path.GetDirectoryName(Resolve(basePath, RelativePath));
            if (dirName != null)
                Directory.CreateDirectory(dirName);
        }

        public static string Resolve(string root, string relative)
        {
            root = Path.GetFullPath(root).TrimEnd('/');
            var path = Path.GetFullPath(Path.Combine(root, relative.Replace('\\', '/').TrimStart('/')));
            if (!path.StartsWith(root + "/", StringComparison.Ordinal)) throw new IOException("补丁路径超出游戏目录。");
            for (var p = path; p != root; p = Path.GetDirectoryName(p)!)
                if (new FileInfo(p).LinkTarget != null || new DirectoryInfo(p).LinkTarget != null) throw new IOException("补丁目标包含符号链接。");
            return path;
        }

        public override string ToString() => RelativePath;

        public static string GetExpansionFolder(byte expansionId) =>
            expansionId == 0 ? "ffxiv" : $"ex{expansionId}";

        public static IEnumerable<string> GetAllExpansionFiles(string fullPath, ushort expansionId)
        {
            var xpacPath = GetExpansionFolder((byte)expansionId);

            var sqpack = Path.Combine(fullPath, "sqpack", xpacPath);
            var movie = Path.Combine(fullPath, "movie", xpacPath);

            var files = Enumerable.Empty<string>();

            if (Directory.Exists(sqpack))
                files = files.Concat(Directory.GetFiles(sqpack));

            if (Directory.Exists(movie))
                files = files.Concat(Directory.GetFiles(movie));

            return files.Select(file => Path.GetRelativePath(fullPath, file));
        }
    }
}
