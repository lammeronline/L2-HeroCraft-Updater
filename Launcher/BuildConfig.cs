namespace Launcher;

/// <summary>
/// Build-time settings. Edit this file before compiling and distributing to players.
/// </summary>
internal static class BuildConfig
{
    /// <summary>
    /// Remote URL of the launcher config file (config.json on your web server).
    /// Applied when UseLocalConfig is false.
    /// </summary>
    public const string ConfigUrl = "https://l2.lammeronline.com/updater/config.json";

    /// <summary>
    /// false (default) — config is downloaded from ConfigUrl.
    ///   Players only need Launcher.exe, no extra files required.
    ///
    /// true — config is read from config.json placed next to Launcher.exe.
    ///   Use for LAN or offline setups where no web server is available.
    /// </summary>
    public static readonly bool UseLocalConfig = false;
}
