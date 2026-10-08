using System;

namespace XIVLauncher.Linux.Patching.ZiPatch
{
    public class ZiPatchException : Exception
    {
        public ZiPatchException(string message = "ZiPatch error", Exception? innerException = null) : base(message, innerException)
        {
        }
    }
}