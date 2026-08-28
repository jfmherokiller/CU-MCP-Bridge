using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CUMCP;

namespace CUMCP.Executor
{
    public class Pathfinder
    {
        public enum JumpType { None, Normal, WallJump }

        public struct PathWaypoint
        {
            public Vector2 World;
            public bool Airborne;
            public Vector2? JumpDir;
            public JumpType JumpType;
        }

        public bool LastPathUsedCrouch { get; private set; }
        public List<Vector2> LastResult { get; private set; }
        public List<PathWaypoint> LastWaypoints { get; private set; }
        internal List<PathVisualizer.JumpMark> LastJumpMarks { get; private set; }

        private WorldGeneration _worldGen;
        private int _chainCount;

        // ── 2D Gravity-Aware A* ──

        private class GNode
        {
            public int X, Y;
            public float G, H;
            public float F => G + H;
            public GNode Parent;
            // When set, this node is the mid-air wall contact of a wall-jump chain
            // (takeoff -> wall contact -> landing). Points away from the wall.
            public Vector2? WallJumpDir;
        }

        private const int MAX_JUMP_UP = 6;
        private const int MAX_JUMP_DIST = 12;
        private const int MAX_WALL_JUMP_UP = 10;
        private const int SEARCH_MARGIN = 40;
        public const int MAX_FALL = 50;

        public IEnumerator FindPathCoroutine(Vector2 start, Vector2 end, int maxIterations = 50000)
        {
            LastResult = null;
            LastWaypoints = null;
            LastJumpMarks = null;
            _chainCount = 0;
            _worldGen = GameObject.FindObjectOfType<WorldGeneration>();
            if (_worldGen == null || !_worldGen.worldExists) yield break;

            var startBlock = _worldGen.WorldToBlockPos(start);
            var endBlock = _worldGen.WorldToBlockPos(end);

            // Find start surface
            int startY = FindSurface(startBlock.x, startBlock.y);
            if (startY == int.MinValue) yield break;
            int endY = FindSurface(endBlock.x, endBlock.y);
            if (endY == int.MinValue) { endY = endBlock.y; }

            _minX = Mathf.Max(1, Mathf.Min(startBlock.x, endBlock.x) - SEARCH_MARGIN);
            _maxX = Mathf.Min((int)_worldGen.width - 2, Mathf.Max(startBlock.x, endBlock.x) + SEARCH_MARGIN);

            var sNode = new GNode { X = startBlock.x, Y = startY };
            var eNode = new GNode { X = endBlock.x, Y = endY };
            var open = new List<GNode> { sNode };
            var closed = new Dictionary<long, float>();
            var openSet = new HashSet<long> { KeyG(sNode.X, sNode.Y) };

            int iter = 0;
            while (open.Count > 0 && iter++ < maxIterations)
            {
                open.Sort((a, b) => a.F.CompareTo(b.F));
                var cur = open[0]; open.RemoveAt(0);
                long ck = KeyG(cur.X, cur.Y);
                openSet.Remove(ck);
                if (closed.ContainsKey(ck)) continue;
                closed[ck] = cur.G;

                // Reached target?
                if (Mathf.Abs(cur.X - eNode.X) <= 1 && Mathf.Abs(cur.Y - eNode.Y) <= 2)
                {
                    var fn = ReconstructGNodes(cur);
                    fn = DropDescentCells(fn);
                    LastResult = NodesToWorld(fn);
                    LastWaypoints = BuildWaypoints(fn);
                    LastJumpMarks = CollectJumpPoints(fn);
                    if (PathVisualizer.Enabled) PathVisualizer.SetComplete(LastResult, LastJumpMarks);
                    UnityEngine.Debug.Log($"[CU-MCP] PATHFIND OK chains={_chainCount} iters={iter} start=({sNode.X},{sNode.Y}) end=({eNode.X},{eNode.Y})");
                    yield break;
                }

                bool grounded = IsSolid(cur.X, cur.Y - 1);

                if (grounded)
                {
                    // Walk / step off ledge (falls if no ground below)
                    for (int dx = -1; dx <= 1; dx += 2)
                    {
                        int wx = cur.X + dx;
                        if (wx < _minX || wx > _maxX) continue;
                        if (IsSolid(wx, cur.Y) || IsSolid(wx, cur.Y + 1)) continue;
                        TryAdd(open, openSet, closed, new GNode { X = wx, Y = cur.Y, G = cur.G + 1f, H = Manhattan(wx, cur.Y, eNode), Parent = cur });
                    }

                    // Jump arcs: cover every cell within jump range (including above cur)
                    for (int dx = -1; dx <= 1; dx += 2)
                    {
                        for (int d = 1; d <= MAX_JUMP_DIST; d++)
                        {
                            int jx = cur.X + dx * d;
                            if (jx < _minX || jx > _maxX) break;
                            for (int h = 0; h <= MAX_JUMP_UP; h++)
                            {
                                int jy = cur.Y + h;
                                if (IsSolid(jx, jy) || IsSolid(jx, jy + 1)) continue;
                                if (!JumpClear(cur, dx, d, h)) continue;
                                TryAdd(open, openSet, closed, new GNode { X = jx, Y = jy, G = cur.G + d + h * 1.5f, H = Manhattan(jx, jy, eNode), Parent = cur });
                            }
                        }
                    }

                    // Wall-jump chains: normal jump onto a wall, then wall-jump to a higher landing.
                    TryAddWallJumpChains(open, openSet, closed, cur, eNode);
                }
                else
                {
                    // Airborne: gravity only - drop straight down to the floor below
                    int fy = FindSurface(cur.X, cur.Y);
                    if (fy != int.MinValue && fy != cur.Y)
                        TryAdd(open, openSet, closed, new GNode { X = cur.X, Y = fy, G = cur.G + (cur.Y - fy) * 0.3f, H = Manhattan(cur.X, fy, eNode), Parent = cur });
                }

                // Visualizer update
                if (PathVisualizer.Enabled)
                {
                    var cn = ReconstructGNodes(cur);
                    PathVisualizer.UpdateState(
                        new Vector2Int(cur.X, cur.Y),
                        ConvertOpenG(open), ConvertClosedG(closed),
                        NodesToWorld(cn),
                        CollectJumpPoints(cn),
                        new Vector2Int(startBlock.x, startY),
                        new Vector2Int(endBlock.x, endY), false
                    );
                    yield return new WaitForSeconds(PathVisualizer.StepDelay);
                }
            }
            if (PathVisualizer.Enabled) PathVisualizer.Clear();
            UnityEngine.Debug.Log($"[CU-MCP] PATHFIND FAIL chains={_chainCount} iters={iter} start=({sNode.X},{sNode.Y}) end=({eNode.X},{eNode.Y})");
        }

        private float Manhattan(int x, int y, GNode target) => Mathf.Abs(x - target.X) + Mathf.Abs(y - target.Y) * 2f;

        // Scans for a wall within jump range in direction dx. If the wall is reachable by a
        // normal jump and a wall-jump from the wall face can land on a grounded cell that is
        // above normal jump reach (up to MAX_WALL_JUMP_UP), inserts the chain
        //   takeoff --(normal jump)--> wall contact --(wall jump)--> landing
        private void TryAddWallJumpChains(List<GNode> open, HashSet<long> openSet, Dictionary<long, float> closed,
            GNode cur, GNode eNode)
        {
            for (int dx = -1; dx <= 1; dx += 2)
            {
                for (int wd = 1; wd <= MAX_JUMP_DIST; wd++)
                {
                    int wx = cur.X + dx * wd;
                    if (wx < _minX || wx > _maxX) break;

                    // Wall base: the first solid cell in this column from the takeoff plane upward.
                    // Wall-jump works even when the wall does NOT start at the takeoff's plane -
                    // you jump up onto the wall, then double-jump (wall-jump) off its face.
                    int wallBase = cur.Y;
                    while (wallBase <= cur.Y + MAX_WALL_JUMP_UP && !IsSolid(wx, wallBase))
                        wallBase++;
                    if (wallBase > cur.Y + MAX_WALL_JUMP_UP) continue;

                    int wallTop = FindWallTopY(wx, wallBase);
                    int baseOff = wallBase - cur.Y;

                    // The normal jump onto the wall can contact it high when the wall is close.
                    // The wall-jump from that contact then adds its own height, so the whole chain
                    // reaches up to MAX_WALL_JUMP_UP landings.
                    int contactMax = Mathf.Max(1, MAX_WALL_JUMP_UP - (wd - 1) - baseOff);
                    for (int ct = 1; ct <= contactMax; ct++)
                    {
                        int cx = wx - dx; // airborne cell hugging the wall
                        int cy = wallBase + ct;
                        if (cx < _minX || cx > _maxX) continue;
                        if (IsSolid(cx, cy) || IsSolid(cx, cy + 1)) continue; // contact cell must be open
                        if (!JumpClearFrom(cur.X, cur.Y, dx, wd - 1, cy - cur.Y)) continue; // jump up to the wall face
                        // The normal jump rises out of the takeoff column - the headroom above it
                        // must be clear, otherwise the body clips a ceiling right after takeoff.
                        bool headroomClear = true;
                        for (int yy = cur.Y + 1; yy <= cy; yy++)
                        {
                            if (IsSolid(cur.X, yy)) { headroomClear = false; break; }
                        }
                        if (!headroomClear) continue;

                        // Option A: land on the wall's own top. The wall has width, so you can
                        // wall-jump off the face and come down on top of it.
                        if (wallTop > wallBase)
                        {
                            int ly = wallTop + 1;
                            int h = ly - cy;
                            if (ly <= cur.Y + MAX_WALL_JUMP_UP && h >= 1 && h <= MAX_JUMP_UP
                                && !IsSolid(wx, ly) && !IsSolid(wx, ly + 1)
                                && JumpClearFrom(cx, cy, -dx, 2, h))
                            {
                                var contact = new GNode { X = cx, Y = cy, G = cur.G + wd + ct * 1.5f, H = Manhattan(cx, cy, eNode), Parent = cur, WallJumpDir = new Vector2(-dx, 0f) };
                                var landing = new GNode { X = wx, Y = ly, G = cur.G + wd + ct * 1.5f + 1 + h * 1.5f, H = Manhattan(wx, ly, eNode), Parent = contact };
                                TryAdd(open, openSet, closed, landing);
                                _chainCount++;
                                if (_chainCount <= 20)
                                    UnityEngine.Debug.Log($"[CU-MCP] CHAIN takeoff=({cur.X},{cur.Y}) wallTop=({wx},{wallTop}) contact=({cx},{cy}) landingOnTop=({wx},{ly}) ct={ct} h={h} totalRise={ly - cur.Y}");
                                return;
                            }
                        }

                        // Option B: land away from the wall on a separate platform.
                        for (int d = 1; d <= MAX_JUMP_DIST; d++)
                        {
                            int lx = cx - dx * d;
                            if (lx < _minX || lx > _maxX) break;
                            // Only interested in landings above normal single-jump reach. The wall-jump
                            // rise is bounded by the same physics as a normal jump (MAX_JUMP_UP).
                            int contactRise = cy - cur.Y;
                            int hMin = Mathf.Max(1, MAX_JUMP_UP + 1 - contactRise);
                            int hMax = Mathf.Min(MAX_WALL_JUMP_UP - contactRise, MAX_JUMP_UP);
                            for (int h = hMin; h <= hMax; h++)
                            {
                                int ly = cy + h;
                                if (ly > cur.Y + MAX_WALL_JUMP_UP) continue;
                                if (!IsSolid(lx, ly - 1)) continue; // landing must be grounded
                                if (IsSolid(lx, ly) || IsSolid(lx, ly + 1)) continue;
                                if (!JumpClearFrom(cx, cy, -dx, d, h)) continue; // wall-jump arc away from wall

                                // Chain: takeoff -> contact -> landing
                                var contact = new GNode { X = cx, Y = cy, G = cur.G + wd + ct * 1.5f, H = Manhattan(cx, cy, eNode), Parent = cur, WallJumpDir = new Vector2(-dx, 0f) };
                                var landing = new GNode { X = lx, Y = ly, G = cur.G + wd + ct * 1.5f + d + h * 1.5f, H = Manhattan(lx, ly, eNode), Parent = contact };
                                TryAdd(open, openSet, closed, landing);
                                _chainCount++;
                                if (_chainCount <= 20)
                                    UnityEngine.Debug.Log($"[CU-MCP] CHAIN takeoff=({cur.X},{cur.Y}) wall=({wx},{cur.Y}) contact=({cx},{cy}) landing=({lx},{ly}) ct={ct} h={h} d={d} totalRise={cy + h - cur.Y}");
                                return; // one chain per direction is enough
                            }
                        }
                    }
                }
            }
        }

        private int FindWallTopY(int x, int fromY)
        {
            int top = fromY;
            for (int y = fromY; y <= fromY + MAX_WALL_JUMP_UP + 2; y++)
            {
                if (!IsSolid(x, y)) break;
                top = y;
            }
            return top;
        }

        // Jump clearance for an arc that starts at (sx, sy) and travels dist cells in direction dx
        // while rising up to maxH cells. The starting cell itself is not re-checked.
        private bool JumpClearFrom(int sx, int sy, int dx, int dist, int maxH)
        {
            if (dist <= 0) return true; // target is the adjacent cell - nothing in between to clear
            for (int cxx = sx + dx; cxx != sx + dx * dist; cxx += dx)
                for (int yy = sy; yy <= sy + maxH; yy++)
                    if (IsSolid(cxx, yy)) return false;
            return true;
        }

        private bool JumpClear(GNode cur, int dx, int d, int h)
        {
            int yMax = Mathf.Max(1, h);
            for (int cxx = cur.X + dx; cxx != cur.X + dx * d; cxx += dx)
                for (int yy = cur.Y; yy <= cur.Y + yMax; yy++)
                    if (IsSolid(cxx, yy)) return false;
            return true;
        }

        private void TryAdd(List<GNode> open, HashSet<long> openSet, Dictionary<long, float> closed, GNode node)
        {
            long k = KeyG(node.X, node.Y);
            if (closed.ContainsKey(k)) return;
            if (!openSet.Contains(k)) { open.Add(node); openSet.Add(k); }
        }

        private int FindSurface(int x, int nearY)
        {
            for (int y = nearY; y > nearY - 50; y--)
                if (IsSolid(x, y) && !IsSolid(x, y + 1)) return y + 1;
            for (int y = nearY; y < nearY + 30; y++)
                if (IsSolid(x, y) && !IsSolid(x, y + 1)) return y + 1;
            return int.MinValue;
        }

        private List<Vector2Int> ConvertOpenG(List<GNode> nodes)
        {
            var r = new List<Vector2Int>(nodes.Count);
            foreach (var n in nodes) r.Add(new Vector2Int(n.X, n.Y));
            return r;
        }

        private List<Vector2Int> ConvertClosedG(Dictionary<long, float> dict)
        {
            var r = new List<Vector2Int>(dict.Count);
            foreach (long k in dict.Keys) r.Add(new Vector2Int((int)(k >> 32), (int)(k & 0xFFFFFFFF)));
            return r;
        }

        private List<GNode> ReconstructGNodes(GNode node)
        {
            var nodes = new List<GNode>();
            while (node != null) { nodes.Add(node); node = node.Parent; }
            nodes.Reverse();
            return nodes;
        }

        private List<Vector2> NodesToWorld(List<GNode> nodes)
        {
            var path = new List<Vector2>(nodes.Count);
            foreach (var n in nodes) path.Add(_worldGen.BlockToWorldPos(new Vector2Int(n.X, n.Y)));
            return path;
        }

        private List<PathWaypoint> BuildWaypoints(List<GNode> nodes)
        {
            var list = new List<PathWaypoint>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                bool grounded = IsSolid(n.X, n.Y - 1);
                Vector2? jumpDir = null;
                JumpType jumpType = JumpType.None;

                if (grounded)
                {
                    // The landing is the next grounded node after n.
                    for (int k = i + 1; k < nodes.Count; k++)
                    {
                        if (IsSolid(nodes[k].X, nodes[k].Y - 1))
                        {
                            var l = nodes[k];
                            // Only mark a real jump-up (or a same-height gap hop), never a descent -
                            // a landing node that drops back down afterwards must not become a takeoff.
                            if (l.Y > n.Y + 1 || (Mathf.Abs(l.X - n.X) > 1 && l.Y >= n.Y))
                                jumpDir = new Vector2(l.X - n.X, l.Y - n.Y);
                            break;
                        }
                    }
                }
                else if (n.WallJumpDir.HasValue)
                {
                    // Mid-air wall contact of a wall-jump chain.
                    jumpDir = n.WallJumpDir;
                    jumpType = JumpType.WallJump;
                }

                // Post-process: a takeoff point whose jump height is 0 (same-plane landing)
                // is only a leftover of scanning above the node - drop it (but not wall-jump contacts,
                // which legitimately launch horizontally away from a wall).
                if (jumpDir.HasValue && jumpDir.Value.y == 0f && jumpType != JumpType.WallJump)
                    jumpDir = null;

                list.Add(new PathWaypoint { World = _worldGen.BlockToWorldPos(new Vector2Int(n.X, n.Y)), Airborne = !grounded, JumpDir = jumpDir, JumpType = jumpType });
            }
            return list;
        }

        // Downward movement carries no search meaning - gravity drops the body on its own.
        // Keep only cells the executor actually steers against: grounded walk cells and
        // mid-air wall-jump contacts (where the executor launches off the wall). Pure
        // mid-air drop cells between two landings are removed.
        private List<GNode> DropDescentCells(List<GNode> nodes)
        {
            var kept = new List<GNode>(nodes.Count);
            foreach (var n in nodes)
            {
                if (IsSolid(n.X, n.Y - 1) || n.WallJumpDir.HasValue)
                    kept.Add(n);
            }
            return kept;
        }

        private List<PathVisualizer.JumpMark> CollectJumpPoints(List<GNode> nodes)
        {
            var wps = BuildWaypoints(nodes);
            var pts = new List<PathVisualizer.JumpMark>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (wps[i].JumpDir.HasValue)
                    pts.Add(new PathVisualizer.JumpMark { Block = new Vector2Int(nodes[i].X, nodes[i].Y), Dir = wps[i].JumpDir.Value });
            }
            return pts;
        }

        private long KeyG(int x, int y) => ((long)x << 32) | (uint)y;

        private int _minX, _maxX;

        private bool IsSolid(int x, int y)
        {
            if (_worldGen == null) return true;
            try
            {
                ushort id = _worldGen.GetBlock(new Vector2Int(x, y));
                if (id == 0) return false;
                var info = _worldGen.GetBlockInfo(id);
                return info != null && !string.IsNullOrEmpty(info.name) && info.name != "air";
            }
            catch { return true; }
        }

        // ── Legacy surface-based fallback ──
        private const int LEGACY_JUMP_H = 8;
        private const int LEGACY_STEP = 1;
        private const int LEGACY_JMP_DIST = 15;
        private class LNode { public int X, GY; public float G, H; public float F => G + H; public LNode P; }
        private int _fh, _hc;

        public List<Vector2> FindPath(Vector2 start, Vector2 end, int maxIter = 20000)
        {
            _worldGen = GameObject.FindObjectOfType<WorldGeneration>();
            if (_worldGen == null || !_worldGen.worldExists) return null;
            var eb = _worldGen.WorldToBlockPos(end);
            LastPathUsedCrouch = false;
            _hc = 1; _fh = 5;
            var r = TryLegacy(start, end, eb.y, maxIter); if (r != null) return r;
            _hc = 0; r = TryLegacy(start, end, eb.y, maxIter); if (r != null) { LastPathUsedCrouch = true; return r; }
            _hc = 1; _fh = 5;
            int fb = FallbackGround(eb); if (fb == int.MinValue) return null;
            r = TryLegacy(start, end, fb, maxIter); if (r != null) return AppendElevated(r, end);
            _hc = 0; r = TryLegacy(start, end, fb, maxIter); if (r != null) { LastPathUsedCrouch = true; return AppendElevated(r, end); }
            _fh = 50; r = TryLegacy(start, end, fb, maxIter); if (r != null) { LastPathUsedCrouch = true; return AppendElevated(r, end); }
            return null;
        }

        private int FallbackGround(Vector2Int eb)
        {
            if (IsSolid(eb.x, eb.y - 1) && !IsSolid(eb.x, eb.y) && !IsSolid(eb.x, eb.y + 1)) return eb.y;
            int g = FindGroundLevel(eb.x, eb.y); if (g == int.MinValue) g = NearestGround(eb.x, eb.y);
            return g;
        }

        private List<Vector2> TryLegacy(Vector2 start, Vector2 end, int egY, int maxI)
        {
            if (_worldGen == null) return null;
            var sb = _worldGen.WorldToBlockPos(start);
            int sg = FindGroundLevel(sb.x, sb.y); if (sg == int.MinValue) return null;
            var eb = _worldGen.WorldToBlockPos(end);
            var sn = new LNode { X = sb.x, GY = sg }; var en = new LNode { X = eb.x, GY = egY };
            var op = new List<LNode> { sn }; var cl = new Dictionary<long, float>(); var os = new HashSet<long> { KeyL(sn) };
            int it = 0;
            while (op.Count > 0 && it++ < maxI)
            {
                op.Sort((a, b) => a.F.CompareTo(b.F));
                var c = op[0]; op.RemoveAt(0); long ck = KeyL(c); os.Remove(ck);
                if (Mathf.Abs(c.X - en.X) <= 0 && Mathf.Abs(c.GY - en.GY) <= 1) return ReconL(c);
                if (cl.TryGetValue(ck, out float eg) && eg <= c.G) continue;
                cl[ck] = c.G;
                for (int dx = -1; dx <= 1; dx += 2)
                {
                    var jp = JumpL(c.X, c.GY, dx, en); if (jp == null) continue;
                    float co = c.G + jp.Value.Cost;
                    float hh = Mathf.Abs(jp.Value.X - en.X) + Mathf.Abs(jp.Value.GY - en.GY) * 0.5f;
                    var nn = new LNode { X = jp.Value.X, GY = jp.Value.GY, G = co, H = hh, P = c };
                    long nk = KeyL(nn); if (cl.TryGetValue(nk, out float cg) && cg <= nn.G) continue;
                    var ex = op.Find(n => n.X == nn.X && n.GY == nn.GY);
                    if (ex == null) { if (!os.Contains(nk)) { op.Add(nn); os.Add(nk); } }
                    else if (nn.G < ex.G) { ex.G = nn.G; ex.P = nn.P; }
                }
            }
            return null;
        }

        private struct JR { public int X, GY; public float Cost; }
        private JR? JumpL(int sx, int gy, int dx, LNode en)
        {
            JR? lf = null;
            for (int d = 1; d <= LEGACY_JMP_DIST; d++)
            {
                int nx = sx + dx * d; int ty = FindGroundLevel(nx, gy);
                if (ty == int.MinValue) return lf;
                int hd = ty - gy; float bc = d * 1f;
                if (hd == 0)
                {
                    if (IsSolid(nx, gy) || IsSolid(nx, gy + _hc)) return lf;
                    if (Mathf.Abs(nx - en.X) <= 0 && Mathf.Abs(ty - en.GY) <= 1) return new JR { X = nx, GY = ty, Cost = bc };
                    int ny = FindGroundLevel(nx + dx, gy);
                    if (ny != int.MinValue && ny != ty) return new JR { X = nx, GY = ty, Cost = bc };
                    lf = new JR { X = nx, GY = ty, Cost = bc };
                }
                else if (hd > LEGACY_STEP && hd <= LEGACY_JUMP_H) { if (!IsSolid(nx, ty + _hc)) return new JR { X = nx, GY = ty, Cost = bc + 2f + hd * 1.5f }; return lf; }
                else if (hd >= -_fh && hd < 0) { if (IsSolid(nx, ty) || IsSolid(nx, ty + 1)) return lf; return new JR { X = nx, GY = ty, Cost = bc + Mathf.Abs(hd) * 0.3f }; }
                else return lf;
            }
            return lf;
        }

        private int FindGroundLevel(int x, int sy)
        {
            for (int y = sy; y > sy - 100; y--) if (IsSolid(x, y) && !IsSolid(x, y + 1) && !IsSolid(x, y + 1 + _hc)) return y + 1;
            for (int y = sy; y < sy + 100; y++) if (IsSolid(x, y) && !IsSolid(x, y + 1) && !IsSolid(x, y + 1 + _hc)) return y + 1;
            return int.MinValue;
        }

        private int NearestGround(int x, int y) { for (int d = 0; d <= 10; d++) { int r = FindGroundLevel(x, y + d); if (r != int.MinValue) return r; r = FindGroundLevel(x, y - d); if (r != int.MinValue) return r; } return int.MinValue; }
        private long KeyL(LNode n) => ((long)n.X << 32) | (uint)n.GY;
        private List<Vector2> ReconL(LNode n) { var p = new List<Vector2>(); while (n != null) { p.Add(_worldGen.BlockToWorldPos(new Vector2Int(n.X, n.GY))); n = n.P; } p.Reverse(); return p; }
        private List<Vector2> AppendElevated(List<Vector2> p, Vector2 t) { if (p == null || p.Count == 0) return p; var l = p[p.Count - 1]; if (t.y > l.y + 2f || t.y < l.y - 2f) p.Add(t); return p; }
    }
}