using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using CUMCP.Protocol;

namespace CUMCP.Contingency
{
    public class ContingencyRunner
    {
        private readonly List<ContingencyRule> _rules = new List<ContingencyRule>();

        public void UpdateRules(List<ContingencyRule> rules)
        {
            _rules.Clear();
            _rules.AddRange(rules);
        }

        public void ClearRules()
        {
            _rules.Clear();
        }

        public void Tick()
        {
            var body = AIPlayerManager.GetActiveBody();
            if (body == null || _rules.Count == 0) return;

            float totalHealth = 0;
            foreach (var limb in body.limbs)
                totalHealth += limb.skinHealth;

            foreach (var rule in _rules)
            {
                if (EvaluateCondition(rule.Condition, body, totalHealth))
                {
                    ExecuteAction(rule.Action, rule.Parameters, body);
                }
            }
        }

        private bool EvaluateCondition(string condition, Body body, float totalHealth)
        {
            if (condition == null) return false;

            var parts = condition.Split(' ');
            if (parts.Length < 3) return false;

            string variable = parts[0];
            string op = parts[1];
            if (!float.TryParse(parts[2], out float threshold)) return false;

            float value = GetVariable(variable, body, totalHealth);
            if (value < 0) return false;

            return op switch
            {
                "<" => value < threshold,
                ">" => value > threshold,
                "<=" => value <= threshold,
                ">=" => value >= threshold,
                "==" => Mathf.Approximately(value, threshold),
                _ => false
            };
        }

        private float GetVariable(string variable, Body body, float totalHealth)
        {
            return variable switch
            {
                "health" => totalHealth,
                "temperature" => body.temperature,
                "oxygen" => body.bloodOxygen,
                "hunger" => body.hunger,
                "thirst" => body.thirst,
                "stamina" => body.stamina,
                _ => -1f
            };
        }

        private void ExecuteAction(string action, JObject parameters, Body body)
        {
            switch (action)
            {
                case "use_item":
                    string itemId = parameters?["item"]?.ToString();
                    if (itemId != null)
                    {
                        for (int i = 0; i < body.slots.Length; i++)
                        {
                            var slot = body.slots[i];
                            if (slot != null)
                            {
                                var item = slot.GetComponent<Item>();
                                if (item != null && item.id == itemId)
                                {
                                    body.UseItem(item);
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case "move_to_safe":
                    var safePoint = FindSafeLocation(body);
                    if (safePoint != null)
                    {
                        Vector2 dir = (safePoint.Value - (Vector2)body.transform.position).normalized;
                        body.moveDir = dir;
                    }
                    break;
            }
        }

        private Vector2? FindSafeLocation(Body body)
        {
            var pos = body.transform.position;
            var worldGen = GameObject.FindObjectOfType<WorldGeneration>();
            if (worldGen == null) return null;

            for (int r = 1; r <= 10; r++)
            {
                for (int angle = 0; angle < 360; angle += 45)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    float nx = pos.x + Mathf.Cos(rad) * r;
                    float ny = pos.y + Mathf.Sin(rad) * r;
                    ushort blockId = worldGen.GetBlock(new Vector2Int(Mathf.RoundToInt(nx), Mathf.RoundToInt(ny)));
                    if (blockId == 0)
                    {
                        ushort belowId = worldGen.GetBlock(new Vector2Int(Mathf.RoundToInt(nx), Mathf.RoundToInt(ny) - 1));
                        if (belowId != 0)
                            return new Vector2(nx, ny);
                    }
                    else
                    {
                        var info = worldGen.GetBlockInfo(blockId);
                        if (info == null || info.name == "air")
                        {
                            ushort belowId = worldGen.GetBlock(new Vector2Int(Mathf.RoundToInt(nx), Mathf.RoundToInt(ny) - 1));
                            if (belowId != 0)
                                return new Vector2(nx, ny);
                        }
                    }
                }
            }
            return null;
        }
    }
}
