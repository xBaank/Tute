using System.Collections.Generic;
using Tute.Shared.Models;

namespace Assets.Scripts
{
    public class GameRoom
    {
        public GameRoom(string roomName, List<Player> players, Player player)
        {
            RoomName = roomName;
            Players = players;
            Player = player;
        }

        public string RoomName { get; }
        public List<Player> Players { get; }
        public Player Player { get; }
    }
}
