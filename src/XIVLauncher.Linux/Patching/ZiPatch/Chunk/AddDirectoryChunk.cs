using System.IO;
using XIVLauncher.Linux.Patching.Util;

namespace XIVLauncher.Linux.Patching.ZiPatch.Chunk
{
    public class AddDirectoryChunk : ZiPatchChunk
    {
        public new static string Type = "ADIR";

        public string DirName { get; protected set; }

        protected override void ReadChunk()
        {
            using var advanceAfter = this.GetAdvanceOnDispose();
            var dirNameLen = this.Reader.ReadUInt32BE();

            DirName = this.Reader.ReadFixedLengthString(dirNameLen);
        }


        public AddDirectoryChunk(BinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        public override void ApplyChunk(ZiPatchConfig config)
        {
            Directory.CreateDirectory(XIVLauncher.Linux.Patching.ZiPatch.Util.SqexFile.Resolve(config.GamePath, DirName));
        }

        public override string ToString()
        {
            return $"{Type}:{DirName}";
        }
    }
}
