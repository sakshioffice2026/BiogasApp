using BiogasApp.Model;
using BiogasApp.Model.ViewModel;

namespace BiogasApp.Repositories.Contracts;

public interface IAccount
{
    Task<AppUserModel?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AppUserModel?> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken = default);

    Task<AppUserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppUserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}