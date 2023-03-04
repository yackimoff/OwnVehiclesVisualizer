
namespace OwnVehiclesVisualizer.PathVisualizerPatch
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;

    using CitiesExtensions;

    using ColossalFramework;
    using ColossalFramework.Math;
    using ColossalFramework.UI;

    using HarmonyLib;

    using JetBrains.Annotations;

    using UnityEngine;

    [HarmonyPatch(typeof(PathVisualizer))]
    internal static class PathVisualizerPatch
    {
        private static readonly FieldInfo mPathsField = AccessTools.Field(typeof(PathVisualizer), "m_paths");
        private static readonly FieldInfo mLastInstance = AccessTools.Field(typeof(PathVisualizer), "m_lastInstance");

        private static int refreshFrame = 0;
        private static bool enabled = false;
        private static bool refreshNeeded = false;

        public static bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                if (value)
                {
                    enabled = value;
                    refreshNeeded = true;
                    refreshFrame = 0;
                } else
                {
                    enabled = false;
                    refreshFrame = 0;
                    PathVisualizer pathVisualizer = NetManager.instance.PathVisualizer;
                    //Dictionary<InstanceID, PathVisualizer.Path> mpaths = (Dictionary<InstanceID, PathVisualizer.Path>)mPathsField.GetValue(pathVisualizer);
                    //try
                    //{
                    //    // Try to desperatly release paths including refreshRequired flag because vanilla mode builds paths for visualization differently.
                    //    while (!Monitor.TryEnter(mpaths, SimulationManager.SYNCHRONIZE_TIMEOUT)) { };
                    //    try
                    //    {
                    //        foreach (KeyValuePair<InstanceID, PathVisualizer.Path> kv in mpaths)
                    //        {
                    //            kv.Value.m_stillNeeded = false;
                    //            kv.Value.m_canRelease = true;
                    //            kv.Value.m_refreshRequired = true;
                    //        }
                    //    } finally
                    //    {
                    //        Monitor.Exit(mpaths);
                    //    }
                    //} catch
                    //{
                    //    // Go with UpdateData if failed.
                    //    pathVisualizer.UpdateData();
                    //}
                    pathVisualizer.PathsVisible = InfoManager.instance is { CurrentMode: InfoManager.InfoMode.TrafficRoutes, CurrentSubMode: InfoManager.SubInfoMode.Paths };
                    mLastInstance.SetValue(pathVisualizer, InstanceID.Empty);
                }
            }
        }
        public static bool UseAlpha { get; set; } = true;

        #region Patches

        [UsedImplicitly]
        [HarmonyPrefix]
        [HarmonyPatch("SimulationStep")]
        internal static bool PathVisualizerPreSimulationStep(PathVisualizer __instance, FastList<PathVisualizer.Path> ___m_stepPaths)
        {
            if (!Enabled)
                return true;

            NetManager.instance.PathVisualizer.PathsVisible = true;
            if (refreshNeeded && OwnVehiclesVisualizer.TryEnter())
            {
                try
                {
                    OwnVehiclesVisualizer.Update();
                    refreshNeeded = false;
                    refreshFrame = 0;
                    __instance.AddInstances(OwnVehiclesVisualizer.HighlightedPaths);
                } finally
                {
                    OwnVehiclesVisualizer.Exit();
                }
            }
            __instance.StepPaths(___m_stepPaths);
            return false;
        }

        [UsedImplicitly]
        [HarmonyPostfix]
        [HarmonyPatch("SimulationStep")]
        internal static void PathVisualizerPostSimulationStep()
        {
            if (Enabled && !refreshNeeded)
            {
                refreshFrame++;
                if (refreshFrame % 0x100 == 0)
                    refreshNeeded = true;
            }
        }

        [UsedImplicitly]
        [HarmonyPostfix]
        [HarmonyPatch("UpdateMesh")]
        internal static void PathVisualizerPostUpdateMesh(ref PathVisualizer.Path path, ref FastList<PathVisualizer.Path> ___m_stepPaths)
        {
            if (Enabled && UseAlpha && ___m_stepPaths.m_size > 1)
            {
                float r = new Randomizer(path.m_id.RawData).UInt32(25) / 25.0f;
                bool isGoingBack = path.m_id is { Type: InstanceType.Vehicle, Vehicle: var vehicleId }
                    && vehicleId.ToVehicle() is Vehicle vehicle
                    && (vehicle.m_flags.IsFlagSet(Vehicle.Flags.GoingBack)
                        || vehicle.Info.GetAI() is TaxiAI taxi
                            && taxi.GetPassengerInstance(path.m_id.Vehicle, ref vehicle) == 0);
                path.m_color = Color.Lerp(
                        path.m_color.linear,
                        (InfoManager.instance.CurrentMode == InfoManager.InfoMode.None ? Color.white : InfoManager.instance.m_properties.m_neutralColor).linear,
                        isGoingBack ? 1 - r : r)
                    .gamma
                    with
                { a = 0.45f };
            }
        }

        #endregion

        private static void AddInstances(this PathVisualizer __instance, IEnumerable<InstanceID> instances)
        {
            __instance.PreAddInstances();
            try
            {
                foreach (InstanceID instanceID in instances) __instance.AddInstance(instanceID);
            } finally
            {
                __instance.PostAddInstances();
            }
        }

        private static void StepPaths(this PathVisualizer __instance, FastList<PathVisualizer.Path> m_paths)
        {
            for (int i = 0; i < m_paths.m_size; ++i)
                __instance.StepPath(m_paths[i]);
        }

        #region Reverse patches

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch("PreAddInstances")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Reverse patch")]
        private static void PreAddInstances(this PathVisualizer __instance) => Debug.LogError("[PathVisualizerReversePatch]:PreAddInstances() Redirection failed!");

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch("PostAddInstances")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Reverse patch")]
        private static void PostAddInstances(this PathVisualizer __instance) => Debug.LogError("[PathVisualizerReversePatch]:PostAddInstances() Redirection failed!");

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch("AddInstance")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Reverse patch")]
        private static void AddInstance(this PathVisualizer __instance, InstanceID id) => Debug.LogError("[PathVisualizerReversePatch]:AddInstance() Redirection failed!");

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch("StepPath")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Reverse patch")]
        private static void StepPath(this PathVisualizer __instance, PathVisualizer.Path path) => Debug.LogError("[PathVisualizerReversePatch]:StepPath() Redirection failed!");

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(TaxiAI), "GetPassengerInstance")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Reverse patch")]
        private static ushort GetPassengerInstance(this TaxiAI __instance, ushort vehicleID, ref Vehicle data)
        {
            Debug.LogError("[PathVisualizerReversePatch]:GetPassengerInstance() Redirection failed!");
            return 0;
        }

        #endregion

    }
}
