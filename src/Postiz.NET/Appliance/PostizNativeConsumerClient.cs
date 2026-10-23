using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Postiz.Appliance;

public sealed record NativeConsumerBinding(string ClientId, string ApplicationEnvironmentId,
    string LogicalDeploymentId, string InstallationId);
public sealed record NativeConsumerScope(string DeploymentId, string Environment, string TenantId, string PharmacyId);
public sealed record NativeConsumerBootstrapRequest(NativeConsumerBinding Binding, NativeConsumerScope Consumer, string RequestId, bool RequireExisting);
public sealed record NativeConsumerBootstrapReceipt(string Contract, NativeConsumerBinding Binding,
    NativeConsumerScope Consumer, string RequestId, string OrganizationId, string UserId);
public sealed record NativeFixtureProvisionRequest(NativeConsumerBinding Binding, string OrganizationId, string Profile);
public sealed record NativeFixtureProvisionReceipt(string Contract, string Mode, string IntegrationId, string Identifier, string Label);

/// <summary>
/// Native setup's phase-bound protected receiver. The key is borrowed for this
/// callback only. A protected receiver may copy it into its private phase-owned
/// KeyRing until that phase ends; no public return, serialization or logging.
/// Implementations own the current native authority and conditional secret commit.
/// </summary>
public interface IPostizNativeCredentialSink
{
    ValueTask ReceiveAsync(NativeConsumerBootstrapReceipt receipt, ReadOnlyMemory<char> nativeKey,
        CancellationToken cancellationToken);
}

/// <summary>No response body, credential, inner exception or uncertain-write retry.</summary>
public sealed class PostizNativeOutcomeUnknownException() : Exception("native_consumer_outcome_unknown") { }

/// <summary>
/// Separate, QA-only native TEST provisioning client. It is not an ordinary
/// consumer or browser API and does not qualify a native owner or a release.
/// The caller must supply the qualified native HttpClient: loopback DNS/address
/// binding, redirects/proxy/cookies disabled and ordinary certificate validation.
/// </summary>
public sealed partial class PostizNativeConsumerClient
{
    public const string BootstrapContract = "happym.native-consumer-bootstrap/v1";
    private const int MaximumResponseBytes = 65_536;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string[] Profiles = ["success", "transient-before-commit", "lost-response-after-commit", "partial-slide"];
    private readonly HttpClient http;
    private readonly Uri baseAddress;
    private readonly string clientId;
    private readonly string controlSecret;
    private readonly TimeSpan deadline;

    public PostizNativeConsumerClient(HttpClient httpClient, PostizOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!options.BaseAddress.IsLoopback || options.BaseAddress.Scheme is not ("http" or "https")
            || options.BaseAddress.UserInfo.Length != 0 || options.BaseAddress.Query.Length != 0
            || options.BaseAddress.Fragment.Length != 0 || !Identifier(options.InternalClientId, 128)
            || !Credential(options.InternalClientSecret, 32, 4096)
            || new[] { "Authorization", "X-HappyM-Organization-Id", "X-HappyM-Client-Id",
                "X-HappyM-Native-Fixture-Authorization" }.Any(httpClient.DefaultRequestHeaders.Contains))
            throw new ArgumentException("A qualified loopback native control transport is required.");
        http = httpClient;
        baseAddress = new Uri(options.BaseAddress.AbsoluteUri.TrimEnd('/') + "/");
        clientId = options.InternalClientId!;
        controlSecret = options.InternalClientSecret!;
        deadline = options.RequestTimeout <= TimeSpan.FromSeconds(10) ? options.RequestTimeout : TimeSpan.FromSeconds(10);
    }

    public async Task<NativeConsumerBootstrapReceipt> BootstrapNativeConsumerAsync(
        NativeConsumerBootstrapRequest request, string purposeCredential, IPostizNativeCredentialSink sink,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sink);
        ValidateRequest(request);
        ValidatePurpose(purposeCredential);
        return await SendAsync("internal/happym/appliance/native-consumer/bootstrap", request, purposeCredential,
            async (root, token) =>
            {
                RequireObject(root, "contract", "binding", "consumer", "requestId", "organizationId", "userId", "apiKey");
                RequireBinding(root.GetProperty("binding"), request.Binding);
                RequireConsumer(root.GetProperty("consumer"), request.Consumer);
                var (organizationId, userId) = Identity(request.Consumer);
                if (String(root, "contract") != BootstrapContract || String(root, "requestId") != request.RequestId
                    || String(root, "organizationId") != organizationId || String(root, "userId") != userId)
                    throw new JsonException("Invalid native consumer receipt.");
                var rawKey = String(root, "apiKey");
                if (!Credential(rawKey, 32, 4096) || rawKey == controlSecret || rawKey == purposeCredential) throw new JsonException("Invalid native credential envelope.");
                var receipt = new NativeConsumerBootstrapReceipt(BootstrapContract, request.Binding,
                    request.Consumer, request.RequestId, organizationId, userId);
                var borrowed = rawKey.ToCharArray();
                try
                {
                    token.ThrowIfCancellationRequested();
                    await sink.ReceiveAsync(receipt, borrowed, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return receipt;
                }
                finally { Array.Clear(borrowed); }
            }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NativeFixtureProvisionReceipt> ProvisionNativeFixtureAsync(
        NativeFixtureProvisionRequest request, string purposeCredential, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ValidateBinding(request.Binding);
        if (!NativeOrganization(request.OrganizationId) || !Profiles.Contains(request.Profile, StringComparer.Ordinal))
            throw new ArgumentException("A current native TEST fixture scope is required.");
        ValidatePurpose(purposeCredential);
        return await SendAsync("internal/happym/appliance/test-fixture/provision", request, purposeCredential,
            (root, _) =>
            {
                RequireObject(root, "contract", "mode", "integrationId", "identifier", "label");
                var integrationId = FixtureId(request);
                var label = "[SIMULAZIONE] " + request.Profile;
                if (String(root, "contract") != SyntheticDeliveryContract.Contract || String(root, "mode") != SyntheticDeliveryContract.Mode
                    || String(root, "integrationId") != integrationId || String(root, "identifier") != "happym-test-fixture"
                    || String(root, "label") != label) throw new JsonException("Invalid native fixture receipt.");
                return Task.FromResult(new NativeFixtureProvisionReceipt(SyntheticDeliveryContract.Contract,
                    SyntheticDeliveryContract.Mode, integrationId, "happym-test-fixture", label));
            }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SendAsync<T>(string path, object input, string purpose,
        Func<JsonElement, CancellationToken, Task<T>> consume, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline);
        var body = JsonSerializer.SerializeToUtf8Bytes(input, JsonOptions);
        var bytes = ArrayPool<byte>.Shared.Rent(MaximumResponseBytes + 1);
        try
        {
            var destination = new Uri(baseAddress, path);
            using var request = new HttpRequestMessage(HttpMethod.Post, destination) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new("application/json");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + controlSecret);
            request.Headers.TryAddWithoutValidation("X-HappyM-Client-Id", clientId);
            request.Headers.TryAddWithoutValidation("X-HappyM-Native-Fixture-Authorization", purpose);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.CacheControl = new() { NoStore = true };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            // The qualified caller disables redirects. This is an additional
            // response-origin check, not a substitute for that handler policy.
            if (response.RequestMessage?.RequestUri != destination
                || response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created)
                || response.Content.Headers.ContentLength > MaximumResponseBytes
                || response.Content.Headers.ContentType?.MediaType != "application/json")
                throw new PostizNativeOutcomeUnknownException();
            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var count = 0;
            while (count <= MaximumResponseBytes)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(count, MaximumResponseBytes + 1 - count), timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                count += read;
            }
            if (count == 0 || count > MaximumResponseBytes) throw new PostizNativeOutcomeUnknownException();
            using var document = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
            return await consume(document.RootElement, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception) { throw new PostizNativeOutcomeUnknownException(); }
        finally
        {
            CryptographicOperations.ZeroMemory(body);
            CryptographicOperations.ZeroMemory(bytes);
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private void ValidatePurpose(string value)
    {
        if (!Credential(value, 32, 256) || value == controlSecret)
            throw new ArgumentException("A distinct native TEST-purpose credential is required.");
    }
    private static void ValidateRequest(NativeConsumerBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateBinding(request.Binding);
        var scope = request.Consumer;
        if (scope is null || scope.Environment != "test" || scope.DeploymentId != request.Binding.LogicalDeploymentId
            || scope.DeploymentId.Length is < 1 or > 64
            || !scope.DeploymentId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
            || !ScopeIdentifier(scope.TenantId) || !ScopeIdentifier(scope.PharmacyId)
            || !Identifier(request.RequestId, 128)) throw new ArgumentException("A selected native TEST consumer scope is required.");
    }
    private static void ValidateBinding(NativeConsumerBinding binding)
    {
        if (binding is null || !new[] { binding.ClientId, binding.ApplicationEnvironmentId, binding.LogicalDeploymentId, binding.InstallationId }
            .All(x => Identifier(x, 128))) throw new ArgumentException("A selected native TEST binding is required.");
    }
    private static (string Organization, string User) Identity(NativeConsumerScope scope)
    {
        var hash = Hash(["happym.postiz.consumer-workspace/v1", scope.DeploymentId, scope.Environment, scope.TenantId, scope.PharmacyId]);
        return ("hm-" + hash, "sm." + hash);
    }
    private static string FixtureId(NativeFixtureProvisionRequest request) => "happym-sim-" + Hash([
        request.Binding.ClientId, request.Binding.ApplicationEnvironmentId, request.Binding.LogicalDeploymentId,
        request.Binding.InstallationId, request.OrganizationId, request.Profile])[..40];
    private static string Hash(string[] values) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values))).ToLowerInvariant();
    private static bool NativeOrganization(string value) => value is { Length: 67 } && value.StartsWith("hm-", StringComparison.Ordinal)
        && value[3..].All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    private static bool Credential(string? value, int minimum, int maximum) => value is not null && value.Length >= minimum
        && value.Length <= maximum && value.All(c => c is >= '!' and <= '~');
    private static bool ScopeIdentifier(string? value) => value is { Length: > 0 and <= 100 }
        && value.All(c => AsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-');
    private static bool Identifier(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum
        && AsciiLetterOrDigit(value[0]) && value.All(c => AsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-');
    private static bool AsciiLetterOrDigit(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
    private static string String(JsonElement element, string field) => element.GetProperty(field).ValueKind == JsonValueKind.String
        ? element.GetProperty(field).GetString()! : throw new JsonException("Invalid native receipt field.");
    private static void RequireObject(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid native receipt object.");
        var names = element.EnumerateObject().Select(x => x.Name).ToArray();
        if (names.Length != expected.Length || names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || !names.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal)))
            throw new JsonException("Invalid native receipt fields.");
    }
    private static void RequireBinding(JsonElement row, NativeConsumerBinding expected)
    {
        RequireObject(row, "clientId", "applicationEnvironmentId", "logicalDeploymentId", "installationId");
        if (String(row, "clientId") != expected.ClientId || String(row, "applicationEnvironmentId") != expected.ApplicationEnvironmentId
            || String(row, "logicalDeploymentId") != expected.LogicalDeploymentId || String(row, "installationId") != expected.InstallationId)
            throw new JsonException("Invalid native binding receipt.");
    }
    private static void RequireConsumer(JsonElement row, NativeConsumerScope expected)
    {
        RequireObject(row, "deploymentId", "environment", "tenantId", "pharmacyId");
        if (String(row, "deploymentId") != expected.DeploymentId || String(row, "environment") != expected.Environment
            || String(row, "tenantId") != expected.TenantId || String(row, "pharmacyId") != expected.PharmacyId)
            throw new JsonException("Invalid native consumer scope receipt.");
    }
}
