namespace AspireForge.Cli.Output;

public sealed record DoctorJsonReport(string Project, IReadOnlyList<DoctorJsonIssue> Issues, int Suppressed, int Baselined);

public sealed record DoctorJsonIssue(string RuleId, string Severity, string Title);
