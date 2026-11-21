using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[CollectionDefinition("overmind kestrel browser")]
public sealed class KestrelBrowserCollection : ICollectionFixture<BrowserHostFixture>;
