using System;
using UnityEngine;

namespace Milkfrog.CombatDemo.BellCrossing
{
    public enum CrossingRoom { Dock, Approach, Hearth, WestWalk, WestCourt, WestSeal, WestReturn, Junction, EastWalk, EastCourt, EastSeal, EastReturn, Shrine, Vestibule, Guardian, Bell }

    // One fixed, authored topology. Coordinates are also used by physical construction and the map.
    public static class BellCrossingLayout
    {
        public const float Size = 14, DoorWidth = 4;
        public static readonly Vector2Int[] Cells = {
            new Vector2Int(0,-3), new Vector2Int(0,-2), new Vector2Int(0,-1),
            new Vector2Int(-1,-1), new Vector2Int(-2,-1), new Vector2Int(-2,0), new Vector2Int(-1,0),
            new Vector2Int(0,0), new Vector2Int(1,-1), new Vector2Int(2,-1), new Vector2Int(2,0), new Vector2Int(1,0),
            new Vector2Int(3,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(0,3)
        };
        public static readonly string[] Names = { "遗舟渡口", "石阶哨庭", "留火院", "西廊", "折剑庭", "灰烬钟龛", "回望廊", "双印门庭", "东廊", "苇影庭", "潮声钟龛", "归潮廊", "无名小祠", "静声前殿", "守钟殿", "渡钟台" };
        public static readonly int[,] Edges = {
            {0,1},{1,2},{2,3},{3,4},{4,5},{5,6},{6,7},{7,2},
            {2,8},{8,9},{9,10},{10,11},{11,7},{10,12},{7,13},{13,14},{14,15}
        };
        public static readonly CrossingRoom[] EnemyRooms = { CrossingRoom.Approach, CrossingRoom.WestCourt, CrossingRoom.WestSeal, CrossingRoom.EastCourt, CrossingRoom.EastSeal, CrossingRoom.EastSeal, CrossingRoom.Shrine, CrossingRoom.Guardian };
        public const int Boss = 7;
        public static Vector3 Center(CrossingRoom room) { var c = Cells[(int)room]; return new Vector3(c.x * Size, .02f, c.y * Size); }
        public static Vector3 Home(int index) => Center(EnemyRooms[index]) + (index == 4 ? new Vector3(-2,0,1.5f) : index == 5 ? new Vector3(2,0,3) : Vector3.forward * 2);
        public static CrossingRoom? At(Vector3 point)
        {
            int x = Mathf.FloorToInt((point.x + Size / 2) / Size), z = Mathf.FloorToInt((point.z + Size / 2) / Size);
            for (int i = 0; i < Cells.Length; i++) if (Cells[i].x == x && Cells[i].y == z) return (CrossingRoom)i;
            return null;
        }
        public static bool Linked(int a, int b)
        {
            for (int i = 0; i < Edges.GetLength(0); i++) if (Edges[i,0] == a && Edges[i,1] == b || Edges[i,0] == b && Edges[i,1] == a) return true;
            return false;
        }
        public static Vector3 WestPoint => Center(CrossingRoom.WestSeal) + new Vector3(-3,0,3);
        public static Vector3 EastPoint => Center(CrossingRoom.EastSeal) + new Vector3(3,0,3);
        public static Vector3 RestPoint => Center(CrossingRoom.Hearth) + new Vector3(-3,0,-2);
        public static Vector3 LeverPoint => Center(CrossingRoom.WestReturn) + new Vector3(3,0,-2);
        public static Vector3 ShrinePoint => Center(CrossingRoom.Shrine) + new Vector3(3,0,3);
        public static Vector3 BellPoint => Center(CrossingRoom.Bell) + Vector3.forward * 2;
    }
}
