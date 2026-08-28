using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using CUMCP.Protocol;
using CUMCP.Executor;

namespace CUMCP
{
    internal class DebugGUI : MonoBehaviour
    {
        private bool _showDebug;
        private Rect _winRect = new Rect(20, 40, 500, 600);
        private Vector2 _scroll;
        private Vector2 _logScroll;
        private bool _logAutoScroll = true;
        private List<string> _logBuffer = new List<string>();
        private const int MaxLogLines = 300;

        private enum Tab { PlayerState, AIState, Actions, Log }
        private Tab _currentTab = Tab.PlayerState;

        private string _waitSeconds = "1";
        private string _itemId = "bandage";
        private string _slotIndex = "0";
        private string _searchRange = "3";
        private string _healLimb = "Head";
        private string _healItem = "bandage";
        private string _moveX = "0";
        private string _moveY = "0";

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) _showDebug = !_showDebug;
        }

        void OnGUI()
        {
            PathVisualizer.Draw();
            if (!_showDebug) return;
            _winRect = GUI.Window(1000, _winRect, DrawWindow, "CU-MCP-Bridge 调试面板");
        }

        public void AddLog(string msg)
        {
            string line = "[" + System.DateTime.Now.ToString("HH:mm:ss") + "] " + msg;
            _logBuffer.Add(line);
            if (_logBuffer.Count > MaxLogLines)
                _logBuffer.RemoveRange(0, _logBuffer.Count - MaxLogLines);
            if (_logAutoScroll) _logScroll.y = float.MaxValue;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            DrawTabs();
            GUILayout.Space(4);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_currentTab)
            {
                case Tab.PlayerState: DrawPlayerState(); break;
                case Tab.AIState: DrawAIState(); break;
                case Tab.Actions: DrawActions(); break;
                case Tab.Log: DrawLog(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("玩家状态", _currentTab == Tab.PlayerState ? TabActive() : TabInactive())) _currentTab = Tab.PlayerState;
            if (GUILayout.Button("AI状态", _currentTab == Tab.AIState ? TabActive() : TabInactive())) _currentTab = Tab.AIState;
            if (GUILayout.Button("操作", _currentTab == Tab.Actions ? TabActive() : TabInactive())) _currentTab = Tab.Actions;
            if (GUILayout.Button("日志", _currentTab == Tab.Log ? TabActive() : TabInactive())) _currentTab = Tab.Log;
            GUILayout.EndHorizontal();
        }

        private GUIStyle TabActive()
        {
            return new GUIStyle(GUI.skin.button) { normal = { textColor = Color.yellow, background = GUI.skin.button.active.background } };
        }

        private GUIStyle TabInactive()
        {
            return GUI.skin.button;
        }

        private void DrawPlayerInfo(Body body, string label)
        {
            if (body == null) { GUILayout.Label(label + ": 不存在"); return; }
            var pos = body.transform.position;
            GUILayout.Label(string.Format("{0} 位置: ({1:F1}, {2:F1})", label, pos.x, pos.y));
            GUILayout.Label(string.Format("{0} 大脑: {1:F0}", label, body.brainHealth));
            GUILayout.Label(string.Format("{0} 温度: {1:F1}", label, body.temperature));
            GUILayout.Label(string.Format("{0} 含氧量: {1:F1}", label, body.bloodOxygen));
            GUILayout.Label(string.Format("{0} 潮湿: {1:F1}", label, body.wetness));
            GUILayout.Label(string.Format("{0} 意识: {1}", label, body.conscious ? "清醒" : "昏迷"));
            GUILayout.Label(string.Format("{0} 地面: {1}", label, body.grounded ? "着地" : "空中"));
            GUILayout.Label(string.Format("{0} 站立: {1}", label, body.standing ? "站立" : "蹲下"));
            if (body.rb != null)
                GUILayout.Label(string.Format("{0} 速度: ({1:F1}, {2:F1})", label, body.rb.velocity.x, body.rb.velocity.y));
            GUILayout.Label(string.Format("{0} 最大速度: {1:F1}", label, body.actualMaxSpeed));

            float totalSkin = 0, totalMuscle = 0;
            int brokenLimbs = 0, dismemberedLimbs = 0;
            foreach (var limb in body.limbs)
            {
                if (limb == null) continue;
                totalSkin += limb.skinHealth;
                totalMuscle += limb.muscleHealth;
                if (limb.broken) brokenLimbs++;
                if (limb.dismembered) dismemberedLimbs++;
            }
            GUILayout.Label(string.Format("{0} 皮肤: {1:F0}/{2:F0}", label, totalSkin, body.limbs.Length * 100f));
            GUILayout.Label(string.Format("{0} 肌肉: {1:F0}/{2:F0}", label, totalMuscle, body.limbs.Length * 100f));
            if (brokenLimbs > 0) GUILayout.Label(label + " 骨折: " + brokenLimbs);
            if (dismemberedLimbs > 0) GUILayout.Label(label + " 肢解: " + dismemberedLimbs);

            GUILayout.Label(label + " 物品:");
            for (int i = 0; i < body.slots.Length; i++)
            {
                var slot = body.slots[i];
                if (slot == null) continue;
                var item = slot.GetComponent<Item>();
                if (item != null)
                    GUILayout.Label("  [" + i + "] " + item.id + " (" + item.Stats.fullName + ")");
            }
        }

        private void DrawPlayerState()
        {
            var body = AIPlayerManager.HumanBody;
            DrawPlayerInfo(body, "玩家");
        }

        private void DrawAIState()
        {
            var body = AIPlayerManager.AIBody;
            DrawPlayerInfo(body, "AI");
        }

        private void DrawActions()
        {
            GUILayout.Label("=== AI管理 ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建AI"))
            {
                var human = AIPlayerManager.HumanBody;
                if (human != null)
                {
                    var pos = human.transform.position;
                    pos.x += 2f;
                    AIPlayerManager.CreateAI(pos);
                }
            }
            if (GUILayout.Button("销毁AI")) AIPlayerManager.DestroyAI();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("=== 移动 ===");
            GUILayout.BeginHorizontal();
            GUILayout.Label("X:", GUILayout.Width(20));
            _moveX = GUILayout.TextField(_moveX, GUILayout.Width(50));
            GUILayout.Label("Y:", GUILayout.Width(20));
            _moveY = GUILayout.TextField(_moveY, GUILayout.Width(50));
            if (GUILayout.Button("移动到")) DoMoveTo();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("移动到玩家", GUILayout.Height(25))) DoMoveToPlayer();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("跳跃左")) DoJump(-1, 0);
            if (GUILayout.Button("跳跃上")) DoJump(0, 0);
            if (GUILayout.Button("跳跃右")) DoJump(1, 0);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("跟踪")) { DoFollow(); }
            if (GUILayout.Button("停止跟踪")) { if (BridgePlugin.Instance?.GetExecutor() != null) BridgePlugin.Instance.GetExecutor().CancelCurrent(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("=== 物品 ===");
            GUILayout.BeginHorizontal();
            GUILayout.Label("物品ID:", GUILayout.Width(60));
            _itemId = GUILayout.TextField(_itemId, GUILayout.Width(120));
            if (GUILayout.Button("使用")) DoUseItem();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("槽位:", GUILayout.Width(40));
            _slotIndex = GUILayout.TextField(_slotIndex, GUILayout.Width(50));
            if (GUILayout.Button("丢弃")) DoDropItem();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("物品ID:", GUILayout.Width(60));
            _itemId = GUILayout.TextField(_itemId, GUILayout.Width(120));
            GUILayout.Label("范围:", GUILayout.Width(40));
            _searchRange = GUILayout.TextField(_searchRange, GUILayout.Width(40));
            if (GUILayout.Button("拾取")) DoPickUpItem();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("=== 寻路可视化 ===");
            PathVisualizer.Enabled = GUILayout.Toggle(PathVisualizer.Enabled, "开启寻路可视化 (逐帧显示A*过程)");
            if (PathVisualizer.Enabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("每步等待:", GUILayout.Width(70));
                string speedStr = GUILayout.TextField((PathVisualizer.StepDelay * 1000).ToString("F0"), GUILayout.Width(60));
                GUILayout.Label("毫秒", GUILayout.Width(40));
                float ms;
                if (float.TryParse(speedStr, out ms) && ms >= 1)
                    PathVisualizer.StepDelay = ms / 1000f;
                GUILayout.EndHorizontal();
                GUILayout.Label("迭代: " + PathVisualizer.IterCount + "  |  Open: " + PathVisualizer.IterCount + "  |  Closed: ~");
                if (GUILayout.Button("清除可视化")) PathVisualizer.Clear();
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("部位:", GUILayout.Width(40));
            _healLimb = GUILayout.TextField(_healLimb, GUILayout.Width(80));
            GUILayout.Label("物品:", GUILayout.Width(40));
            _healItem = GUILayout.TextField(_healItem, GUILayout.Width(80));
            if (GUILayout.Button("治疗")) DoHealAI();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("=== 其他 ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("睡眠")) DoSleep();
            GUILayout.Label("等待秒数:", GUILayout.Width(70));
            _waitSeconds = GUILayout.TextField(_waitSeconds, GUILayout.Width(50));
            if (GUILayout.Button("等待")) DoWait();
            GUILayout.EndHorizontal();
        }

        private void DrawLog()
        {
            _logAutoScroll = GUILayout.Toggle(_logAutoScroll, "自动滚动");
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(Mathf.Max(200, _winRect.height - 120)));
            for (int i = 0; i < _logBuffer.Count; i++)
                GUILayout.Label(_logBuffer[i]);
            GUILayout.EndScrollView();
        }

        private void DoAction(string action, JObject parameters)
        {
            var executor = BridgePlugin.Instance?.GetExecutor();
            if (executor == null) return;
            executor.ExecuteOrder(new OrderParams
            {
                Action = action,
                Id = "gui_" + action,
                Parameters = parameters
            });
        }

        private void DoMoveTo()
        {
            float x, y;
            if (float.TryParse(_moveX, out x) && float.TryParse(_moveY, out y))
            {
                var p = new JObject { ["x"] = x, ["y"] = y, ["arrival_distance"] = 1.5, ["pathfind"] = true };
                DoAction("move_to", p);
            }
        }

        private void DoMoveToPlayer()
        {
            var human = AIPlayerManager.HumanBody;
            if (human == null) return;
            var p = new JObject { ["x"] = human.transform.position.x, ["y"] = human.transform.position.y, ["arrival_distance"] = 1.5, ["pathfind"] = true };
            DoAction("move_to", p);
        }

        private void DoJump(float dirX, float hold)
        {
            var p = new JObject { ["x"] = dirX, ["hold_seconds"] = hold };
            DoAction("jump", p);
        }

        private void DoFollow()
        {
            var p = new JObject { ["distance"] = 2.0, ["max_ydiff"] = 3 };
            DoAction("follow", p);
        }

        private void DoUseItem()
        {
            var p = new JObject { ["item"] = _itemId };
            DoAction("use_item", p);
        }

        private void DoDropItem()
        {
            int slot;
            var p = new JObject();
            if (int.TryParse(_slotIndex, out slot))
                p["slot"] = slot;
            else
                p["item_id"] = _itemId;
            DoAction("drop_item", p);
        }

        private void DoPickUpItem()
        {
            float range;
            var p = new JObject { ["item"] = _itemId };
            if (float.TryParse(_searchRange, out range))
                p["search_range"] = range;
            DoAction("pick_up_item", p);
        }

        private void DoHealAI()
        {
            var p = new JObject { ["limb"] = _healLimb, ["item_id"] = _healItem };
            DoAction("heal_ai", p);
        }

        private void DoSleep()
        {
            DoAction("sleep", new JObject());
        }

        private void DoWait()
        {
            float sec;
            var p = new JObject { ["seconds"] = float.TryParse(_waitSeconds, out sec) ? sec : 1f };
            DoAction("wait", p);
        }
    }
}