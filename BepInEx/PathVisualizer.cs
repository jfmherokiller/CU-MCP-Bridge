using UnityEngine;
using System.Collections.Generic;

namespace CUMCP
{
    internal static class PathVisualizer
    {
        public static bool Enabled;
        public static float StepDelay = 0.1f;

        public struct JumpMark
        {
            public Vector2Int Block;
            public Vector2 Dir;
        }

        private static Vector2Int _current;
        private static List<Vector2Int> _open = new List<Vector2Int>();
        private static List<Vector2Int> _closed = new List<Vector2Int>();
        private static List<Vector2> _currentPath = new List<Vector2>();
        private static List<Vector2> _finalPath = new List<Vector2>();
        private static List<JumpMark> _jumpPoints = new List<JumpMark>();
        private static Vector2Int _startBlock;
        private static Vector2Int _endBlock;
        private static bool _hasState;

        public static int IterCount;

        public static void Clear()
        {
            _hasState = false;
            _current = default;
            _open.Clear();
            _closed.Clear();
            _currentPath.Clear();
            _finalPath.Clear();
            _jumpPoints.Clear();
            IterCount = 0;
        }

        public static void UpdateState(Vector2Int cur, List<Vector2Int> open, List<Vector2Int> closed,
            List<Vector2> path, List<JumpMark> jumpPoints, Vector2Int start, Vector2Int end, bool complete)
        {
            _current = cur;
            _open = open;
            _closed = closed;
            _currentPath = path;
            _jumpPoints = jumpPoints;
            _startBlock = start;
            _endBlock = end;
            _hasState = true;
            IterCount++;
            if (complete)
                _finalPath = path;
        }

        public static void SetComplete(List<Vector2> finalPath, List<JumpMark> jumpPoints)
        {
            _finalPath = finalPath;
            _jumpPoints = jumpPoints;
        }

        private static Texture2D _tex;
        private static Texture2D GetTex(Color c)
        {
            if (_tex == null)
            {
                _tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _tex.hideFlags = HideFlags.HideAndDontSave;
            }
            _tex.SetPixel(0, 0, c);
            _tex.Apply();
            return _tex;
        }

        public static void Draw()
        {
            if (!Enabled || !_hasState) return;
            var cam = Camera.main;
            if (cam == null) return;
            var worldGen = UnityEngine.Object.FindObjectOfType<WorldGeneration>();
            if (worldGen == null || !worldGen.worldExists) return;

            // Draw closed nodes (dark red, 8px)
            foreach (var n in _closed)
            {
                var sp = WorldToScreen(cam, worldGen.BlockToWorldPos(new Vector2Int(n.x, n.y)));
                if (sp.HasValue)
                    GUI.DrawTexture(new Rect(sp.Value.x - 4, sp.Value.y - 4, 8, 8), GetTex(new Color(0.3f, 0.06f, 0.06f, 0.5f)));
            }

            // Draw open nodes (blue, 10px)
            foreach (var n in _open)
            {
                var sp = WorldToScreen(cam, worldGen.BlockToWorldPos(new Vector2Int(n.x, n.y)));
                if (sp.HasValue)
                    GUI.DrawTexture(new Rect(sp.Value.x - 5, sp.Value.y - 5, 10, 10), GetTex(new Color(0.1f, 0.3f, 0.9f, 0.6f)));
            }

            // Draw current node (yellow, 14px)
            var curSp = WorldToScreen(cam, worldGen.BlockToWorldPos(new Vector2Int(_current.x, _current.y)));
            if (curSp.HasValue)
                GUI.DrawTexture(new Rect(curSp.Value.x - 7, curSp.Value.y - 7, 14, 14), GetTex(Color.yellow));

            // Draw current path (cyan line)
            if (_currentPath != null && _currentPath.Count > 1)
            {
                for (int i = 1; i < _currentPath.Count; i++)
                    DrawLineOnScreen(cam, _currentPath[i - 1], _currentPath[i], Color.cyan, 3f);
            }

            // Draw final path (green line) if complete
            if (_finalPath != null && _finalPath.Count > 1)
            {
                for (int i = 1; i < _finalPath.Count; i++)
                    DrawLineOnScreen(cam, _finalPath[i - 1], _finalPath[i], Color.green, 4f);
            }

            // Draw jump takeoff points (orange square + direction arrow + vector label)
            foreach (var jm in _jumpPoints)
            {
                var sp = WorldToScreen(cam, worldGen.BlockToWorldPos(new Vector2Int(jm.Block.x, jm.Block.y)));
                if (!sp.HasValue) continue;
                GUI.DrawTexture(new Rect(sp.Value.x - 6, sp.Value.y - 6, 12, 12), GetTex(new Color(1f, 0.55f, 0f, 0.95f)));

                if (jm.Dir.sqrMagnitude > 0.0001f)
                {
                    float bs = BlockSize(worldGen);
                    Vector2 n = jm.Dir.normalized;
                    Vector2 tailW = worldGen.BlockToWorldPos(new Vector2Int(jm.Block.x, jm.Block.y)) + n * bs * 0.35f;
                    Vector2 tipW = tailW + n * bs * 1.5f;
                    DrawArrowOnScreen(cam, tailW, tipW, new Color(1f, 0.55f, 0f, 1f), 3f);
                    GUI.Label(new Rect(sp.Value.x + 8, sp.Value.y - 8, 80, 16), "(" + (int)jm.Dir.x + "," + (int)jm.Dir.y + ")", GUI.skin.label);
                }
            }

            // Draw start/end markers (16px)
            var startSp = WorldToScreen(cam, worldGen.BlockToWorldPos(new Vector2Int(_startBlock.x, _startBlock.y)));
            if (startSp.HasValue)
                GUI.DrawTexture(new Rect(startSp.Value.x - 8, startSp.Value.y - 8, 16, 16), GetTex(new Color(0f, 1f, 0f, 0.8f)));
            var endSp = WorldToScreen(cam, worldGen.BlockToWorldPos(new Vector2Int(_endBlock.x, _endBlock.y)));
            if (endSp.HasValue)
                GUI.DrawTexture(new Rect(endSp.Value.x - 8, endSp.Value.y - 8, 16, 16), GetTex(new Color(1f, 0f, 0f, 0.8f)));

            DrawLegend();
        }

        private static Vector2? WorldToScreen(Camera cam, Vector2 worldPos)
        {
            var sp = cam.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0));
            if (sp.z < 0) return null;
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        private static void DrawLineOnScreen(Camera cam, Vector2 worldA, Vector2 worldB, Color color, float thickness)
        {
            var a = cam.WorldToScreenPoint(new Vector3(worldA.x, worldA.y, 0));
            var b = cam.WorldToScreenPoint(new Vector3(worldB.x, worldB.y, 0));
            a.y = Screen.height - a.y;
            b.y = Screen.height - b.y;
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float len = d.magnitude;
            var mat = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness / 2f, len, thickness), GetTex(color));
            GUI.matrix = mat;
        }

        private static float BlockSize(WorldGeneration wg)
        {
            var p1 = wg.BlockToWorldPos(new Vector2Int(0, 0));
            var p2 = wg.BlockToWorldPos(new Vector2Int(1, 0));
            return Vector2.Distance(p1, p2);
        }

        private static void DrawArrowOnScreen(Camera cam, Vector2 worldTail, Vector2 worldTip, Color color, float thickness)
        {
            DrawLineOnScreen(cam, worldTail, worldTip, color, thickness);
            Vector2 dir = worldTip - worldTail;
            float len = dir.magnitude;
            if (len < 0.001f) return;
            Vector2 n = dir / len;
            Vector2 perp = new Vector2(-n.y, n.x);
            float headLen = len * 0.35f;
            Vector2 headBase = worldTip - n * headLen;
            DrawLineOnScreen(cam, worldTip, headBase + perp * headLen * 0.5f, color, thickness);
            DrawLineOnScreen(cam, worldTip, headBase - perp * headLen * 0.5f, color, thickness);
        }

        private static void DrawLegend()
        {
            float x = 20;
            float y = Screen.height - 140;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            float lh = 17;
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 200, lh), "寻路可视化  (迭代: " + IterCount + ")", style); y += lh;
            DrawColorDot(x, y, new Color(1f, 0f, 0f), "终点"); y += lh;
            DrawColorDot(x, y, new Color(0f, 1f, 0f), "起点"); y += lh;
            DrawColorDot(x, y, Color.yellow, "当前节点"); y += lh;
            DrawColorDot(x, y, new Color(0.15f, 0.35f, 0.8f), "Open (待探索)"); y += lh;
            DrawColorDot(x, y, new Color(0.3f, 0.08f, 0.08f), "Closed (已探索)"); y += lh;
            DrawColorDot(x, y, new Color(1f, 0.55f, 0f), "起跳点(方块+方向)"); y += lh;
            DrawColorDot(x, y, Color.cyan, "当前路径");
        }

        private static void DrawColorDot(float x, float y, Color c, string label)
        {
            GUI.DrawTexture(new Rect(x, y, 10, 10), GetTex(c));
            GUI.Label(new Rect(x + 14, y - 1, 180, 12), label, GUI.skin.label);
        }
    }
}