namespace FollowerForge.Ui;

/// <summary>Search box matching for wizard lists, including the hidden FormKey / plugin.</summary>
public static class PickerFilter
{
    public static bool Matches(PickerItem item, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return item.Display.Contains(query, StringComparison.OrdinalIgnoreCase)
               || (item.Detail?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
               || item.FormKey.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<PickerItem> Filter(IReadOnlyList<PickerItem> all, string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? all
            : all.Where(i => Matches(i, query)).ToList();
}
