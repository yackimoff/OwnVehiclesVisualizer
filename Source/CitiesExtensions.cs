namespace OwnVehiclesVisualizer.CitiesExtensions
{
    using ColossalFramework;
    using ColossalFramework.UI;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public static class CitiesExtensions
    {
        public static IEnumerable<ushort> EnumerateOwnVehicles(this Building building)
        {
            ushort vehicleID = building.m_ownVehicles;
            while (vehicleID != 0)
            {
                if (Singleton<VehicleManager>.instance.m_vehicles?.m_buffer[vehicleID] is Vehicle vehicle)
                {
                    if ((vehicle.m_flags & Vehicle.Flags.Created) != 0)
                        yield return vehicleID;
                    vehicleID = vehicle.m_nextOwnVehicle;
                }
            }
        }
        public static IEnumerable<ushort> EnumerateGuestVehicles(this Building building)
        {
            ushort vehicleID = building.m_guestVehicles;
            while (vehicleID != 0)
            {
                if (Singleton<VehicleManager>.instance.m_vehicles?.m_buffer[vehicleID] is Vehicle vehicle)
                {
                    if ((vehicle.m_flags & Vehicle.Flags.Created) != 0)
                        yield return vehicleID;
                    vehicleID = vehicle.m_nextGuestVehicle;
                }
            }
        }
        public static IEnumerable<ushort> EnumerateOwnVehicles(this ushort buidingID) => Singleton<BuildingManager>.instance?.m_buildings?.m_buffer[buidingID].EnumerateOwnVehicles();
        public static IEnumerable<ushort> EnumerateGuestVehicles(this ushort buidingID) => Singleton<BuildingManager>.instance?.m_buildings?.m_buffer[buidingID].EnumerateGuestVehicles();

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
        public static ref Citizen ToCitizen(this uint citizenId) => ref CitizenManager.instance.m_citizens.m_buffer[citizenId];
        public static ref Vehicle ToVehicle(this ushort vehicleId) => ref VehicleManager.instance.m_vehicles.m_buffer[vehicleId];
        public static ref CitizenInstance ToCitizenInstance(this ushort citizenInstanceId) => ref CitizenManager.instance.m_instances.m_buffer[citizenInstanceId];
        public static IEnumerable<ushort> WithTrailers(this ushort vehicleId)
        {
            while (vehicleId != 0)
            {
                yield return vehicleId;
                vehicleId = vehicleId.ToVehicle().m_trailingVehicle;
            }
        }

        public static ref NetNode ToNode(this ushort nodeId) => ref NetManager.instance.m_nodes.m_buffer[nodeId];

    }
}
