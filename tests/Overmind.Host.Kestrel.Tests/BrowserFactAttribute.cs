using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute()
    {
        if (ChromeLocator.Find() is null)
        {
            Skip = "chrome binary not found on PATH or common install paths (probe forced off via OVERMIND_TEST_CHROME_BIN)";
            return;
        }

        if (NodeLocator.Find() is null)
        {
            Skip = "node/npm toolchain not found (probe forced off via OVERMIND_TEST_NODE_BIN)";
        }
    }
}
