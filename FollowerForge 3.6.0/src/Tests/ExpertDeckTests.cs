using Avalonia.Controls;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class ExpertDeckTests
{
    private static readonly DeckRecord[] Records =
    [
        new("00012E46:Skyrim.esm", "Iron Sword", "Weapon · OneHandSword", "BASE GAME", "IronSword", new object()),
        new("0010FAF2:ExampleWeapons.esp", "Moonlit Blade", "Weapon · enchanted", "MOD", "ExampleMoonBlade", new object()),
        new("0001397E:Skyrim.esm", "Iron Dagger", "Weapon · OneHandDagger", "BASE GAME", "IronDagger", new object()),
    ];

    [Theory]
    [InlineData("moonlit", "0010FAF2:ExampleWeapons.esp")]
    [InlineData("ExampleMoonBlade", "0010FAF2:ExampleWeapons.esp")]
    [InlineData("ExampleWeapons.esp", "0010FAF2:ExampleWeapons.esp")]
    [InlineData("0010FAF2:ExampleWeapons.esp", "0010FAF2:ExampleWeapons.esp")]
    [InlineData("enchanted", "0010FAF2:ExampleWeapons.esp")]
    public void Search_matches_every_expert_identity_surface(string query, string expectedKey)
    {
        var deck = new ExpertDeckSession("Weapons", Records, DeckSelectionMode.Multi, []);

        var result = Assert.Single(deck.Filter(query));

        Assert.Equal(expectedKey, result.Key);
    }

    [Fact]
    public void Empty_search_explains_filters_without_claiming_the_record_does_not_exist()
    {
        var deck = new ExpertDeckSession("Weapons", Records, DeckSelectionMode.Multi, []);

        Assert.Empty(deck.Filter("Dwemer railgun"));
        Assert.Equal(
            "No Weapons match “Dwemer railgun” with the current filters. Clear filters to keep browsing.",
            deck.EmptyStateMessage("Dwemer railgun"));
    }

    [Fact]
    public void Single_selection_replaces_the_previous_choice()
    {
        var deck = new ExpertDeckSession(
            "Weapons", Records, DeckSelectionMode.Single, ["00012E46:Skyrim.esm"]);

        deck.SetSelected("0010FAF2:ExampleWeapons.esp", true);

        Assert.Equal(["0010FAF2:ExampleWeapons.esp"], deck.Commit());
    }

    [Fact]
    public void Multi_selection_survives_filters_and_commits_in_stable_display_order()
    {
        var deck = new ExpertDeckSession(
            "Weapons", Records, DeckSelectionMode.Multi, ["00012E46:Skyrim.esm"]);
        deck.SetSelected("0010FAF2:ExampleWeapons.esp", true);

        Assert.Single(deck.Filter("Moonlit"));
        Assert.Equal(2, deck.SelectionCart.Count);
        Assert.Equal(
            ["00012E46:Skyrim.esm", "0010FAF2:ExampleWeapons.esp"],
            deck.Commit());
    }

    [Fact]
    public void Cancel_restores_the_original_selection()
    {
        var deck = new ExpertDeckSession(
            "Weapons", Records, DeckSelectionMode.Multi, ["0001397E:Skyrim.esm"]);
        deck.SetSelected("0001397E:Skyrim.esm", false);
        deck.SetSelected("0010FAF2:ExampleWeapons.esp", true);

        Assert.Equal(["0001397E:Skyrim.esm"], deck.Cancel());
        Assert.Equal(["0001397E:Skyrim.esm"], deck.Commit());
    }

    [Fact]
    public void OfferedKeys_are_every_record_in_the_session_not_the_search_slice()
    {
        var deck = new ExpertDeckSession("Weapons", Records, DeckSelectionMode.Multi, []);
        Assert.Equal(3, deck.OfferedKeys.Count);
        Assert.Single(deck.Filter("Moonlit"));
        Assert.Equal(3, deck.OfferedKeys.Count);
    }

    [Fact]
    public void Record_exposes_plugin_and_keeps_the_real_source_object()
    {
        var source = new object();
        var record = new DeckRecord(
            "00ABCDEF:Example.esp", "Example", "detail", "MOD", "ExampleEditorId", source);

        Assert.Equal("Example.esp", record.Plugin);
        Assert.Same(source, record.Source);
    }

    [Fact]
    public void Multi_commit_honours_checkbox_IsSelected_without_grid_selection()
    {
        var deck = new ExpertDeckSession("Weapons", Records, DeckSelectionMode.Multi, []);
        var record = Assert.Single(deck.Records, item => item.Key == "0010FAF2:ExampleWeapons.esp");
        record.IsSelected = true;

        Assert.Equal(["0010FAF2:ExampleWeapons.esp"], deck.Commit());
    }

    [Fact]
    public void Belongings_deck_apply_does_not_wipe_slices_it_never_showed()
    {
        // The belongings deck is built from LoreSource() — one of books/misc/food/ingredients.
        // OfferedKeys is that slice. Apply must not clear the other three.
        var remembered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "book-keep:Skyrim.esm",
            "misc-old:Example.esp",
            "food-keep:Skyrim.esm",
        };
        var offered = new[] { "misc-old:Example.esp", "misc-new:Example.esp" };

        DeckSelectionMerge.ReplaceFamily(remembered, offered, ["misc-new:Example.esp"]);

        Assert.Equal(
            ["book-keep:Skyrim.esm", "food-keep:Skyrim.esm", "misc-new:Example.esp"],
            remembered.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Family_commit_preserves_remembered_keys_from_other_families()
    {
        var remembered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "torso-old:Skyrim.esm",
            "helmet-keep:Skyrim.esm",
        };

        DeckSelectionMerge.ReplaceFamily(
            remembered,
            ["torso-old:Skyrim.esm", "torso-new:Example.esp"],
            ["torso-new:Example.esp"]);

        Assert.Equal(
            ["helmet-keep:Skyrim.esm", "torso-new:Example.esp"],
            remembered.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Single_mode_grid_rejects_SelectedItems_mutation()
    {
        // Pins the exact Avalonia behavior behind the 3.6.0 crash: opening a single-choice
        // deck called SelectedItems.Clear() on a Single-mode DataGrid and killed the app.
        var grid = new DataGrid { SelectionMode = DataGridSelectionMode.Single };
        grid.ItemsSource = Records;

        Assert.Throws<InvalidOperationException>(() => grid.SelectedItems.Clear());
    }

    [Fact]
    public void SyncSelected_in_single_mode_uses_SelectedItem_without_touching_SelectedItems()
    {
        var session = new ExpertDeckSession(
            "Weapons", Records, DeckSelectionMode.Single, ["0010FAF2:ExampleWeapons.esp"]);
        var grid = new DataGrid { SelectionMode = DataGridSelectionMode.Single };
        grid.ItemsSource = session.Records;

        DeckGridSelection.SyncSelected(grid, session.Records);

        var selected = Assert.IsType<DeckRecord>(grid.SelectedItem);
        Assert.Equal("0010FAF2:ExampleWeapons.esp", selected.Key);
    }

    [Fact]
    public void SyncSelected_in_single_mode_with_no_selection_leaves_the_grid_clear()
    {
        var session = new ExpertDeckSession("Weapons", Records, DeckSelectionMode.Single, []);
        var grid = new DataGrid { SelectionMode = DataGridSelectionMode.Single };
        grid.ItemsSource = session.Records;

        DeckGridSelection.SyncSelected(grid, session.Records);

        Assert.Null(grid.SelectedItem);
    }

    [Fact]
    public void SyncSelected_in_extended_mode_mirrors_every_selected_row()
    {
        var session = new ExpertDeckSession(
            "Weapons", Records, DeckSelectionMode.Multi, ["00012E46:Skyrim.esm", "0001397E:Skyrim.esm"]);
        var grid = new DataGrid { SelectionMode = DataGridSelectionMode.Extended };
        grid.ItemsSource = session.Records;

        DeckGridSelection.SyncSelected(grid, session.Records);

        Assert.Equal(2, grid.SelectedItems.Count);
    }
}
