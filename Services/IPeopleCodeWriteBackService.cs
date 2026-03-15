using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public interface IPeopleCodeWriteBackService
{
    Task<PeopleCodeSaveResult> SaveAsync(PeopleCodePendingEdit pendingEdit, CancellationToken cancellationToken = default);
}
