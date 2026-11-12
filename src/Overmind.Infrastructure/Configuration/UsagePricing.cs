namespace Overmind.Infrastructure.Configuration;

public sealed record UsagePricing(
    decimal Input,
    decimal Output,
    decimal CacheRead,
    decimal CacheWrite);
