using Postiz.Analytics;
using Postiz.Authentication;
using Postiz.Capabilities;
using Postiz.Integrations;
using Postiz.Media;
using Postiz.Notifications;
using Postiz.Posts;
using Postiz.Transport;
using Postiz.Webhooks;

namespace Postiz;

public sealed class PostizClient : IPostizClient
{
    public PostizClient(HttpClient httpClient, PostizOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        httpClient.BaseAddress ??= options.BaseAddress;
        var transport = new PostizTransport(httpClient, options);

        Integrations = new PostizIntegrationsClient(transport);
        Media = new PostizMediaClient(transport);
        Posts = new PostizPostsClient(transport);
        Analytics = new PostizAnalyticsClient(transport);
        Groups = new PostizGroupsClient(transport);
        Health = new PostizHealthClient(transport);
        Capabilities = new PostizCapabilitiesClient(transport);
        Notifications = new PostizNotificationsClient(transport);
        Webhooks = new PostizWebhooksClient(transport);
    }

    public IPostizIntegrationsClient Integrations { get; }

    public IPostizMediaClient Media { get; }

    public IPostizPostsClient Posts { get; }

    public IPostizAnalyticsClient Analytics { get; }

    public IPostizGroupsClient Groups { get; }

    public IPostizHealthClient Health { get; }

    public IPostizCapabilitiesClient Capabilities { get; }

    public IPostizNotificationsClient Notifications { get; }

    public IPostizWebhooksClient Webhooks { get; }
}
