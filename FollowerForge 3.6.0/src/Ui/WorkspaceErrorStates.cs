using FollowerForge.Domain;

namespace FollowerForge.Ui;

public static class WorkspaceErrorStates
{
    public static string ManagerUnavailable(string manager, string detail) =>
        $"{manager} setup problem: {detail}. Open MO2 setup, switch manager, or review Paths, then retry.";

    public static string IndexingFailed(string subject, string detail) =>
        $"Could not index {subject}: {detail}. Correct the path or lock, then retry catalogue discovery.";
}

public sealed record ReviewFindingGroups(
    IReadOnlyList<string> MustFix,
    IReadOnlyList<string> CheckBeforeBuilding,
    IReadOnlyList<string> Information)
{
    public static ReviewFindingGroups Empty { get; } = new([], [], []);

    public static ReviewFindingGroups From(IEnumerable<ValidationFinding> findings)
    {
        var materialized = findings.ToList();
        static string Present(ValidationFinding finding) => $"{finding.Message} [{finding.Code}]";
        return new ReviewFindingGroups(
            materialized.Where(finding => finding.Severity == ValidationSeverity.Error).Select(Present).ToList(),
            materialized.Where(finding => finding.Severity == ValidationSeverity.Warning).Select(Present).ToList(),
            materialized.Where(finding => finding.Severity == ValidationSeverity.Info).Select(Present).ToList());
    }
}
