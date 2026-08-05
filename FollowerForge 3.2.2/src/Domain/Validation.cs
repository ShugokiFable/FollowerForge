namespace FollowerForge.Domain;

public enum ValidationSeverity { Info, Warning, Error }

/// <summary>One finding from the build validation suite.</summary>
public sealed record ValidationFinding(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? Subject = null);

/// <summary>Aggregated validation outcome for a build.</summary>
public sealed class ValidationReport
{
    private readonly List<ValidationFinding> _findings = [];
    public IReadOnlyList<ValidationFinding> Findings => _findings;
    public bool HasErrors => _findings.Any(f => f.Severity == ValidationSeverity.Error);

    public void Add(ValidationSeverity severity, string code, string message, string? subject = null)
        => _findings.Add(new ValidationFinding(severity, code, message, subject));

    public void AddRange(IEnumerable<ValidationFinding> findings) => _findings.AddRange(findings);
}
