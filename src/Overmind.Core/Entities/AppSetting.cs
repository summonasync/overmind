namespace Overmind.Core;

public sealed class AppSetting
{
    private AppSetting()
    {
    }

    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    public static AppSetting Create(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        return new AppSetting { Key = key.Trim(), Value = value };
    }

    public void UpdateValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }
}
