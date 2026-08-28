using HarmonyLib;
using UnityEngine;

namespace CUMCP.Executor
{
    [HarmonyPatch(typeof(Body))]
    [HarmonyPatch("SetVelocity")]
    public static class BodyMovementPatch
    {
        internal static Vector2? OverrideVelocity;

        [HarmonyPostfix]
        static void AfterSetVelocity(Body __instance, Vector2 vel)
        {
            if (OverrideVelocity.HasValue)
            {
                __instance.rb.velocity = OverrideVelocity.Value;
            }
        }
    }
}
