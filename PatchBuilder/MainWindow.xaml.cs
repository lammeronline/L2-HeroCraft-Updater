using L2ModernUpdater.Core;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;

namespace PatchBuilder;

// Manifest authoring tool. The top half of the class contains UI commands;
// helper sections below keep URL, metadata, file filtering and persistence logic separated.
public partial class MainWindow : Window
{
    private const string UpdaterBaseUrl = "https://yoursite.com/updater/";
    private const string LiveManifestFileName = "manifest.json";
    private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "patchbuilder.settings.json");
    private UpdateManifest? _loadedManifest;
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Startup
    public MainWindow()
    {
        InitializeComponent();

        SourceDirectoryBox.Text = Environment.CurrentDirectory;
        BaseUrlBox.Text = BuildPatchUrl();
        OutputManifestBox.Text = Path.Combine(Environment.CurrentDirectory, LiveManifestFileName);
        CompressedOutputBox.Text = Path.Combine(Environment.CurrentDirectory, "PatchCompressed");
        VersionBox.Text = DateTimeOffset.Now.ToString("yyyy.MM.dd.HHmm");
        IgnoreRulesBox.Text = string.Join(Environment.NewLine, DefaultIgnoreRules);
        LauncherFileBox.Text = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "LauncherSingle", "Launcher.exe"));
        LauncherUrlBox.Text = BuildLauncherUrl();
        LoadLastSettings();
        AppendLog("Patch Builder started.");
    }

    // Primary commands
    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            MainProgressBar.Value = 0;

            var sourceDirectory = Path.GetFullPath(SourceDirectoryBox.Text.Trim());
            var baseUrl = BaseUrlBox.Text.Trim().TrimEnd('/');
            var outputManifest = Path.GetFullPath(OutputManifestBox.Text.Trim());
            SaveLastSettings();
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
                Files = files,
                Ignore = ignoreRules,
                ExtraFilePolicy = extraFilePolicy,
                Launcher = launcherInfo
            };

            await ManifestStore.SaveAsync(manifest, outputManifest);

            SetStatus("Manifest generated", outputManifest, 100);
            AppendLog("Manifest written: " + outputManifest);
            AppendLog("Upload manifest to: " + BuildManifestUrl());
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
            SaveLastSettings();
            var compressFiles = CompressFilesBox.IsChecked == true;

            if (!Directory.Exists(sourceDirectory))
            {
                SetStatus("Source folder not found", sourceDirectory, 0);
                AppendLog("Source folder not found: " + sourceDirectory);
                return;
            }

            if (!IsHttpUrl(baseUrl))
            {
                SetStatus("HTTP URL required", "Use http:// or https:// for URL validation.", 0);
                AppendLog("URL validation requires http:// or https:// URL.");
                return;
            }

            var filePaths = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Where(path => IsPatchFile(path, outputManifest))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
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

            SetStatus("Validation complete", "Checked " + filePaths.Count + " file URL(s).", 100);
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

    private async void LoadMetadataButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            var manifestPath = Path.GetFullPath(OutputManifestBox.Text.Trim());
            SaveLastSettings();
            AppendLog("Loading manifest metadata: " + manifestPath);

            _loadedManifest = await ManifestStore.LoadAsync(manifestPath, _httpClient);
            VersionBox.Text = _loadedManifest.Version;
            ReportExtraFilesBox.IsChecked = string.Equals(_loadedManifest.ExtraFilePolicy, "report", StringComparison.OrdinalIgnoreCase);
            IgnoreRulesBox.Text = string.Join(Environment.NewLine, _loadedManifest.Ignore.Count == 0 ? DefaultIgnoreRules : _loadedManifest.Ignore);
            LauncherUrlBox.Text = NormalizeLauncherUrl(_loadedManifest.Launcher?.Url ?? string.Empty);

            SetStatus("Metadata loaded", $"{_loadedManifest.Files.Count} manifest file(s) preserved.", 100);
            FileCountText.Text = $"{_loadedManifest.Files.Count} files";
            AppendLog($"Metadata loaded. Files preserved: {_loadedManifest.Files.Count}.");
        }
        catch (Exception ex)
        {
            SetStatus("Metadata load failed", ex.Message, MainProgressBar.Value);
            AppendLog("Metadata load failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SaveMetadataButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            var manifestPath = Path.GetFullPath(OutputManifestBox.Text.Trim());
            SaveLastSettings();
            _loadedManifest ??= await ManifestStore.LoadAsync(manifestPath, _httpClient);

            var version = string.IsNullOrWhiteSpace(VersionBox.Text)
                ? _loadedManifest.Version
                : VersionBox.Text.Trim();
            var baseUrl = BaseUrlBox.Text.Trim().TrimEnd('/');
            var ignoreRules = ParseIgnoreRules(IgnoreRulesBox.Text);
            var extraFilePolicy = ReportExtraFilesBox.IsChecked == true ? "report" : "ignore";
            var launcherInfo = await BuildMetadataLauncherInfoAsync(version, _loadedManifest.Launcher);

            var updatedManifest = new UpdateManifest
            {
                Version = version,
                Files = RebuildFileUrls(_loadedManifest.Files, baseUrl),
                Ignore = ignoreRules,
                ExtraFilePolicy = extraFilePolicy,
                Launcher = launcherInfo
            };

            await ManifestStore.SaveAsync(updatedManifest, manifestPath);
            _loadedManifest = updatedManifest;

            SetStatus("Metadata saved", $"{updatedManifest.Files.Count} manifest file(s) preserved.", 100);
            FileCountText.Text = $"{updatedManifest.Files.Count} files";
            AppendLog("Metadata saved without rebuilding patch files: " + manifestPath);
            AppendLog("File URLs updated from source URL: " + baseUrl);
            AppendLog("Upload manifest to: " + BuildManifestUrl());
        }
        catch (Exception ex)
        {
            SetStatus("Metadata save failed", ex.Message, MainProgressBar.Value);
            AppendLog("Metadata save failed: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // File and folder pickers
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

            SaveLastSettings();
        }
    }

    private void UseLocalButton_Click(object sender, RoutedEventArgs e)
    {
        BaseUrlBox.Text = BuildLocalBaseUrl(SourceDirectoryBox.Text);
        SaveLastSettings();
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
            SaveLastSettings();
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
            SaveLastSettings();
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
            SaveLastSettings();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        SaveLastSettings();
        base.OnClosing(e);
    }

    // URL helpers
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

    private static string BuildPatchUrl()
    {
        return UpdaterBaseUrl + "patch/";
    }

    private static string BuildManifestUrl()
    {
        return UpdaterBaseUrl + "manifest.json";
    }

    private static string BuildLauncherUrl()
    {
        return UpdaterBaseUrl + "Launcher.exe";
    }

    private static string NormalizeLauncherUrl(string value)
    {
        var launcherUrl = value.Trim();
        var rootLauncherUrl = BuildLauncherUrl();

        if (string.IsNullOrWhiteSpace(launcherUrl))
        {
            return rootLauncherUrl;
        }

        if (Uri.TryCreate(launcherUrl, UriKind.Absolute, out var uri)
            && launcherUrl.StartsWith(UpdaterBaseUrl, StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.EndsWith("/Launcher.exe", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(launcherUrl, rootLauncherUrl, StringComparison.OrdinalIgnoreCase))
        {
            return rootLauncherUrl;
        }

        return launcherUrl;
    }

    // Manifest metadata helpers
    private async Task<LauncherUpdateInfo?> BuildLauncherInfoAsync(string version)
    {
        var launcherPath = LauncherFileBox.Text.Trim();
        var launcherUrl = NormalizeLauncherUrl(LauncherUrlBox.Text);
        LauncherUrlBox.Text = launcherUrl;

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

    private async Task<LauncherUpdateInfo?> BuildMetadataLauncherInfoAsync(string version, LauncherUpdateInfo? existing)
    {
        var launcherPath = LauncherFileBox.Text.Trim();
        var launcherUrl = NormalizeLauncherUrl(LauncherUrlBox.Text);
        LauncherUrlBox.Text = launcherUrl;

        if (string.IsNullOrWhiteSpace(launcherUrl))
        {
            return null;
        }

        if (File.Exists(launcherPath) && IsHttpUrl(launcherUrl))
        {
            return await BuildLauncherInfoAsync(version);
        }

        if (existing is not null)
        {
            return new LauncherUpdateInfo
            {
                Version = string.IsNullOrWhiteSpace(version) ? existing.Version : version,
                Sha256 = existing.Sha256,
                Size = existing.Size,
                Url = launcherUrl
            };
        }

        AppendLog("Launcher metadata skipped: launcher exe was not found.");
        return null;
    }

    private static List<ManifestFile> RebuildFileUrls(IEnumerable<ManifestFile> files, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return files.ToList();
        }

        var normalizedBaseUrl = baseUrl.TrimEnd('/');
        return files
            .Select(file =>
            {
                var urlPath = file.Compressed ? file.Path + ".gz" : file.Path;
                return new ManifestFile
                {
                    Path = file.Path,
                    Sha256 = file.Sha256,
                    Size = file.Size,
                    Url = normalizedBaseUrl + "/" + EscapeUrlPath(urlPath),
                    Compressed = file.Compressed,
                    CompressedSize = file.CompressedSize,
                    CompressedSha256 = file.CompressedSha256
                };
            })
            .ToList();
    }

    // File filtering and compression
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
        "config.json",
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

    // Validation and defaults
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

    // UI state and settings persistence
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
        LoadMetadataButton.IsEnabled = !busy;
        SaveMetadataButton.IsEnabled = !busy;
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private void LoadLastSettings()
    {
        var settings = PatchBuilderSettings.Load(_settingsPath);
        if (settings is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(settings.SourceDirectory))
        {
            SourceDirectoryBox.Text = settings.SourceDirectory;
        }

        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            BaseUrlBox.Text = settings.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(settings.OutputManifest))
        {
            OutputManifestBox.Text = settings.OutputManifest;
        }

        if (!string.IsNullOrWhiteSpace(settings.CompressedOutputDirectory))
        {
            CompressedOutputBox.Text = settings.CompressedOutputDirectory;
        }

        if (!string.IsNullOrWhiteSpace(settings.LauncherFile))
        {
            LauncherFileBox.Text = settings.LauncherFile;
        }

        if (!string.IsNullOrWhiteSpace(settings.LauncherUrl))
        {
            LauncherUrlBox.Text = settings.LauncherUrl;
        }
    }

    private void SaveLastSettings()
    {
        var settings = new PatchBuilderSettings
        {
            SourceDirectory = SourceDirectoryBox.Text,
            BaseUrl = BaseUrlBox.Text,
            OutputManifest = OutputManifestBox.Text,
            CompressedOutputDirectory = CompressedOutputBox.Text,
            LauncherFile = LauncherFileBox.Text,
            LauncherUrl = LauncherUrlBox.Text
        };

        settings.Save(_settingsPath);
    }

    private sealed class PatchBuilderSettings
    {
        public string SourceDirectory { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public string OutputManifest { get; set; } = string.Empty;

        public string CompressedOutputDirectory { get; set; } = string.Empty;

        public string LauncherFile { get; set; } = string.Empty;

        public string LauncherUrl { get; set; } = string.Empty;

        public static PatchBuilderSettings? Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PatchBuilderSettings>(json);
            }
            catch
            {
                return null;
            }
        }

        public void Save(string path)
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Last-location persistence should never block patch generation.
            }
        }
    }
}
