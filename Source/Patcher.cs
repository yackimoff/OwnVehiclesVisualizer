
namespace OwnVehiclesVisualizer
{
    using HarmonyLib;
    using System.Reflection;
    using UnityEngine;
    using System;

    public class Patcher
    {
        private const string HarmonyId = "com.lexkimov.OwnVehiclesVisualizer";
        private static bool patched = false;

        public static void PatchAll()
        {
            if (patched) return;

            try
            {
#if DEBUG
                Harmony.DEBUG = true;
                Debug.Log($"[BVMPVE] Patching {Assembly.GetExecutingAssembly().GetName().Version}");
#endif
                var harmony = new Harmony(HarmonyId);
                harmony.PatchAll();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                patched = true;
            }
        }

        public static void UnpatchAll()
        {
            if (!patched) return;

#if DEBUG
            Harmony.DEBUG = true;
            Debug.Log($"[BVMPVE] Unpatching {Assembly.GetExecutingAssembly().GetName().Version}");
#endif
            var harmony = new Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);

            patched = false;
        }
    }
}
