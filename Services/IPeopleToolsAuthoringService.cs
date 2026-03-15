using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public interface IPeopleToolsAuthoringService
{
    Task<PeopleToolsSaveResult> SaveAsync(PeopleToolsSaveRequest request, CancellationToken cancellationToken = default);
}
