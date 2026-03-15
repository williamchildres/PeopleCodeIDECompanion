using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public interface IPeopleCodeBackupService
{
    PeopleCodeBackupPlan CreateBackupPlan(PeopleCodeSourceSnapshot snapshot);
}
