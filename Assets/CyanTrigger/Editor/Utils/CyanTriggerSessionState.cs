using UnityEditor;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerSessionState
    {
        private const string SessionKeyPrefix = "com.cyan.cyantrigger.session";

        public const string ShowUtilityActions = "show_utility_actions";

        private static string GetSessionKey(string key)
        {
            return $"{SessionKeyPrefix}.{key}";
        }
        
        public static bool GetBool(string key)
        {
            return SessionState.GetBool(GetSessionKey(key), false);
        }

        public static void SetBool(string key, bool value)
        {
            SessionState.SetBool(GetSessionKey(key), value);
        }
    }
}