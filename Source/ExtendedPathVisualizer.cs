namespace OwnVehiclesVisualizer.PathVisualizerPatch.ExtendedPaths
{
    using System;
    using System.Collections.Generic;

    using CitiesExtensions;

    using ColossalFramework.Math;

    using HarmonyLib;

    using UnityEngine;

    [HarmonyPatch]
    internal class ExtendedPathVisualizer
    {
        [HarmonyPatch(typeof(PathVisualizer), "RefreshPath")]
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> RefreshPathTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions
                .MethodReplacer(
                    AccessTools.Method(typeof(TransportLine), nameof(TransportLine.CalculatePathSegmentCount)),
                    AccessTools.Method(typeof(ExtendedPathVisualizer), nameof(TransportLineCalculatePathSegmentCallCatch)))
                .MethodReplacer(
                    AccessTools.Method(typeof(TransportLine), nameof(TransportLine.FillPathSegments)),
                    AccessTools.Method(typeof(ExtendedPathVisualizer), nameof(TransportLineFillPathSegmentsCallCatch)));
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
            if (true || PathVisualizerPatch.Enabled)
            {
                laneTypes = NetInfo.LaneType.None;
                vehicleTypes = VehicleInfo.VehicleType.None;
                vehicleCategories = VehicleInfo.VehicleCategory.None;
                return NewCalculatePathSegmentCount(path, startIndex, laneTypes, vehicleTypes, vehicleCategories, ref data, ref curveCount, ref totalLength, ref position);
            }
            return TransportLine.CalculatePathSegmentCount(path, startIndex, laneTypes, vehicleTypes, vehicleCategories, ref data, ref curveCount, ref totalLength, ref position);
        }

        internal static void TransportLineFillPathSegmentsCallCatch(uint path, int startIndex, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, VehicleInfo.VehicleCategory vehicleCategories, ref TransportLine.TempUpdateMeshData[] data, Bezier3[] curves, Vector2[] curveOffsets, ref int curveIndex, ref float currentLength, float lengthScale, out Vector3 minPos, out Vector3 maxPos, bool ignoreY, bool useStopOffset)
        {
            if (true || PathVisualizerPatch.Enabled)
            {
                laneTypes = NetInfo.LaneType.None;
                vehicleTypes = VehicleInfo.VehicleType.None;
                vehicleCategories = VehicleInfo.VehicleCategory.None;
                NewFillPathSegments(path, startIndex, laneTypes, vehicleTypes, vehicleCategories, ref data, curves, curveOffsets, ref curveIndex, ref currentLength, lengthScale, out minPos, out maxPos, ignoreY);
                return;
            }
            TransportLine.FillPathSegments(path, startIndex, laneTypes, vehicleTypes, vehicleCategories, ref data, curves, curveOffsets, ref curveIndex, ref currentLength, lengthScale, out minPos, out maxPos, ignoreY, useStopOffset);
        }


        public static bool NewCalculatePathSegmentCount(uint path, int startIndex, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, VehicleInfo.VehicleCategory vehicleCategories, ref TransportLine.TempUpdateMeshData[] data, ref int curveCount, ref float totalLength, ref Vector3 position)
        {
            bool isFirstPass = true;
            Vector3 prevPos = Vector3.zero;
            try
            {
                foreach (PathUnit.Position pathPos in path.EnumeratePositions(startIndex, true, true))
                {
                    NetInfo info = pathPos.m_segment.ToSegment().Info;
                    if (info == null || info.m_lanes == null || info.m_lanes.Length <= (int)pathPos.m_lane)
                        return true;
                    if (!info.m_lanes[(int)pathPos.m_lane].CheckType(laneTypes, vehicleTypes, vehicleCategories))
                        return true;

                    uint laneID = PathManager.GetLaneID(pathPos);
                    Vector3 curPos = laneID.ToLane().CalculatePosition((float)pathPos.m_offset / 255f);
                    position = curPos;

                    if (isFirstPass)
                    {
                        prevPos = curPos;
                        isFirstPass = false;
                        continue;
                    }

                    laneID.ToLane().GetClosestPosition(prevPos, out Vector3 segmentPos, out float laneOffset);
                    float distToPrevPos = Vector3.Distance(segmentPos, prevPos);
                    float distToCurPos = Vector3.Distance(curPos, segmentPos);

                    if (distToPrevPos > 1f)
                    {
                        int patchIndex = data.Length == 1 ? 0 : TerrainManager.instance.GetPatchIndex((prevPos + segmentPos) / 2f);
                        data[patchIndex].m_pathSegmentCount++;
                        curveCount++;
                        totalLength += distToPrevPos;
                        prevPos = segmentPos;
                    }
                    if (distToCurPos > 1f)
                    {
                        int patchIndex = data.Length == 1 ? 0 : TerrainManager.instance.GetPatchIndex((curPos + segmentPos) / 2f);
                        data[patchIndex].m_pathSegmentCount++;
                        curveCount++;
                        totalLength += distToCurPos;
                        prevPos = curPos;
                    }
                }
            } catch (InvalidOperationException)
            {
                return false;
            }
            return true;
        }

        public static void NewFillPathSegments(uint path, int startIndex, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, VehicleInfo.VehicleCategory vehicleCategories, ref TransportLine.TempUpdateMeshData[] data, Bezier3[] curves, Vector2[] curveOffsets, ref int curveIndex, ref float currentLength, float lengthScale, out Vector3 minPos, out Vector3 maxPos, bool ignoreY)
        {
            const float halfWidth = 4f;
            Vector3 margin = new(halfWidth, halfWidth, halfWidth);

            Vector3 prevPos = default;
            Vector3 prevDir = default;
            PathUnit.Position prevPathPos = default;

            bool isFirstPass = true;
            bool wasPed = false;

            minPos = new Vector3(100000f, 100000f, 100000f);
            maxPos = new Vector3(-100000f, -100000f, -100000f);

            foreach (PathUnit.Position pathPos in path.EnumeratePositions(startIndex, true))
            {
                NetInfo info = pathPos.m_segment.ToSegment().Info;
                if (info == null || info.m_lanes == null || info.m_lanes.Length <= (int)pathPos.m_lane)
                    return;
                if (!info.m_lanes[(int)pathPos.m_lane].CheckType(laneTypes, vehicleTypes, vehicleCategories))
                    return;

                bool isPed = info.m_lanes[pathPos.m_lane] is { m_laneType: NetInfo.LaneType.Pedestrian } or { m_laneType: NetInfo.LaneType.Vehicle, m_vehicleType: VehicleInfo.VehicleType.Bicycle };

                uint laneId = PathManager.GetLaneID(pathPos);
                float offset = (float)pathPos.m_offset / 255f;

                laneId.ToLane().CalculatePositionAndDirection(offset, out Vector3 curPos, out Vector3 curDir);

                minPos = Vector3.Min(minPos, curPos - margin);
                maxPos = Vector3.Max(maxPos, curPos + margin);

                if (isFirstPass)
                {
                    prevPos = curPos;
                    prevDir = curDir;
                    isFirstPass = false;
                } else
                {
                    laneId.ToLane().GetClosestPosition(prevPos, out Vector3 segmentPos, out float laneOffset);
                    Vector3 segmentDir = laneId.ToLane().CalculateDirection(laneOffset);

                    minPos = Vector3.Min(minPos, segmentPos - margin);
                    maxPos = Vector3.Max(maxPos, segmentPos + margin);

                    float distToPrevPos = Vector3.Distance(segmentPos, prevPos);
                    float distToCurPos = Vector3.Distance(curPos, segmentPos);

                    if (distToCurPos > 1f)
                    {
                        if (offset < laneOffset)
                        {
                            segmentDir = -segmentDir;
                            curDir = -curDir;
                        }
                    } else if (offset > 0.5f)
                    {
                        segmentDir = -segmentDir;
                        curDir = -curDir;
                    }

                    if (distToPrevPos > 1f)
                    {
                        ushort startNode = pathPos.m_segment.ToSegment().m_startNode;
                        ushort endNode = pathPos.m_segment.ToSegment().m_endNode;
                        ushort prevStartNode = prevPathPos.m_segment.ToSegment().m_startNode;
                        ushort prevEndNode = prevPathPos.m_segment.ToSegment().m_endNode;

                        float tempLength = currentLength + distToPrevPos;

                        Bezier3 bezier;
                        if ((isPed || wasPed) && prevPathPos.m_segment == pathPos.m_segment || startNode != prevStartNode && startNode != prevEndNode && endNode != prevStartNode && endNode != prevEndNode)
                        {
                            Vector3 b = VectorUtils.NormalizeXZ(segmentPos - prevPos);
                            bezier = new Bezier3(prevPos - b, prevPos * 0.75f + segmentPos * 0.25f, segmentPos * 0.75f + prevPos * 0.25f, segmentPos + b);
                        } else
                            bezier = new Bezier3(prevPos, prevPos + prevDir.normalized * (distToPrevPos / 2f), segmentPos - segmentDir.normalized * (distToPrevPos / 2f), segmentPos);

                        int patchIndex = data.Length == 1 ? 0 : TerrainManager.instance.GetPatchIndex((prevPos + segmentPos) / 2f);
                        try
                        {
                            TransportLine.FillPathSegment(bezier, data[patchIndex].m_meshData, curves, data[patchIndex].m_pathSegmentIndex, curveIndex, currentLength * lengthScale, tempLength * lengthScale, halfWidth, ignoreY ? 20f : 5f, ignoreY);
                        } catch
                        {
                            Debug.Log($"first, curves: {curves.Length}/{curveIndex}, segment: {data[patchIndex].m_pathSegmentIndex}");
                            throw;
                        }

                        if (curveOffsets != null) curveOffsets[curveIndex] = new Vector2(currentLength * lengthScale, tempLength * lengthScale);
                        data[patchIndex].m_pathSegmentIndex++; curveIndex++;
                        currentLength = tempLength;
                        prevPos = segmentPos; prevDir = segmentDir;
                    }

                    if (distToCurPos > 1f)
                    {
                        float tempLength = currentLength + distToCurPos;
                        Bezier3 bezier = laneId.ToLane().GetSubCurve(laneOffset, offset);

                        int patchIndex = data.Length == 1 ? 0 : TerrainManager.instance.GetPatchIndex((segmentPos + curPos) / 2f);
                        try
                        {
                            TransportLine.FillPathSegment(bezier, data[patchIndex].m_meshData, curves, data[patchIndex].m_pathSegmentIndex, curveIndex, currentLength * lengthScale, tempLength * lengthScale, halfWidth, ignoreY ? 20f : 5f, ignoreY);

                        } catch
                        {
                            Debug.Log($"second, curves: {curves.Length}/{curveIndex}, segment: {data[patchIndex].m_pathSegmentIndex}/{data[patchIndex].m_meshData.m_normals.Length}");
                            throw;
                        }

                        if (curveOffsets != null) curveOffsets[curveIndex] = new Vector2(currentLength * lengthScale, tempLength * lengthScale);
                        data[patchIndex].m_pathSegmentIndex++; curveIndex++;
                        currentLength = tempLength;
                        prevPos = curPos; prevDir = curDir;
                    }
                }
                wasPed = isPed;
                prevPathPos = pathPos;
            }
        }

    }
}
