namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleToolsAuthoringCapabilitySnapshot
{
    public PeopleToolsAuthoringCapabilityStatus Status { get; init; } = PeopleToolsAuthoringCapabilityStatus.Unavailable;

    public string ConfiguredPsidePath { get; init; } = string.Empty;

    public string ResolvedPsideExecutablePath { get; init; } = string.Empty;

    public bool IsPsideConfigured { get; init; }

    public bool DoesConfiguredPathExist { get; init; }

    public bool IsPeopleToolsSaveCandidateAvailable => Status == PeopleToolsAuthoringCapabilityStatus.Available;

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}
