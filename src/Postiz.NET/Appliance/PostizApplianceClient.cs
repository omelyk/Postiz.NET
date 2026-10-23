using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed record ProviderOAuthAppStatus(
    string Provider,
    bool Configured,
    string? AppIdMasked);

public sealed record ProvidersStatus(IReadOnlyList<ProviderOAuthAppStatus> Providers);

public sealed record SetFacebookOAuthAppRequest(string AppId, string? AppSecret = null);

public sealed record SetProviderOAuthAppResult(bool Configured, string? AppIdMasked);

public static class SyntheticDeliveryContract
{
    public const string Contract = "happym.synthetic-delivery/v1";
    public const string Mode = "SIMULATION";
}

public static class SyntheticDeliveryStatuses
{
    public const string NotStarted = "NotStarted";
    public const string Partial = "Partial";
    public const string SimulatedPublished = "SimulatedPublished";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SyntheticDeliverySlide([property: JsonRequired] int SlideIndex, string SyntheticReceiptId, string Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SyntheticDeliveryEvent(int Ordinal, string Kind);

/// <summary>A TEST simulator journal readback. It never confirms provider publication.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SyntheticDeliveryReceipt(
    string Contract,
    string Mode,
    string Status,
    string OrganizationId,
    string IntegrationId,
    string PostId,
    string OccurrenceId,
    [property: JsonRequired] string? BodyHash,
    IReadOnlyList<SyntheticDeliverySlide> Slides,
    IReadOnlyList<SyntheticDeliveryEvent> Events);

public interface IPostizApplianceClient
{
    Task<ApplianceHealth> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<ApplianceStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<ApplianceCredentials> ProvisionCredentialsAsync(CancellationToken cancellationToken = default);
    Task<ApplianceCredentials> RotateCredentialsAsync(CancellationToken cancellationToken = default);
    Task<EnsuredOrganization> EnsureOrganizationAsync(EnsureOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<EnsuredUser> EnsureUserAsync(EnsureUserRequest request, CancellationToken cancellationToken = default);
    Task<AdminPasswordReset> ResetAdminPasswordAsync(string password, CancellationToken cancellationToken = default);
    Task<ProvidersStatus> GetProvidersStatusAsync(CancellationToken cancellationToken = default);
    Task<SetProviderOAuthAppResult> SetFacebookOAuthAppAsync(SetFacebookOAuthAppRequest request, CancellationToken cancellationToken = default);
    Task<SyntheticDeliveryReceipt> GetSyntheticDeliveryReceiptAsync(
        string organizationId, string postId, string occurrenceId, CancellationToken cancellationToken = default);
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

    public Task<ProvidersStatus> GetProvidersStatusAsync(CancellationToken cancellationToken = default) =>
        transport.GetInternalAsync<ProvidersStatus>("appliance/providers", cancellationToken);

    public Task<SetProviderOAuthAppResult> SetFacebookOAuthAppAsync(
        SetFacebookOAuthAppRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AppId);
        return transport.PutInternalAsync<SetProviderOAuthAppResult>(
            "appliance/providers/facebook", request, cancellationToken);
    }

    public async Task<SyntheticDeliveryReceipt> GetSyntheticDeliveryReceiptAsync(
        string organizationId, string postId, string occurrenceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var id in new[] { organizationId, postId, occurrenceId })
            if (!SyntheticIdentifier(id)) throw new ArgumentException("A valid synthetic receipt scope is required.");
        var path = "internal/happym/appliance/test-fixture/receipts"
            + $"?organizationId={Uri.EscapeDataString(organizationId)}&postId={Uri.EscapeDataString(postId)}"
            + $"&occurrenceId={Uri.EscapeDataString(occurrenceId)}";
        var receipt = await transport.GetInternalAsync<SyntheticDeliveryReceipt>(path, cancellationToken).ConfigureAwait(false);
        ValidateSyntheticReceipt(receipt, organizationId, postId, occurrenceId);
        return receipt;
    }

    private static bool SyntheticIdentifier(string? value) => value is { Length: >= 1 and <= 128 }
        && AsciiLetterOrDigit(value[0])
        && value.All(c => AsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-');

    private static bool AsciiLetterOrDigit(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool Hash64(string? value) => value is { Length: 64 }
        && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');

    private static void ValidateSyntheticReceipt(
        SyntheticDeliveryReceipt? receipt, string organizationId, string postId, string occurrenceId)
    {
        if (receipt is null || receipt.Contract != SyntheticDeliveryContract.Contract || receipt.Mode != SyntheticDeliveryContract.Mode
            || receipt.Status is not (SyntheticDeliveryStatuses.NotStarted or SyntheticDeliveryStatuses.Partial or SyntheticDeliveryStatuses.SimulatedPublished)
            || receipt.OrganizationId != organizationId || receipt.PostId != postId || receipt.OccurrenceId != occurrenceId
            || !SyntheticIdentifier(receipt.IntegrationId) || receipt.Slides is null || receipt.Events is null
            || receipt.Slides.Count > 20 || receipt.Events.Count > 1000)
            throw new JsonException("Invalid current synthetic delivery receipt contract or scope.");
        var hasJournal = receipt.Slides.Count > 0 || receipt.Events.Count > 0;
        if (hasJournal ? !Hash64(receipt.BodyHash) : receipt.BodyHash is not null)
            throw new JsonException("Invalid synthetic delivery journal hash.");
        for (var index = 0; index < receipt.Slides.Count; index++)
        {
            var slide = receipt.Slides[index];
            if (slide is null || slide.SlideIndex != index || slide.Status != "SimulatedCommitted"
                || slide.SyntheticReceiptId is not { Length: 74 }
                || !slide.SyntheticReceiptId.StartsWith("synthetic_", StringComparison.Ordinal)
                || !Hash64(slide.SyntheticReceiptId[10..]))
                throw new JsonException("Invalid synthetic slide receipt.");
        }
        for (var index = 0; index < receipt.Events.Count; index++)
        {
            var entry = receipt.Events[index];
            if (entry is null || entry.Ordinal != index + 1
                || entry.Kind is not ("Attempted" or "TransientRejected" or "SlideCommitted" or "ResponseLost" or "Completed"))
                throw new JsonException("Invalid synthetic delivery event journal.");
        }
        if (receipt.Status == SyntheticDeliveryStatuses.NotStarted && receipt.Slides.Count != 0
            || (receipt.Status is SyntheticDeliveryStatuses.Partial or SyntheticDeliveryStatuses.SimulatedPublished) && receipt.Slides.Count == 0
            || receipt.Status == SyntheticDeliveryStatuses.SimulatedPublished && !receipt.Events.Any(item => item.Kind == "Completed"))
            throw new JsonException("Invalid synthetic delivery completion state.");
    }

    private ApplianceCredentials Apply(ApplianceCredentials credentials)
    {
        options.ApiKey = credentials.ApiKey;
        return credentials;
    }
}
