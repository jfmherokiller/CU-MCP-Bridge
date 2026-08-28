using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CUMCP.Executor
{
    /// <summary>
    /// Lets the OrderExecutor detect mid-air wall sliding and force a wall-jump launch.
    /// Works by reading Body's private slide fields after HandleGroundedState runs.
    /// </summary>
    public static class WallJumpTrigger
    {
        /// <summary>True when the active body is currently sliding against a wall (left or right).</summary>
        public static bool IsSliding;

        /// <summary>When set, the next frame the active body is found sliding, launch it in this direction.</summary>
        internal static Vector2? ForceJumpDir;

        private static FieldInfo _slidingLeft;
        private static FieldInfo _slidingRight;
        private static bool _fieldsResolved;

        private static void ResolveFields()
        {
            if (_fieldsResolved) return;
            try
            {
                var t = typeof(Body);
                _slidingLeft = t.GetField("slidingLeft", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _slidingRight = t.GetField("slidingRight", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch { }
            _fieldsResolved = true;
        }

        public static void RequestWallJump(Vector2 dir)
        {
            ForceJumpDir = dir;
        }

        /// <summary>
        /// Launch the active body away/toward in the given direction immediately, without waiting
        /// for the game's wall-slide flag. Same velocity model as the game's own wall-jump.
        /// </summary>
        public static void LaunchWallJump(Vector2 dir)
        {
            var body = AIPlayerManager.GetActiveBody();
            if (body == null || body.rb == null) return;
            float d = dir.x >= 0f ? 1f : -1f;
            body.rb.velocity = new Vector2(d * body.actualJumpSpeed, body.actualJumpSpeed);
            ForceJumpDir = null;
        }

        public static void CancelRequest()
        {
            ForceJumpDir = null;
        }

        [HarmonyPatch(typeof(Body))]
        [HarmonyPatch("HandleGroundedState")]
        public static class Patch
        {
            [HarmonyPostfix]
            static void AfterGroundedState(Body __instance)
            {
                if (__instance != AIPlayerManager.GetActiveBody()) return;

                ResolveFields();
                bool sl = false, sr = false;
                try
                {
                    if (_slidingLeft != null) sl = (bool)_slidingLeft.GetValue(__instance);
                    if (_slidingRight != null) sr = (bool)_slidingRight.GetValue(__instance);
                }
                catch { }
                IsSliding = sl || sr;

                if (ForceJumpDir.HasValue && IsSliding && __instance.rb != null)
                {
                    // Launch away from the wall at full jump speed (same as the game's wall-jump).
                    float dir = ForceJumpDir.Value.x >= 0f ? 1f : -1f;
                    __instance.rb.velocity = new Vector2(dir * __instance.actualJumpSpeed, __instance.actualJumpSpeed);
                    ForceJumpDir = null;
                }
            }
        }
    }
}
