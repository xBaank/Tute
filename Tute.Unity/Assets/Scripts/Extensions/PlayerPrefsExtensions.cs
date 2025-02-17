using UnityEngine;

namespace Assets.Scripts.Extensions
{
    internal static class PlayerPrefsUtils
    {
        public static string GetOrSetString(string key, string value)
        {
            var valueStored = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(valueStored))
            {
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
                return value;
            }
            return valueStored;
        }
    }
}
