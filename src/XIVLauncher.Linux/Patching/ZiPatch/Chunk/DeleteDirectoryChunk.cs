using System;
using System.IO;
using XIVLauncher.Linux.Patching.Util;

namespace XIVLauncher.Linux.Patching.ZiPatch.Chunk
{
    public class DeleteDirectoryChunk : ZiPatchChunk
    {
        public new static string Type = "DELD";

        public string DirName { get; protected set; }

        public DeleteDirectoryChunk(BinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            using var advanceAfter = this.GetAdvanceOnDispose();
            var dirNameLen = this.Reader.ReadUInt32BE();

            DirName = this.Reader.ReadFixedLengthString(dirNameLen);
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            try
            {
                Directory.Delete(XIVLauncher.Linux.Patching.ZiPatch.Util.SqexFile.Resolve(config.GamePath, DirName));
            }
            catch (Exception)
            {
                // 忽略刪除失敗
                throw;
            }
        }

        public override string ToString()
        {
            return $"{Type}:{DirName}";
        }
    }
}
