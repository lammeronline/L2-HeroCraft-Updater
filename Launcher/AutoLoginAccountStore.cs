using System.IO;
using System.Text.Json;

namespace Launcher;

/// <summary>
/// Local AutoLogin account persistence. Server config only controls feature visibility;
/// real permission checks still belong on the game/auth server.
/// </summary>
public static class AutoLoginAccountStore
{
    public static async Task<List<AutoLoginAccount>> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<AutoLoginAccount>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static async Task SaveAsync(string path, IEnumerable<AutoLoginAccount> accounts)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var values = accounts
            .Where(account => !string.IsNullOrWhiteSpace(account.Login))
            .Select(account => new AutoLoginAccount
            {
                Login = account.Login.Trim(),
                Password = account.Password
            })
            .DistinctBy(account => account.Login, StringComparer.OrdinalIgnoreCase)
            .OrderBy(account => account.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var json = JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}
