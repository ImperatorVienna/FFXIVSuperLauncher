namespace XIVLauncher.Linux.Processes;
internal static class ProcessFileOwner
{
    internal static string Stamp(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        return stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19];
    }
    internal static void Publish(string path, int pid)
    {
        File.WriteAllText(path + ".tmp", $"{pid} {Stamp(pid)}\n");
        File.Move(path + ".tmp", path, true);
    }
    internal static bool IsProtected(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            if (new FileInfo(path).LinkTarget != null) return true;
            var contents = File.ReadAllText(path);
            var fields = contents.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2 || !int.TryParse(fields[0], out var pid) || pid <= 0) return true;
            bool gone;
            try { gone = ProcessFacts.Read(pid).Presence == ProcessPresence.Gone || Stamp(pid) != fields[1]; }
            catch (FileNotFoundException) { gone = true; }
            catch (DirectoryNotFoundException) { gone = true; }
            if (!gone || File.ReadAllText(path) != contents) return true;
            File.Delete(path); return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }
}
