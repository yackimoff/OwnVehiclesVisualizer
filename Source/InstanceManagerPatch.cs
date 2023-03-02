namespace OwnVehiclesVisualizer.InstanceManagerPatch
{
    using System;

    using HarmonyLib;

    [HarmonyPatch(typeof(InstanceManager), nameof(InstanceManager.SelectInstance))]
    internal class InstanceManagerPatch
    {

        public static event Action<InstanceID> EventInstanceSelected;

        [HarmonyPatch(typeof(InstanceManager), nameof(InstanceManager.SelectInstance))]
        [HarmonyPostfix]
        internal static void InstanceManagerPostSelectInstance(InstanceManager __instance, ref InstanceID id, ref bool __result)
        {
            if (__result)
                EventInstanceSelected?.Invoke(id);
        }
    }
}
