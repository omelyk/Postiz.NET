using System.Text.Json;

namespace Postiz.Appliance;

public sealed record NativeConsumerIdentity30(string ClientId, string ApplicationEnvironmentId, string LogicalDeploymentId, string ServiceId);
public sealed record NativeConsumerScopeClaim30(string TenantId, string PharmacyId);
public sealed record NativeConsumerAdmissionScope30(string TenantId, string PharmacyId, string OrganizationId, string UserId);
public sealed record NativeConsumerEnrollment30(string Contract, NativeConsumerBinding ApplianceBinding,
    NativeConsumerIdentity30 ConsumerBinding, string RequestId, bool RequireExisting, NativeConsumerScopeClaim30[] Scopes);
/// <summary>Sanitized server metadata. A DTO is not native authority.</summary>
public sealed record NativeConsumerAdmission30(string Contract, string AdmissionId, string RequestId,
    NativeConsumerBinding ApplianceBinding, NativeConsumerIdentity30 ConsumerBinding, NativeConsumerAdmissionScope30[] Scopes);
public sealed record NativeSharedConsumerBootstrapRequest30(string Contract, string AdmissionId, NativeConsumerBinding ApplianceBinding,
    NativeConsumerIdentity30 ConsumerBinding, NativeConsumerScope Consumer, string RequestId, bool RequireExisting);
public sealed record NativeSharedConsumerBootstrapReceipt30(string Contract, string AdmissionId, NativeConsumerBinding ApplianceBinding,
    NativeConsumerIdentity30 ConsumerBinding, NativeConsumerScope Consumer, string RequestId, string OrganizationId, string UserId);
public interface IPostizSharedNativeCredentialSink30
{
    ValueTask ReceiveAsync(NativeSharedConsumerBootstrapReceipt30 receipt, ReadOnlyMemory<char> nativeKey, CancellationToken ct);
}

public sealed partial class PostizNativeConsumerClient
{
    public const string AdmissionContract30 = "happym.native-consumer-admission/v1";
    public const string SharedBootstrapContract30 = "happym.native-consumer-bootstrap/v2";

    public async Task<NativeConsumerAdmission30> EnrollNativeConsumer30Async(NativeConsumerEnrollment30 input,
        string purposeCredential, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input); ValidateBinding(input.ApplianceBinding); ValidateIdentity30(input.ConsumerBinding);
        ValidatePurpose(purposeCredential);
        if (input.Contract != "happym.native-consumer-enroll/v1" || !RequestId30(input.RequestId)
            || input.Scopes is not { Length: >= 1 and <= 2 } || input.Scopes.Any(x => x is null
                || !SharedScopeIdentifier30(x.TenantId) || !SharedScopeIdentifier30(x.PharmacyId))
            || input.Scopes.Select(x => x.PharmacyId).Distinct(StringComparer.Ordinal).Count() != input.Scopes.Length)
            throw new ArgumentException("A finite selected consumer enrollment is required.");
        return await SendAsync("internal/happym/appliance/native-consumer/enroll30", input, purposeCredential,
            (root, _) => Task.FromResult(ParseAdmission30(root, input.ApplianceBinding, input.ConsumerBinding,
                input.RequestId, input.Scopes)), ct).ConfigureAwait(false);
    }

    public async Task<NativeSharedConsumerBootstrapReceipt30> BootstrapSharedNativeConsumer30Async(
        NativeSharedConsumerBootstrapRequest30 input, string purposeCredential, IPostizSharedNativeCredentialSink30 sink,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(sink);
        ValidateBinding(input.ApplianceBinding); ValidateIdentity30(input.ConsumerBinding); ValidatePurpose(purposeCredential);
        if (input.Contract != SharedBootstrapContract30 || input.AdmissionId != AdmissionId30(input.ApplianceBinding, input.ConsumerBinding)
            || !RequestId30(input.RequestId) || input.Consumer is null || input.Consumer.Environment != "test"
            || input.Consumer.DeploymentId != input.ConsumerBinding.LogicalDeploymentId
            || !SharedScopeIdentifier30(input.Consumer.TenantId) || !SharedScopeIdentifier30(input.Consumer.PharmacyId))
            throw new ArgumentException("An admitted selected consumer scope is required.");
        return await SendAsync("internal/happym/appliance/native-consumer/bootstrap30", input, purposeCredential,
            async (root, token) =>
            {
                RequireObject(root, "contract", "admissionId", "applianceBinding", "consumerBinding", "consumer", "requestId", "organizationId", "userId", "apiKey");
                RequireBinding(root.GetProperty("applianceBinding"), input.ApplianceBinding);
                RequireIdentity30(root.GetProperty("consumerBinding"), input.ConsumerBinding);
                RequireConsumer(root.GetProperty("consumer"), input.Consumer);
                var (org, user) = SharedIdentity30(input.ConsumerBinding, input.Consumer);
                if (String(root, "contract") != SharedBootstrapContract30 || String(root, "admissionId") != input.AdmissionId
                    || String(root, "requestId") != input.RequestId || String(root, "organizationId") != org || String(root, "userId") != user)
                    throw new JsonException("Invalid admitted consumer receipt.");
                var raw = String(root, "apiKey");
                if (!Credential(raw, 32, 4096) || raw == controlSecret || raw == purposeCredential) throw new JsonException("Invalid native envelope.");
                var result = new NativeSharedConsumerBootstrapReceipt30(SharedBootstrapContract30, input.AdmissionId,
                    input.ApplianceBinding, input.ConsumerBinding, input.Consumer, input.RequestId, org, user);
                var borrowed = raw.ToCharArray();
                try { token.ThrowIfCancellationRequested(); await sink.ReceiveAsync(result, borrowed, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); return result; }
                finally { Array.Clear(borrowed); }
            }, ct).ConfigureAwait(false);
    }

    public static string AdmissionId30(NativeConsumerBinding appliance, NativeConsumerIdentity30 consumer)
    {
        ValidateBinding(appliance); ValidateIdentity30(consumer);
        return Hash([AdmissionContract30, appliance.ClientId, appliance.ApplicationEnvironmentId, appliance.LogicalDeploymentId,
            appliance.InstallationId, consumer.ClientId, consumer.ApplicationEnvironmentId, consumer.LogicalDeploymentId, consumer.ServiceId]);
    }

    public static NativeConsumerAdmission30 ParseAdmission30(JsonElement root, NativeConsumerBinding appliance,
        NativeConsumerIdentity30 consumer, string requestId, NativeConsumerScopeClaim30[] scopes)
    {
        ValidateBinding(appliance); ValidateIdentity30(consumer);
        RequireObject(root, "contract", "admissionId", "requestId", "applianceBinding", "consumerBinding", "scopes");
        RequireBinding(root.GetProperty("applianceBinding"), appliance); RequireIdentity30(root.GetProperty("consumerBinding"), consumer);
        var id = AdmissionId30(appliance, consumer);
        if (String(root, "contract") != AdmissionContract30 || String(root, "admissionId") != id
            || String(root, "requestId") != requestId || !RequestId30(requestId) || scopes is not { Length: >= 1 and <= 2 }
            || scopes.Any(x => x is null || !SharedScopeIdentifier30(x.TenantId) || !SharedScopeIdentifier30(x.PharmacyId))
            || scopes.Select(x => x.PharmacyId).Distinct(StringComparer.Ordinal).Count() != scopes.Length)
            throw new JsonException("Invalid server admission.");
        var rows = root.GetProperty("scopes");
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != scopes.Length) throw new JsonException("Invalid admission scopes.");
        var result = new List<NativeConsumerAdmissionScope30>();
        var expected = scopes.OrderBy(x => x.PharmacyId, StringComparer.Ordinal).ToArray();
        var ordinal = 0;
        foreach (var row in rows.EnumerateArray())
        {
            RequireObject(row, "tenantId", "pharmacyId", "organizationId", "userId");
            var scope = expected[ordinal++];
            var (org, user) = SharedIdentity30(consumer, new NativeConsumerScope(consumer.LogicalDeploymentId, "test", scope.TenantId, scope.PharmacyId));
            if (String(row, "tenantId") != scope.TenantId || String(row, "pharmacyId") != scope.PharmacyId
                || String(row, "organizationId") != org || String(row, "userId") != user) throw new JsonException("Invalid admitted workspace.");
            result.Add(new(scope.TenantId, scope.PharmacyId, org, user));
        }
        return new(AdmissionContract30, id, requestId, appliance, consumer, result.ToArray());
    }

    private static (string Organization, string User) SharedIdentity30(NativeConsumerIdentity30 identity, NativeConsumerScope scope)
    {
        var digest = Hash(["happym.postiz.consumer-workspace/v2", identity.ClientId, identity.ApplicationEnvironmentId,
            identity.LogicalDeploymentId, identity.ServiceId, scope.Environment, scope.TenantId, scope.PharmacyId]);
        return ("hm-" + digest, "sm." + digest);
    }

    private static bool SharedScopeIdentifier30(string? value) => ScopeIdentifier(value) && AsciiLetterOrDigit(value![0]);
    private static bool RequestId30(string? value) => value is { Length: 32 } && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    private static void ValidateIdentity30(NativeConsumerIdentity30 value)
    {
        if (value is null || !new[] { value.ClientId, value.ApplicationEnvironmentId, value.LogicalDeploymentId, value.ServiceId }
            .All(x => x is { Length: >= 1 and <= 64 } && (x[0] is >= 'a' and <= 'z' or >= '0' and <= '9') && x.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')))
            throw new ArgumentException("An exact installed consumer identity is required.");
        if (value.LogicalDeploymentId.Any(c => c == '_')) throw new ArgumentException("A current consumer deployment id is required.");
    }
    private static void RequireIdentity30(JsonElement root, NativeConsumerIdentity30 expected)
    {
        ValidateIdentity30(expected); RequireObject(root, "clientId", "applicationEnvironmentId", "logicalDeploymentId", "serviceId");
        if (String(root, "clientId") != expected.ClientId || String(root, "applicationEnvironmentId") != expected.ApplicationEnvironmentId
            || String(root, "logicalDeploymentId") != expected.LogicalDeploymentId || String(root, "serviceId") != expected.ServiceId)
            throw new JsonException("Invalid installed consumer receipt.");
    }
}
