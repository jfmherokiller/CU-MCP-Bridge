using System;
using System.Collections.Generic;
using UnityEngine;
using CUMCP.Protocol;

namespace CUMCP.Collector
{
    public class EnvironmentScan
    {
        public EnvironmentSnapshot Scan(Body body, float range = 20f, bool debug = false)
        {
            var snapshot = new EnvironmentSnapshot
            {
                Entities = new List<EntityInfo>(),
                NearbyTerrain = new List<TerrainBlock>()
            };

            if (body == null) return snapshot;

            if (WorldGeneration.world == null)
            {
                if (debug) Debug.Log("[CU-MCP-SCAN] WorldGeneration.world is null");
                return snapshot;
            }

            var pos = body.transform.position;
            var blockPos = WorldGeneration.world.WorldToBlockPos(pos);
            int cx = blockPos.x;
            int cy = blockPos.y;
            if (debug) Debug.Log($"[CU-MCP-SCAN] Player world=({pos.x:F1},{pos.y:F1}) block=({cx},{cy})");

            // Find entities
            var allLimbs = GameObject.FindObjectsOfType<Limb>();
            foreach (var limb in allLimbs)
            {
                if (limb.body == body) continue;
                var dist = Vector2.Distance(pos, limb.transform.position);
                if (dist > range) continue;

                snapshot.Entities.Add(new EntityInfo
                {
                    Id = limb.gameObject.name + "_" + limb.GetInstanceID(),
                    Type = IsEnemy(limb) ? "enemy" : "creature",
                    X = limb.transform.position.x,
                    Y = limb.transform.position.y,
                    Distance = dist,
                    Health = limb.skinHealth
                });
            }

            // Find terrain blocks using correct coordinate system
            int scanRadius = Mathf.RoundToInt(range);
            int blocksFound = 0;
            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                for (int dy = -scanRadius; dy <= scanRadius; dy++)
                {
                    try
                    {
                        int tx = cx + dx;
                        int ty = cy + dy;
                        ushort blockId = WorldGeneration.world.GetBlock(new Vector2Int(tx, ty));
                        if (blockId != 0)
                        {
                            var info = WorldGeneration.world.GetBlockInfo(blockId);
                            if (info != null && !string.IsNullOrEmpty(info.name) && info.name != "air")
                            {
                                snapshot.NearbyTerrain.Add(new TerrainBlock
                                {
                                    TileX = tx,
                                    TileY = ty,
                                    Material = info.name
                                });
                                blocksFound++;
                            }
                        }
                    }
                    catch { }
                }
            }
            if (debug) Debug.Log($"[CU-MCP-SCAN] Found {snapshot.Entities.Count} entities, {blocksFound} blocks");

            return snapshot;
        }

        public void ScanAtPosition(Vector2 center, float range, EnvironmentSnapshot result)
        {
            if (WorldGeneration.world == null) return;

            var blockPos = WorldGeneration.world.WorldToBlockPos(center);
            int cx = blockPos.x;
            int cy = blockPos.y;

            int scanRadius = Mathf.RoundToInt(range);
            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                for (int dy = -scanRadius; dy <= scanRadius; dy++)
                {
                    try
                    {
                        int tx = cx + dx;
                        int ty = cy + dy;
                        ushort blockId = WorldGeneration.world.GetBlock(new Vector2Int(tx, ty));
                        if (blockId != 0)
                        {
                            var info = WorldGeneration.world.GetBlockInfo(blockId);
                            if (info != null && !string.IsNullOrEmpty(info.name) && info.name != "air")
                            {
                                result.NearbyTerrain.Add(new TerrainBlock
                                {
                                    TileX = tx,
                                    TileY = ty,
                                    Material = info.name
                                });
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public SearchResults SearchBlocks(string material, Vector2 center, int range, int limit)
        {
            var result = new SearchResults
            {
                MaterialFilter = material,
                Matches = new List<SearchMatch>(),
                TotalScanned = 0,
                TotalMatched = 0
            };

            if (WorldGeneration.world == null) return result;

            var blockPos = WorldGeneration.world.WorldToBlockPos(center);
            int cx = blockPos.x;
            int cy = blockPos.y;

            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    result.TotalScanned++;
                    try
                    {
                        int tx = cx + dx;
                        int ty = cy + dy;
                        ushort blockId = WorldGeneration.world.GetBlock(new Vector2Int(tx, ty));
                        if (blockId != 0)
                        {
                            var info = WorldGeneration.world.GetBlockInfo(blockId);
                            if (info != null && !string.IsNullOrEmpty(info.name)
                                && info.name.IndexOf(material, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                result.TotalMatched++;
                                if (result.Matches.Count < limit)
                                {
                                    var worldPos = WorldGeneration.world.BlockToWorldPos(new Vector2Int(tx, ty));
                                    result.Matches.Add(new SearchMatch
                                    {
                                        TileX = tx,
                                        TileY = ty,
                                        Material = info.name,
                                        WorldX = worldPos.x,
                                        WorldY = worldPos.y
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            return result;
        }

        private bool IsEnemy(Limb limb)
        {
            if (limb.body == null) return false;
            return limb.body.gameObject.CompareTag("Enemy");
        }
    }
}
