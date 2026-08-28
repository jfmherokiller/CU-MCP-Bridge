using System;
using HarmonyLib;
using UnityEngine;

namespace CUMCP.Executor
{
    [HarmonyPatch(typeof(PlayerCamera))]
    [HarmonyPatch("ToggleWoundView")]
    public static class WoundViewAIPatch
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            try
            {
                var aiBody = AIPlayerManager.AIBody;
                var humanBody = AIPlayerManager.HumanBody;
                if (aiBody == null || humanBody == null) return true;

                bool panelOpen = WoundView.view != null && WoundView.view.gameObject.activeSelf;

                // Panel is open → always close (let original run)
                if (panelOpen) return true;

                // Panel is closed → set target body based on cursor
                bool cursorOnAI = IsCursorOverBody(aiBody);

                if (cursorOnAI && WoundView.view != null)
                {
                    WoundView.view.body = aiBody;
                    WoundView.view.napbutton.gameObject.SetActive(false);
                    Transform wb = WoundView.view.transform.Find("WorkoutButton");
                    if (wb != null) wb.gameObject.SetActive(false);
                    WoundView.view.nameText.text = "AI Character";
                    WoundView.view.bodyStatText.text = "";
                }
                else if (WoundView.view != null)
                {
                    WoundView.view.body = humanBody;
                    WoundView.view.napbutton.gameObject.SetActive(true);
                    Transform wb = WoundView.view.transform.Find("WorkoutButton");
                    if (wb != null) wb.gameObject.SetActive(true);
                    WoundView.view.nameText.text = "EXPERIMENT";
                }

                return true; // let original open the panel
            }
            catch (Exception e)
            {
                BridgePlugin.Log?.LogError($"[CU-MCP] WV ERROR: {e.Message}");
                return true;
            }
        }

        private static bool IsCursorOverBody(Body body)
        {
            if (body == null) return false;
            Vector2 cursorWorld = GetCursorWorldPos();

            var parent = body.transform.parent;
            var colliders = parent != null
                ? parent.GetComponentsInChildren<Collider2D>()
                : body.GetComponentsInChildren<Collider2D>();
            if (colliders == null) return false;
            foreach (var col in colliders)
            {
                if (col != null && col.enabled && col.OverlapPoint(cursorWorld))
                    return true;
            }
            return false;
        }

        private static Vector2 GetCursorWorldPos()
        {
            Vector3 mouseScreen = Input.mousePosition;
            mouseScreen.z = -Camera.main.transform.position.z;
            return Camera.main.ScreenToWorldPoint(mouseScreen);
        }
    }
}
