using Postiz.Transport;

namespace Postiz.Appliance;

public sealed record ApplianceHealth(bool Up, bool ApplianceMode, bool Ready);

public sealed record ApplianceStatus(
    bool Up,
    bool ApiOk,
    bool ApplianceMode,
    bool Ready,
    string ProductName,
    string? SystemOrganizationId,
    bool AdminProvisioned,
    bool ServiceKeyProvisioned);

public sealed record ApplianceCredentials(
    string ApiKey,
    string OrganizationId,
    DateTimeOffset IssuedAt,
    bool Rotated);

public sealed record EnsureOrganizationRequest(string PharmacyCode, string? OrganizationId = null, string? Name = null);

public sealed record EnsuredOrganization(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    string PharmacyCode);

public sealed record EnsureUserRequest(
    string OrganizationId,
    string UserId,
    string? Email = null,
    string? DisplayName = null,
    string Role = "USER");

public sealed record EnsuredUser(
    string Id,
    string Email,
    string Name,
    string OrganizationId,
    string Role,
    bool Disabled);

public sealed record AdminPasswordReset(bool Reset, string AdminUserId);

public interface IPostizApplianceClient
{
    Task<ApplianceHealth> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<ApplianceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<ApplianceCredentials> ProvisionCredentialsAsync(CancellationToken cancellationToken = default);
    Task<ApplianceCredentials> RotateCredentialsAsync(CancellationToken cancellationToken = default);
    Task<EnsuredOrganization> EnsureOrganizationAsync(EnsureOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<EnsuredUser> EnsureUserAsync(EnsureUserRequest request, CancellationToken cancellationToken = default);
    Task<AdminPasswordReset> ResetAdminPasswordAsync(string password, CancellationToken cancellationToken = default);
}

internal sealed class PostizApplianceClient(PostizTransport transport, PostizOptions options) : IPostizApplianceClient
{
    public Task<ApplianceHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
        transport.GetPublicAsync<ApplianceHealth>("internal/happym/appliance/health", cancellationToken);

    public Task<ApplianceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        transport.GetInternalAsync<ApplianceStatus>("internal/happym/appliance/linked", cancellationToken);

    public async Task<ApplianceCredentials> ProvisionCredentialsAsync(CancellationToken cancellationToken = default) =>
        Apply(await transport.PostInternalAsync<ApplianceCredentials>("internal/happym/appliance/credentials/provision", new { }, cancellationToken).ConfigureAwait(false));

    public async Task<ApplianceCredentials> RotateCredentialsAsync(CancellationToken cancellationToken = default) =>
        Apply(await transport.PostInternalAsync<ApplianceCredentials>("internal/happym/appliance/credentials/rotate", new { }, cancellationToken).ConfigureAwait(false));

    public Task<EnsuredOrganization> EnsureOrganizationAsync(EnsureOrganizationRequest request, CancellationToken cancellationToken = default) =>
        transport.PostInternalAsync<EnsuredOrganization>("internal/happym/appliance/organizations/ensure", request, cancellationToken);

    public Task<EnsuredUser> EnsureUserAsync(EnsureUserRequest request, CancellationToken cancellationToken = default) =>
        transport.PostInternalAsync<EnsuredUser>("internal/happym/appliance/users/ensure", request, cancellationToken);

    public Task<AdminPasswordReset> ResetAdminPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return transport.PostInternalAsync<AdminPasswordReset>("internal/happym/appliance/admin/password/reset", new { password }, cancellationToken);
    }

    private ApplianceCredentials Apply(ApplianceCredentials credentials)
    {
        options.ApiKey = credentials.ApiKey;
        return credentials;
    }
}
