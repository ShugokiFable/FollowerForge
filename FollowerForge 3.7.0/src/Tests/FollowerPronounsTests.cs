using FollowerForge.Domain;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class FollowerPronounsTests
{
    [Fact]
    public void Female_KeepsTheHistoricalSheHerCopy()
    {
        var p = FollowerPronouns.Female;
        Assert.Equal("Who is she?", WizardCopy.WhoTitle(p));
        Assert.Equal("Protected — only you can kill her (recommended)", WizardCopy.ProtectedOption(p));
        Assert.Equal("2   Her look", WizardCopy.StepLook(p));
        Assert.Equal("These are hers alone", p.Fill("These are {possessiveNoun} alone"));
    }

    [Fact]
    public void Male_SwitchesSubjectObjectAndPossessiveIndependently()
    {
        var p = FollowerPronouns.Male;
        Assert.Equal("Who is he?", WizardCopy.WhoTitle(p));
        Assert.Equal("Protected — only you can kill him (recommended)", WizardCopy.ProtectedOption(p));
        Assert.Equal("2   His look", WizardCopy.StepLook(p));
        Assert.Equal("Copy it into his plugin so I can tweak it later (never edits the original)",
            WizardCopy.CloneStyle(p));
        Assert.Equal("These are his alone", p.Fill("These are {possessiveNoun} alone"));
        Assert.Contains("he owns", WizardCopy.WearHint(p), StringComparison.Ordinal);
        Assert.DoesNotContain(" she ", $" {WizardCopy.WearHint(p)} ", StringComparison.Ordinal);
        Assert.DoesNotContain(" her ", $" {WizardCopy.WearHint(p)} ", StringComparison.Ordinal);
    }

    [Fact]
    public void FromFemale_MatchesTheSexBox()
    {
        Assert.Equal(FollowerPronouns.Female, FollowerPronouns.FromFemale(true));
        Assert.Equal(FollowerPronouns.Male, FollowerPronouns.FromFemale(false));
    }
}
