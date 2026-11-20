using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Overmind.Host.Kestrel.Tests;

public sealed class KestrelResponseDelayStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<KestrelResponseDelayMiddleware>();
            next(app);
        };
}
