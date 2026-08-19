using System.Xml.Linq;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class FieldMigrationLedgerTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Every_named_3_5_control_has_one_3_6_destination_and_one_live_control()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var current = XDocument.Load(Path.Combine(src, "Ui", "WizardWindow.axaml"));
        var snapshot = Directory.GetParent(src)!.FullName;
        var parent = XDocument.Load(Path.Combine(snapshot, "..", "FollowerForge 3.5.0", "src", "Ui", "WizardWindow.axaml"));
        var legacyNames = Names(parent).OrderBy(name => name, StringComparer.Ordinal).ToList();
        var currentNames = Names(current).ToList();
        var ledger = FieldMigrationLedger.Entries;

        Assert.NotEmpty(legacyNames);
        foreach (var name in legacyNames)
        {
            Assert.Equal(1, currentNames.Count(candidate => candidate == name));
            var entry = Assert.Single(ledger.Where(candidate => candidate.ControlName == name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Destination));
            Assert.False(string.IsNullOrWhiteSpace(entry.Contract));
        }
        Assert.Equal(legacyNames.Count, ledger.Count);
    }

    private static IEnumerable<string> Names(XDocument document) => document.Descendants()
        .Select(element => element.Attribute(Xaml + "Name")?.Value)
        .Where(name => name is not null)!;
}
