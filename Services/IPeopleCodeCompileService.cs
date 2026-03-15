using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public interface IPeopleCodeCompileService
{
    Task<PeopleCodeCompileResult> CompileAsync(PeopleCodeSourceSnapshot snapshot, CancellationToken cancellationToken = default);
}
