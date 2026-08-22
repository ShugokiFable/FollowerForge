using System.Xml.Linq;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

/// <summary>
/// A user added the wrong clothing, hit a must-fix build, went back to undo it, and found no
/// way to. Click-again-to-deselect is invisible, the deck's checkbox column is a read-only
/// status light, and only Kin and custom lines had a "Remove selected" button — which taught
/// people a button exists everywhere. These pin the escape hatch so it cannot go missing again.
/// </summary>
public sealed class DeselectionTests
{
    private static readonly XDocument Xaml = XDocument.Load(Path.Combine(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
        "Ui", "WizardWindow.axaml"));

    /// <summary>Race, voice, class and location must always hold a value; the rest are optional.</summary>
    private static readonly string[] Required = ["Race", "Voice", "Class", "Location"];

    private static IEnumerable<string> TagsFor(string handler) => Xaml.Descendants()
        .Where(e => e.Attribute("Click")?.Value == handler)
        .Select(e => e.Attribute("Tag")?.Value)
        .Where(tag => tag is not null)!;

    [Fact]
    public void Every_optional_picker_can_be_emptied_from_the_page()
    {
        var browsable = TagsFor("OnOpenDeck").Where(tag => !Required.Contains(tag)).ToList();
        var clearable = TagsFor("OnClearPicks").ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(browsable);
        var missing = browsable.Where(tag => !clearable.Contains(tag)).ToList();
        Assert.True(missing.Count == 0, $"pickers with no way to clear them: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Required_pickers_are_deliberately_not_clearable()
    {
        var clearable = TagsFor("OnClearPicks").ToHashSet(StringComparer.Ordinal);

        foreach (var required in Required)
            Assert.DoesNotContain(required, clearable);
    }

    [Fact]
    public void The_deck_offers_clear_selection_and_says_how_selection_works()
    {
        var clicks = Xaml.Descendants()
            .Select(e => e.Attribute("Click")?.Value)
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("OnClearDeckSelection", clicks);

        var hint = Xaml.Descendants()
            .FirstOrDefault(e => e.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name")?.Value == "DeckReturnHint");
        Assert.NotNull(hint);
        Assert.Contains("Ctrl+click", hint!.Attribute("Text")?.Value ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The column renders as a checkbox, so it MUST stay read-only or say so — a checkbox that
    /// looks clickable and is not is exactly what was reported as "stuck".
    /// </summary>
    [Fact]
    public void The_in_cart_column_is_labelled_as_a_status_not_a_control()
    {
        var column = Assert.Single(Xaml.Descendants()
            .Where(e => e.Name.LocalName == "DataGridCheckBoxColumn"));

        Assert.Equal("In cart", column.Attribute("Header")?.Value);
        Assert.Equal("True", column.Attribute("IsReadOnly")?.Value);
    }

    [Fact]
    public void Clearing_the_deck_cart_empties_it_but_cancel_still_restores()
    {
        DeckRecord[] records =
        [
            new("00012E46:Skyrim.esm", "Iron Sword", null, null, "IronSword", new object()),
            new("0001397E:Skyrim.esm", "Iron Dagger", null, null, "IronDagger", new object()),
        ];
        var deck = new ExpertDeckSession("weapons", records, DeckSelectionMode.Multi,
            ["00012E46:Skyrim.esm", "0001397E:Skyrim.esm"]);

        deck.ClearSelection();

        Assert.Empty(deck.SelectionCart);
        Assert.Empty(deck.Commit());
        Assert.All(deck.Records, record => Assert.False(record.IsSelected));

        // Nothing is committed until Apply, so backing out must bring both back.
        Assert.Equal(2, deck.Cancel().Count);
    }

    /// <summary>
    /// The seven armor slots share one remembered set. Clearing torso must not drop the helmet
    /// the user chose in a different list — the same scoping bug the deck's Apply once had.
    /// </summary>
    [Fact]
    public void Clearing_one_family_leaves_the_other_families_alone()
    {
        var remembered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "torso-a:Skyrim.esm", "torso-b:Skyrim.esm", "helmet-keep:Skyrim.esm",
        };

        DeckSelectionMerge.ReplaceFamily(
            remembered, ["torso-a:Skyrim.esm", "torso-b:Skyrim.esm"], []);

        Assert.Equal(["helmet-keep:Skyrim.esm"], remembered);
    }
}
