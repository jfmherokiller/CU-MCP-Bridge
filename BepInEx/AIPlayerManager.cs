using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace CUMCP
{
    public static class AIPlayerManager
    {
        private static Body _humanBody;
        private static Body _aiBody;
        private static GameObject _characterPrefab; // inactive template
        private static float _findTimer;
        private const float FIND_INTERVAL = 1f;

        public static bool IsAIEnabled => _aiBody != null;
        public static Body HumanBody => _humanBody;
        public static Body AIBody => _aiBody;

        public static void Tick()
        {
            _findTimer -= Time.deltaTime;
            if (_findTimer <= 0f)
            {
                _findTimer = FIND_INTERVAL;
                RefreshBodyRefs();
            }
        }

        private static void RefreshBodyRefs()
        {
            if (_humanBody == null || _humanBody.gameObject == null)
            {
                _humanBody = null;
                var allBodies = GameObject.FindObjectsOfType<Body>();
                foreach (var b in allBodies)
                {
                    if (b.GetComponent<AIPlayerTag>() == null)
                    {
                        _humanBody = b;
                        BridgePlugin.Log?.LogInfo("[CU-MCP] Found human body");
                        break;
                    }
                }
            }

            if (_aiBody != null && (_aiBody.gameObject == null || _aiBody == null))
            {
                BridgePlugin.Log?.LogInfo("[CU-MCP] AI body lost");
                _aiBody = null;
            }
        }

        public static Body GetActiveBody()
        {
            if (_aiBody != null)
                return _aiBody;
            return _humanBody;
        }

        // Like mod's ClientMain._InitializePlayerCharacter
        private static void EnsurePrefabTemplate()
        {
            if (_characterPrefab != null) return;
            
            var experimentGO = GameObject.Find("Experiment");
            if (experimentGO == null)
            {
                BridgePlugin.Log?.LogWarning("[CU-MCP] 'Experiment' not found");
                return;
            }

            // Clone the Experiment and store as inactive template (like mod does)
            var savedCamera = PlayerCamera.main;
            var savedMoodle = MoodleManager.main;

            experimentGO.SetActive(false);
            _characterPrefab = Object.Instantiate(experimentGO);
            _characterPrefab.name = "AI_Character_Prefab";
            _characterPrefab.SetActive(false);
            experimentGO.SetActive(true);

            // Restore singletons
            RestoreStatic(typeof(PlayerCamera), "main", savedCamera);
            RestoreStatic(typeof(MoodleManager), "main", savedMoodle);

            // Destroy conflicting components on template (like mod's approach)
            DestroyImmediateComponent<PlayerCamera>(_characterPrefab);
            DestroyImmediateComponent<MoodleManager>(_characterPrefab);

            BridgePlugin.Log?.LogInfo("[CU-MCP] Character prefab template created");
        }

        public static Body CreateAI(Vector2 position)
        {
            if (_aiBody != null)
            {
                BridgePlugin.Log?.LogInfo("[CU-MCP] AI body already exists");
                return _aiBody;
            }

            if (_humanBody == null)
            {
                BridgePlugin.Log?.LogWarning("[CU-MCP] No human body to clone from");
                return null;
            }

            // Ensure template exists
            EnsurePrefabTemplate();
            if (_characterPrefab == null)
            {
                BridgePlugin.Log?.LogWarning("[CU-MCP] No prefab template");
                return null;
            }

            // Like mod's _Internal_CreateNetBody
            var cloneGO = Object.Instantiate(_characterPrefab, Vector3.zero, Quaternion.identity);
            cloneGO.name = "Character_AI";
            cloneGO.SetActive(true);

            var cloneBody = cloneGO.GetComponentInChildren<Body>();
            if (cloneBody == null)
            {
                BridgePlugin.Log?.LogWarning("[CU-MCP] No Body in prefab");
                Object.Destroy(cloneGO);
                return null;
            }

            cloneBody.name = "Body_AI";
            // The prefab template may snapshot the Experiment while flipped, so the clone
            // inherits isRight=true + scaleX=-1 (inverted bookkeeping). Normalize it to the
            // normal convention (isRight=true = facing right, scaleX=+1) so the game's own
            // facing logic and the head/eyes aim both point the same way.
            cloneBody.isRight = true;
            var cloneScale = cloneBody.transform.localScale;
            cloneBody.transform.localScale = new Vector3(Mathf.Abs(cloneScale.x), cloneScale.y, cloneScale.z);
            cloneBody.targetLookPos = new Vector2(1000f, 460f);
            cloneBody.transform.position = position;

            cloneGO.AddComponent<AIPlayerTag>();

            _aiBody = cloneBody;
            BridgePlugin.Log?.LogInfo($"[CU-MCP] AI player created at ({position.x:F1},{position.y:F1})");
            return _aiBody;
        }

        // Like mod's Body_Start_MultiplayerPatch.Postfix - ignores collisions with other bodies
        [HarmonyLib.HarmonyPatch(typeof(Body))]
        [HarmonyLib.HarmonyPatch("Start")]
        public static class BodyStartPatch
        {
            [HarmonyLib.HarmonyPostfix]
            static void Postfix(Body __instance)
            {
                if (__instance.GetComponent<AIPlayerTag>() == null) return;

                IgnoreAllBodyCollisions(__instance);
            }
        }

        private static void IgnoreAllBodyCollisions(Body instance)
        {
            var allBodies = GameObject.FindObjectsOfType<Body>();
            var myCols = instance.transform.parent.GetComponentsInChildren<Collider2D>();
            foreach (var other in allBodies)
            {
                if (other == instance) continue;
                var otherCols = other.transform.parent.GetComponentsInChildren<Collider2D>();
                foreach (var mc in myCols)
                    foreach (var oc in otherCols)
                        if (mc != null && oc != null)
                            Physics2D.IgnoreCollision(mc, oc, true);
            }
        }

        private static void DestroyImmediateComponent<T>(GameObject go) where T : MonoBehaviour
        {
            var comp = go.GetComponentInChildren<T>(true);
            if (comp != null) Object.DestroyImmediate(comp);
        }

        private static void RestoreStatic(System.Type type, string field, object value)
        {
            if (value == null) return;
            try
            {
                var f = type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null && f.GetValue(null) != value)
                    f.SetValue(null, value);
            }
            catch { }
        }

        public static void DestroyAI()
        {
            if (_aiBody != null)
            {
                Object.Destroy(_aiBody.gameObject);
                _aiBody = null;
                BridgePlugin.Log?.LogInfo("[CU-MCP] AI player destroyed");
            }
        }
    }
}
