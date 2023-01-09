
namespace OwnVehiclesVisualizer.BuildingManagerPatch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using CitiesExtensions;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Epic.OnlineServices.Presence;
    using static InfoManager;
    using UnityEngine;
    using ICities;

    [HarmonyPatch(typeof(BuildingManager))]
    internal static class BuildingManagerPatch
    {

        internal const int UPDATE_INTERVAL = 200;
        internal static int m_frame = 0;
        internal static int ID_BuildingSize = Shader.PropertyToID("_BuildingSize");

        [UsedImplicitly]
        [HarmonyPatch("SimulationStepImpl")]
        [HarmonyPostfix]
        internal static void PostSimulationStep(BuildingManager __instance)
        {
            m_frame++;
            if (m_frame == UPDATE_INTERVAL)
            {
                m_frame = 0;
                if (!OwnVehiclesVisualizer.ShowBuildingVehiclesPaths && (OwnVehiclesVisualizer.HighlightTargetBuildings || OwnVehiclesVisualizer.HighlightSourceBuildings || OwnVehiclesVisualizer.HighlightThirdTypeBuildings))
                    // don't update here if ShowBuildingVehiclePaths is true, let PathVisualizerPatch update.
                    OwnVehiclesVisualizer.Update();
            }
        }

        [UsedImplicitly]
        [HarmonyPatch("BeginOverlayImpl")]
        [HarmonyPostfix]
        internal static void BuildingManagerPostBeginOverlayImpl(BuildingManager __instance, RenderManager.CameraInfo cameraInfo, ref Material ___m_highlightMaterial, ref Mesh ___m_highlightMesh, ref Mesh ___m_highlightMesh2)
        {
            if (!OwnVehiclesVisualizer.HighlightTargetBuildings && !OwnVehiclesVisualizer.HighlightSourceBuildings)
                return;
            OwnVehiclesVisualizer.Enter();
            try
            {
                if (OwnVehiclesVisualizer.HighlightTargetBuildings)
                {
                    __instance.DrawHighlightMeshes(OwnVehiclesVisualizer.HighlightedTargetBuildings, OwnVehiclesVisualizer.TragetBuildingColor, ___m_highlightMaterial, ___m_highlightMesh, ___m_highlightMesh2);
                    __instance.DrawHighlightMeshesAtWorldPoses(OwnVehiclesVisualizer.HighlightedWorldPoses, OwnVehiclesVisualizer.TragetBuildingColor, ___m_highlightMaterial, ___m_highlightMesh2);
                }
                if (OwnVehiclesVisualizer.HighlightSourceBuildings)
                    __instance.DrawHighlightMeshes(OwnVehiclesVisualizer.HighlightedSourceBuildings, OwnVehiclesVisualizer.SourceBuildingColor, ___m_highlightMaterial, ___m_highlightMesh, ___m_highlightMesh2);
                if (OwnVehiclesVisualizer.HighlightThirdTypeBuildings)
                    __instance.DrawHighlightMeshes(OwnVehiclesVisualizer.HighlightedThirdTypeBuildings, OwnVehiclesVisualizer.ThirdTypeBuildingColor, ___m_highlightMaterial, ___m_highlightMesh, ___m_highlightMesh2);
            }
            finally
            {
                OwnVehiclesVisualizer.Exit();
            }
        }

        private static void DrawHighlightMeshesAtWorldPoses(this BuildingManager __instance, IEnumerable<Vector3> poses, Color color, Material material, Mesh circularMesh)
        {
            foreach (Vector3 pos in poses)
                DrawHighlightMeshAtWorldPos(material, circularMesh, pos, color);
        }

        private static void DrawHighlightMeshes(this BuildingManager __instance, IEnumerable<ushort> buildings, Color color, Material material, Mesh mesh, Mesh circularMesh)
        {
            foreach (ushort buildingId in buildings)
                DrawHighlightMesh(material, mesh, circularMesh, buildingId, color);
        }

        private static void DrawHighlightMeshAtWorldPos(Material material, Mesh mesh, Vector3 pos, Color color)
        {
            Vector4 size = new Vector4 { x = 5f, z = 5f, w = 1f };
            material.color = color;
            material.SetVector(BuildingManager.instance.ID_BuildingSize, size);
            if (material.SetPass(0))
            {
                BuildingManager.instance.m_drawCallData.m_overlayCalls++;
                Graphics.DrawMeshNow(mesh, pos, Quaternion.LookRotation(Vector3.forward));
            }
        }

        private static void DrawHighlightMesh(Material highlightMaterial, Mesh highlightMesh, Mesh highlightMeshCircular, ushort id, Color color, bool forceuseCircular = false)
        {
            Building data = Singleton<BuildingManager>.instance.m_buildings.m_buffer[id];
            BuildingInfo info = data.Info;
            highlightMaterial.color = color;

            data.CalculateMeshPosition(out Vector3 meshPos, out Quaternion meshRot);
            float width = (float)data.Width * 4f;
            float length = (float)data.Length * 4f;
            Vector3 min = Vector3.Max(info.m_generatedInfo.m_min, new Vector3(-width, 0f, -length));
            Vector3 max = Vector3.Min(info.m_generatedInfo.m_max, new Vector3(width, 0f, length));
            ushort subBuilding = data.m_subBuilding;
            if (subBuilding != 0)
            {
                Quaternion antiRot = Quaternion.Inverse(meshRot);
                while (subBuilding != 0)
                {
                    Building subData = Singleton<BuildingManager>.instance.m_buildings.m_buffer[subBuilding];
                    BuildingInfo subInfo = subData.Info;

                    subData.CalculateMeshPosition(out Vector3 subMeshPos, out Quaternion subMeshRot);
                    float subWidth = (float)subData.Width * 4f;
                    float subLength = (float)subData.Length * 4f;
                    Vector3 subMin = Vector3.Min(Vector3.Max(subInfo.m_generatedInfo.m_min, new Vector3(-subWidth, 0f, -subLength)), new Vector3(subWidth, 0f, subLength));
                    Vector3 subMax = Vector3.Max(Vector3.Min(subInfo.m_generatedInfo.m_max, new Vector3(subWidth, 0f, subLength)), new Vector3(-subWidth, 0f, -subLength));
                    Vector3 rhs1 = antiRot * (subMeshPos + subMeshRot * new Vector3(subMin.x, 0f, subMin.z) - meshPos);
                    Vector3 rhs2 = antiRot * (subMeshPos + subMeshRot * new Vector3(subMin.x, 0f, subMax.z) - meshPos);
                    Vector3 rhs3 = antiRot * (subMeshPos + subMeshRot * new Vector3(subMax.x, 0f, subMin.z) - meshPos);
                    Vector3 rhs4 = antiRot * (subMeshPos + subMeshRot * new Vector3(subMax.x, 0f, subMax.z) - meshPos);
                    min = Vector3.Min(min, rhs1);
                    min = Vector3.Min(min, rhs2);
                    min = Vector3.Min(min, rhs3);
                    min = Vector3.Min(min, rhs4);
                    max = Vector3.Max(max, rhs1);
                    max = Vector3.Max(max, rhs2);
                    max = Vector3.Max(max, rhs3);
                    max = Vector3.Max(max, rhs4);
                    subBuilding = subData.m_subBuilding;
                }
            }
            Vector4 buildingSize = max - min;
            if (info.m_circular)
            {
                buildingSize.x = Mathf.Max(buildingSize.x, buildingSize.z);
                buildingSize.z = buildingSize.x;
            }
            meshPos += (meshRot * ((max + min) * 0.5f)) with { y = 0f };
            //if (buildingSize.x * buildingSize.z < (float)(width * length) * 6f)
            //{
            //    meshPos = data.m_position;
            //    buildingSize = new Vector4((float)width * 8f, info.m_size.y, (float)length * 8f);
            //}
            buildingSize.w = ((!info.m_circular) ? 0f : 1f);
            highlightMaterial.SetVector(BuildingManager.instance.ID_BuildingSize, buildingSize);
            if (highlightMaterial.SetPass(0))
            {
                BuildingManager.instance.m_drawCallData.m_overlayCalls++;
                Graphics.DrawMeshNow(info.m_circular ? highlightMeshCircular : highlightMesh, meshPos, meshRot);
            }
        }

    }
}
