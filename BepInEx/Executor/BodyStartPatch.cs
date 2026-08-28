using HarmonyLib;
using UnityEngine;

namespace CUMCP.Executor
{
    // All Body lifecycle patches are removed - let them run normally
    // Body.Start initializes renderers
    // Body.PlaceBody does ground detection via OverlapBox
}
