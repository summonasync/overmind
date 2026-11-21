namespace Overmind.Host.Kestrel.Tests;

public static class ChromeLocator
{
    private static readonly string[] BinaryNames =
    [
        "google-chrome",
        "google-chrome-stable",
        "chromium",
        "chromium-browser",
    ];

    private static readonly string[] ExtraDirectories =
    [
        "/usr/bin",
        "/usr/local/bin",
        "/snap/bin",
        "/opt/google/chrome",
    ];

    public static string? Find() =>
        ExecutableSearch.Find(
            Environment.GetEnvironmentVariable("OVERMIND_TEST_CHROME_BIN"),
            BinaryNames,
            ExtraDirectories);
}
