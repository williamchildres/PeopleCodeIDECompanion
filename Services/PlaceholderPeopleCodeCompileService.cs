using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PlaceholderPeopleCodeCompileService : IPeopleCodeCompileService
{
    private readonly PeopleCodeAuthoringCapabilityService _capabilityService = new();

    public async Task<PeopleCodeCompileResult> CompileAsync(
        PeopleCodeSourceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        PeopleCodeAuthoringCapabilitySnapshot capability = await _capabilityService.GetCurrentAsync(cancellationToken);

        string message = capability.IsCompileOrchestrationAvailable
            ? "Compile tooling was detected, but PSIDE invocation is intentionally disabled in this build."
            : "Compile tooling is not available. Configure a valid PSIDE path in Settings before compile orchestration can be enabled.";

        return new PeopleCodeCompileResult
        {
            WasAttempted = false,
            IsSuccess = false,
            Message = message,
            LogEntries =
            [
                new PeopleCodeCompileLogEntry
                {
                    Level = "Info",
                    Message = "Compile log capture scaffolding is present, but no local CLI process was started."
                }
            ]
        };
    }
}
