using System.Text.RegularExpressions;

namespace L2ModernUpdater.Core;

public sealed class IgnoreRuleMatcher
{
    private readonly List<Regex> _rules;

    public IgnoreRuleMatcher(IEnumerable<string> rules)
    {
        _rules = rules
            .Select(NormalizeRule)
            .Where(rule => !string.IsNullOrWhiteSpace(rule) && !rule.StartsWith('#'))
            .Select(CreateRegex)
            .ToList();
    }

    public bool IsIgnored(string relativePath)
    {
        var normalizedPath = NormalizePath(relativePath);
        return _rules.Any(rule => rule.IsMatch(normalizedPath));
    }

    private static string NormalizeRule(string rule)
    {
        return NormalizePath(rule.Trim());
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static Regex CreateRegex(string rule)
    {
        var pattern = "^" + Regex.Escape(rule)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]*")
            .Replace("\\?", "[^/]") + "$";

        if (rule.EndsWith("/*", StringComparison.Ordinal))
        {
            pattern = "^" + Regex.Escape(rule[..^2])
                .Replace("\\*\\*", ".*")
                .Replace("\\*", "[^/]*")
                .Replace("\\?", "[^/]") + "(/.*)?$";
        }

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
