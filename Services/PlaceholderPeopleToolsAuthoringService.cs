using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PlaceholderPeopleToolsAuthoringService : IPeopleToolsAuthoringService
{
    public Task<PeopleToolsSaveResult> SaveAsync(
        PeopleToolsSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new PeopleToolsSaveResult
        {
            WasAttempted = false,
            IsSuccess = false,
            Summary = "PeopleTools-backed save is not implemented yet.",
            Detail =
                "This build carries the authoritative App Package identity needed for a future PSIDE-backed save path, but it does not invoke PSIDE or Application Designer yet."
        });
    }
}
