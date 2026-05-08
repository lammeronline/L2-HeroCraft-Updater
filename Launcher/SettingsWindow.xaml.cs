using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Launcher;

public partial class SettingsWindow : Window
{
    private readonly LauncherSettings _originalSettings;
    private readonly bool _showClientSettings;
    public bool ShouldApplySettings { get; private set; }

    public SettingsWindow(LauncherSettings settings, IEnumerable<string> resolutions, bool showClientSettings)
    {
        InitializeComponent();

        _originalSettings = settings;
        _showClientSettings = showClientSettings;
        ClientDirectoryBox.Text = settings.ClientDirectory;
        ManifestSourceBox.Text = settings.ManifestSource;
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
            ManifestSource = ManifestSourceBox.Text,
            DisplayMode = _showClientSettings
                ? GetComboBoxText(DisplayModeBox, "Windowed")
                : _originalSettings.DisplayMode,
            Resolution = _showClientSettings
                ? GetComboBoxText(ResolutionBox, "1920x1080")
                : _originalSettings.Resolution,
            AudioMuteOn = _showClientSettings
                ? AudioMuteBox.IsChecked == true
                : _originalSettings.AudioMuteOn
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

    private void BrowseManifestButton_Click(object sender, RoutedEventArgs e)
    {
        var manifestDirectory = Path.GetDirectoryName(ManifestSourceBox.Text);
        var dialog = new OpenFileDialog
        {
            Title = "Select manifest",
            FileName = "manifest.json",
            Filter = "JSON manifest (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(manifestDirectory)
                ? manifestDirectory
                : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            ManifestSourceBox.Text = dialog.FileName;
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
