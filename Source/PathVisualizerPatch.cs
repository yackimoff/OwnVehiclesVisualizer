
namespace OwnVehiclesVisualizer.PathVisualizerPatch
{
    using ColossalFramework;
    using ColossalFramework.Math;
    using ColossalFramework.UI;
    using Epic.OnlineServices.Presence;
    using HarmonyLib;
    using JetBrains.Annotations;
    using System;
    using System.CodeDom;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using System.Threading;
    using UnityEngine;

    [HarmonyPatch]
    internal static class PathVisualizerPatch
    {

        internal const int UPDATE_INTERVAL = 200;

        internal static int m_frame = 0;

        [UsedImplicitly]
        [HarmonyPatch(typeof(PathVisualizer), "SimulationStep")]
        [HarmonyPrefix]
        public static bool PathVisualizerPreSimulationStep(PathVisualizer __instance, ref bool ___m_pathsVisible, FastList<PathVisualizer.Path> ___m_stepPaths, ref int ___m_pathRefreshFrame)
        {
            if (!OwnVehiclesVisualizer.ShowBuildingVehiclesPaths)
                return true;
            ___m_pathsVisible = true;
            if (m_frame == 0)
            {
                OwnVehiclesVisualizer.Enter();
                try
                {
                    __instance.AddInstances(OwnVehiclesVisualizer.HighlightedPaths);
                }
                finally
                {
                    OwnVehiclesVisualizer.Exit();
                }
            }
            __instance.StepPaths(___m_stepPaths);
            return false;
        }

        [UsedImplicitly]
        [HarmonyPatch(typeof(PathVisualizer), "SimulationStep")]
        [HarmonyPostfix]
        public static void PathVisualizerPostSimulationStep()
        {
            m_frame++;
            if (m_frame == UPDATE_INTERVAL)
            {
                m_frame = 0;
                if (OwnVehiclesVisualizer.ShowBuildingVehiclesPaths)
                    OwnVehiclesVisualizer.Update();
            }
        }



        [UsedImplicitly]
        [HarmonyPatch(typeof(PathVisualizer), "UpdateMesh")]
        [HarmonyPostfix]
        internal static void PathVisualizerPostUpdateMesh(PathVisualizer __instance, ref PathVisualizer.Path path)
        {
            if (OwnVehiclesVisualizer.ShowBuildingVehiclesPaths)
            {
                float r = new Randomizer(path.m_id.Vehicle).UInt32(25) / 25.0f;
                bool returningToFacility = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[path.m_id.Vehicle] is Vehicle vehicle &&
                    (vehicle.m_flags.IsFlagSet(Vehicle.Flags.GoingBack) || vehicle.Info.GetAI() is TaxiAI taxi && taxi.GetPassengerInstance(path.m_id.Vehicle, ref vehicle) == 0);
                r = returningToFacility ? 1 - r / 3f : r / 3f;
                path.m_color = Color.Lerp(Singleton<InfoManager>.instance.CurrentMode == InfoManager.InfoMode.None ? Color.white.linear : Singleton<InfoManager>.instance.m_properties.m_neutralColor.linear, path.m_color.linear, r).gamma with { a = 0.45f };
            }
        }
    }


    [HarmonyPatch]
    internal static class PathVisualizerAccess
    {

        public static void AddInstances(this PathVisualizer __instance, IEnumerable<InstanceID> instances)
        {
            __instance.PreAddInstances();
            try
            {
                instances.ForEach((instanceID) => __instance.AddInstance(instanceID));
            } finally
            {
                __instance.PostAddInstances();
            }
        }

        public static void StepPaths(this PathVisualizer __instance, FastList<PathVisualizer.Path> m_paths)
        {
            for (int i = 0; i < m_paths.m_size; ++i)
            {
                __instance.StepPath(m_paths[i]);
            }

        }

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PathVisualizer), "PreAddInstances")]
        internal static void PreAddInstances(this PathVisualizer __instance)
        {
            Debug.LogError("[PathVisualizerReversePatch]:PreAddInstances() Redirection failed!");
        }

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PathVisualizer), "PostAddInstances")]
        internal static void PostAddInstances(this PathVisualizer __instance)
        {
            Debug.LogError("[PathVisualizerReversePatch]:PostAddInstances() Redirection failed!");
        }

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PathVisualizer), "AddInstance")]
        internal static void AddInstance(this PathVisualizer __instance, InstanceID id)
        {
            Debug.LogError("[PathVisualizerReversePatch]:AddInstance() Redirection failed!");
        }

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PathVisualizer), "StepPath")]
        internal static void StepPath(this PathVisualizer __instance, PathVisualizer.Path path)
        {
            Debug.LogError("[PathVisualizerReversePatch]:StepPath() Redirection failed!");
        }

        [UsedImplicitly]
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(TaxiAI), "GetPassengerInstance")]
        internal static ushort GetPassengerInstance(this TaxiAI __instance, ushort vehicleID, ref Vehicle data)
        {
            Debug.LogError("[PathVisualizerReversePatch]:StepPath() Redirection failed!");
            return 0;
        }

    }
}
