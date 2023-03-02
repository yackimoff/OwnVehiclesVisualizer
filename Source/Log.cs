namespace OwnVehiclesVisualizer
{
    internal static class Debug
    {
        public static void Log(string message)
        {
            UnityEngine.Debug.Log("[OVV] " + message);
        }

        public static void LogError(string message)
        {
            UnityEngine.Debug.LogError("[OVV] " + message);
        }

        public static void LogException(System.Exception exception)
        {
            Log("");
            UnityEngine.Debug.LogException(exception);
        }
    }
}
