using System.Collections.Generic;
using UnityEngine;
using CUMCP.Protocol;

namespace CUMCP.Collector
{
    public class PlayerSnapshot
    {
        public PlayerState Collect(Body body = null)
        {
            if (body == null) body = AIPlayerManager.GetActiveBody();
            if (body == null) return null;

            float totalHealth = 0;
            float maxHealth = 0;
            foreach (var limb in body.limbs)
            {
                totalHealth += limb.skinHealth;
                maxHealth += 100f;
            }

            var state = new PlayerState
            {
                Position = new PlayerPosition
                {
                    X = body.transform.position.x,
                    Y = body.transform.position.y
                },
                Health = totalHealth,
                MaxHealth = maxHealth,
                Temperature = body.temperature,
                Oxygen = body.bloodOxygen,
                Items = new List<string>(),
                CurrentAction = body.conscious ? "active" : "unconscious"
            };

            foreach (var slot in body.slots)
            {
                if (slot?.GetComponent<Item>() is Item item)
                    state.Items.Add(item.id);
            }

            return state;
        }
    }
}
