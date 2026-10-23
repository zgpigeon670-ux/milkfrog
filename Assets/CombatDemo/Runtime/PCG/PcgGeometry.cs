using UnityEngine;

namespace Milkfrog.CombatDemo.PCG
{
    public static class PcgGeometry
    {
        public static Vector3 Center(WfcConfig c, int cell) => new Vector3((cell % c.width - (c.width - 1) * .5f) * c.cellSize, 0, (cell / c.width - (c.height - 1) * .5f) * c.cellSize);
        public static int Cell(WfcConfig c, Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x / c.cellSize + c.width * .5f), z = Mathf.FloorToInt(position.z / c.cellSize + c.height * .5f);
            return x < 0 || z < 0 || x >= c.width || z >= c.height ? -1 : z * c.width + x;
        }
        public static GameObject Build(WfcConfig c, WfcResult map, Transform parent, Material floor, Material wall, Material start, Material goal, Material encounter)
        {
            var root = new GameObject("Generated seed " + map.stats.seed); root.transform.SetParent(parent, false);
            for (int cell = 0; cell < map.tiles.Length; cell++)
            {
                var module = c.modules[map.tiles[cell]]; if (module.ports == 0) continue;
                var tile = new GameObject(cell + " · " + module.id); tile.transform.SetParent(root.transform, false); tile.transform.position = Center(c, cell);
                Material surface = cell == map.start ? start : cell == map.goal ? goal : System.Array.IndexOf(map.encounters, cell) >= 0 ? encounter : floor;
                float half = c.cellSize * .5f, lane = c.corridorWidth * .5f, thickness = .25f;
                if (module.room)
                {
                    Box(tile.transform, "Room floor", new Vector3(0, -.15f, 0), new Vector3(c.cellSize, .3f, c.cellSize), surface);
                    for (int d = 0; d < 4; d++)
                    {
                        bool door = (module.ports & (1 << d)) != 0;
                        if (door)
                        {
                            float segment = (c.cellSize - c.corridorWidth) * .5f;
                            EdgeWall(tile.transform, d, half - thickness * .5f, -(lane + segment * .5f), segment, c.wallHeight, thickness, wall);
                            EdgeWall(tile.transform, d, half - thickness * .5f, lane + segment * .5f, segment, c.wallHeight, thickness, wall);
                        }
                        else EdgeWall(tile.transform, d, half - thickness * .5f, 0, c.cellSize, c.wallHeight, thickness, wall);
                    }
                }
                else
                {
                    Box(tile.transform, "Junction floor", new Vector3(0, -.15f, 0), new Vector3(c.corridorWidth, .3f, c.corridorWidth), surface);
                    for (int d = 0; d < 4; d++)
                    {
                        if ((module.ports & (1 << d)) == 0) { EdgeWall(tile.transform, d, lane + thickness * .5f, 0, c.corridorWidth + thickness * 2, c.wallHeight, thickness, wall); continue; }
                        float length = half - lane;
                        Vector3 outward = new Vector3(WfcSolver.Dx[d], 0, WfcSolver.Dz[d]);
                        Vector3 along = new Vector3(-outward.z, 0, outward.x);
                        Vector3 center = outward * (lane + length * .5f);
                        Vector3 floorSize = d % 2 == 0 ? new Vector3(c.corridorWidth, .3f, length) : new Vector3(length, .3f, c.corridorWidth);
                        Box(tile.transform, "Corridor floor", center + Vector3.down * .15f, floorSize, surface);
                        for (int side = -1; side <= 1; side += 2)
                            Box(tile.transform, "Corridor wall", center + along * ((lane + thickness * .5f) * side) + Vector3.up * c.wallHeight * .5f,
                                d % 2 == 0 ? new Vector3(thickness, c.wallHeight, length) : new Vector3(length, c.wallHeight, thickness), wall);
                    }
                }
                if (cell == map.start || cell == map.goal)
                {
                    var beacon = Box(tile.transform, cell == map.start ? "START" : "EXIT", new Vector3(2.6f, 1, 2.6f), new Vector3(.35f, 2, .35f), surface);
                    var light = beacon.AddComponent<Light>(); light.type = LightType.Point; light.color = surface.color; light.intensity = 2; light.range = 5;
                }
            }
            return root;
        }
        static void EdgeWall(Transform parent, int direction, float offset, float tangent, float length, float height, float thickness, Material material)
        {
            var outward = new Vector3(WfcSolver.Dx[direction], 0, WfcSolver.Dz[direction]);
            var along = new Vector3(-outward.z, 0, outward.x);
            Box(parent, "Wall", outward * offset + along * tangent + Vector3.up * height * .5f,
                direction % 2 == 0 ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length), material);
        }
        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false); box.transform.localPosition = localPosition; box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material; return box;
        }
    }
}
