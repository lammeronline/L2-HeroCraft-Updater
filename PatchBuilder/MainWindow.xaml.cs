using L2ModernUpdater.Core;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;

namespace PatchBuilder;

public partial class MainWindow : Window
{
    private const string UpdaterBaseUrl = "https://l2.lammeronline.com/updater/";
    private const string DefaultChannel = "live";
    private const string LiveManifestFileName = "manifest.json";
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public MainWindow()
    {
        InitializeComponent();

        SourceDirectoryBox.Text = Environment.CurrentDirectory;
        ChannelBox.Text = DefaultChannel;
        BaseUrlBox.Text = BuildPatchUrl(DefaultChannel);
        OutputManifestBox.Text = Path.Combine(Environment.CurrentDirectory, LiveManifestFileName);
        CompressedOutputBox.Text = Path.Combine(Environment.CurrentDirectory, "PatchCompressed", DefaultChannel);
        VersionBox.Text = DateTimeOffset.Now.ToString("yyyy.MM.dd.HHmm");
        IgnoreRulesBox.Text = string.Join(Environment.NewLine, DefaultIgnoreRules);
        LauncherFileBox.Text = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "LauncherSingle", "Launcher.exe"));
        LauncherUrlBox.Text = BuildLauncherUrl(DefaultChannel);
        AppendLog("Patch Builder started.");
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            MainProgressBar.Value = 0;

            var sourceDirectory = Path.GetFullPath(SourceDirectoryBox.Text.Trim());
            var baseUrl = BaseUrlBox.Text.Trim().TrimEnd('/');
            var outputManifest = Path.GetFullPath(OutputManifestBox.Text.Trim());
            var channel = NormalizeChannel(ChannelBox.Text);
            var version = string.IsNullOrWhiteSpace(VersionBox.Text)
                ? DateTimeOffset.UtcNow.ToString("yyyy.MM.dd.HHmm")
                : VersionBox.Text.Trim();
            var ignoreRules = ParseIgnoreRules(IgnoreRulesBox.Text);
            var extraFilePolicy = ReportExtraFilesBox.IsChecked == true ? "report" : "ignore";
            var compressFiles = CompressFilesBox.IsChecked == true;
            var compressedOutputDirectory = Path.GetFullPath(CompressedOutputBox.Text.Trim());
            var launcherInfo = await BuildLauncherInfoAsync(version);

            if (!Directory.Exists(sourceDirectory))
            {
                SetStatus("Source folder not found", sourceDirectory, 0);
                AppendLog("Source folder not found: " + sourceDirectory);
                return;
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                SetStatus("Source URL is empty", "Enter source URL.", 0);
                AppendLog("Source URL is empty.");
                return;
            }

            AppendLog("Scanning: " + sourceDirectory);
            var filePaths = await Task.Run(() =>
                Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => IsPatchFile(path, outputManifest))
                    .Where(path => !compressFiles || !IsUnderDirectory(path, compressedOutputDirectory))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList());
            AppendLog($"Skipped generated/tool files: {CountSkippedFiles(sourceDirectory, outputManifest)}");

            if (compressFiles)
            {
                Directory.CreateDirectory(compressedOutputDirectory);
                AppendLog("Compression enabled: " + compressedOutputDirectory);
            }

            var files = new List<ManifestFile>();
            for (var index = 0; index < filePaths.Count; index++)
            {
                var filePath = filePaths[index];
                var relativePath = Path.GetRelativePath(sourceDirectory, filePath).Replace('\\', '/');
                SetStatus("Hashing files", relativePath, filePaths.Count == 0 ? 100 : index * 100d / filePaths.Count);

                var info = new FileInfo(filePath);
                var sha256 = await FileHasher.ComputeSha256Async(filePath);
                var urlPath = compressFiles ? relativePath + ".gz" : relativePath;
                var url = baseUrl + "/" + EscapeUrlPath(urlPath);

                var compressedPath = compressFiles
                    ? await CompressFileAsync(filePath, compressedOutputDirectory, relativePath)
                    : null;
                var compressedInfo = compressedPath is null ? null : new FileInfo(compressedPath);
                var compressedSha256 = compressedPath is null
                    ? string.Empty
                    : await FileHasher.ComputeSha256Async(compressedPath);

                files.Add(new ManifestFile
                {
                    Path = relativePath,
                    Sha256 = sha256,
                    Size = info.Length,
                    Url = url,
                    Compressed = compressFiles,
                    CompressedSize = compressedInfo?.Length ?? 0,
                    CompressedSha256 = compressedSha256
                });

                FileCountText.Text = $"{files.Count} files";
                AppendLog($"{files.Count,6} {relativePath}");
            }

            var manifest = new UpdateManifest
            {
                Version = version,
                Channel = channel,
                Files = files,
                Ignore = ignoreRules,
                ExtraFilePolicy = extraFilePolicy,
                Launcher = launcherInfo,
                News =
                [
                    new NewsItem
                    {
                        Title = "Patch " + version,
                        Body = "Manifest generated by PatchBuilder.",
                        PublishedAt = DateTimeOffset.UtcNow
                    }
                ]
            };

            await ManifestStore.SaveAsync(manifest, outputManifest);

            SetStatus("Manifest generated", outputManifest, 100);
            AppendLog("Manifest written: " + outputManifest);
            AppendLog("Channel: " + channel);
            AppendLog("Upload manifest to: " + BuildManifestUrl(channel));
            AppendLog("Files: " + files.Count);
            if (compressFiles)
            {
                var originalBytes = files.Sum(file => file.Size);
                var compressedBytes = files.Sum(file => file.CompressedSize);
                var ratio = originalBytes == 0 ? 0 : compressedBytes * 100d / originalBytes;
                AppendLog($"Compressed size: {compressedBytes} / {originalBytes} bytes ({ratio:0.0}%).");
            }
            AppendLog("Ignore rules: " + ignoreRules.Count);
            AppendLog("Extra file policy: " + extraFilePolicy);
            AppendLog(launcherInfo is null
                ? "Launcher self-update: disabled."
                : $"Launcher self-update: {launcherInfo.Url}");
            AppendLog("Output manifest excluded from patch file list.");
        }
        catch (Exception ex)
        {
            SetStatus("Generation failed", ex.Message, MainProgressBar.Value);
            AppendLog("Generation failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            MainProgressBar.Value = 0;

            var sourceDirectory = Path.GetFullPath(SourceDirectoryBox.Text.Trim());
            var baseUrl = BaseUrlBox.Text.Trim().TrimEnd('/');
            var outputManifest = Path.GetFullPath(OutputManifestBox.Text.Trim());
            var compressFiles = CompressFilesBox.IsChecked == true;

            if (!Directory.Exists(sourceDirectory))
            {
                SetStatus("Source folder not found", sourceDirectory, 0);
                AppendLog("Source folder not found: " + sourceDirectory);
                return;
            }

            if (!IsHttpUrl(baseUrl))
            {
                SetStatus("HTTP URL required", "Use http:// or https:// for live validation.", 0);
                AppendLog("Live validation requires http:// or https:// URL.");
                return;
            }

            var filePaths = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Where(path => IsPatchFile(path, outputManifest))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            if (filePaths.Count == 0)
            {
                SetStatus("No files", "Source folder is empty.", 0);
                AppendLog("No files found to validate.");
                return;
            }

            AppendLog($"Validating {filePaths.Count} URL(s) from: {baseUrl}");
            for (var index = 0; index < filePaths.Count; index++)
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, filePaths[index]).Replace('\\', '/');
                var url = baseUrl + "/" + EscapeUrlPath(compressFiles ? relativePath + ".gz" : relativePath);
                SetStatus("Validating URLs", relativePath, index * 100d / filePaths.Count);

                var status = await ValidateUrlAsync(url);
                AppendLog($"{status} {relativePath}");
            }

            SetStatus("Validation complete", "Checked first " + filePaths.Count + " file URL(s).", 100);
        }
        catch (Exception ex)
        {
            SetStatus("Validation failed", ex.Message, MainProgressBar.Value);
            AppendLog("Validation failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void BrowseSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select patch folder",
            InitialDirectory = Directory.Exists(SourceDirectoryBox.Text)
                ? SourceDirectoryBox.Text
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            SourceDirectoryBox.Text = dialog.FolderName;
            if (ShouldReplaceBaseUrl(BaseUrlBox.Text))
            {
                BaseUrlBox.Text = BuildLocalBaseUrl(dialog.FolderName);
            }
        }
    }

    private void UseLocalButton_Click(object sender, RoutedEventArgs e)
    {
        BaseUrlBox.Text = BuildLocalBaseUrl(SourceDirectoryBox.Text);
    }

    private void ApplyChannelButton_Click(object sender, RoutedEventArgs e)
    {
        var channel = NormalizeChannel(ChannelBox.Text);
        ChannelBox.Text = channel;
        BaseUrlBox.Text = BuildPatchUrl(channel);
        LauncherUrlBox.Text = BuildLauncherUrl(channel);
        CompressedOutputBox.Text = Path.Combine(Environment.CurrentDirectory, "PatchCompressed", channel);
        AppendLog("Channel applied: " + channel);
        AppendLog("Manifest URL: " + BuildManifestUrl(channel));
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save manifest",
            FileName = "manifest.json",
            Filter = "JSON manifest (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(OutputManifestBox.Text))
                ? Path.GetDirectoryName(OutputManifestBox.Text)
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputManifestBox.Text = dialog.FileName;
        }
    }

    private void BrowseCompressedOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select compressed patch output folder",
            InitialDirectory = Directory.Exists(CompressedOutputBox.Text)
                ? CompressedOutputBox.Text
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            CompressedOutputBox.Text = dialog.FolderName;
        }
    }

    private void BrowseLauncherButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select launcher exe for self-update",
            FileName = "Launcher.exe",
            Filter = "Launcher executable (*.exe)|*.exe|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(LauncherFileBox.Text))
                ? Path.GetDirectoryName(LauncherFileBox.Text)
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            LauncherFileBox.Text = dialog.FileName;
        }
    }

    private static string EscapeUrlPath(string relativePath)
    {
        return string.Join(
            "/",
            relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
    }

    private static string BuildLocalBaseUrl(string sourceDirectory)
    {
        var fullPath = Path.GetFullPath(sourceDirectory);
        if (!fullPath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullPath += Path.DirectorySeparatorChar;
        }

        return new Uri(fullPath).AbsoluteUri.TrimEnd('/');
    }

    private static bool ShouldReplaceBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.Contains("cdn.site.com", StringComparison.OrdinalIgnoreCase)
            || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile);
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string NormalizeChannel(string value)
    {
        var channel = value.Trim().Trim('/', '\\');
        if (string.IsNullOrWhiteSpace(channel))
        {
            return DefaultChannel;
        }

        var chars = channel
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray();

        return chars.Length == 0 ? DefaultChannel : new string(chars);
    }

    private static string BuildPatchUrl(string channel)
    {
        return UpdaterBaseUrl + NormalizeChannel(channel) + "/patch/";
    }

    private static string BuildManifestUrl(string channel)
    {
        return UpdaterBaseUrl + NormalizeChannel(channel) + "/manifest.json";
    }

    private static string BuildLauncherUrl(string channel)
    {
        return UpdaterBaseUrl + NormalizeChannel(channel) + "/Launcher.exe";
    }

    private async Task<LauncherUpdateInfo?> BuildLauncherInfoAsync(string version)
    {
        var launcherPath = LauncherFileBox.Text.Trim();
        var launcherUrl = LauncherUrlBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(launcherPath) && string.IsNullOrWhiteSpace(launcherUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
        {
            AppendLog("Launcher self-update skipped: launcher exe was not found.");
            return null;
        }

        if (!IsHttpUrl(launcherUrl))
        {
            AppendLog("Launcher self-update skipped: launcher URL must be http:// or https://.");
            return null;
        }

        var info = new FileInfo(launcherPath);
        return new LauncherUpdateInfo
        {
            Version = string.IsNullOrWhiteSpace(version) ? DateTimeOffset.UtcNow.ToString("yyyy.MM.dd.HHmm") : version,
            Sha256 = await FileHasher.ComputeSha256Async(launcherPath),
            Size = info.Length,
            Url = launcherUrl
        };
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPatchFile(string path, string outputManifest)
    {
        var fileName = Path.GetFileName(path);

        return !path.EndsWith(".download", StringComparison.OrdinalIgnoreCase)
            && !PathsEqual(path, outputManifest)
            && !ExcludedPatchFileNames.Contains(fileName);
    }

    private static bool IsUnderDirectory(string filePath, string directoryPath)
    {
        var directory = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var file = Path.GetFullPath(filePath);
        return file.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> CompressFileAsync(
        string sourceFile,
        string compressedOutputDirectory,
        string relativePath)
    {
        var outputPath = Path.Combine(
            compressedOutputDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)) + ".gz";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await using var input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
        await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true);
        await using var gzip = new GZipStream(output, CompressionLevel.SmallestSize);
        await input.CopyToAsync(gzip);

        return outputPath;
    }

    private static int CountSkippedFiles(string sourceDirectory, string outputManifest)
    {
        return Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Count(path => !IsPatchFile(path, outputManifest));
    }

    private static readonly HashSet<string> ExcludedPatchFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "manifest.json",
        "launcher.log",
        "updater.log",
        "crash.log",
        "launcher.settings.json",
        "Launcher.exe",
        "Launcher.dll",
        "Launcher.deps.json",
        "Launcher.runtimeconfig.json",
        "Launcher.pdb",
        "PatchBuilder.exe",
        "PatchBuilder.dll",
        "PatchBuilder.deps.json",
        "PatchBuilder.runtimeconfig.json",
        "PatchBuilder.pdb",
        "Updater.Core.dll",
        "Updater.Core.pdb"
    };

    private async Task<string> ValidateUrlAsync(string url)
    {
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
        using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);
        if (headResponse.IsSuccessStatusCode)
        {
            return $"OK {(int)headResponse.StatusCode}";
        }

        if (headResponse.StatusCode != HttpStatusCode.MethodNotAllowed
            && headResponse.StatusCode != HttpStatusCode.NotImplemented)
        {
            return $"FAIL {(int)headResponse.StatusCode} {headResponse.ReasonPhrase}";
        }

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
        getRequest.Headers.Range = new RangeHeaderValue(0, 0);
        using var getResponse = await _httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
        return getResponse.IsSuccessStatusCode
            ? $"OK {(int)getResponse.StatusCode}"
            : $"FAIL {(int)getResponse.StatusCode} {getResponse.ReasonPhrase}";
    }

    private static List<string> ParseIgnoreRules(string text)
    {
        return text
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static readonly string[] DefaultIgnoreRules =
    [
        "# launcher generated",
        "launcher.log",
        "updater.log",
        "crash.log",
        "launcher.settings.json",
        "manifest.json",
        "",
        "# temporary files",
        "*.download",
        "*.tmp",
        "",
        "# client generated",
        "Screenshots/*",
        "Cache/*",
        "system/GameGuard/*",
        "system/*.log",
        "system/chatfilter.ini",
        "system/l2.ini",
        "system/Option.ini",
        "system/WindowsInfo.ini",
        "system/Running.ini",
        "system/s_info.ini"
    ];

    private void SetStatus(string status, string currentFile, double progress)
    {
        StatusText.Text = status;
        CurrentFileText.Text = currentFile;
        MainProgressBar.Value = Math.Clamp(progress, 0, 100);
    }

    private void SetBusy(bool busy)
    {
        GenerateButton.IsEnabled = !busy;
        ValidateButton.IsEnabled = !busy;
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
}
