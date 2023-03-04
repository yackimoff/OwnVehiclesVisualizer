namespace OwnVehiclesVisualizer.CitiesExtensions
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;


    public static class CitiesExtensions
    {
        const MethodImplOptions MethodImplOptionsAggressiveInlining = (MethodImplOptions)256;

        public static IEnumerable<ushort> EnumerateOwnVehicles(this Building building)
        {
            ushort vehicleId = building.m_ownVehicles;
            while (vehicleId != 0 && vehicleId.ToVehicle() is { m_flags: Vehicle.Flags flags, m_nextOwnVehicle: ushort nextId })
            {
                if ((flags & Vehicle.Flags.Created) != 0)
                    yield return vehicleId;
                vehicleId = nextId;
            }
        }

        public static IEnumerable<ushort> EnumerateGuestVehicles(this Building building)
        {
            ushort vehicleId = building.m_guestVehicles;
            while (vehicleId != 0 && vehicleId.ToVehicle() is { m_flags: Vehicle.Flags flags, m_nextGuestVehicle: ushort nextId })
            {
                if ((flags & Vehicle.Flags.Created) != 0)
                    yield return vehicleId;
                vehicleId = nextId;
            }
        }

        public static IEnumerable<ushort> EnumerateOwnVehicles(this ushort buidingID) => BuildingManager.instance.m_buildings.m_buffer[buidingID].EnumerateOwnVehicles();

        public static IEnumerable<ushort> EnumerateGuestVehicles(this ushort buidingID) => BuildingManager.instance.m_buildings.m_buffer[buidingID].EnumerateGuestVehicles();

        public static IEnumerable<uint> EnumerateCitizens(this uint citizenUnitId)
        {
            while (citizenUnitId != 0)
            {
                CitizenUnit unit = CitizenManager.instance.m_units.m_buffer[citizenUnitId];
                if (unit.m_citizen0 != 0)
                    yield return unit.m_citizen0;
                if (unit.m_citizen1 != 0)
                    yield return unit.m_citizen1;
                if (unit.m_citizen2 != 0)
                    yield return unit.m_citizen2;
                if (unit.m_citizen3 != 0)
                    yield return unit.m_citizen3;
                if (unit.m_citizen4 != 0)
                    yield return unit.m_citizen4;
                citizenUnitId = unit.m_nextUnit;
            }
        }

        [MethodImpl(MethodImplOptionsAggressiveInlining)]
        public static ref Citizen ToCitizen(this uint citizenId) => ref CitizenManager.instance.m_citizens.m_buffer[citizenId];

        [MethodImpl(MethodImplOptionsAggressiveInlining)]
        public static ref CitizenInstance ToCitizenInstance(this ushort citizenInstanceId) => ref CitizenManager.instance.m_instances.m_buffer[citizenInstanceId];

        [MethodImpl(MethodImplOptionsAggressiveInlining)]
        public static ref Vehicle ToVehicle(this ushort vehicleId) => ref VehicleManager.instance.m_vehicles.m_buffer[vehicleId];

        public static IEnumerable<ushort> WithTrailers(this ushort vehicleId)
        {
            while (vehicleId != 0)
            {
                yield return vehicleId;
                vehicleId = vehicleId.ToVehicle().m_trailingVehicle;
            }
        }

        [MethodImpl(MethodImplOptionsAggressiveInlining)]
        public static ref NetNode ToNode(this ushort nodeId) => ref NetManager.instance.m_nodes.m_buffer[nodeId];

        [MethodImpl(MethodImplOptionsAggressiveInlining)]
        public static ref NetSegment ToSegment(this ushort segmentId) => ref NetManager.instance.m_segments.m_buffer[segmentId];

        [MethodImpl(MethodImplOptionsAggressiveInlining)]
        public static ref NetLane ToLane(this uint laneId) => ref NetManager.instance.m_lanes.m_buffer[laneId];

        [MethodImpl(MethodImplOptionsAggressiveInlining)]
        public static ref PathUnit ToPathUnit(this uint pathUnitId) => ref PathManager.instance.m_pathUnits.m_buffer[pathUnitId];

        public static IEnumerable<PathUnit.Position> EnumeratePositions(this uint pathUnitId, int startIndex = 0, bool recursive = false, bool throwIfInvalid = false)
        {
            // Skip first path positions that are transport (i. e. current line),
            // but proceed with the rest (i. e. show other public transits).
            bool skipTransport = true;
            while (pathUnitId != 0)
            {
                for (int i = startIndex; i < pathUnitId.ToPathUnit().m_positionCount; i++)
                {
                    if (!pathUnitId.ToPathUnit().GetPosition(i, out PathUnit.Position pos))
                        if (throwIfInvalid)
                            throw new InvalidOperationException();
                        else
                            yield break;
                    if (recursive && pos.m_segment.ToSegment().m_path != 0)
                    {
                        if (!skipTransport)
                            foreach (PathUnit.Position innerPos in pos.m_segment.ToSegment().m_path.EnumeratePositions(0, false, false))
                                yield return innerPos;

                    } else
                    {
                        skipTransport = false;
                        yield return pos;
                    }
                }
                pathUnitId = pathUnitId.ToPathUnit().m_nextPathUnit;
                startIndex = 0;
            }
        }

    }
}
