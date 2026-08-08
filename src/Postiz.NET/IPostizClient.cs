using Postiz.Analytics;
using Postiz.Authentication;
using Postiz.Integrations;
using Postiz.Media;
using Postiz.Posts;

namespace Postiz;

public interface IPostizClient
{
    IPostizIntegrationsClient Integrations { get; }

    IPostizMediaClient Media { get; }

    IPostizPostsClient Posts { get; }

    IPostizAnalyticsClient Analytics { get; }

    IPostizGroupsClient Groups { get; }

    IPostizHealthClient Health { get; }
}
