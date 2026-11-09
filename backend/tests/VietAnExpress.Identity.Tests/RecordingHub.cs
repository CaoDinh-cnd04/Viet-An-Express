using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using VietAnExpress.Identity.Support;

namespace VietAnExpress.Identity.Tests;

/// <summary>Hub SignalR giả: ghi lại tín hiệu Hỗ trợ &amp; góp ý gửi tới nhóm nào, mã gì — thay kết nối thật trong test.</summary>
internal sealed class RecordingHub : IHubContext<SupportLiveHub>
{
    private readonly RecordingClients clients;

    public RecordingHub() => clients = new RecordingClients(this);

    public List<(IReadOnlyList<string> Groups, string Event, object?[] Args)> Sent { get; } = [];

    public SupportLive Live => new(this, NullLogger<SupportLive>.Instance);

    public IHubClients Clients => clients;
    public IGroupManager Groups => throw new NotSupportedException();

    private sealed class RecordingClients(RecordingHub hub) : IHubClients
    {
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new Proxy(hub, groupNames);
        public IClientProxy Group(string groupName) => new Proxy(hub, [groupName]);
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class Proxy(RecordingHub hub, IReadOnlyList<string> groups) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            hub.Sent.Add((groups, method, args));
            return Task.CompletedTask;
        }
    }
}
