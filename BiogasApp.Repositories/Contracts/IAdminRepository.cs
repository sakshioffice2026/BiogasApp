using BiogasApp.Model;
using BiogasApp.Model.ViewModel;

namespace BiogasApp.Repositories.Contracts;

public interface IAdminRepository
{
    Task<AdminViewModel> GetOverviewAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<List<AdminUserViewModel>> GetUsersAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<List<UserRoleModel>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    Task ChangeUserRoleAsync(Guid userId, int roleId, CancellationToken cancellationToken = default);

    Task<List<ExceptionLogItemViewModel>> GetRecentExceptionsAsync(
        int count = 50,
        CancellationToken cancellationToken = default);
}