using Postiz.Analytics;
using Postiz.Appliance;
using Postiz.Authentication;
using Postiz.Capabilities;
using Postiz.Chat;
using Postiz.Integrations;
using Postiz.Media;
using Postiz.Notifications;
using Postiz.Posts;
using Postiz.PrePublishRender;
using Postiz.Webhooks;

namespace Postiz;

public interface IPostizClient
{
    IPostizApplianceClient Appliance { get; }

    IPostizIntegrationsClient Integrations { get; }

    IPostizMediaClient Media { get; }

    IPostizChatClient Chat { get; }

    IPostizPostsClient Posts { get; }

    IPostizPrePublishRenderClient PrePublishRender { get; }

    IPostizAnalyticsClient Analytics { get; }

    IPostizGroupsClient Groups { get; }

    IPostizHealthClient Health { get; }

    IPostizCapabilitiesClient Capabilities { get; }

    IPostizNotificationsClient Notifications { get; }

    IPostizWebhooksClient Webhooks { get; }
}
