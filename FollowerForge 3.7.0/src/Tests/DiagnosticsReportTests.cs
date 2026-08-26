using FollowerForge.Domain;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

/// <summary>
/// The report exists to make a stranger's bug report actionable on the first message, so these
/// pin the two things that would make it useless: leaving out what was actually asked for, and
/// publishing the reporter's Windows account name when they paste it into a Nexus comment.
/// </summary>
public sealed class DiagnosticsReportTests
{
    private static EnvironmentSnapshot Mo2Env() => new()
    {
        Manager = ModManagerKind.Mo2,
        ManagerLabel = "Mod Organizer 2",
        GameRootPath = @"D:\Games\Skyrim Special Edition",
        GameDataPath = @"D:\Games\Skyrim Special Edition\Data",
        PluginDataPath = @"D:\view",
        InstancePath = @"D:\MO2\Skyrim",
        StagingPath = @"D:\MO2\Skyrim\mods",
        ProfilesPath = @"D:\MO2\Skyrim\profiles",
        RuntimePluginsTxtPath = @"D:\MO2\Skyrim\profiles\Default\plugins.txt",
        ActiveProfileId = "Default",
        ActiveProfileReason = "chosen in MO2 setup",
        EnabledPluginCount = 1337,
        LoadOrderCount = 1400,
        StagingModCount = 2100,
        Warnings = ["Overwrite folder is not empty"],
    };

    private static string Render(
        EnvironmentSnapshot? env = null,
        bool indexing = false,
        DiagnosticsDraft? draft = null,
        IReadOnlyList<string>? mustFix = null) =>
        DiagnosticsReport.Render(
            "3.6.1", env ?? Mo2Env(), indexing, knownPlaceCount: 3369, exportedFaceCount: 2,
            UiPreferences.Default, draft ?? DiagnosticsDraft.Empty,
            mustFix ?? [], []);

    [Fact]
    public void Carries_the_facts_a_first_reply_would_otherwise_have_to_ask_for()
    {
        var report = Render();

        Assert.Contains("3.6.1", report);
        Assert.Contains("Mod Organizer 2", report);
        Assert.Contains("Default", report);
        Assert.Contains("1,337", report);           // enabled plugins
        Assert.Contains("2,100", report);           // staging mods
        Assert.Contains("Overwrite folder is not empty", report);
    }

    [Fact]
    public void Reports_the_last_builds_must_fix_findings()
    {
        var report = Render(mustFix: ["Required master 'SOSVoices.esm' is not installed [MASTER_MISSING]"]);

        Assert.Contains("MASTER_MISSING", report);
        Assert.Contains("Must fix", report);
    }

    [Fact]
    public void Says_when_the_catalogue_was_still_indexing_rather_than_implying_a_finished_scan()
    {
        Assert.Contains("STILL INDEXING", Render(indexing: true));
        Assert.Contains("ready", Render(indexing: false));
    }

    [Fact]
    public void An_unresolved_manager_is_stated_not_omitted()
    {
        var report = DiagnosticsReport.Render(
            "3.6.1", env: null, isIndexing: false, knownPlaceCount: 0, exportedFaceCount: 0,
            UiPreferences.Default, DiagnosticsDraft.Empty, [], []);

        Assert.Contains("NOT RESOLVED", report);
    }

    [Fact]
    public void Unchosen_draft_fields_read_as_unchosen_instead_of_blank()
    {
        var report = Render();

        Assert.Contains("(not chosen yet)", report);
    }

    [Theory]
    [InlineData(Environment.SpecialFolder.LocalApplicationData, "%LOCALAPPDATA%")]
    [InlineData(Environment.SpecialFolder.ApplicationData, "%APPDATA%")]
    [InlineData(Environment.SpecialFolder.UserProfile, "%USERPROFILE%")]
    public void Home_paths_are_tokenised(Environment.SpecialFolder folder, string token)
    {
        var path = Path.Combine(Environment.GetFolderPath(folder), "FollowerForge", "catalog.db");

        var redacted = DiagnosticsReport.Redact(path);

        Assert.StartsWith(token, redacted);
        Assert.EndsWith(Path.Combine("FollowerForge", "catalog.db"), redacted);
    }

    /// <summary>
    /// LocalAppData is a child of the user profile. Matching the profile first would leave
    /// "%USERPROFILE%\AppData\Local\..." — still readable, but it no longer hides anything if
    /// the account name is the interesting part, and it is the longer-prefix rule that matters.
    /// </summary>
    [Fact]
    public void LocalAppData_wins_over_the_user_profile_it_lives_inside()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FollowerForge");

        Assert.StartsWith("%LOCALAPPDATA%", DiagnosticsReport.Redact(path));
    }

    [Fact]
    public void A_pasted_report_does_not_publish_the_windows_account_name()
    {
        var env = Mo2Env() with
        {
            InstancePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MO2"),
            StagingPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MO2", "mods"),
            Mo2OverwritePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MO2", "overwrite"),
        };

        var report = DiagnosticsReport.Render(
            "3.6.1", env, isIndexing: false, knownPlaceCount: 0, exportedFaceCount: 0,
            UiPreferences.Default, DiagnosticsDraft.Empty, [], []);

        Assert.DoesNotContain(Environment.UserName, report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", report);
        Assert.Contains("%LOCALAPPDATA%", report);
    }

    [Fact]
    public void Paths_outside_the_home_folder_are_left_alone()
    {
        Assert.Equal(@"D:\Games\Skyrim Special Edition", DiagnosticsReport.Redact(@"D:\Games\Skyrim Special Edition"));
        Assert.Equal("(none)", DiagnosticsReport.Redact(null));
        Assert.Equal("(none)", DiagnosticsReport.Redact("   "));
    }
}
