using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milkfrog.CombatDemo.Editor
{
    public static class CombatDemoBuilder
    {
        public const string ScenePath = "Assets/CombatDemo/Scenes/CombatDemo.unity";
        const string Root = "Assets/CombatDemo";

        [MenuItem("Tools/Combat Demo/Create Missing Demo Scene")]
        public static void Build()
        {
            // A new additive scene avoids replacing or saving the user's working scene.
            if (File.Exists(ScenePath)) { Debug.Log("CombatDemo scene already exists; preserved."); return; }
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Settings");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<CombatDemoSettings>(Root + "/Settings/DefaultCombat.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CombatDemoSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/Settings/DefaultCombat.asset");
            }
            var previous = SceneManager.GetActiveScene();
            bool additive = !Application.isBatchMode;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                Material actorMaterial = Material("Actor", new Color(.5f, .7f, .8f));
                Material floorMaterial = Material("Floor", new Color(.09f, .13f, .18f));
                Material lineMaterial = Material("Lines", new Color(.22f, .32f, .40f));
                Material forwardMaterial = Material("Facing", new Color(.7f, 1, 1));
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Arena Floor";
                floor.transform.position = new Vector3(0, -.25f, 0);
                floor.transform.localScale = new Vector3(20, .5f, 20);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                for (int i = -10; i <= 10; i += 2)
                {
                    Decoration("Grid X", new Vector3(i, .008f, 0), new Vector3(.018f, .012f, 20), lineMaterial);
                    Decoration("Grid Z", new Vector3(0, .008f, i), new Vector3(20, .012f, .018f), lineMaterial);
                }
                Wall(new Vector3(0, .5f, 10), new Vector3(20, 1, .25f), lineMaterial);
                Wall(new Vector3(0, .5f, -10), new Vector3(20, 1, .25f), lineMaterial);
                Wall(new Vector3(10, .5f, 0), new Vector3(.25f, 1, 20), lineMaterial);
                Wall(new Vector3(-10, .5f, 0), new Vector3(.25f, 1, 20), lineMaterial);

                var camera = new GameObject("Combat Camera", typeof(Camera), typeof(AudioListener), typeof(DemoCamera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.fieldOfView = 50;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .055f, .085f);
                camera.transform.position = new Vector3(3, 6.25f, -7.25f);
                camera.transform.LookAt(new Vector3(0, 1.25f, 0));
                var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.7f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.48f, .53f, .62f);
                var player = Actor("Player", new Vector3(0, .02f, -.85f), Quaternion.identity, true, actorMaterial, forwardMaterial, camera);
                var enemy = Actor("Enemy", new Vector3(0, .02f, .85f), Quaternion.Euler(0, 180, 0), false, actorMaterial, forwardMaterial, camera);
                var follow = camera.GetComponent<DemoCamera>();
                follow.player = player.transform;
                follow.enemy = enemy.transform;
                var session = new GameObject("Combat Demo", typeof(CombatDemoSession), typeof(CombatDebugHud)).GetComponent<CombatDemoSession>();
                session.settings = settings;
                session.player = player;
                session.enemy = enemy;
                session.gameplayCamera = camera;
                session.GetComponent<CombatDebugHud>().session = session;
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CombatDemo] Created " + ScenePath);
            }
            finally
            {
                if (additive)
                {
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static Material Material(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Decoration(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void Wall(Vector3 position, Vector3 scale, Material material)
        {
            var go = Decoration("Arena Boundary", position, scale, material);
            go.AddComponent<BoxCollider>();
        }

        static CombatActor Actor(string name, Vector3 position, Quaternion rotation, bool isPlayer, Material material, Material facing, Camera camera)
        {
            var root = new GameObject(name, typeof(CharacterController), typeof(CombatActor), typeof(CombatActorView));
            root.transform.SetPositionAndRotation(position, rotation);
            var controller = root.GetComponent<CharacterController>();
            controller.height = 2;
            controller.radius = .4f;
            controller.center = Vector3.up;
            controller.skinWidth = .035f;
            controller.minMoveDistance = 0;
            var actor = root.GetComponent<CombatActor>();
            actor.isPlayer = isPlayer;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(.8f, 1, .8f);
            body.GetComponent<Renderer>().sharedMaterial = material;
            var nose = Decoration("Facing Marker", Vector3.zero, new Vector3(.16f, .12f, .65f), facing);
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0, 1.3f, .5f);
            var marker = Decoration("Attack Telegraph", Vector3.zero, Vector3.one, material);
            marker.transform.SetParent(root.transform, false);
            marker.SetActive(false);
            var label = new GameObject("State Label", typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = Vector3.up * 2.45f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = .032f;
            label.text = name.ToUpperInvariant();
            var view = root.GetComponent<CombatActorView>();
            view.actor = actor;
            view.body = body.GetComponent<Renderer>();
            view.attackMarker = marker.transform;
            view.attackRenderer = marker.GetComponent<Renderer>();
            view.label = label;
            view.viewCamera = camera;
            return actor;
        }
    }
}
