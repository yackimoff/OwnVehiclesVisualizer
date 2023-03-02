
namespace OwnVehiclesVisualizer
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using CitiesExtensions;

    using ColossalFramework;
    using ColossalFramework.UI;

    using UnityEngine;

    public static class OwnVehiclesVisualizer
    {

        public record struct HighlightDataBase(Color Color, ushort buildingId);
        public static bool Initialized { get; private set; }

        public static Color TragetBuildingColor = Color.green;
        public static Color SourceBuildingColor = Color.red;
        public static Color ThirdTypeBuildingColor = Color.yellow;

        private static bool m_showBuildingVehiclePaths = false;
        private static bool m_expandVehicleToSourceBuilding = false;
        private static bool m_highlightBuildings = false;
        internal static InfoManager.InfoMode CurrentInfomode => InfoManager.instance.CurrentMode;
        internal static object m_lock = new();


        internal static void Enter() { while (!Monitor.TryEnter(OwnVehiclesVisualizer.m_lock)) { }; }
        internal static bool TryEnter() => Monitor.TryEnter(OwnVehiclesVisualizer.m_lock);
        internal static void Exit() => Monitor.Exit(OwnVehiclesVisualizer.m_lock);
        public static bool HighlightPaths { get => m_showBuildingVehiclePaths; }
        public static bool HighlightBuildings { get => m_highlightBuildings; }
        public static List<ushort> HighlightedTargetBuildings { get; private set; } = new List<ushort>();
        public static List<Vector3> HighlightedWorldPoses { get; private set; } = new List<Vector3>();
        public static List<ushort> HighlightedSourceBuildings { get; private set; } = new List<ushort>();
        public static List<ushort> HighlightedThirdTypeBuildings { get; private set; } = new List<ushort>();
        public static List<InstanceID> HighlightedPaths { get; private set; } = new List<InstanceID>();

        public static void Init()
        {
#if DEBUG
            Debug.Log("Init");
#endif
            Singleton<InfoManager>.instance.EventInfoModeChanged += OnInfoModeChanged;
            InstanceManagerPatch.InstanceManagerPatch.EventInstanceSelected += OnInstanceSelected;
            PathVisualizerPatch.PathVisualizerPatch.Enabled = true;
            BuildingManagerPatch.BuildingManagerPatch.Enabled = true;
            Initialized = true;
        }

        public static void Deinit()
        {
#if DEBUG
            Debug.Log("Deinit");
#endif
            if (!Initialized) return;
            InfoManager.instance.EventInfoModeChanged -= OnInfoModeChanged;
            InstanceManagerPatch.InstanceManagerPatch.EventInstanceSelected -= OnInstanceSelected;
        }

        internal static void OnInfoModeChanged(InfoManager.InfoMode mode, InfoManager.SubInfoMode subMode)
        {
            PathVisualizerPatch.PathVisualizerPatch.Enabled = InfoManager.instance
                is not { CurrentMode: InfoManager.InfoMode.TrafficRoutes, CurrentSubMode: InfoManager.SubInfoMode.Paths }
                and not { CurrentMode: InfoManager.InfoMode.Transport }
                and not { CurrentMode: InfoManager.InfoMode.EscapeRoutes }
                and not { CurrentMode: InfoManager.InfoMode.Tours };
            BuildingManagerPatch.BuildingManagerPatch.Enabled = mode is
                InfoManager.InfoMode.None or
                InfoManager.InfoMode.Happiness or
                InfoManager.InfoMode.Density or
                InfoManager.InfoMode.NoisePollution or
                InfoManager.InfoMode.Pollution or
                InfoManager.InfoMode.LandValue or
                InfoManager.InfoMode.Districts or
                InfoManager.InfoMode.Connections or
                InfoManager.InfoMode.Traffic or
                InfoManager.InfoMode.BuildingLevel or
                InfoManager.InfoMode.TerrainHeight or
                InfoManager.InfoMode.TrafficRoutes or
                InfoManager.InfoMode.Underground;
#if DEBUG
            Debug.Log($"InfoMode changed, PathVisualizer.Enabled: {PathVisualizerPatch.PathVisualizerPatch.Enabled}, BuidlingManagerPatch.Enabled: {BuildingManagerPatch.BuildingManagerPatch.Enabled}");
#endif
        }

        internal static void OnInstanceSelected(InstanceID id)
        {
#if DEBUG
            Debug.Log("Instance selected");
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
                    && (vehicle.m_flags & (Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget | Vehicle.Flags.WaitingCargo | Vehicle.Flags.WaitingPath)) == 0
                    && vehicle.m_path != 0
                    && PathManager.instance.m_pathUnits.m_buffer[vehicle.m_path].GetLastPosition(out PathUnit.Position pos))
                {
                    HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                    if (addTarget)
                        HighlightedWorldPoses.Add(NetManager.instance.m_lanes.m_buffer[PathManager.GetLaneID(pos)].CalculatePosition((float)pos.m_offset / 255.0f));
                } else if (vehicle.Info.m_class.m_service == ItemClass.Service.PublicTransport)
                {
                    if (addTarget)
                    {
                        foreach (ushort transportVehicleId in vehicleId.WithTrailers())
                            foreach (ushort citizenInstanceId in transportVehicleId.ToVehicle().m_citizenUnits.EnumerateCitizens().Select(citizenId => citizenId.ToCitizen().m_instance))
                                AddCitizenInstance(citizenInstanceId, m_expandVehicleToSourceBuilding);
                        HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                    } else
                        HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                } else
                {
                    HighlightedPaths.Add(new InstanceID { Vehicle = vehicleId });
                    if (addTarget && !vehicle.m_flags.IsFlagSet(Vehicle.Flags.GoingBack) && vehicle.Info.m_vehicleAI.GetTargetID(vehicleId, ref vehicle) is InstanceID targetID)
                    {
                        if (targetID.Building != 0)
                            HighlightedTargetBuildings.Add(targetID.Building);
                        else if (targetID.NetNode != 0)
                            HighlightedWorldPoses.Add(targetID.NetNode.ToNode().m_position);
                    }
                    if (addSource && vehicle.m_sourceBuilding is not 0 and var sourceBuilding)
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

            Enter();
            try
            {
                HighlightedTargetBuildings.Clear();
                HighlightedWorldPoses.Clear();
                HighlightedSourceBuildings.Clear();
                HighlightedThirdTypeBuildings.Clear();
                HighlightedPaths.Clear();

                InstanceID instanceID = InstanceManager.instance.GetSelectedInstance();

                if (instanceID.Vehicle is not 0 and ushort vehicle)
                {
                    if (m_expandVehicleToSourceBuilding &&
                        vehicle.ToVehicle() is { Info.m_class.m_service: not ItemClass.Service.PublicTransport, m_sourceBuilding: not 0 and ushort sourceBuilding })
                    {
                        sourceBuilding.EnumerateOwnVehicles().ForEach(vehicle => AddVehicle(vehicle, true, false));
                        HighlightedThirdTypeBuildings.Add(sourceBuilding);
                    } else
                        AddVehicle(vehicle, true, true);
                } else if (instanceID.Building is not 0 and ushort building)
                {
                    building.EnumerateGuestVehicles().ForEach(vehicle => AddVehicle(vehicle, false, true));
                    building.EnumerateOwnVehicles().ForEach(vehicle => AddVehicle(vehicle, true, false));
                    HighlightedThirdTypeBuildings.Add(building);
                } else if (instanceID.CitizenInstance is not 0 and ushort citizenInstance)
                {
                    AddCitizenInstance(citizenInstance);
                }
            } finally
            {
                Exit();
            }
#if DEBUG
            Debug.Log($"Updated, paths: {HighlightedPaths.Count}, target buidlings: {HighlightedTargetBuildings.Count}");
#endif
        }
    }
}
