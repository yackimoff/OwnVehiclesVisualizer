namespace OwnVehiclesVisualizer
{
    using System;

    using CitiesHarmony.API;

    using ICities;

    using JetBrains.Annotations;

    using UnityEngine;

    public class OwnVehiclesVisualizerMod : LoadingExtensionBase, IUserMod
    {
        public static Version ModVersion => typeof(OwnVehiclesVisualizerMod).Assembly.GetName().Version;
        public static bool HaveIndustriesDLC;

#if DEBUG
        public string Name => "Own vehicle visualizer " + ModVersion.ToString(4) + " DEBUG";
#else
        public string Name => "Own vehicle visualizer " + ModVersion.ToString(3);
#endif

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            if (mode is LoadMode.NewGame or LoadMode.LoadGame or LoadMode.NewGameFromScenario or LoadMode.LoadScenario)
                OwnVehiclesVisualizer.Init();
        }

        public override void OnLevelUnloading()
        {
            if (OwnVehiclesVisualizer.Initialized)
                OwnVehiclesVisualizer.Deinit();
            base.OnLevelUnloading();
        }

        public string Description => "Building Vehicle Monitor Path Visualizer Extension";
        [UsedImplicitly]
        public void OnEnabled()
        {
            HaveIndustriesDLC = SteamHelper.IsDLCOwned(SteamHelper.DLC.IndustryDLC);
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        [UsedImplicitly]
        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
