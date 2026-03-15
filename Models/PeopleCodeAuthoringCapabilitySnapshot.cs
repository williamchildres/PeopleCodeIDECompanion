namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeAuthoringCapabilitySnapshot
{
    public PeopleCodeAuthoringCapabilityStatus Status { get; init; } = PeopleCodeAuthoringCapabilityStatus.ReadOnly;

    public PeopleToolsAuthoringCapabilitySnapshot PeopleToolsAuthoring { get; init; } = new();

    public string ConfiguredPsidePath { get; init; } = string.Empty;

    public string ResolvedPsideExecutablePath { get; init; } = string.Empty;

    public bool IsPsideConfigured { get; init; }

    public bool DoesConfiguredPathExist { get; init; }

    public bool IsCompileOrchestrationAvailable { get; init; }

    public bool IsWriteBackAvailable =>
        Status is PeopleCodeAuthoringCapabilityStatus.WriteBackAvailable or PeopleCodeAuthoringCapabilityStatus.CompileAvailable;

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}
