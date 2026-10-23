using System;
using System.Collections.Generic;
using UnityEngine;

namespace Milkfrog.CombatDemo.PCG
{
    [Serializable]
    public sealed class WfcModule
    {
        public string id;
        [Range(0, 15)] public int ports; // N=1, E=2, S=4, W=8
        public bool room;
        public float weight = 1;
    }

    [CreateAssetMenu(menuName = "Combat Demo/PCG/WFC Configuration")]
    public sealed class WfcConfig : ScriptableObject
    {
        public int width = 10, height = 10, seed = 18;
        public float cellSize = 8, corridorWidth = 3, wallHeight = 3;
        public int maxBacktracks = 2048, maxAttempts = 32;
        public List<WfcModule> modules = Defaults();
        public static int Rotate(int mask) => ((mask << 1) & 15) | ((mask >> 3) & 1);
        public static List<WfcModule> Defaults()
        {
            var result = new List<WfcModule> { new WfcModule { id = "Empty", ports = 0, weight = 4 } };
            int[] bases = { 1, 5, 3, 7, 15 };
            string[] names = { "End", "Straight", "Corner", "Tee", "Cross" };
            float[] weights = { .8f, 1.5f, 1.2f, .25f, .05f };
            for (int b = 0; b < bases.Length; b++)
            {
                var seen = new HashSet<int>(); int mask = bases[b];
                for (int r = 0; r < 4; r++, mask = Rotate(mask))
                    if (seen.Add(mask))
                    {
                        result.Add(new WfcModule { id = names[b] + "_" + r, ports = mask, weight = weights[b] });
                        result.Add(new WfcModule { id = "Room_" + names[b] + "_" + r, ports = mask, room = true, weight = .12f });
                    }
            }
            return result;
        }
        public string Error()
        {
            if (width < 6 || height < 6 || width > 24 || height > 24) return "Grid must be between 6 and 24 cells per side.";
            if (!Finite(cellSize) || !Finite(corridorWidth) || !Finite(wallHeight) || cellSize < 6 || corridorWidth < 2 || corridorWidth > cellSize - 1 || wallHeight < 3) return "Invalid physical dimensions.";
            if (maxBacktracks < 0 || maxBacktracks > 2048 || maxAttempts < 1 || maxAttempts > 32) return "Invalid solve budget (maximum 2048 / 32).";
            if (modules == null || modules.Count == 0 || modules.Count > 63) return "Expected 1–63 module states.";
            foreach (var module in modules)
                if (module == null || module.ports < 0 || module.ports > 15 || module.room && module.ports == 0 || !Finite(module.weight) || module.weight <= 0) return "Invalid module or weight.";
            return null;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
