using HarmonyLib;
using UnityEngine;

namespace CUMCP.Executor
{
    [HarmonyPatch(typeof(Body))]
    [HarmonyPatch("FixedUpdate")]
    public static class BodyMoveDirOverride
    {
        internal static Vector2? OverrideMoveDir;
        internal static bool IsCrouching;

        [HarmonyPrefix]
        static bool BeforeFixedUpdate(Body __instance)
        {
            if (__instance != AIPlayerManager.GetActiveBody()) return true;

            if (OverrideMoveDir.HasValue)
            {
                __instance.moveDir = OverrideMoveDir.Value;
            }
            // Use the game's real crouch flag (which lerps crouchAmount and shrinks the collider).
            // Forcing `standing = false` would put the body into the prone/ragdoll state instead.
            __instance.crouching = IsCrouching;
            return true;
        }

        [HarmonyPostfix]
        static void AfterFixedUpdate(Body __instance)
        {
            if (__instance != AIPlayerManager.GetActiveBody()) return;

            var rb = __instance.rb;
            if (OverrideMoveDir.HasValue && rb != null && rb.simulated)
            {
                float targetVx = OverrideMoveDir.Value.x * __instance.actualMaxSpeed;
                rb.velocity = new Vector2(targetVx, rb.velocity.y);

                if (OverrideMoveDir.Value.x > 0.01f)
                {
                    __instance.targetLookPos = new Vector3(__instance.transform.position.x + 10f, __instance.transform.position.y, 0f);
                }
                else if (OverrideMoveDir.Value.x < -0.01f)
                {
                    __instance.targetLookPos = new Vector3(__instance.transform.position.x - 10f, __instance.transform.position.y, 0f);
                }
            }
        }
    }
}