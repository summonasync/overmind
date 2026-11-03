namespace Overmind.Infrastructure.Notifications;

public static class NotificationLinks
{
    public static string? Dashboard(Uri origin, Guid? projectId, string? featureSlug, string? tab)
    {
        if (featureSlug is null)
        {
            return null;
        }

        var query = new List<string>();
        if (projectId is { } id)
        {
            query.Add(Pair("project", id.ToString()));
        }

        query.Add(Pair("feature", featureSlug));
        if (tab is not null)
        {
            query.Add(Pair("tab", tab));
        }

        return new Uri(origin, $"/dashboard?{string.Join("&", query)}").ToString();
    }

    private static string Pair(string key, string value) =>
        $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
}
