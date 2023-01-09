
namespace OwnVehiclesVisualizer.PathVisualizerPatch.ExtendedPaths
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Emit;
    using System.Runtime.CompilerServices;
    using System.Text;
    using ColossalFramework;
    using ColossalFramework.Math;
    using HarmonyLib;
    using UnityEngine;

    [HarmonyPatch]
    internal class ExtendedPathVisualizer
    {
        [HarmonyPatch(typeof(PathVisualizer), "RefreshPath")]
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var enumerator = instructions.GetEnumerator();
            var calculatePathSegmentCount = AccessTools.Method(typeof(TransportLine), nameof(TransportLine.CalculatePathSegmentCount));
            var fillPathSegments = AccessTools.Method(typeof(TransportLine), nameof(TransportLine.FillPathSegments));

            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;
                if (instruction.opcode == OpCodes.Call && instruction.operand == calculatePathSegmentCount)
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExtendedPathVisualizer), nameof(TransportLineCalculatePathSegmentCallCatch)));
                    continue;
                }
                else if (instruction.opcode == OpCodes.Call && instruction.operand == fillPathSegments)
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExtendedPathVisualizer), nameof(TransportLineFillPathSegmentsCallCatch)));
                    continue;
                }

                yield return instruction;
            }
        }

        internal static bool TransportLineCalculatePathSegmentCallCatch(
            uint path, int startIndex,
            NetInfo.LaneType laneTypes,
            VehicleInfo.VehicleType vehicleTypes,
            VehicleInfo.VehicleCategory vehicleCategories,
            ref TransportLine.TempUpdateMeshData[] data,
            ref int curveCount,
            ref float totalLength,
            ref Vector3 position)
        {
            if (vehicleTypes == VehicleInfo.VehicleType.Bicycle)
            {
                laneTypes |= NetInfo.LaneType.Vehicle | NetInfo.LaneType.Parking | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle;
                vehicleTypes |= VehicleInfo.VehicleType.All;
                vehicleCategories |= VehicleInfo.VehicleCategory.All;
            }
            return TransportLinePathSegmentCopy.CalculatePathSegmentCount(path, startIndex, laneTypes, vehicleTypes, vehicleCategories, ref data, ref curveCount, ref totalLength, ref position);
        }

        internal static void TransportLineFillPathSegmentsCallCatch(uint path, int startIndex, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, VehicleInfo.VehicleCategory vehicleCategories, ref TransportLine.TempUpdateMeshData[] data, Bezier3[] curves, Vector2[] curveOffsets, ref int curveIndex, ref float currentLength, float lengthScale, out Vector3 minPos, out Vector3 maxPos, bool ignoreY, bool useStopOffset)
        {
            if (vehicleTypes == VehicleInfo.VehicleType.Bicycle)
            {
                laneTypes |= NetInfo.LaneType.Vehicle | NetInfo.LaneType.Parking | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle;
                vehicleTypes |= VehicleInfo.VehicleType.All;
                vehicleCategories |= VehicleInfo.VehicleCategory.All;
            }
            TransportLinePathSegmentCopy.FillPathSegments(path, startIndex, laneTypes, vehicleTypes, vehicleCategories, ref data, curves, curveOffsets, ref curveIndex, ref currentLength, lengthScale, out minPos, out maxPos, ignoreY, useStopOffset);
        }
    }
    internal static class TransportLinePathSegmentCopy
    {
        // TransportLine
        public static bool CalculatePathSegmentCount(uint path, int startIndex, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, VehicleInfo.VehicleCategory vehicleCategories, ref TransportLine.TempUpdateMeshData[] data, ref int curveCount, ref float totalLength, ref Vector3 position)
        {
            bool flag = true;
            Vector3 vector = Vector3.zero;
            PathManager pathMan = Singleton<PathManager>.instance;
            NetManager netMan = Singleton<NetManager>.instance;
            TerrainManager terMan = Singleton<TerrainManager>.instance;
            int num = 0;
            while (path != 0U)
            {
                uint nextPathUnit = pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)path)].m_nextPathUnit;
                int positionCount = (int)pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)path)].m_positionCount;
                for (int i = startIndex; i < positionCount; i++)
                {
                    PathUnit.Position pathPos;
                    if (!pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)path)].GetPosition(i, out pathPos))
                    {
                        return false;
                    }
                    NetInfo info = netMan.m_segments.m_buffer[(int)pathPos.m_segment].Info;
                    if (info.m_netAI is TransportLineAI)
                    {
                        var path2 = netMan.m_segments.m_buffer[(int)pathPos.m_segment].m_path;
                        while (path2 != 0)
                        {
                            var nextPathUnit2 = pathMan.m_pathUnits.m_buffer[path2].m_nextPathUnit;
                            var positionCount2 = pathMan.m_pathUnits.m_buffer[path2].m_positionCount;
                            for (int j = 0; j < positionCount2; j++)
                            {
                                PathUnit.Position pathPos2;
                                if (!pathMan.m_pathUnits.m_buffer[path2].GetPosition(j, out pathPos2))
                                    return false;
                                NetInfo info2 = netMan.m_segments.m_buffer[(int)pathPos2.m_segment].Info;
                                //if (!info2.m_lanes[(int)pathPos.m_lane].CheckType(laneTypes, vehicleTypes, vehicleCategories))
                                //{
                                //    return true;
                                //}
                                if (!CountPaths(data, ref curveCount, ref totalLength, ref flag, ref vector, netMan, terMan, pathPos2, ref position))
                                    return true;
                            }
                            path2 = nextPathUnit2;
                        }
                        continue;
                    }
                    //if (!info.m_lanes[(int)pathPos.m_lane].CheckType(laneTypes, vehicleTypes, vehicleCategories))
                    //{
                    //    return true;
                    //}
                    if (!CountPaths(data, ref curveCount, ref totalLength, ref flag, ref vector, netMan, terMan, pathPos, ref position))
                        return true;
                }
                path = nextPathUnit;
                startIndex = 0;
                if (++num >= 262144)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
            return true;

            static bool CountPaths(TransportLine.TempUpdateMeshData[] data, ref int curveCount, ref float totalLength, ref bool flag, ref Vector3 vector, NetManager netMan, TerrainManager terMan, PathUnit.Position pathPos, ref Vector3 position)
            {
                NetInfo info = netMan.m_segments.m_buffer[(int)pathPos.m_segment].Info;
                if (info == null || info.m_lanes == null || info.m_lanes.Length <= (int)pathPos.m_lane)
                {
                    return true;
                }
                uint laneID = PathManager.GetLaneID(pathPos);
                Vector3 vector2 = netMan.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePosition((float)pathPos.m_offset * 0.003921569f);
                position = vector2;
                if (flag)
                {
                    vector = vector2;
                    flag = false;
                }
                else
                {
                    Vector3 vector3;
                    float num2;
                    netMan.m_lanes.m_buffer[(int)((UIntPtr)laneID)].GetClosestPosition(vector, out vector3, out num2);
                    float num3 = Vector3.Distance(vector3, vector);
                    float num4 = Vector3.Distance(vector2, vector3);
                    if (num3 > 1f)
                    {
                        int num5 = 0;
                        if (data.Length > 1)
                        {
                            num5 = terMan.GetPatchIndex((vector + vector3) * 0.5f);
                        }
                        TransportLine.TempUpdateMeshData[] array = data;
                        int num6 = num5;
                        array[num6].m_pathSegmentCount = array[num6].m_pathSegmentCount + 1;
                        curveCount++;
                        totalLength += num3;
                        vector = vector3;
                    }
                    if (num4 > 1f)
                    {
                        int num7 = 0;
                        if (data.Length > 1)
                        {
                            num7 = terMan.GetPatchIndex((vector + vector2) * 0.5f);
                        }
                        TransportLine.TempUpdateMeshData[] array2 = data;
                        int num8 = num7;
                        array2[num8].m_pathSegmentCount = array2[num8].m_pathSegmentCount + 1;
                        curveCount++;
                        totalLength += num4;
                        vector = vector2;
                    }
                }

                return true;
            }
        }

        // TransportLine
        public static void FillPathSegments(uint path, int startIndex, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, VehicleInfo.VehicleCategory vehicleCategories, ref TransportLine.TempUpdateMeshData[] data, Bezier3[] curves, Vector2[] curveOffsets, ref int curveIndex, ref float currentLength, float lengthScale, out Vector3 minPos, out Vector3 maxPos, bool ignoreY, bool useStopOffset)
        {
            bool flag = true;
            bool flag2 = true;
            bool flag3 = false;
            PathUnit.Position position = default(PathUnit.Position);
            Vector3 vector = Vector3.zero;
            Vector3 vector2 = Vector3.zero;
            minPos = new Vector3(100000f, 100000f, 100000f);
            maxPos = new Vector3(-100000f, -100000f, -100000f);
            PathManager pathMan = Singleton<PathManager>.instance;
            NetManager netMan = Singleton<NetManager>.instance;
            TerrainManager instance3 = Singleton<TerrainManager>.instance;
            int num = 0;
            while (path != 0U)
            {
                uint nextPathUnit = pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)path)].m_nextPathUnit;
                int positionCount = (int)pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)path)].m_positionCount;
                for (int i = startIndex; i < positionCount; i++)
                {
                    bool flag4 = nextPathUnit == 0U && i == positionCount - 1;
                    PathUnit.Position pathPos;
                    if (!pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)path)].GetPosition(i, out pathPos))
                    {
                        return;
                    }
                    NetInfo info = netMan.m_segments.m_buffer[(int)pathPos.m_segment].Info;
                    if (info.m_netAI is TransportLineAI)
                    {
                        var path2 = netMan.m_segments.m_buffer[(int)pathPos.m_segment].m_path;
                        while (path2 != 0)
                        {
                            var nextPathUnit2 = pathMan.m_pathUnits.m_buffer[path2].m_nextPathUnit;
                            var positionCount2 = pathMan.m_pathUnits.m_buffer[path2].m_positionCount;
                            for (int j = 0; j < positionCount2; j++)
                            {
                                PathUnit.Position pathPos2;
                                if (!pathMan.m_pathUnits.m_buffer[path2].GetPosition(j, out pathPos2))
                                    return;
                                //NetInfo info2 = netMan.m_segments.m_buffer[(int)pathPos2.m_segment].Info;
                                //if (!info2.m_lanes[(int)pathPos2.m_lane].CheckType(laneTypes, vehicleTypes, vehicleCategories))
                                //{
                                //    return;
                                //}
                                WorkPathPos(data, curves, curveOffsets, ref curveIndex, ref currentLength, lengthScale, ref minPos, ref maxPos, ignoreY, useStopOffset, ref flag, ref flag2, ref flag3, ref position, ref vector, ref vector2, netMan, instance3, flag4, pathPos2);
                            }
                            path2 = nextPathUnit2;
                        }
                        continue;
                    }
                    //if (!info.m_lanes[(int)pathPos.m_lane].CheckType(laneTypes, vehicleTypes, vehicleCategories))
                    //{
                    //    return;
                    //}
                    WorkPathPos(data, curves, curveOffsets, ref curveIndex, ref currentLength, lengthScale, ref minPos, ref maxPos, ignoreY, useStopOffset, ref flag, ref flag2, ref flag3, ref position, ref vector, ref vector2, netMan, instance3, flag4, pathPos);
                }
                path = nextPathUnit;
                startIndex = 0;
                if (++num >= 262144)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }

            static void WorkPathPos(TransportLine.TempUpdateMeshData[] data, Bezier3[] curves, Vector2[] curveOffsets, ref int curveIndex, ref float currentLength, float lengthScale, ref Vector3 minPos, ref Vector3 maxPos, bool ignoreY, bool useStopOffset, ref bool flag, ref bool flag2, ref bool flag3, ref PathUnit.Position position, ref Vector3 vector, ref Vector3 vector2, NetManager netMan, TerrainManager instance3, bool flag4, PathUnit.Position pathPos)
            {
                NetInfo info = netMan.m_segments.m_buffer[(int)pathPos.m_segment].Info;
                if (info == null || info.m_lanes == null || info.m_lanes.Length <= (int)pathPos.m_lane)
                {
                    return;
                }
                NetInfo.LaneType laneType = info.m_lanes[(int)pathPos.m_lane].m_laneType;
                VehicleInfo.VehicleType vehicleType = info.m_lanes[(int)pathPos.m_lane].m_vehicleType;
                bool flag5 = laneType == NetInfo.LaneType.Pedestrian || (laneType == NetInfo.LaneType.Vehicle && vehicleType == VehicleInfo.VehicleType.Bicycle);
                uint laneID = PathManager.GetLaneID(pathPos);
                float num2 = (float)pathPos.m_offset * 0.003921569f;
                Vector3 vector3;
                Vector3 vector4;
                netMan.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection(num2, out vector3, out vector4);
                minPos = Vector3.Min(minPos, vector3 - new Vector3(4f, 4f, 4f));
                maxPos = Vector3.Max(maxPos, vector3 + new Vector3(4f, 4f, 4f));
                if (flag)
                {
                    vector = vector3;
                    vector2 = vector4;
                    flag = false;
                }
                else
                {
                    Vector3 vector5;
                    float num3;
                    netMan.m_lanes.m_buffer[(int)((UIntPtr)laneID)].GetClosestPosition(vector, out vector5, out num3);
                    Vector3 vector6 = netMan.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculateDirection(num3);
                    minPos = Vector3.Min(minPos, vector5 - new Vector3(4f, 4f, 4f));
                    maxPos = Vector3.Max(maxPos, vector5 + new Vector3(4f, 4f, 4f));
                    float num4 = Vector3.Distance(vector5, vector);
                    float num5 = Vector3.Distance(vector3, vector5);
                    if (num5 > 1f)
                    {
                        if (num2 < num3)
                        {
                            vector6 = -vector6;
                            vector4 = -vector4;
                        }
                    }
                    else if (num2 > 0.5f)
                    {
                        vector6 = -vector6;
                        vector4 = -vector4;
                    }
                    if (num4 > 1f)
                    {
                        ushort startNode = netMan.m_segments.m_buffer[(int)pathPos.m_segment].m_startNode;
                        ushort endNode = netMan.m_segments.m_buffer[(int)pathPos.m_segment].m_endNode;
                        ushort startNode2 = netMan.m_segments.m_buffer[(int)position.m_segment].m_startNode;
                        ushort endNode2 = netMan.m_segments.m_buffer[(int)position.m_segment].m_endNode;
                        bool flag6 = startNode != startNode2 && startNode != endNode2 && endNode != startNode2 && endNode != endNode2;
                        int num6 = 0;
                        if (data.Length > 1)
                        {
                            num6 = instance3.GetPatchIndex((vector + vector5) * 0.5f);
                        }
                        float num7 = currentLength + num4;
                        Bezier3 bezier = default(Bezier3);
                        if (((flag5 || flag3) && position.m_segment == pathPos.m_segment) || flag6)
                        {
                            Vector3 b = VectorUtils.NormalizeXZ(vector5 - vector);
                            bezier.a = vector - b;
                            bezier.b = vector * 0.7f + vector5 * 0.3f;
                            bezier.c = vector5 * 0.7f + vector * 0.3f;
                            bezier.d = vector5 + b;
                        }
                        else
                        {
                            bezier.a = vector;
                            bezier.b = vector + vector2.normalized * (num4 * 0.5f);
                            bezier.c = vector5 - vector6.normalized * (num4 * 0.5f);
                            bezier.d = vector5;
                        }
                        TransportLine.FillPathSegment(bezier, data[num6].m_meshData, curves, data[num6].m_pathSegmentIndex, curveIndex, currentLength * lengthScale, num7 * lengthScale, 4f, (!ignoreY) ? 5f : 20f, ignoreY);
                        if (curveOffsets != null)
                        {
                            curveOffsets[curveIndex] = new Vector2(currentLength * lengthScale, num7 * lengthScale);
                        }
                        TransportLine.TempUpdateMeshData[] array = data;
                        int num8 = num6;
                        array[num8].m_pathSegmentIndex = array[num8].m_pathSegmentIndex + 1;
                        curveIndex++;
                        currentLength = num7;
                        vector = vector5;
                        vector2 = vector6;
                        flag2 = false;
                    }
                    if (num5 > 1f)
                    {
                        int num9 = 0;
                        if (data.Length > 1)
                        {
                            num9 = instance3.GetPatchIndex((vector + vector3) * 0.5f);
                        }
                        float num10 = currentLength + num5;
                        Bezier3 subCurve = netMan.m_lanes.m_buffer[(int)((UIntPtr)laneID)].GetSubCurve(num3, num2);
                        if ((flag2 || flag4) && useStopOffset)
                        {
                            float num11 = netMan.m_segments.m_buffer[(int)pathPos.m_segment].Info.m_lanes[(int)pathPos.m_lane].m_stopOffset;
                            if ((netMan.m_segments.m_buffer[(int)pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                            {
                                num11 = -num11;
                            }
                            if (num2 < num3)
                            {
                                num11 = -num11;
                            }
                            if (flag2)
                            {
                                subCurve.a += Vector3.Cross(Vector3.up, vector6).normalized * num11;
                            }
                            if (flag4)
                            {
                                subCurve.d += Vector3.Cross(Vector3.up, vector4).normalized * num11;
                            }
                        }
                        TransportLine.FillPathSegment(subCurve, data[num9].m_meshData, curves, data[num9].m_pathSegmentIndex, curveIndex, currentLength * lengthScale, num10 * lengthScale, 4f, (!ignoreY) ? 5f : 20f, ignoreY);
                        if (curveOffsets != null)
                        {
                            curveOffsets[curveIndex] = new Vector2(currentLength * lengthScale, num10 * lengthScale);
                        }
                        TransportLine.TempUpdateMeshData[] array2 = data;
                        int num12 = num9;
                        array2[num12].m_pathSegmentIndex = array2[num12].m_pathSegmentIndex + 1;
                        curveIndex++;
                        currentLength = num10;
                        vector = vector3;
                        vector2 = vector4;
                        flag2 = false;
                    }
                }
                flag3 = flag5;
                position = pathPos;
            }
        }


    }


}
