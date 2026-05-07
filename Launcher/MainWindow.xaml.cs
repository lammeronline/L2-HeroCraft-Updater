using L2ModernUpdater.Core;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Launcher;

public partial class MainWindow : Window
{
    private const string DefaultManifestUrl = "https://l2.lammeronline.com/updater/live/manifest.json";
    private readonly string _appDirectory = AppContext.BaseDirectory;
    private readonly string _settingsPath;
    private readonly FileLog _launcherLog;
    private readonly FileLog _updaterLog;
    private readonly HttpClient _httpClient = new();
    private readonly FileVerificationService _verificationService = new();
    private readonly ExtraFileScanService _extraFileScanService = new();
    private readonly FileDownloadService _downloadService;
    private IReadOnlyList<FileVerificationResult> _pendingUpdates = [];
    private IReadOnlyList<ExtraFileResult> _extraFiles = [];
    private UpdateManifest? _manifest;
    private bool _suppressSettingsSave = true;

    public MainWindow()
    {
        _settingsPath = Path.Combine(_appDirectory, "launcher.settings.json");
        _launcherLog = new FileLog(Path.Combine(_appDirectory, "launcher.log"));
        _updaterLog = new FileLog(Path.Combine(_appDirectory, "updater.log"));

        InitializeComponent();
        _downloadService = new FileDownloadService(_httpClient);

        GameDirectoryBox.Text = Environment.CurrentDirectory;
        ManifestSourceBox.Text = DefaultManifestUrl;
        AppendLog("Launcher started.");
        _ = LoadSettingsAsync();
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckFilesAsync(VerificationMode.Fast);
    }

    private async void RepairButton_Click(object sender, RoutedEventArgs e)
    {
        await RepairAsync();
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);

            if (_pendingUpdates.Count == 0)
            {
                await CheckFilesAsync(VerificationMode.Fast, keepBusy: true);
            }

            if (_pendingUpdates.Count == 0)
            {
                SetStatus("Ready to play", "No updates required.", 100);
                return;
            }

            AppendUpdaterLog($"Downloading {_pendingUpdates.Count} file(s).");
            var progress = new Progress<DownloadProgress>(value =>
            {
                var percent = value.TotalBytes == 0 ? 100 : value.CompletedBytes * 100d / value.TotalBytes;
                SetStatus("Downloading", value.CurrentPath, percent);
                TransferText.Text = $"{value.CompletedFiles} / {value.TotalFiles}";
            });

            await _downloadService.DownloadAsync(_pendingUpdates, GameDirectoryBox.Text, progress);
            AppendUpdaterLog("Download complete. Verifying files.");

            await CheckFilesAsync(VerificationMode.Fast, keepBusy: true);
        }
        catch (Exception ex)
        {
            AppendUpdaterLog("Update failed: " + ex.Message);
            SetStatus("Update failed", ex.Message, MainProgressBar.Value);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var clientDirectory = GameDirectoryBox.Text;
            await ApplyClientSettingsAsync(clientDirectory);

            var candidates = new[]
            {
                Path.Combine(clientDirectory, "system", "l2.exe"),
                Path.Combine(clientDirectory, "l2.exe")
            };

            var executable = candidates.FirstOrDefault(File.Exists);
            if (executable is null)
            {
                AppendLog("Game executable was not found.");
                SetStatus("Cannot launch", "system/l2.exe was not found.", MainProgressBar.Value);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = true
            });

            AppendLog("Game launched: " + executable);
        }
        catch (Exception ex)
        {
            AppendLog("Launch failed: " + ex.Message);
            SetStatus("Launch failed", ex.Message, MainProgressBar.Value);
        }
    }

    private async void ApplySettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SaveSettingsAsync();
            await ApplyClientSettingsAsync(GameDirectoryBox.Text);
            SetStatus("Settings applied", "Client settings updated.", MainProgressBar.Value);
        }
        catch (Exception ex)
        {
            AppendLog("Settings apply failed: " + ex.Message);
            SetStatus("Settings failed", ex.Message, MainProgressBar.Value);
        }
    }

    private void ClientSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingsSave)
        {
            return;
        }

        _ = SaveSettingsAsync();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Lineage II client folder",
            InitialDirectory = Directory.Exists(GameDirectoryBox.Text)
                ? GameDirectoryBox.Text
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            GameDirectoryBox.Text = dialog.FolderName;
            _ = SaveSettingsAsync();
        }
    }

    private void BrowseManifestButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select manifest",
            FileName = "manifest.json",
            Filter = "JSON manifest (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(ManifestSourceBox.Text))
                ? Path.GetDirectoryName(ManifestSourceBox.Text)
                : _appDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            ManifestSourceBox.Text = dialog.FileName;
            _ = SaveSettingsAsync();
        }
    }

    private async Task RepairAsync()
    {
        try
        {
            SetBusy(true);
            AppendUpdaterLog("Repair started.");

            await CheckFilesAsync(VerificationMode.Full, keepBusy: true);

            if (_pendingUpdates.Count == 0)
            {
                SetStatus("Repair complete", $"No damaged files. Extra files: {_extraFiles.Count}.", 100);
                AppendUpdaterLog($"Repair complete. No damaged files. Extra files: {_extraFiles.Count}.");
                return;
            }

            AppendUpdaterLog($"Repair downloading {_pendingUpdates.Count} file(s).");
            var progress = new Progress<DownloadProgress>(value =>
            {
                var percent = value.TotalBytes == 0 ? 100 : value.CompletedBytes * 100d / value.TotalBytes;
                SetStatus("Repairing", value.CurrentPath, percent);
                TransferText.Text = $"{value.CompletedFiles} / {value.TotalFiles}";
            });

            await _downloadService.DownloadAsync(_pendingUpdates, GameDirectoryBox.Text, progress);
            AppendUpdaterLog("Repair download complete. Running full verification.");

            await CheckFilesAsync(VerificationMode.Full, keepBusy: true);
            SetStatus(
                _pendingUpdates.Count == 0 ? "Repair complete" : "Repair incomplete",
                _pendingUpdates.Count == 0
                    ? $"All manifest files verified. Extra files: {_extraFiles.Count}."
                    : $"{_pendingUpdates.Count} file(s) still need attention.",
                100);
        }
        catch (Exception ex)
        {
            AppendUpdaterLog("Repair failed: " + ex.Message);
            SetStatus("Repair failed", ex.Message, MainProgressBar.Value);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CheckFilesAsync(VerificationMode mode, bool keepBusy = false)
    {
        try
        {
            if (!keepBusy)
            {
                SetBusy(true);
            }

            var manifestSource = ResolveManifestSource(ManifestSourceBox.Text);
            await SaveSettingsAsync();
            AppendUpdaterLog("Loading manifest: " + manifestSource);

            _manifest = await ManifestStore.LoadAsync(manifestSource, _httpClient);
            NewsList.ItemsSource = _manifest.News;
            AppendUpdaterLog($"Manifest version {_manifest.Version}, files: {_manifest.Files.Count}.");

            if (await TrySelfUpdateAsync(_manifest))
            {
                return;
            }

            var progress = new Progress<VerificationProgress>(value =>
            {
                var percent = value.Total == 0 ? 100 : value.Completed * 100d / value.Total;
                SetStatus(mode == VerificationMode.Fast ? "Fast checking" : "Full checking", value.CurrentPath, percent);
                TransferText.Text = $"{value.Completed} / {value.Total}";
            });

            _pendingUpdates = await _verificationService.VerifyAsync(_manifest, GameDirectoryBox.Text, mode, progress);
            _extraFiles = ShouldReportExtraFiles(_manifest)
                ? _extraFileScanService.FindExtraFiles(_manifest, GameDirectoryBox.Text)
                : [];

            if (_pendingUpdates.Count == 0)
            {
                var modeText = mode == VerificationMode.Fast ? "Fast check" : "Full check";
                SetStatus("Ready to play", BuildExtraSummary("No updates required"), 100);
                AppendUpdaterLog($"{modeText} complete. {BuildExtraSummary("No updates required")}.");
            }
            else
            {
                var missing = _pendingUpdates.Count(item => item.State == VerificationState.Missing);
                var outdated = _pendingUpdates.Count(item => item.State == VerificationState.Outdated);
                var extraText = ShouldReportExtraFiles(_manifest)
                    ? $", {_extraFiles.Count} extra"
                    : ", extra scan disabled";
                SetStatus("Update required", $"{_pendingUpdates.Count} file(s): {missing} missing, {outdated} outdated{extraText}.", 100);
                AppendUpdaterLog($"Update required: {_pendingUpdates.Count} file(s), {missing} missing, {outdated} outdated{extraText}.");
            }

            LogExtraFilePreview();
        }
        catch (Exception ex)
        {
            AppendUpdaterLog("Check failed: " + ex.Message);
            SetStatus("Check failed", ex.Message, MainProgressBar.Value);
        }
        finally
        {
            if (!keepBusy)
            {
                SetBusy(false);
            }
        }
    }

    private static string ResolveManifestSource(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return source;
        }

        return Path.GetFullPath(source);
    }

    private void SetStatus(string status, string currentFile, double progress)
    {
        StatusText.Text = status;
        CurrentFileText.Text = currentFile;
        MainProgressBar.Value = Math.Clamp(progress, 0, 100);
    }

    private void SetBusy(bool busy)
    {
        CheckButton.IsEnabled = !busy;
        RepairButton.IsEnabled = !busy;
        UpdateButton.IsEnabled = !busy;
        PlayButton.IsEnabled = !busy;
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
        _launcherLog.Write(message);
    }

    private void AppendUpdaterLog(string message)
    {
        AppendLog(message);
        _updaterLog.Write(message);
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await LauncherSettings.LoadAsync(_settingsPath);

        if (!string.IsNullOrWhiteSpace(settings.ClientDirectory))
        {
            GameDirectoryBox.Text = settings.ClientDirectory;
        }

        if (!string.IsNullOrWhiteSpace(settings.ManifestSource))
        {
            ManifestSourceBox.Text = settings.ManifestSource;
        }

        SelectComboBoxItem(DisplayModeBox, string.IsNullOrWhiteSpace(settings.DisplayMode) ? "Windowed" : settings.DisplayMode);
        SelectComboBoxItem(ResolutionBox, string.IsNullOrWhiteSpace(settings.Resolution) ? "1920x1080" : settings.Resolution);
        AudioMuteBox.IsChecked = settings.AudioMuteOn;

        _suppressSettingsSave = false;
        AppendLog("Settings loaded.");
    }

    private Task SaveSettingsAsync()
    {
        var settings = new LauncherSettings
        {
            ClientDirectory = GameDirectoryBox.Text,
            ManifestSource = ManifestSourceBox.Text,
            DisplayMode = GetComboBoxText(DisplayModeBox, "Windowed"),
            Resolution = GetComboBoxText(ResolutionBox, "1920x1080"),
            AudioMuteOn = AudioMuteBox.IsChecked == true
        };

        return SaveSettingsCoreAsync(settings);
    }

    private async Task SaveSettingsCoreAsync(LauncherSettings settings)
    {
        try
        {
            await settings.SaveAsync(_settingsPath);
            _launcherLog.Write("Settings saved.");
        }
        catch (Exception ex)
        {
            AppendLog("Settings save failed: " + ex.Message);
        }
    }

    private void LogExtraFilePreview()
    {
        if (_extraFiles.Count == 0)
        {
            return;
        }

        AppendUpdaterLog($"Extra files found: {_extraFiles.Count}. They were not deleted.");
        foreach (var extra in _extraFiles.Take(10))
        {
            AppendUpdaterLog("Extra: " + extra.RelativePath);
        }

        if (_extraFiles.Count > 10)
        {
            AppendUpdaterLog($"Extra preview truncated: {_extraFiles.Count - 10} more file(s).");
        }
    }

    private static bool ShouldReportExtraFiles(UpdateManifest manifest)
    {
        return string.Equals(manifest.ExtraFilePolicy, "report", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildExtraSummary(string prefix)
    {
        if (_manifest is null || !ShouldReportExtraFiles(_manifest))
        {
            return prefix + ". Extra scan disabled";
        }

        return $"{prefix}. Extra files: {_extraFiles.Count}";
    }

    private async Task<bool> TrySelfUpdateAsync(UpdateManifest manifest)
    {
        var launcher = manifest.Launcher;
        if (launcher is null || string.IsNullOrWhiteSpace(launcher.Url) || string.IsNullOrWhiteSpace(launcher.Sha256))
        {
            return false;
        }

        var currentPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
        {
            AppendUpdaterLog("Self-update skipped: current launcher path was not found.");
            return false;
        }

        var currentSha256 = await FileHasher.ComputeSha256Async(currentPath);
        if (string.Equals(currentSha256, launcher.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            AppendUpdaterLog("Launcher is up to date.");
            return false;
        }

        AppendUpdaterLog($"Launcher update found: {launcher.Version}");
        SetStatus("Updating launcher", launcher.Url, 0);

        var updateDirectory = Path.Combine(Path.GetTempPath(), "L2ModernUpdater");
        Directory.CreateDirectory(updateDirectory);
        var downloadedLauncher = Path.Combine(updateDirectory, "Launcher.update.exe");

        await DownloadLauncherAsync(launcher, downloadedLauncher);
        var downloadedHash = await FileHasher.ComputeSha256Async(downloadedLauncher);
        if (!string.Equals(downloadedHash, launcher.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Downloaded launcher failed SHA256 verification. "
                + $"Expected {launcher.Sha256}, actual {downloadedHash}.");
        }

        var downloadedSize = new FileInfo(downloadedLauncher).Length;
        if (launcher.Size > 0 && downloadedSize != launcher.Size)
        {
            throw new InvalidOperationException($"Downloaded launcher size mismatch: {downloadedSize} != {launcher.Size}.");
        }

        var scriptPath = WriteSelfUpdateScript(downloadedLauncher, currentPath);
        AppendUpdaterLog("Launcher update downloaded. Restarting updater.");

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"" + scriptPath + "\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        Application.Current.Shutdown();
        return true;
    }

    private async Task DownloadLauncherAsync(LauncherUpdateInfo launcher, string outputPath)
    {
        if (Uri.TryCreate(launcher.Url, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            File.Copy(uri.LocalPath, outputPath, true);
            return;
        }

        using var response = await _httpClient.GetAsync(launcher.Url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {launcher.Url}");
        }

        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true);
        await input.CopyToAsync(output);
    }

    private static string WriteSelfUpdateScript(string downloadedLauncher, string currentLauncher)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "L2ModernUpdater", "apply-launcher-update.cmd");
        var processId = Environment.ProcessId;
        var launcherDirectory = Path.GetDirectoryName(currentLauncher) ?? AppContext.BaseDirectory;

        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine("setlocal");
        script.AppendLine("set \"pid=" + processId + "\"");
        script.AppendLine("set \"source=" + downloadedLauncher + "\"");
        script.AppendLine("set \"target=" + currentLauncher + "\"");
        script.AppendLine("set \"workdir=" + launcherDirectory + "\"");
        script.AppendLine(":wait");
        script.AppendLine("tasklist /FI \"PID eq %pid%\" | find \"%pid%\" >nul");
        script.AppendLine("if not errorlevel 1 (");
        script.AppendLine("  timeout /t 1 /nobreak >nul");
        script.AppendLine("  goto wait");
        script.AppendLine(")");
        script.AppendLine("copy /Y \"%source%\" \"%target%\" >nul");
        script.AppendLine("start \"\" /D \"%workdir%\" \"%target%\"");
        script.AppendLine("del \"%source%\" >nul 2>nul");
        script.AppendLine("del \"%~f0\" >nul 2>nul");

        File.WriteAllText(scriptPath, script.ToString(), Encoding.ASCII);
        return scriptPath;
    }

    private async Task ApplyClientSettingsAsync(string clientDirectory)
    {
        var systemDirectory = Path.Combine(clientDirectory, "system");
        var optionPath = Path.Combine(systemDirectory, "Option.ini");
        var l2IniPath = Path.Combine(systemDirectory, "l2.ini");
        var mode = GetComboBoxText(DisplayModeBox, "Windowed");
        var resolution = GetComboBoxText(ResolutionBox, "1920x1080");
        var (width, height) = ParseResolution(resolution);
        var fullscreen = string.Equals(mode, "Fullscreen", StringComparison.OrdinalIgnoreCase);
        var borderless = string.Equals(mode, "Borderless", StringComparison.OrdinalIgnoreCase);

        await UpdateOptionIniAsync(optionPath, width, height, fullscreen, AudioMuteBox.IsChecked == true);

        if (File.Exists(l2IniPath) && !fullscreen)
        {
            await UpdateL2IniWindowFrameAsync(l2IniPath, useWindowFrame: !borderless);
        }
        else if (!File.Exists(l2IniPath) && !fullscreen)
        {
            AppendLog("l2.ini was not found. Window frame setting skipped.");
        }

        AppendLog($"Client settings applied: {mode}, {width}x{height}, mute={AudioMuteBox.IsChecked == true}.");
    }

    private static async Task UpdateOptionIniAsync(
        string path,
        int width,
        int height,
        bool fullscreen,
        bool audioMuteOn)
    {
        var lines = File.Exists(path)
            ? (await File.ReadAllLinesAsync(path)).ToList()
            : ["[Video]", "", "[Audio]"];

        SetIniValue(lines, "Video", "GamePlayViewportX", width.ToString());
        SetIniValue(lines, "Video", "GamePlayViewportY", height.ToString());
        SetIniValue(lines, "Video", "StartupFullScreen", fullscreen ? "True" : "False");
        SetIniValue(lines, "Audio", "AudioMuteOn", audioMuteOn ? "True" : "False");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllLinesAsync(path, lines);
    }

    private void SelectComboBoxItem(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static string GetComboBoxText(ComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? comboBox.Text
            ?? fallback;
    }

    private static (int Width, int Height) ParseResolution(string value)
    {
        var parts = value.Split('x', 'X');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var width)
            && int.TryParse(parts[1], out var height)
            && width > 0
            && height > 0)
        {
            return (width, height);
        }

        return (1920, 1080);
    }

    private static void SetIniValue(List<string> lines, string section, string key, string value)
    {
        var sectionHeader = "[" + section + "]";
        var sectionIndex = lines.FindIndex(line => string.Equals(line.Trim(), sectionHeader, StringComparison.OrdinalIgnoreCase));

        if (sectionIndex < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add(sectionHeader);
            lines.Add(key + "=" + value);
            return;
        }

        var insertIndex = lines.Count;
        for (var index = sectionIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                insertIndex = index;
                break;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex > 0 && string.Equals(line[..equalsIndex].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                lines[index] = key + "=" + value;
                return;
            }
        }

        lines.Insert(insertIndex, key + "=" + value);
    }

    private async Task UpdateL2IniWindowFrameAsync(string path, bool useWindowFrame)
    {
        try
        {
            var encrypted = await File.ReadAllBytesAsync(path);
            var plain = L2IniCodec.Decode413(encrypted);
            var encoding = DetectTextEncoding(plain);
            var text = encoding.GetString(plain);
            text = SetIniTextValue(text, "UseWindowFrame", useWindowFrame ? "true" : "false");
            var updatedPlain = encoding.GetBytes(text);
            var updatedEncrypted = L2IniCodec.Encode413(updatedPlain);
            await File.WriteAllBytesAsync(path, updatedEncrypted);
        }
        catch (Exception ex)
        {
            AppendLog("l2.ini update skipped: " + ex.Message);
        }
    }

    private static Encoding DetectTextEncoding(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        var zeroOddBytes = 0;
        var pairsToCheck = Math.Min(data.Length / 2, 256);
        for (var index = 0; index < pairsToCheck; index++)
        {
            if (data[index * 2 + 1] == 0)
            {
                zeroOddBytes++;
            }
        }

        return pairsToCheck > 0 && zeroOddBytes > pairsToCheck / 2
            ? Encoding.Unicode
            : Encoding.UTF8;
    }

    private static string SetIniTextValue(string text, string key, string value)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                var prefixLength = line.Length - trimmed.Length;
                lines[index] = line[..prefixLength] + key + "=" + value;
                return string.Join(newline, lines);
            }
        }

        if (lines.Count > 0 && lines[^1].Length > 0)
        {
            lines.Add(key + "=" + value);
        }
        else if (lines.Count > 0)
        {
            lines[^1] = key + "=" + value;
        }
        else
        {
            lines.Add(key + "=" + value);
        }

        return string.Join(newline, lines);
    }
}
