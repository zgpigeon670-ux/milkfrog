using UnityEngine;

namespace Milkfrog.CombatDemo.BellCrossing
{
    public static class BellCrossingGeometry
    {
        public static GameObject Build(Transform parent, Material stone, Material wall, Material copper, Material jade, Material gold)
        {
            var root = new GameObject("沉钟渡 · Authored Architecture"); root.transform.SetParent(parent, false);
            for (int i = 0; i < BellCrossingLayout.Cells.Length; i++)
            {
                var room = (CrossingRoom)i; var center = BellCrossingLayout.Center(room); center.y = 0;
                var group = new GameObject(BellCrossingLayout.Names[i]); group.transform.SetParent(root.transform, false);
                Box(group.transform, "Stone foundation", center + Vector3.down * .2f, new Vector3(14,.4f,14), stone);
                Material route = room >= CrossingRoom.WestWalk && room <= CrossingRoom.WestReturn ? copper : room >= CrossingRoom.EastWalk && room <= CrossingRoom.Shrine ? jade : gold;
                Box(group.transform, "Inlaid wayfinding", center + Vector3.up * .012f, new Vector3(.12f,.02f,9), route, false);
                Box(group.transform, "Crossing inlay", center + Vector3.up * .012f, new Vector3(9,.02f,.12f), route, false);
                for(int line=-6;line<=6;line+=3)
                {
                    Box(group.transform,"Paving joint",center+new Vector3(line,.008f,0),new Vector3(.025f,.01f,13.6f),wall,false);
                    Box(group.transform,"Paving joint",center+new Vector3(0,.008f,line),new Vector3(13.6f,.01f,.025f),wall,false);
                }
                if(room==CrossingRoom.WestWalk || room==CrossingRoom.EastWalk || room==CrossingRoom.Vestibule)
                    foreach(float beam in new[]{-4f,0f,4f}) Box(group.transform,"Low cloister beam",center+new Vector3(beam,4.1f,0),new Vector3(.35f,.5f,13.5f),wall);
                for (int d = 0; d < 4; d++)
                {
                    var direction = d == 0 ? Vector3.forward : d == 1 ? Vector3.right : d == 2 ? Vector3.back : Vector3.left;
                    int neighbor = -1;
                    for (int j = 0; j < BellCrossingLayout.Cells.Length; j++)
                        if ((BellCrossingLayout.Center((CrossingRoom)j) - BellCrossingLayout.Center(room) - direction * 14).sqrMagnitude < .01f) neighbor = j;
                    bool open = neighbor >= 0 && BellCrossingLayout.Linked(i, neighbor);
                    if (open && neighbor < i) continue;
                    Vector3 seam = center + direction * 7, across = Vector3.Cross(direction, Vector3.up);
                    Vector3 dimensions = d % 2 == 0 ? new Vector3(14,4.6f,.4f) : new Vector3(.4f,4.6f,14);
                    if (!open) Box(group.transform, "Enclosure", seam + Vector3.up * 2.3f, dimensions, wall);
                    else
                    {
                        dimensions = d % 2 == 0 ? new Vector3(5,4.6f,.45f) : new Vector3(.45f,4.6f,5);
                        Box(group.transform, "Portal left", seam + across * 4.5f + Vector3.up * 2.3f, dimensions, wall);
                        Box(group.transform, "Portal right", seam - across * 4.5f + Vector3.up * 2.3f, dimensions, wall);
                        Box(group.transform, "Portal crown", seam + Vector3.up * 4.2f, d % 2 == 0 ? new Vector3(4,.8f,.55f) : new Vector3(.55f,.8f,4), wall);
                    }
                }
                // Corner columns never occupy the AI's central combat square.
                foreach (float x in new[] {-5.8f,5.8f}) foreach (float z in new[] {-5.8f,5.8f})
                    Box(group.transform, "Weathered pier", center + new Vector3(x,1.8f,z), new Vector3(.7f,3.6f,.7f), wall);
                foreach (float x in new[] {-4.8f,4.8f})
                {
                    var p = center + new Vector3(x,0,-4.8f);
                    Box(group.transform,"Lantern plinth",p+Vector3.up*.45f,new Vector3(.6f,.9f,.6f),wall);
                    Box(group.transform,"Lantern glass",p+Vector3.up*1.12f,new Vector3(.42f,.45f,.42f),route,false);
                }
            }
            Marker(root.transform,"West bell seal", BellCrossingLayout.WestPoint,copper);
            Marker(root.transform,"East bell seal", BellCrossingLayout.EastPoint,jade);
            Marker(root.transform,"Hearth", BellCrossingLayout.RestPoint,gold);
            Marker(root.transform,"Shortcut winch", BellCrossingLayout.LeverPoint,copper);
            Marker(root.transform,"Pilgrim offering", BellCrossingLayout.ShrinePoint,jade);
            // Stepped bronze profile uses built-in cylinder meshes, so every part remains editable.
            foreach(var profile in new[]{new Vector3(1.2f,1.0f,.16f),new Vector3(.93f,1.5f,.35f),new Vector3(.66f,2.05f,.2f),new Vector3(.3f,2.4f,.17f)})
            {
                var bell=GameObject.CreatePrimitive(PrimitiveType.Cylinder);bell.name="Crossing bell bronze tier";bell.transform.SetParent(root.transform,false);
                bell.transform.localPosition=BellCrossingLayout.BellPoint+Vector3.up*profile.y;bell.transform.localScale=new Vector3(profile.x,profile.z,profile.x);bell.GetComponent<Renderer>().sharedMaterial=gold;
                if(Application.isPlaying)Object.Destroy(bell.GetComponent<Collider>());else Object.DestroyImmediate(bell.GetComponent<Collider>());
            }
            Box(root.transform,"Bell beam",BellCrossingLayout.BellPoint+Vector3.up*3.5f,new Vector3(6,.45f,.5f),wall);
            foreach (float x in new[] {-2.8f,2.8f}) Box(root.transform,"Bell support",BellCrossingLayout.BellPoint+new Vector3(x,1.7f,0),new Vector3(.45f,3.5f,.45f),wall);
            Gate(root.transform,"Shortcut gate",new Vector3(-7,2.1f,0),new Vector3(.5f,4.2f,4),copper);
            Gate(root.transform,"Two seal gate",new Vector3(0,2.1f,7),new Vector3(4,4.2f,.5f),gold);
            var entrance = Gate(root.transform,"Arena entrance",new Vector3(0,2.1f,21),new Vector3(4,4.2f,.5f),gold); entrance.SetActive(false);
            Gate(root.transform,"Arena exit",new Vector3(0,2.1f,35),new Vector3(4,4.2f,.5f),gold);
            // A flooded landscape below the enclosed playable foundations.
            Box(root.transform,"Still marsh",new Vector3(0,-1.5f,0),new Vector3(160,.1f,160),jade,false);
            return root;
        }
        static GameObject Gate(Transform parent,string name,Vector3 position,Vector3 size,Material mat) => Box(parent,name,position,size,mat);
        static GameObject Marker(Transform parent,string name,Vector3 feet,Material material)
        {
            Box(parent,name+" base",feet+Vector3.up*.18f,new Vector3(1.2f,.36f,1.2f),material);
            return Box(parent,name,feet+Vector3.up*1.1f,new Vector3(.65f,1.5f,.65f),material,false);
        }
        public static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider) { if(Application.isPlaying) Object.Destroy(go.GetComponent<Collider>()); else Object.DestroyImmediate(go.GetComponent<Collider>()); }
            return go;
        }
    }
}
