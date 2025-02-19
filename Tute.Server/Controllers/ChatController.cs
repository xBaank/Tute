using MagicOnion.Server.Hubs;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Controllers;

internal class ChatController(IGroup<IGamingHubReceiver> room, Player self)
{
    private readonly List<(Player sender, string message)> messages = [];

    public ValueTask SendMessage(string message)
    {
        var max = Math.Min(message.Length, 100);
        var trimmed = message.Trim()[..max];
        if (string.IsNullOrWhiteSpace(trimmed)) return ValueTask.CompletedTask;
        messages.Add((self, trimmed));
        room.All.OnMessage(trimmed, self);
        return ValueTask.CompletedTask;
    }
}
