using L2ModernUpdater.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace ConfigBuilder;

// Small authoring tool for server-side config.json. Keep it deliberately thin:
// the UI maps one-to-one to LauncherConfig so release operators can verify output easily.
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplyConfig(LauncherConfig.CreateDefault());
        ConfigFileBox.Text = Path.Combine(Environment.CurrentDirectory, "config.json");
        AppendLog("Config Builder started.");
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.GetFullPath(ConfigFileBox.Text.Trim());
            var config = await LauncherConfigStore.LoadAsync(path);
            ApplyConfig(config);
            AppendLog("Config loaded: " + path);
        }
        catch (Exception ex)
        {
            AppendLog("Config load failed: " + ex.Message);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.GetFullPath(ConfigFileBox.Text.Trim());
            var config = BuildConfig();
            await LauncherConfigStore.SaveAsync(config, path);
            AppendLog("Config saved: " + path);
            AppendLog("Upload to: https://yoursite.com/updater/config.json");
        }
        catch (Exception ex)
        {
            AppendLog("Config save failed: " + ex.Message);
        }
    }

    private void BrowseConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save config",
            FileName = "config.json",
            Filter = "JSON config (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(ConfigFileBox.Text))
                ? Path.GetDirectoryName(ConfigFileBox.Text)
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            ConfigFileBox.Text = dialog.FileName;
        }
    }

    private LauncherConfig BuildConfig()
    {
        return new LauncherConfig
        {
            ManifestUrl = ManifestUrlBox.Text.Trim(),
            NewsUrl = NewsUrlBox.Text.Trim(),
            PlayButtonText = string.IsNullOrWhiteSpace(PlayButtonTextBox.Text) ? "PLAY" : PlayButtonTextBox.Text.Trim(),
            ShowClientSettings = ShowClientSettingsBox.IsChecked == true,
            RequireUpdateBeforePlay = RequireUpdateBeforePlayBox.IsChecked == true,
            AutoLoginEnabled = AutoLoginEnabledBox.IsChecked == true,
            GameExecutables = ParseLines(GameExecutablesBox.Text),
            Resolutions = ParseLines(ResolutionsBox.Text),
            DefaultDisplayMode = string.IsNullOrWhiteSpace(DefaultDisplayModeBox.Text) ? "Windowed" : DefaultDisplayModeBox.Text.Trim(),
            DefaultResolution = string.IsNullOrWhiteSpace(DefaultResolutionBox.Text) ? "1920x1080" : DefaultResolutionBox.Text.Trim(),
            DefaultAudioMuteOn = DefaultAudioMuteBox.IsChecked == true
        };
    }

    private void ApplyConfig(LauncherConfig config)
    {
        ManifestUrlBox.Text = config.ManifestUrl;
        NewsUrlBox.Text = config.NewsUrl;
        PlayButtonTextBox.Text = config.PlayButtonText;
        ShowClientSettingsBox.IsChecked = config.ShowClientSettings;
        RequireUpdateBeforePlayBox.IsChecked = config.RequireUpdateBeforePlay;
        AutoLoginEnabledBox.IsChecked = config.AutoLoginEnabled;
        DefaultAudioMuteBox.IsChecked = config.DefaultAudioMuteOn;
        GameExecutablesBox.Text = string.Join(Environment.NewLine, config.GameExecutables);
        ResolutionsBox.Text = string.Join(Environment.NewLine, config.Resolutions);
        DefaultDisplayModeBox.Text = config.DefaultDisplayMode;
        DefaultResolutionBox.Text = config.DefaultResolution;
    }

    private static List<string> ParseLines(string text)
    {
        return text
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
}
