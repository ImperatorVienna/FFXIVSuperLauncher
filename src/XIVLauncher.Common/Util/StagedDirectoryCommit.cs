namespace XIVLauncher.Common.Util;

// Same-volume staged replacement; never delete the working installation before download.
public static class StagedDirectoryCommit
{
    public static void Replace(string staging, string destination)
    {
        var backup = destination + ".old-" + Guid.NewGuid().ToString("N");
        if (Directory.Exists(destination)) Directory.Move(destination, backup);
        try { Directory.Move(staging, destination); }
        catch { if (Directory.Exists(backup)) Directory.Move(backup, destination); throw; }
        try { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
