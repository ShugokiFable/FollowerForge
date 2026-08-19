using Avalonia.Controls;

namespace FollowerForge.Ui;

public enum DeckSelectionMode
{
    Single,
    Multi,
}

public sealed class DeckRecord(
    string key,
    string display,
    string? detail,
    string? badge,
    string? editorId,
    object source)
{
    public string Key { get; } = key;
    public string Display { get; } = display;
    public string? Detail { get; } = detail;
    public string? Badge { get; } = badge;
    public string? EditorId { get; } = editorId;
    public object Source { get; } = source;
    public string Plugin => Key.Contains(':', StringComparison.Ordinal)
        ? Key[(Key.IndexOf(':') + 1)..]
        : string.Empty;
    public bool IsSelected { get; internal set; }

    internal DeckRecord Copy() => new(Key, Display, Detail, Badge, EditorId, Source);
}

public sealed class ExpertDeckSession
{
    private readonly List<DeckRecord> _records;
    private readonly HashSet<string> _original;
    private readonly HashSet<string> _selected;

    public ExpertDeckSession(
        string family,
        IReadOnlyList<DeckRecord> records,
        DeckSelectionMode mode,
        IEnumerable<string> selectedKeys)
    {
        Family = family;
        Mode = mode;
        _records = records.Select(record => record.Copy()).ToList();
        _original = new HashSet<string>(selectedKeys, StringComparer.OrdinalIgnoreCase);
        _selected = new HashSet<string>(_original, StringComparer.OrdinalIgnoreCase);
        SyncSelectionFlags();
    }

    public string Family { get; }
    public DeckSelectionMode Mode { get; }
    public IReadOnlyList<DeckRecord> Records => _records;
    public IReadOnlyList<DeckRecord> SelectionCart => _records
        .Where(record => _selected.Contains(record.Key))
        .OrderBy(record => record.Display, StringComparer.OrdinalIgnoreCase)
        .ThenBy(record => record.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<DeckRecord> Filter(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _records;
        var needle = query.Trim();
        return _records.Where(record =>
                Contains(record.Display, needle)
                || Contains(record.EditorId, needle)
                || Contains(record.Plugin, needle)
                || Contains(record.Key, needle)
                || Contains(record.Detail, needle))
            .ToList();
    }

    public string EmptyStateMessage(string? query) =>
        $"No {Family} match “{query?.Trim()}” with the current filters. Clear filters to keep browsing.";

    public void SetSelected(string key, bool selected)
    {
        if (!_records.Any(record => record.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) return;

        if (Mode == DeckSelectionMode.Single && selected)
            _selected.Clear();

        if (selected) _selected.Add(key);
        else _selected.Remove(key);
        SyncSelectionFlags();
    }

    public IReadOnlyList<string> Commit() => OrderedKeys(_selected);

    public IReadOnlyList<string> Cancel()
    {
        _selected.Clear();
        _selected.UnionWith(_original);
        SyncSelectionFlags();
        return OrderedKeys(_selected);
    }

    private IReadOnlyList<string> OrderedKeys(HashSet<string> keys) => _records
        .Where(record => keys.Contains(record.Key))
        .OrderBy(record => record.Display, StringComparer.OrdinalIgnoreCase)
        .ThenBy(record => record.Key, StringComparer.OrdinalIgnoreCase)
        .Select(record => record.Key)
        .ToList();

    private void SyncSelectionFlags()
    {
        foreach (var record in _records)
            record.IsSelected = _selected.Contains(record.Key);
    }

    private static bool Contains(string? value, string needle) =>
        value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;
}

public static class DeckSelectionMerge
{
    public static void ReplaceFamily(
        HashSet<string> remembered,
        IEnumerable<string> familyKeys,
        IEnumerable<string> committedKeys)
    {
        remembered.ExceptWith(familyKeys);
        remembered.UnionWith(committedKeys);
    }
}

public static class DeckGridSelection
{
    /// <summary>
    /// Mirrors the session's selection flags onto the deck grid. Avalonia only allows the
    /// SelectedItems collection to be mutated in Extended mode; single-choice decks run the
    /// grid in Single mode and must go through SelectedItem instead.
    /// </summary>
    public static void SyncSelected(DataGrid grid, IReadOnlyList<DeckRecord> records)
    {
        if (grid.SelectionMode == DataGridSelectionMode.Extended)
        {
            grid.SelectedItems.Clear();
            foreach (var record in records.Where(record => record.IsSelected))
                grid.SelectedItems.Add(record);
        }
        else
        {
            grid.SelectedItem = records.FirstOrDefault(record => record.IsSelected);
        }
    }
}
