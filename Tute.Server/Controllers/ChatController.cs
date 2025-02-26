using MagicOnion.Server.Hubs;
using Tute.Server.Services;
using Tute.Shared.GamingHub;
using Tute.Shared.Models;

namespace Tute.Server.Controllers;

internal class ChatController(IGroup<IGamingHubReceiver> room, GameRoom gameRoom, Player self)
{

    public ValueTask SendMessage(string message)
    {
        var max = Math.Min(message.Length, 100);
        var trimmed = message.Trim()[..max];
        if (string.IsNullOrWhiteSpace(trimmed)) return ValueTask.CompletedTask;
        room.All.OnMessage(trimmed, self, gameRoom.PlayerDataByConnetion.GetValueOrDefault(self.ConnectionId)?.TeamIndex ?? 0);
        return ValueTask.CompletedTask;
    }
}
