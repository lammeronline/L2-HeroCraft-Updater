using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Launcher;

public partial class SettingsWindow : Window
{
    private readonly LauncherSettings _originalSettings;
    private readonly bool _showClientSettings;
    private readonly string _defaultConfigSource;
    public bool ShouldApplySettings { get; private set; }

    public SettingsWindow(
        LauncherSettings settings,
        IEnumerable<string> resolutions,
        bool showClientSettings,
        string defaultConfigSource)
    {
        InitializeComponent();

        _originalSettings = settings;
        _showClientSettings = showClientSettings;
        _defaultConfigSource = defaultConfigSource;
        ClientDirectoryBox.Text = settings.ClientDirectory;
        ConfigSourceBox.Text = string.IsNullOrWhiteSpace(settings.ConfigSource)
            ? _defaultConfigSource
            : settings.ConfigSource;
        ShowLogBox.IsChecked = settings.ShowLog;
        ClientSettingsPanel.Visibility = showClientSettings ? Visibility.Visible : Visibility.Collapsed;
        ApplyButton.Visibility = showClientSettings ? Visibility.Visible : Visibility.Collapsed;
        FillResolutionBox(resolutions, settings.Resolution);
        SelectComboBoxItem(DisplayModeBox, settings.DisplayMode);
        AudioMuteBox.IsChecked = settings.AudioMuteOn;
    }

    public LauncherSettings CreateSettings()
    {
        return new LauncherSettings
        {
            ClientDirectory = ClientDirectoryBox.Text,
            ConfigSource = string.Equals(ConfigSourceBox.Text.Trim(), _defaultConfigSource, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : ConfigSourceBox.Text.Trim(),
            DisplayMode = _showClientSettings
                ? GetComboBoxText(DisplayModeBox, "Windowed")
                : _originalSettings.DisplayMode,
            Resolution = _showClientSettings
                ? GetComboBoxText(ResolutionBox, "1920x1080")
                : _originalSettings.Resolution,
            AudioMuteOn = _showClientSettings
                ? AudioMuteBox.IsChecked == true
                : _originalSettings.AudioMuteOn,
            ShowLog = ShowLogBox.IsChecked == true
        };
    }

    private void BrowseClientButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Lineage II client folder",
            InitialDirectory = Directory.Exists(ClientDirectoryBox.Text)
                ? ClientDirectoryBox.Text
                : Environment.CurrentDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            ClientDirectoryBox.Text = dialog.FolderName;
        }
    }

    private void BrowseConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var configDirectory = GetExistingDirectory(ConfigSourceBox.Text);
        var dialog = new OpenFileDialog
        {
            Title = "Select launcher config",
            FileName = "config.json",
            Filter = "JSON config (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = configDirectory ?? AppContext.BaseDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            ConfigSourceBox.Text = dialog.FileName;
        }
    }

    private static string? GetExistingDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            if (Directory.Exists(value))
            {
                return value;
            }

            var directory = Path.GetDirectoryName(value);
            return Directory.Exists(directory) ? directory : null;
        }
        catch
        {
            return null;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldApplySettings = false;
        DialogResult = true;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ShouldApplySettings = true;
        DialogResult = true;
    }

    private void FillResolutionBox(IEnumerable<string> resolutions, string selectedResolution)
    {
        var values = resolutions
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (values.Count == 0)
        {
            values = ["1024x768", "1280x720", "1366x768", "1600x900", "1920x1080", "2560x1440"];
        }

        foreach (var value in values)
        {
            ResolutionBox.Items.Add(new ComboBoxItem { Content = value });
        }

        SelectComboBoxItem(ResolutionBox, string.IsNullOrWhiteSpace(selectedResolution) ? values[0] : selectedResolution);
    }

    private static void SelectComboBoxItem(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static string GetComboBoxText(ComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? comboBox.Text
            ?? fallback;
    }
}
