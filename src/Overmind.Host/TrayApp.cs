using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;

namespace Overmind.Host;

public class TrayApp : Application
{
    internal static Uri? OpenUri { get; set; }
    internal static Func<Task>? QuitAsync { get; set; }

    public override void OnFrameworkInitializationCompleted()
    {
        Styles.Add(new FluentTheme());

        var openItem = new NativeMenuItem { Header = "Open Overmind" };
        openItem.Click += OnOpenClicked;
        var quitItem = new NativeMenuItem { Header = "Quit" };
        quitItem.Click += OnQuitClicked;

        var trayIcon = new TrayIcon
        {
            ToolTipText = "Overmind",
            Icon = CreateIcon(),
            IsVisible = true,
            Menu = new NativeMenu(),
        };
        trayIcon.Menu.Items.Add(openItem);
        trayIcon.Menu.Items.Add(quitItem);

        TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
        base.OnFrameworkInitializationCompleted();
    }

    private async void OnOpenClicked(object? sender, EventArgs e) => await OpenInBrowserAsync();

    private async void OnQuitClicked(object? sender, EventArgs e) => await QuitAsync!();

    private static Task OpenInBrowserAsync()
    {
        if (OpenUri is not null)
        {
            SingleInstance.OpenInDefaultBrowser(OpenUri);
        }

        return Task.CompletedTask;
    }

    private static WindowIcon CreateIcon()
    {
        var assembly = typeof(TrayApp).Assembly;
        using var stream = assembly.GetManifestResourceStream("Overmind.Host.Assets.tray-icon.png")
            ?? throw new InvalidOperationException("Embedded tray icon is missing");
        return new WindowIcon(new Bitmap(stream));
    }
}
