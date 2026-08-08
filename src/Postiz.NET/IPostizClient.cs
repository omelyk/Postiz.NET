using Postiz.Analytics;
using Postiz.Authentication;
using Postiz.Capabilities;
using Postiz.Integrations;
using Postiz.Media;
using Postiz.Notifications;
using Postiz.Posts;
using Postiz.Webhooks;

namespace Postiz;

public interface IPostizClient
{
    IPostizIntegrationsClient Integrations { get; }

    IPostizMediaClient Media { get; }

    IPostizPostsClient Posts { get; }

    IPostizAnalyticsClient Analytics { get; }

    IPostizGroupsClient Groups { get; }

    IPostizHealthClient Health { get; }

    IPostizCapabilitiesClient Capabilities { get; }

    IPostizNotificationsClient Notifications { get; }

    IPostizWebhooksClient Webhooks { get; }
}
