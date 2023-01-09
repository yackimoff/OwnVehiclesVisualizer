
namespace OwnVehiclesVisualizer
{
    using ColossalFramework;
    using ColossalFramework.UI;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;
    using CitiesExtensions;
    using System.Threading;
    using System.CodeDom;
    using UnityEngine.Assertions.Must;
    using System.CodeDom.Compiler;

    public static class OwnVehiclesVisualizer
    {

        public static Color TragetBuildingColor = Color.green;
        public static Color SourceBuildingColor = Color.red;
        public static Color ThirdTypeBuildingColor = Color.yellow;

        public static readonly IEnumerable<InfoManager.InfoMode> workingInfoModes = new HashSet<InfoManager.InfoMode>{
            InfoManager.InfoMode.None,
            InfoManager.InfoMode.Density,
            InfoManager.InfoMode.NoisePollution,
            InfoManager.InfoMode.Pollution,
            InfoManager.InfoMode.LandValue,
            InfoManager.InfoMode.Districts,
            InfoManager.InfoMode.Connections,
            InfoManager.InfoMode.Traffic,
            InfoManager.InfoMode.Wind,
            InfoManager.InfoMode.BuildingLevel,
            InfoManager.InfoMode.Underground,
            InfoManager.InfoMode.TerrainHeight,
        };

        private static bool m_listenToSelectedInstances = true;
        private static bool m_showBuildingVehiclePaths = false;
        private static bool m_expandVehicleToSourceBuilding = false;
        private static bool m_highlightBuildings = false;
        internal static InfoManager.InfoMode CurrentInfomode { get => InfoManager.instance.CurrentMode; }
        public static bool ShowBuildingVehiclesPaths { get => m_listenToSelectedInstances && m_showBuildingVehiclePaths && workingInfoModes.Contains(CurrentInfomode); }
        public static bool HighlightTargetBuildings { get => m_listenToSelectedInstances && m_highlightBuildings && workingInfoModes.Contains(CurrentInfomode); }
        public static bool HighlightSourceBuildings { get => m_listenToSelectedInstances && m_highlightBuildings && workingInfoModes.Contains(CurrentInfomode); }
        public static bool HighlightThirdTypeBuildings { get => m_listenToSelectedInstances && m_highlightBuildings && workingInfoModes.Contains(CurrentInfomode); }
        internal static object m_lock = new();
        internal static void Enter()
        {
            while (!Monitor.TryEnter(OwnVehiclesVisualizer.m_lock)) { };
        }
        internal static void Exit()
        {
            Monitor.Exit(OwnVehiclesVisualizer.m_lock);
        }
        public static List<ushort> HighlightedTargetBuildings { get; private set; } = new List<ushort>();
        public static List<Vector3> HighlightedWorldPoses { get; private set; } = new List<Vector3>();
        public static List<ushort> HighlightedSourceBuildings { get; private set; } = new List<ushort>();
        public static List<ushort> HighlightedThirdTypeBuildings { get; private set; } = new List<ushort>();
        public static List<InstanceID> HighlightedPaths { get; private set; } = new List<InstanceID>();

        public static void Init()
        {
#if DEBUG
            Debug.Log("[OVV] Init");
#endif
            Singleton<InfoManager>.instance.EventInfoModeChanged += OnInfoModeChanged;
            InstanceManagerPatch.InstanceManagerPatch.InstanceSelected += OnInstanceSelected;
        }

        internal static void OnInfoModeChanged(InfoManager.InfoMode mode, InfoManager.SubInfoMode subMode)
        {
#if DEBUG
            Debug.Log("[OVV] InfoMode changed");
#endif
            m_listenToSelectedInstances = workingInfoModes.Contains(mode);
            NetManager.instance.PathVisualizer.PathsVisible = false;
            Update();
        }

        internal static void OnInstanceSelected(InstanceID id)
        {
#if DEBUG
            Debug.Log("[OVV] Instance selected");
#endif
            m_showBuildingVehiclePaths = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            m_expandVehicleToSourceBuilding = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            m_highlightBuildings = Input.GetKey(KeyCode.LeftAlt);
            Update();
        }

        public static void Update()
        {

            static void AddVehicle(ushort vehicleId, bool addTarget, bool addSource)
            {
                
                vehicleId = VehicleManager.instance.m_vehicles.m_buffer[vehicleId].GetFirstVehicle(vehicleId);


                ref Vehicle vehicle = ref VehicleManager.instance.m_vehicles.m_buffer[vehicleId];

                if (vehicle.Info.m_vehicleAI is TaxiAI taxiAI
                    && (vehicle.m_flags & (Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget | Vehicle.Flags.WaitingCargo | Vehicle.Flags.WaitingPath)) == 0 && vehicle.m_path != 0
                    && PathManager.instance.m_pathUnits.m_buffer[vehicle.m_path].GetLastPosition(out PathUnit.Position pos))
                {
                    HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                    if (addTarget)
                        HighlightedWorldPoses.Add(NetManager.instance.m_lanes.m_buffer[PathManager.GetLaneID(pos)].CalculatePosition((float)pos.m_offset / 255.0f));
                }
                else if (vehicle.Info.m_class.m_service == ItemClass.Service.PublicTransport)
                {
                    if (addTarget)
                    {
                        foreach (ushort transportVehicleId in vehicleId.WithTrailers())
                            foreach (ushort citizenInstanceId in transportVehicleId.ToVehicle().m_citizenUnits.EnumerateCitizens().Select(citizenId => citizenId.ToCitizen().m_instance))
                                AddCitizenInstance(citizenInstanceId, m_expandVehicleToSourceBuilding);
                        if (!m_expandVehicleToSourceBuilding)
                            HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                    }
                    else
                        HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                }
                else
                {
                    HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                    if (addTarget && vehicle.Info.m_vehicleAI.GetTargetID(vehicleId, ref vehicle) is InstanceID targetID && !vehicle.m_flags.IsFlagSet(Vehicle.Flags.GoingBack))
                    {
                        if (targetID.Building != 0)
                            HighlightedTargetBuildings.Add(targetID.Building);
                        else if (targetID.NetNode != 0)
                            HighlightedWorldPoses.Add(targetID.NetNode.ToNode().m_position);
                    }
                    if (addSource && vehicle.m_sourceBuilding is ushort sourceBuilding and not 0)
                        HighlightedSourceBuildings.Add(sourceBuilding);
                }
            }

            static void AddCitizenInstance(ushort citizenInstanceId, bool addPath = true)
            {
                if (citizenInstanceId == 0)
                    return;
                if (addPath)
                    HighlightedPaths.Add(new InstanceID { CitizenInstance = citizenInstanceId });
                if (citizenInstanceId.ToCitizenInstance().m_targetBuilding is ushort targetBuilding and not 0)
                {
                    if (citizenInstanceId.ToCitizenInstance().m_flags.IsFlagSet(CitizenInstance.Flags.TargetIsNode))
                        HighlightedWorldPoses.Add(targetBuilding.ToNode().m_position);
                    else
                        HighlightedTargetBuildings.Add(targetBuilding);
                }
            }

            if (CurrentInfomode != InfoManager.InfoMode.TrafficRoutes && !m_showBuildingVehiclePaths)
                NetManager.instance.PathVisualizer.PathsVisible = false;

            Enter();
            try
            {
                HighlightedTargetBuildings.Clear();
                HighlightedWorldPoses.Clear();
                HighlightedSourceBuildings.Clear();
                HighlightedThirdTypeBuildings.Clear();
                HighlightedPaths.Clear();

                InstanceID instanceID = Singleton<InstanceManager>.instance.GetSelectedInstance();

                if (instanceID.Vehicle != 0)
                {
                    if (m_expandVehicleToSourceBuilding && VehicleManager.instance.m_vehicles.m_buffer[instanceID.Vehicle].m_sourceBuilding is ushort sourceBuilding and not 0)
                    {
                        sourceBuilding.EnumerateOwnVehicles().ForEach(vehicle => AddVehicle(vehicle, true, false));
                        HighlightedThirdTypeBuildings.Add(sourceBuilding);
                    }
                    else
                        AddVehicle(instanceID.Vehicle, true, true);
                }
                else if (instanceID.Building != 0)
                {
                    instanceID.Building.EnumerateGuestVehicles().ForEach(vehicle => AddVehicle(vehicle, false, true));
                    instanceID.Building.EnumerateOwnVehicles().ForEach(vehicle => AddVehicle(vehicle, true, false));
                    HighlightedThirdTypeBuildings.Add(instanceID.Building);
                }
                else if (instanceID.CitizenInstance != 0)
                {
                    AddCitizenInstance(instanceID.CitizenInstance);
                }
                else
                    return;
            }
            finally
            {
                Exit();
            }
        }
    }
}
