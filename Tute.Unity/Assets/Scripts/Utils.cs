using System.Collections.Generic;

namespace Assets.Scripts
{
    internal static class Utils
    {
        public static readonly Dictionary<int, string> ColorByTeamIndex = new() { [0] = "red", [1] = "lightblue", [2] = "yellow" };


        public static string GetPlayerName(string playerName, int teamIndex) => $"<color={ColorByTeamIndex.GetValueOrDefault(teamIndex, "white")}>{playerName}</color>";
    }
}
