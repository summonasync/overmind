using System.Diagnostics;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api;

public static class SystemApi
{
    private static readonly SemaphoreSlim DialogLock = new(1, 1);
    private static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(120);
    // MVP measure: spawning a shell dialog per request. The Avalonia tray (task 08) replaces this with a proper picker.
    public static RouteGroupBuilder MapSystemApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system");
        group.MapPost("/browse-folder", BrowseFolder);
        return group;
    }

    private static async Task<IResult> BrowseFolder()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        }

        if (!await DialogLock.WaitAsync(TimeSpan.Zero))
        {
            return Results.StatusCode(StatusCodes.Status409Conflict);
        }

        try
        {
            var processStartInfo = CreateDialogProcess();

            using (var process = Process.Start(processStartInfo))
            {
                if (process is null)
                {
                    return Results.StatusCode(StatusCodes.Status501NotImplemented);
                }

                using var timeoutCts = new CancellationTokenSource(DialogTimeout);

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill();
                    return Results.NoContent();
                }

                var selectedPath = (await process.StandardOutput.ReadToEndAsync()).Trim();

                if (selectedPath.Length == 0)
                {
                    return Results.NoContent();
                }

                return Results.Ok(new BrowseFolderResponse(selectedPath));
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Folder picker unavailable",
                detail: ex.Message);
        }
        finally
        {
            DialogLock.Release();
        }
    }

    private static ProcessStartInfo CreateDialogProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Sta -NoProfile -NonInteractive -Command \"Add-Type -AssemblyName System.Windows.Forms; $d = New-Object System.Windows.Forms.FolderBrowserDialog; $d.Description = 'Select project folder'; if ($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Out.Write($d.SelectedPath) }\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        return new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = "-e 'POSIX path of (choose folder with prompt \"Select project folder\")'",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }
}
