using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
        const int size = 32;
        const double center = size / 2d;
        const double ringRadius = 10d;
        const double ringHalfWidth = 2.5d;

        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - center;
                var dy = y + 0.5 - center;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                byte r, g, b;
                if (distance is >= ringRadius - ringHalfWidth and <= ringRadius + ringHalfWidth)
                {
                    (r, g, b) = (139, 92, 246);
                }
                else
                {
                    (r, g, b) = (21, 14, 40);
                }

                var offset = (y * size + x) * 4;
                pixels[offset] = b;
                pixels[offset + 1] = g;
                pixels[offset + 2] = r;
                pixels[offset + 3] = 255;
            }
        }

        using var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        return new WindowIcon(bitmap);
    }
}
