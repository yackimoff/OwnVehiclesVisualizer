namespace OwnVehiclesVisualizer.InstanceManagerPatch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using HarmonyLib;
    using UnityEngine;

    [HarmonyPatch(typeof(InstanceManager), nameof(InstanceManager.SelectInstance))]
    internal class InstanceManagerPatch
    {

        public static event Action<InstanceID> InstanceSelected;

        [HarmonyPatch(typeof(InstanceManager), nameof(InstanceManager.SelectInstance))]
        [HarmonyPostfix]
        internal static void InstanceManagerPostSelectInstance(InstanceManager __instance, ref InstanceID id, ref bool __result)
        {
            if (__result)
                InstanceSelected?.Invoke(id);
        }
    }
}
