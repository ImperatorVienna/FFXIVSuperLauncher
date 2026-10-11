namespace XIVLauncher.Linux.Processes;

// Lifetime policy: retain confirmed identities through temporary inspection failures.
// Unreadable processes never associated with this session do not extend its lifetime.
internal sealed class GameSessionTracker
{
    private readonly Dictionary<int,long> known = new();
    internal bool Observe(string prefix) => Update(ProcessSessions.Inspect(prefix,false), ProcessFacts.Identify);
    internal bool Update(ProcessSessions.Inspection inspection, Func<int,long,ProcessPresence> identify)
    {
        foreach(var fact in inspection.Matched)
            if(fact.StartedAt is long started) known[fact.Id]=started;
        foreach(var item in known.ToArray())
            if(identify(item.Key,item.Value)==ProcessPresence.Gone)known.Remove(item.Key);
        return known.Count>0 || inspection.Matched.Count>0;
    }
}
