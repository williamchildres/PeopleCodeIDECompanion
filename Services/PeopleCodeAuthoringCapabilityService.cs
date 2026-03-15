using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PeopleCodeAuthoringCapabilityService
{
    private static readonly string[] KnownPsideExecutableNames =
    [
        "pside.exe",
        "psidew.exe"
    ];

    private readonly LocalToolingSettingsStore _settingsStore = new();

    public async Task<PeopleCodeAuthoringCapabilitySnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        LocalToolingSettings settings = await _settingsStore.LoadAsync(cancellationToken);
        string configuredPath = settings.PsidePath.Trim();

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new PeopleCodeAuthoringCapabilitySnapshot
            {
                Status = PeopleCodeAuthoringCapabilityStatus.ReadOnly,
                PeopleToolsAuthoring = new PeopleToolsAuthoringCapabilitySnapshot
                {
                    Status = PeopleToolsAuthoringCapabilityStatus.Unavailable,
                    Summary = "PeopleTools save unavailable",
                    Detail = "No local PSIDE path is configured."
                },
                Summary = "Read-only mode",
                Detail = "No local PSIDE path is configured. Save and compile actions remain disabled."
            };
        }

        bool pathExists = File.Exists(configuredPath) || Directory.Exists(configuredPath);
        if (!pathExists)
        {
            return new PeopleCodeAuthoringCapabilitySnapshot
            {
                Status = PeopleCodeAuthoringCapabilityStatus.Misconfigured,
                PeopleToolsAuthoring = new PeopleToolsAuthoringCapabilitySnapshot
                {
                    Status = PeopleToolsAuthoringCapabilityStatus.Misconfigured,
                    ConfiguredPsidePath = configuredPath,
                    IsPsideConfigured = true,
                    Summary = "PeopleTools tooling misconfigured",
                    Detail = "A PSIDE path is configured, but that file or directory does not exist on this machine."
                },
                ConfiguredPsidePath = configuredPath,
                IsPsideConfigured = true,
                Summary = "Tooling misconfigured",
                Detail = "A PSIDE path is configured, but that file or directory does not exist on this machine."
            };
        }

        string resolvedExecutablePath = ResolvePsideExecutablePath(configuredPath);
        if (string.IsNullOrWhiteSpace(resolvedExecutablePath))
        {
            return new PeopleCodeAuthoringCapabilitySnapshot
            {
                Status = PeopleCodeAuthoringCapabilityStatus.Misconfigured,
                PeopleToolsAuthoring = new PeopleToolsAuthoringCapabilitySnapshot
                {
                    Status = PeopleToolsAuthoringCapabilityStatus.Misconfigured,
                    ConfiguredPsidePath = configuredPath,
                    IsPsideConfigured = true,
                    DoesConfiguredPathExist = true,
                    Summary = "PeopleTools tooling misconfigured",
                    Detail = "The configured PSIDE directory exists, but a PSIDE executable could not be resolved from it."
                },
                ConfiguredPsidePath = configuredPath,
                IsPsideConfigured = true,
                DoesConfiguredPathExist = true,
                Summary = "Tooling misconfigured",
                Detail = "The configured PSIDE directory exists, but a PSIDE executable could not be resolved from it."
            };
        }

        return new PeopleCodeAuthoringCapabilitySnapshot
        {
            Status = PeopleCodeAuthoringCapabilityStatus.CompileAvailable,
            PeopleToolsAuthoring = new PeopleToolsAuthoringCapabilitySnapshot
            {
                Status = PeopleToolsAuthoringCapabilityStatus.Available,
                ConfiguredPsidePath = configuredPath,
                ResolvedPsideExecutablePath = resolvedExecutablePath,
                IsPsideConfigured = true,
                DoesConfiguredPathExist = true,
                Summary = "PeopleTools tooling detected",
                Detail = "A local PSIDE executable was found. App Package save should eventually pivot through PeopleTools-backed authoring instead of direct database DML."
            },
            ConfiguredPsidePath = configuredPath,
            ResolvedPsideExecutablePath = resolvedExecutablePath,
            IsPsideConfigured = true,
            DoesConfiguredPathExist = true,
            IsCompileOrchestrationAvailable = true,
            Summary = "PeopleTools tooling detected",
            Detail = "A local PSIDE executable was found. Compile orchestration can be enabled later, and App Package save should flow through PeopleTools-backed authoring rather than direct database updates."
        };
    }

    public PeopleCodeAuthoringPresentationState CreatePresentationState(
        PeopleCodeAuthoringCapabilitySnapshot snapshot,
        PeopleCodeSourceIdentity? sourceIdentity,
        bool hasLoadedSource)
    {
        bool isAppPackage = sourceIdentity?.ObjectType == AllObjectsPeopleCodeBrowserService.AppPackageMode;
        string commonSaveMessage = isAppPackage
            ? BuildAppPackageSaveMessage(snapshot, hasLoadedSource)
            : hasLoadedSource
                ? "Write-back is intentionally disabled in this build while table/key validation and backup execution are still pending."
                : "Load a source first. Write-back is intentionally disabled in this build.";

        string revertMessage = hasLoadedSource
            ? "Revert will light up when editable PeopleCode buffers are introduced."
            : "Load a source first. Revert will light up when editable PeopleCode buffers are introduced.";

        string compileMessage = snapshot.Status switch
        {
            PeopleCodeAuthoringCapabilityStatus.CompileAvailable =>
                "Local compile tooling is present, but real PSIDE invocation is intentionally disabled in this build.",
            PeopleCodeAuthoringCapabilityStatus.Misconfigured =>
                snapshot.Detail,
            _ =>
                "Compile requires a valid local PSIDE path in Settings. This build does not invoke PSIDE yet."
        };

        return new PeopleCodeAuthoringPresentationState
        {
            StatusLabel = snapshot.Status.ToString(),
            Summary = isAppPackage ? BuildAppPackageSummary(snapshot) : snapshot.Summary,
            Detail = isAppPackage ? BuildAppPackageDetail(snapshot) : snapshot.Detail,
            SaveToolTip = commonSaveMessage,
            SaveCompileToolTip = compileMessage,
            RevertToolTip = revertMessage,
            IsSaveEnabled = false,
            IsSaveCompileEnabled = false,
            IsRevertEnabled = false
        };
    }

    private static string BuildAppPackageSaveMessage(PeopleCodeAuthoringCapabilitySnapshot snapshot, bool hasLoadedSource)
    {
        string prefix = hasLoadedSource
            ? "Direct App Package save is disabled because PSPCMPROG stores binary PROGTXT, so direct database DML is unsafe."
            : "Load a source first. Direct App Package save is disabled because PSPCMPROG stores binary PROGTXT, so direct database DML is unsafe.";

        return snapshot.PeopleToolsAuthoring.IsPeopleToolsSaveCandidateAvailable
            ? $"{prefix} Local PSIDE tooling is configured, so PeopleTools-backed save is the intended future path."
            : $"{prefix} Configure local PSIDE tooling to prepare for a future PeopleTools-backed save path.";
    }

    private static string BuildAppPackageSummary(PeopleCodeAuthoringCapabilitySnapshot snapshot)
    {
        return snapshot.PeopleToolsAuthoring.IsPeopleToolsSaveCandidateAvailable
            ? "PeopleTools-backed save path can be prototyped"
            : "Direct database save blocked";
    }

    private static string BuildAppPackageDetail(PeopleCodeAuthoringCapabilitySnapshot snapshot)
    {
        string detail =
            "Discovery validated PSPCMPROG as the authoritative App Package store and PROGTXT as binary program data. The app now carries the full authoritative identity, but this build does not invoke PSIDE yet.";

        return snapshot.PeopleToolsAuthoring.IsPeopleToolsSaveCandidateAvailable
            ? $"{detail} Local PeopleTools tooling is available for a future external save prototype."
            : $"{detail} Local PeopleTools tooling is not configured yet.";
    }

    private static string ResolvePsideExecutablePath(string configuredPath)
    {
        if (File.Exists(configuredPath))
        {
            return configuredPath;
        }

        if (!Directory.Exists(configuredPath))
        {
            return string.Empty;
        }

        foreach (string fileName in KnownPsideExecutableNames)
        {
            string candidate = Path.Combine(configuredPath, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }
}
