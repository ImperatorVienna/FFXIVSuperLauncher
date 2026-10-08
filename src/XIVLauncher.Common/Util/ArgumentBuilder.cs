using System.Text;
namespace XIVLauncher.Common.Util;

// Plain game arguments shared by China and Global launch adapters.
public sealed class ArgumentBuilder
{
    private readonly List<KeyValuePair<string, string>> arguments = [];
    public ArgumentBuilder Append(string key, string value)
    {
        arguments.Add(new(key, value));
        return this;
    }
    public string Build() => arguments.Aggregate(new StringBuilder(),
        (whole, part) => whole.Append($" {part.Key}={part.Value}")).ToString();
}
