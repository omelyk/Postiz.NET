using System.Text.Json;

namespace Postiz.Appliance;

/// <summary>Phase-owned SYSTEM credential, not a public DTO or caller-ready flag.
/// No public key property, serialization, logging or credential-value hash.</summary>
public sealed class NativeAdministrativeCredential31 : IDisposable
{
    private readonly string organization;
    private char[]? key;
    public NativeAdministrativeCredential31(string systemOrganizationId, ReadOnlyMemory<char> credential)
    {
        if (string.IsNullOrWhiteSpace(systemOrganizationId) || systemOrganizationId.Length > 128
            || systemOrganizationId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or ':'))
            || credential.Length is < 32 or > 4096)
            throw new ArgumentException("An actual native SYSTEM administrative credential is required.");
        foreach (var value in credential.Span) if (value is < '!' or > '~') throw new ArgumentException("Invalid administrative credential.");
        organization = systemOrganizationId; key = credential.ToArray();
    }
    internal string Organization => key is not null ? organization : throw new ObjectDisposedException(nameof(NativeAdministrativeCredential31));
    internal bool EqualsKey(string other) => key is not null && other.AsSpan().SequenceEqual(key);
    internal void Attach(HttpRequestMessage request, string control, string purpose)
    {
        if (key is null || EqualsKey(control) || EqualsKey(purpose)) throw new ArgumentException("Distinct administrative authority is required.");
        request.Headers.Add("X-HappyM-Native-System-Authorization31", new string(key));
        request.Headers.Add("X-HappyM-Native-System-Organization31", organization);
    }
    public void Dispose() { if (key is not null) Array.Clear(key); key = null; }
    public override string ToString() => nameof(NativeAdministrativeCredential31);
}

public sealed record NativeOperationalScopeClaim31(string PackagePharmacyKey, string TenantId, string PharmacyId);
public sealed record NativeOperationalScope31(string PackagePharmacyKey, string TenantId, string PharmacyId, string OrganizationId, string UserId);
public sealed record NativeOperationalAdministrator31(string OrganizationId, string UserId);
public sealed record NativeOperationalRegistrationRequest31(string Contract, NativeConsumerBinding ApplianceBinding,
    NativeConsumerIdentity30 ConsumerBinding, string RequestId, string InputSha256, string? PreviousOperationId,
    string NotAfterUtc, NativeOperationalScopeClaim31[] Scopes);
public sealed record NativeOperationalRegistration31(string Contract, string OperationId, string RequestId, string InputSha256,
    string? PreviousOperationId, int Revision, string NotAfterUtc, NativeConsumerBinding ApplianceBinding,
    NativeConsumerIdentity30 ConsumerBinding, NativeOperationalAdministrator31 Administrator, NativeOperationalScope31[] Scopes);
public sealed record NativeOperationalBootstrapRequest31(string Contract, string OperationId, NativeConsumerBinding ApplianceBinding,
    NativeConsumerIdentity30 ConsumerBinding, NativeConsumerScope Consumer, string RequestId);
public sealed record NativeOperationalBootstrapReceipt31(string Contract, string OperationId, NativeConsumerBinding ApplianceBinding,
    NativeConsumerIdentity30 ConsumerBinding, NativeConsumerScope Consumer, string RequestId, string OrganizationId, string UserId);
public sealed record NativeOperationalRegistrationState31(string Contract, string OperationId, string State);
public sealed record NativeOperationalWorkspaceState31(string Contract, string OperationId, string RequestId, string OrganizationId,
    string UserId, NativeConsumerScope Consumer, string State);
public interface IPostizOperationalNativeCredentialSink31
{
    ValueTask ReceiveAsync(NativeOperationalBootstrapReceipt31 receipt, ReadOnlyMemory<char> nativeKey, CancellationToken ct);
}

public sealed partial class PostizNativeConsumerClient
{
    public const string OperationalRegistrationContract31 = "happym.postiz-operational-registration31/v1";
    public const string OperationalBootstrapContract31 = "happym.postiz-operational-bootstrap31/v1";
    public static string OperationalId31(NativeConsumerBinding appliance, NativeConsumerIdentity30 consumer, string requestId, string inputSha256)
    {
        ValidateBinding(appliance); ValidateIdentity30(consumer);
        if (!RequestId30(requestId) || !MetadataDigest31(inputSha256)) throw new ArgumentException("An exact operation identity is required.");
        return Hash([OperationalRegistrationContract31, appliance.ClientId, appliance.ApplicationEnvironmentId, appliance.LogicalDeploymentId,
            appliance.InstallationId, consumer.ClientId, consumer.ApplicationEnvironmentId, consumer.LogicalDeploymentId, consumer.ServiceId, requestId, inputSha256]);
    }

    public async Task<NativeOperationalRegistration31> RegisterOperationalConsumer31Async(NativeOperationalRegistrationRequest31 input,
        string purpose, NativeAdministrativeCredential31 administrator, CancellationToken ct = default)
    {
        ValidateRegistration31(input); ArgumentNullException.ThrowIfNull(administrator); ValidatePurpose(purpose);
        return await SendAsync("internal/happym/appliance/native-consumer/operational-register31", input, purpose,
            (root, _) => Task.FromResult(ParseOperationalRegistration31(root, input, administrator.Organization)), ct, administrator).ConfigureAwait(false);
    }

    public async Task<NativeOperationalRegistration31> ReadOperationalRegistration31Async(NativeOperationalRegistrationRequest31 expected,
        string purpose, NativeAdministrativeCredential31 administrator, CancellationToken ct = default)
    {
        ValidateRegistration31(expected); ArgumentNullException.ThrowIfNull(administrator); ValidatePurpose(purpose);
        var id = OperationalId31(expected.ApplianceBinding, expected.ConsumerBinding, expected.RequestId, expected.InputSha256);
        return await SendAsync("internal/happym/appliance/native-consumer/operational31/" + id, expected, purpose,
            (root, _) => Task.FromResult(ParseOperationalRegistration31(root, expected, administrator.Organization)), ct, administrator, HttpMethod.Get).ConfigureAwait(false);
    }

    public async Task<NativeOperationalRegistrationState31> ObserveOperationalRegistration31Async(NativeOperationalRegistrationRequest31 expected,
        string purpose, NativeAdministrativeCredential31 administrator, CancellationToken ct = default)
    {
        ValidateRegistration31(expected); ValidatePurpose(purpose); ArgumentNullException.ThrowIfNull(administrator);
        var id = OperationalId31(expected.ApplianceBinding, expected.ConsumerBinding, expected.RequestId, expected.InputSha256);
        return await SendAsync("internal/happym/appliance/native-consumer/operational31/" + id + "/status", expected, purpose,
            (root, _) => {
                RequireObject(root, "contract", "operationId", "state"); var state = String(root, "state");
                if (String(root, "contract") != "happym.postiz-operational-registration-state31/v1"
                    || String(root, "operationId") != id || state is not ("Absent" or "Present"))
                    throw new JsonException("Invalid observed registration state.");
                return Task.FromResult(new NativeOperationalRegistrationState31("happym.postiz-operational-registration-state31/v1", id, state));
            }, ct, administrator, HttpMethod.Get, expected).ConfigureAwait(false);
    }

    public async Task<NativeOperationalWorkspaceState31> ObserveOperationalWorkspace31Async(NativeOperationalBootstrapRequest31 expected,
        string purpose, NativeAdministrativeCredential31 administrator, CancellationToken ct = default)
    {
        ValidateOperationalBootstrap31(expected); ValidatePurpose(purpose); ArgumentNullException.ThrowIfNull(administrator);
        var (org, user) = SharedIdentity30(expected.ConsumerBinding, expected.Consumer);
        return await SendAsync("internal/happym/appliance/native-consumer/operational31/" + expected.OperationId + "/state/" + org,
            expected, purpose, (root, _) =>
            {
                RequireObject(root, "contract", "operationId", "requestId", "organizationId", "userId", "consumer", "state");
                RequireConsumer(root.GetProperty("consumer"), expected.Consumer);
                var state = String(root, "state");
                if (String(root, "contract") != "happym.postiz-operational-workspace-state31/v1"
                    || String(root, "operationId") != expected.OperationId || String(root, "requestId") != expected.RequestId
                    || String(root, "organizationId") != org || String(root, "userId") != user || state is not ("Absent" or "Present"))
                    throw new JsonException("Invalid observed workspace state.");
                return Task.FromResult(new NativeOperationalWorkspaceState31("happym.postiz-operational-workspace-state31/v1",
                    expected.OperationId, expected.RequestId, org, user, expected.Consumer, state));
            }, ct, administrator, HttpMethod.Get).ConfigureAwait(false);
    }

    /// <summary>Fresh create uses require-ABSENT server semantics. Explicit
    /// readExisting uses a distinct GET and a read-only server transaction.
    /// Neither path retries an uncertain mutating POST.</summary>
    public async Task<NativeOperationalBootstrapReceipt31> BootstrapOperationalConsumer31Async(NativeOperationalBootstrapRequest31 input,
        string purpose, NativeAdministrativeCredential31 administrator, IPostizOperationalNativeCredentialSink31 sink,
        bool readExisting = false, CancellationToken ct = default)
    {
        ValidateOperationalBootstrap31(input); ArgumentNullException.ThrowIfNull(sink); ArgumentNullException.ThrowIfNull(administrator); ValidatePurpose(purpose);
        var (org, user) = SharedIdentity30(input.ConsumerBinding, input.Consumer);
        var path = readExisting ? "internal/happym/appliance/native-consumer/operational31/" + input.OperationId + "/workspace/" + org
            : "internal/happym/appliance/native-consumer/operational-bootstrap31";
        return await SendAsync(path, input, purpose, async (root, token) =>
        {
            RequireObject(root, "contract", "operationId", "applianceBinding", "consumerBinding", "consumer", "requestId", "organizationId", "userId", "apiKey");
            RequireBinding(root.GetProperty("applianceBinding"), input.ApplianceBinding); RequireIdentity30(root.GetProperty("consumerBinding"), input.ConsumerBinding);
            RequireConsumer(root.GetProperty("consumer"), input.Consumer);
            if (String(root, "contract") != OperationalBootstrapContract31 || String(root, "operationId") != input.OperationId
                || String(root, "requestId") != input.RequestId || String(root, "organizationId") != org || String(root, "userId") != user)
                throw new JsonException("Invalid operational workspace receipt.");
            var raw = String(root, "apiKey");
            if (!Credential(raw, 32, 4096) || raw == controlSecret || raw == purpose || administrator.EqualsKey(raw))
                throw new JsonException("Invalid private workspace envelope.");
            var receipt = new NativeOperationalBootstrapReceipt31(OperationalBootstrapContract31, input.OperationId,
                input.ApplianceBinding, input.ConsumerBinding, input.Consumer, input.RequestId, org, user);
            var borrowed = raw.ToCharArray();
            try { token.ThrowIfCancellationRequested(); await sink.ReceiveAsync(receipt, borrowed, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); return receipt; }
            finally { Array.Clear(borrowed); }
        }, ct, administrator, readExisting ? HttpMethod.Get : HttpMethod.Post).ConfigureAwait(false);
    }

    public static NativeOperationalRegistration31 ParseOperationalRegistration31(JsonElement root, NativeOperationalRegistrationRequest31 expected,
        string systemOrganization)
    {
        ValidateRegistration31(expected);
        RequireObject(root, "contract", "operationId", "requestId", "inputSha256", "previousOperationId", "revision", "notAfterUtc",
            "applianceBinding", "consumerBinding", "administrator", "scopes");
        RequireBinding(root.GetProperty("applianceBinding"), expected.ApplianceBinding); RequireIdentity30(root.GetProperty("consumerBinding"), expected.ConsumerBinding);
        var previous = root.GetProperty("previousOperationId").ValueKind == JsonValueKind.Null ? null : String(root, "previousOperationId");
        var operationId = OperationalId31(expected.ApplianceBinding, expected.ConsumerBinding, expected.RequestId, expected.InputSha256);
        if (String(root, "contract") != OperationalRegistrationContract31 || String(root, "operationId") != operationId
            || String(root, "requestId") != expected.RequestId || String(root, "inputSha256") != expected.InputSha256
            || String(root, "notAfterUtc") != expected.NotAfterUtc || previous != expected.PreviousOperationId
            || !root.GetProperty("revision").TryGetInt32(out var revision) || revision < 1 || previous is null && revision != 1)
            throw new JsonException("Invalid authoritative operational registration.");
        var admin = root.GetProperty("administrator"); RequireObject(admin, "organizationId", "userId");
        if (String(admin, "organizationId") != systemOrganization || !SharedScopeIdentifier30(String(admin, "userId")))
            throw new JsonException("Invalid native administrator provenance.");
        var actual = root.GetProperty("scopes"); var sorted = expected.Scopes.OrderBy(x => x.PackagePharmacyKey, StringComparer.Ordinal).ToArray();
        if (actual.ValueKind != JsonValueKind.Array || actual.GetArrayLength() != sorted.Length) throw new JsonException("Invalid full scope set.");
        var scopes = new List<NativeOperationalScope31>(); var i = 0;
        foreach (var row in actual.EnumerateArray())
        {
            RequireObject(row, "packagePharmacyKey", "tenantId", "pharmacyId", "organizationId", "userId"); var selected = sorted[i++];
            var (org, user) = SharedIdentity30(expected.ConsumerBinding, new(expected.ConsumerBinding.LogicalDeploymentId, "test", selected.TenantId, selected.PharmacyId));
            if (String(row, "packagePharmacyKey") != selected.PackagePharmacyKey || String(row, "tenantId") != selected.TenantId
                || String(row, "pharmacyId") != selected.PharmacyId || String(row, "organizationId") != org || String(row, "userId") != user)
                throw new JsonException("Invalid registered pharmacy identity.");
            scopes.Add(new(selected.PackagePharmacyKey, selected.TenantId, selected.PharmacyId, org, user));
        }
        return new(OperationalRegistrationContract31, operationId, expected.RequestId, expected.InputSha256, previous, revision, expected.NotAfterUtc,
            expected.ApplianceBinding, expected.ConsumerBinding, new(systemOrganization, String(admin, "userId")), scopes.ToArray());
    }
    private static bool MetadataDigest31(string? v) => v is { Length: 64 } && v.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    private static void ValidateRegistration31(NativeOperationalRegistrationRequest31 input)
    {
        ArgumentNullException.ThrowIfNull(input); ValidateBinding(input.ApplianceBinding); ValidateIdentity30(input.ConsumerBinding);
        if (input.Contract != "happym.postiz-operational-register31/v1" || !RequestId30(input.RequestId) || !MetadataDigest31(input.InputSha256)
            || input.PreviousOperationId is not null && !MetadataDigest31(input.PreviousOperationId)
            || !DateTimeOffset.TryParseExact(input.NotAfterUtc, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out _)
            || input.Scopes is not { Length: >= 1 and <= 100 } || input.Scopes.Any(x => x is null || !Identifier(x.PackagePharmacyKey, 50)
                || x.PackagePharmacyKey.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c is not ('.' or '-'))
                || !SharedScopeIdentifier30(x.TenantId) || !SharedScopeIdentifier30(x.PharmacyId))
            || input.Scopes.Select(x => x.PackagePharmacyKey).Distinct(StringComparer.Ordinal).Count() != input.Scopes.Length
            || input.Scopes.Select(x => x.PharmacyId).Distinct(StringComparer.Ordinal).Count() != input.Scopes.Length)
            throw new ArgumentException("A complete finite native operational scope set is required.");
    }
    private static void ValidateOperationalBootstrap31(NativeOperationalBootstrapRequest31 input)
    {
        ArgumentNullException.ThrowIfNull(input); ValidateBinding(input.ApplianceBinding); ValidateIdentity30(input.ConsumerBinding);
        if (input.Contract != OperationalBootstrapContract31 || !MetadataDigest31(input.OperationId) || !RequestId30(input.RequestId)
            || input.Consumer is null || input.Consumer.Environment != "test" || input.Consumer.DeploymentId != input.ConsumerBinding.LogicalDeploymentId
            || !SharedScopeIdentifier30(input.Consumer.TenantId) || !SharedScopeIdentifier30(input.Consumer.PharmacyId))
            throw new ArgumentException("An exact operationally registered scope is required.");
    }
}
