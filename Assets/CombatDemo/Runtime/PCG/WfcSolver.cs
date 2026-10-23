using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Milkfrog.CombatDemo.PCG
{
    public enum WfcStepKind { Initialize, Collapse, Propagate, Backtrack, Retry, Complete, Failed }
    public sealed class WfcStep
    {
        public WfcStepKind kind;
        public int cell, attempt;
        public ulong[] domains;
    }
    [Serializable]
    public sealed class WfcStatistics
    {
        public int seed, attempts, backtracks, collapses, propagations, localSolutions;
        public int walkable, rooms, reachable, shortestPath, loops;
        public double milliseconds;
    }
    public sealed class WfcResult
    {
        public bool success;
        public string error;
        public int[] tiles, route, encounters;
        public int start, goal;
        public WfcStatistics stats = new WfcStatistics();
    }

    // Pure constraint solver: no GameObjects, physics, global Unity random state or disk I/O.
    public sealed class WfcSolver
    {
        public static readonly int[] Dx = { 0, 1, 0, -1 }, Dz = { 1, 0, -1, 0 };
        readonly WfcConfig config;
        readonly int seed, count;
        readonly bool constrained;
        readonly ulong[,] support;
        readonly ulong emptyMask, roomMask, all;
        readonly List<Decision> decisions = new List<Decision>();
        readonly Queue<int> queue = new Queue<int>();
        readonly bool[] queued;
        Random random;
        ulong[] wave;
        int start, goal;
        int[] encounters, required;
        int attemptBacktracks;
        public WfcResult Result { get; private set; } = new WfcResult();
        sealed class Decision { public ulong[] before; public int cell; public ulong remaining; }

        public WfcSolver(WfcConfig config, int seed, bool constrained = true)
        {
            this.config = config; this.seed = seed; this.constrained = constrained;
            if (config.Error() != null)
            {
                count = 0; queued = Array.Empty<bool>(); support = new ulong[0, 4]; return;
            }
            count = config.width * config.height; queued = new bool[count];
            int n = config.modules == null ? 0 : config.modules.Count;
            support = new ulong[Math.Min(n, 63), 4];
            if (n > 0 && n <= 63)
            {
                all = (1UL << n) - 1;
                for (int t = 0; t < n; t++)
                {
                    var module = config.modules[t]; if (module == null) continue;
                    if (module.ports == 0 && !module.room) emptyMask |= 1UL << t;
                    if (module.room) roomMask |= 1UL << t;
                    for (int d = 0; d < 4; d++)
                        for (int u = 0; u < n; u++)
                            if (config.modules[u] != null && ((module.ports >> d) & 1) == ((config.modules[u].ports >> ((d + 2) % 4)) & 1)) support[t, d] |= 1UL << u;
                }
            }
        }
        public WfcResult Solve()
        {
            foreach (var ignored in Steps(false)) { }
            return Result;
        }
        public static int Bits(ulong value) { int n = 0; while (value != 0) { value &= value - 1; n++; } return n; }
        public static int Tile(ulong value) { for (int t = 0; t < 63; t++) if ((value & (1UL << t)) != 0) return t; return -1; }
        public double Entropy(ulong domain)
        {
            double sum = 0, weightedLog = 0;
            for (int t = 0; t < config.modules.Count; t++) if ((domain & (1UL << t)) != 0)
            { double w = config.modules[t].weight; sum += w; weightedLog += w * Math.Log(w); }
            return sum <= 0 ? double.NegativeInfinity : Math.Log(sum) - weightedLog / sum;
        }
        int Neighbor(int cell, int direction)
        {
            int x = cell % config.width + Dx[direction], z = cell / config.width + Dz[direction];
            return x < 0 || z < 0 || x >= config.width || z >= config.height ? -1 : z * config.width + x;
        }
        void Enqueue(int cell) { if (!queued[cell]) { queued[cell] = true; queue.Enqueue(cell); } }
        bool Restrict(int cell, ulong mask)
        {
            ulong next = wave[cell] & mask;
            if (next != wave[cell]) { wave[cell] = next; Enqueue(cell); }
            return next != 0;
        }
        bool Propagate()
        {
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(); queued[cell] = false;
                if (wave[cell] == 0) return false;
                for (int d = 0; d < 4; d++)
                {
                    int next = Neighbor(cell, d); if (next < 0) continue;
                    ulong allowed = 0;
                    for (int t = 0; t < config.modules.Count; t++) if ((wave[cell] & (1UL << t)) != 0) allowed |= support[t, d];
                    if (!Restrict(next, allowed)) return false;
                }
                Result.stats.propagations++;
            }
            if (!constrained) return true;
            // Additional GLOBAL constraint, deliberately separate from ordinary WFC adjacency.
            // A committed walkable tile must still have a possible path to the start.
            var reached = new bool[count]; var open = new Queue<int>(); reached[start] = true; open.Enqueue(start);
            while (open.Count > 0)
            {
                int cell = open.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int next = Neighbor(cell, d);
                    if (next >= 0 && !reached[next] && CanOpen(wave[cell], d) && CanOpen(wave[next], (d + 2) % 4)) { reached[next] = true; open.Enqueue(next); }
                }
            }
            for (int i = 0; i < count; i++)
                if (!reached[i] && !Restrict(i, emptyMask)) return false;
            return queue.Count == 0 || Propagate();
        }
        bool CanOpen(ulong domain, int d)
        {
            for (int t = 0; t < config.modules.Count; t++) if ((domain & (1UL << t)) != 0 && (config.modules[t].ports & (1 << d)) != 0) return true;
            return false;
        }
        void Scaffold()
        {
            required = new int[count]; start = config.width + 1; goal = (config.height - 2) * config.width + config.width - 2;
            var main = new List<int> { start }; int cursor = start;
            while (cursor != goal)
            {
                bool east = cursor % config.width < config.width - 2;
                bool north = cursor / config.width < config.height - 2;
                cursor += !north || east && random.Next(2) == 0 ? 1 : config.width; main.Add(cursor);
            }
            Connect(main);
            // A second distinct monotone path provides a guaranteed loop, with no carved repair.
            var branch = new List<int> { start }; cursor = start;
            bool eastFirst = main[1] != start + 1;
            for (int phase = 0; phase < 2; phase++)
            {
                bool east = phase == 0 ? eastFirst : !eastFirst;
                while (east ? cursor % config.width < config.width - 2 : cursor / config.width < config.height - 2)
                { cursor += east ? 1 : config.width; branch.Add(cursor); }
            }
            Connect(branch);
            encounters = new[] { main[main.Count / 3], main[main.Count * 2 / 3] };
        }
        void Connect(List<int> route)
        {
            for (int k = 1; k < route.Count; k++)
            {
                int a = route[k - 1], b = route[k], d = b == a + 1 ? 1 : 0;
                required[a] |= 1 << d; required[b] |= 1 << ((d + 2) % 4);
            }
        }
        bool Initialize(int attempt)
        {
            random = new Random(unchecked(seed * 486187739 + attempt * 16777619));
            queue.Clear(); Array.Clear(queued, 0, count); decisions.Clear(); attemptBacktracks = 0;
            wave = new ulong[count]; for (int i = 0; i < count; i++) wave[i] = all;
            if (constrained) Scaffold();
            for (int i = 0; i < count; i++)
            {
                ulong valid = 0;
                for (int t = 0; t < config.modules.Count; t++)
                {
                    int p = config.modules[t].ports; bool ok = true;
                    for (int d = 0; d < 4; d++) if (Neighbor(i, d) < 0 && (p & (1 << d)) != 0) ok = false;
                    if (constrained && (p & required[i]) != required[i]) ok = false;
                    if (ok) valid |= 1UL << t;
                }
                if (constrained && (i == start || i == goal || Array.IndexOf(encounters, i) >= 0)) valid &= roomMask;
                wave[i] &= valid; Enqueue(i);
            }
            return Propagate();
        }
        int Select()
        {
            int chosen = -1; double best = double.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (wave[i] == 0) return -2;
                if (Bits(wave[i]) <= 1) continue;
                double h = Entropy(wave[i]) + random.NextDouble() * 1e-7;
                if (h < best) { best = h; chosen = i; }
            }
            return chosen;
        }
        ulong Choose(ulong domain)
        {
            double total = 0; for (int t = 0; t < config.modules.Count; t++) if ((domain & (1UL << t)) != 0) total += config.modules[t].weight;
            double draw = random.NextDouble() * total;
            for (int t = 0; t < config.modules.Count; t++) if ((domain & (1UL << t)) != 0) { draw -= config.modules[t].weight; if (draw <= 0) return 1UL << t; }
            return domain & (~domain + 1);
        }
        bool Backtrack()
        {
            while (decisions.Count > 0 && attemptBacktracks < config.maxBacktracks)
            {
                attemptBacktracks++; Result.stats.backtracks++;
                var decision = decisions[decisions.Count - 1];
                if (decision.remaining == 0) { decisions.RemoveAt(decisions.Count - 1); continue; }
                wave = (ulong[])decision.before.Clone(); queue.Clear(); Array.Clear(queued, 0, count);
                ulong choice = Choose(decision.remaining); decision.remaining &= ~choice;
                wave[decision.cell] = choice; Enqueue(decision.cell);
                if (Propagate()) return true;
            }
            return false;
        }
        WfcStep Step(WfcStepKind kind, int cell, bool capture) => new WfcStep { kind = kind, cell = cell, attempt = Result.stats.attempts, domains = capture && wave != null ? (ulong[])wave.Clone() : null };
        public IEnumerable<WfcStep> Steps(bool capture = true)
        {
            Result = new WfcResult(); Result.stats.seed = seed;
            string invalid = config.Error();
            if (invalid != null) { Result.error = invalid; yield return Step(WfcStepKind.Failed, -1, capture); yield break; }
            var watch = new Stopwatch();
            for (int attempt = 0; attempt < config.maxAttempts; attempt++)
            {
                Result.stats.attempts++; watch.Start(); bool valid = Initialize(attempt); watch.Stop();
                yield return Step(WfcStepKind.Initialize, -1, capture);
                while (valid)
                {
                    watch.Start(); int cell = Select(); watch.Stop();
                    if (cell == -1)
                    {
                        watch.Start(); Result.stats.localSolutions++;
                        var tiles = new int[count]; for (int i = 0; i < count; i++) tiles[i] = Tile(wave[i]);
                        var candidate = Inspect(config, tiles, constrained ? start : -1, constrained ? goal : -1);
                        Result.stats.walkable = candidate.stats.walkable; Result.stats.rooms = candidate.stats.rooms;
                        Result.stats.reachable = candidate.stats.reachable; Result.stats.shortestPath = candidate.stats.shortestPath; Result.stats.loops = candidate.stats.loops;
                        Result.tiles = tiles; Result.start = candidate.start; Result.goal = candidate.goal; Result.route = candidate.route;
                        Result.encounters = constrained ? encounters : Array.Empty<int>();
                        Result.error = candidate.error; Result.success = candidate.success;
                        watch.Stop();
                        if (Result.success) { Result.stats.milliseconds = watch.Elapsed.TotalMilliseconds; yield return Step(WfcStepKind.Complete, -1, capture); yield break; }
                        break;
                    }
                    if (cell == -2) break;
                    watch.Start(); ulong choice = Choose(wave[cell]);
                    decisions.Add(new Decision { cell = cell, before = (ulong[])wave.Clone(), remaining = wave[cell] & ~choice });
                    wave[cell] = choice; Enqueue(cell); Result.stats.collapses++; watch.Stop();
                    yield return Step(WfcStepKind.Collapse, cell, capture);
                    watch.Start(); valid = Propagate(); watch.Stop();
                    yield return Step(WfcStepKind.Propagate, cell, capture);
                    if (!valid)
                    {
                        watch.Start(); valid = Backtrack(); watch.Stop();
                        yield return Step(WfcStepKind.Backtrack, cell, capture);
                    }
                }
                yield return Step(WfcStepKind.Retry, -1, capture);
            }
            Result.success = false; Result.error = Result.error ?? "Contradiction: solve budget exhausted.";
            Result.stats.milliseconds = watch.Elapsed.TotalMilliseconds;
            yield return Step(WfcStepKind.Failed, -1, capture);
        }

        public static WfcResult Inspect(WfcConfig cfg, int[] tiles, int start = -1, int goal = -1)
        {
            var r = new WfcResult { tiles = tiles, start = start, goal = goal };
            if (tiles == null || tiles.Length != cfg.width * cfg.height) { r.error = "Wrong grid size."; return r; }
            int edges = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] < 0 || tiles[i] >= cfg.modules.Count) { r.error = "Invalid tile."; return r; }
                var m = cfg.modules[tiles[i]];
                if (m.ports != 0) { r.stats.walkable++; if (r.start < 0) r.start = i; }
                if (m.room) r.stats.rooms++;
                for (int d = 0; d < 4; d++)
                {
                    int x = i % cfg.width + Dx[d], z = i / cfg.width + Dz[d];
                    bool opened = (m.ports & (1 << d)) != 0;
                    if (x < 0 || z < 0 || x >= cfg.width || z >= cfg.height) { if (opened) { r.error = "Open boundary."; return r; } continue; }
                    int other = tiles[z * cfg.width + x];
                    if (other < 0 || other >= cfg.modules.Count) { r.error = "Invalid neighbor."; return r; }
                    if (opened != ((cfg.modules[other].ports & (1 << ((d + 2) % 4))) != 0)) { r.error = "Mismatched sockets."; return r; }
                    if (opened && d < 2) edges++;
                }
            }
            if (r.start < 0 || r.start >= tiles.Length || cfg.modules[tiles[r.start]].ports == 0) { r.error = "No walkable start."; return r; }
            int[] parent = new int[tiles.Length], dist = new int[tiles.Length];
            for (int i = 0; i < parent.Length; i++) { parent[i] = -1; dist[i] = -1; }
            var q = new Queue<int>(); q.Enqueue(r.start); dist[r.start] = 0; int farthest = r.start;
            while (q.Count > 0)
            {
                int cell = q.Dequeue(); r.stats.reachable++; if (dist[cell] > dist[farthest]) farthest = cell;
                int ports = cfg.modules[tiles[cell]].ports;
                for (int d = 0; d < 4; d++) if ((ports & (1 << d)) != 0)
                {
                    int next = cell + Dz[d] * cfg.width + Dx[d];
                    if (dist[next] < 0) { dist[next] = dist[cell] + 1; parent[next] = cell; q.Enqueue(next); }
                }
            }
            if (r.goal < 0) r.goal = farthest;
            if (r.goal >= tiles.Length || dist[r.goal] < 0) { r.error = "Goal unreachable."; return r; }
            // Cycle rank is E - V + C. Using +1 on a disconnected baseline gives meaningless negative counts.
            int components = 0; var seen = new bool[tiles.Length];
            for (int root = 0; root < tiles.Length; root++)
                if (!seen[root] && cfg.modules[tiles[root]].ports != 0)
                {
                    components++; seen[root] = true; q.Enqueue(root);
                    while (q.Count > 0)
                    {
                        int cell = q.Dequeue(), ports = cfg.modules[tiles[cell]].ports;
                        for (int d = 0; d < 4; d++) if ((ports & (1 << d)) != 0)
                        {
                            int next = cell + Dz[d] * cfg.width + Dx[d];
                            if (!seen[next]) { seen[next] = true; q.Enqueue(next); }
                        }
                    }
                }
            r.stats.shortestPath = dist[r.goal]; r.stats.loops = edges - r.stats.walkable + components;
            var path = new List<int>(); for (int i = r.goal; i >= 0; i = parent[i]) path.Add(i); path.Reverse(); r.route = path.ToArray();
            r.success = r.stats.reachable == r.stats.walkable && r.stats.rooms >= 4 && r.stats.loops >= 1 && r.goal != r.start;
            r.error = r.success ? null : $"Global check failed: reachable {r.stats.reachable}/{r.stats.walkable}, rooms {r.stats.rooms}, loops {r.stats.loops}.";
            return r;
        }
    }
}
