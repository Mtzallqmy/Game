// NAGEEL recovery tool: generates NEW playable Unity 3D scenes without overwriting
// the historical (broken/missing GUID) scenes. Requires Unity 6 with Android + URP.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Nageel.Vehicle;
using Nageel.Camera;
using Nageel.Recovery;

namespace Nageel.EditorTools
{
    public static class NageelRecoverySceneBuilder
    {
        private const string Root = "Assets/Recovery";
        private const string MenuScene = "Assets/Scenes/RecoveryMainMenu.unity";
        private const string DriveScene = "Assets/Scenes/RecoveryDrive.unity";
        private const float Length = 1800f;
        private const float Step = 8f;

        [MenuItem("NAGEEL/Recovery/1 - Generate Playable 3D Scenes")]
        public static void Generate()
        {
            PrepareFolders();
            SetAndroidSettings();
            ConfigurePipeline();
            CreateMenuScene();
            CreateDriveScene();
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(DriveScene, true)
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NAGEEL: recovery scenes generated. Test BOTH scenes in Play Mode, then use Android build.");
        }

        [MenuItem("NAGEEL/Recovery/2 - Build Android ARM64 APK")]
        public static void BuildAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch Unity active target to Android before running the APK builder, then rerun.");
            Generate();
            Directory.CreateDirectory("Builds");
            var options = new BuildPlayerOptions {
                scenes = new[] { MenuScene, DriveScene },
                target = BuildTarget.Android,
                locationPathName = "Builds/NageelRecovery-arm64.apk",
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android Unity build FAILED; no APK is reported ready.");
            Debug.Log("NAGEEL: Unity Android build completed: " + options.locationPathName);
        }

        private static void PrepareFolders()
        {
            EnsureFolder("Assets", "Recovery");
            EnsureFolder(Root, "Art");
            EnsureFolder(Root, "Generated");
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Prefabs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var full = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetAndroidSettings()
        {
            PlayerSettings.companyName = "Moataz Alalqami";
            PlayerSettings.productName = "NAGEEL - Yemen Mountain Driving";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.moataz.nageel");
            PlayerSettings.bundleVersion = "2.1.0";
            PlayerSettings.Android.bundleVersionCode = 2100;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
        }

        private static void ConfigurePipeline()
        {
            const string rendererPath = Root + "/Generated/NageelRenderer.asset";
            const string pipelinePath = Root + "/Generated/NageelURP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null) throw new InvalidOperationException("URP renderer data property is unavailable for this Unity version");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static Material MakeMaterial(string key, Color color, bool noisy)
        {
            string materialPath = Root + "/Art/" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP/Lit shader missing; check package installation");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            mat.SetColor("_BaseColor", Color.white);
            if (noisy)
            {
                string texturePath = Root + "/Art/" + key + "_Procedural.asset";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex == null)
                {
                    tex = new Texture2D(128,128,TextureFormat.RGBA32,false);
                    tex.wrapMode = TextureWrapMode.Repeat;
                    var rng = new System.Random(key.GetHashCode());
                    for (int y=0;y<128;y++)
                    for (int x=0;x<128;x++)
                    {
                        float n = 0.8f + (float)rng.NextDouble()*0.30f;
                        tex.SetPixel(x,y,new Color(color.r*n,color.g*n,color.b*n,1f));
                    }
                    tex.Apply();
                    AssetDatabase.CreateAsset(tex, texturePath);
                }
                mat.SetTexture("_BaseMap", tex);
            }
            else
            {
                mat.SetTexture("_BaseMap", null);
                mat.SetColor("_BaseColor", color);
            }
            mat.SetFloat("_Metallic", 0.04f);
            mat.SetFloat("_Smoothness", 0.20f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreateMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("RecoveryMenuCamera");
            var camera = cameraGo.AddComponent<UnityEngine.Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f,0.15f,0.20f,1f);
            cameraGo.AddComponent<AudioListener>();
            new GameObject("RecoveryMenu").AddComponent<RecoveryMenuUI>();
            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        private static Mesh BuildRibbon(List<Vector3> centers, float[] widths, float[] vertical, bool flip)
        {
            var vertices = new List<Vector3>(centers.Count*widths.Length);
            var uvs = new List<Vector2>(centers.Count*widths.Length);
            var triangles = new List<int>();
            int stride = widths.Length;
            for (int i=0; i<centers.Count; i++)
            {
                Vector3 prev = centers[Mathf.Max(0,i-1)];
                Vector3 next = centers[Mathf.Min(centers.Count-1,i+1)];
                Vector3 right = Vector3.Cross(Vector3.up,(next-prev).normalized).normalized;
                for (int j=0;j<widths.Length;j++)
                {
                    vertices.Add(centers[i]+right*widths[j]+Vector3.up*vertical[j]);
                    uvs.Add(new Vector2(j*2f,i*Step*0.13f));
                    if (i>0 && j>0)
                    {
                        int a = (i-1)*stride + j-1;
                        int b = i*stride + j-1;
                        int c = (i-1)*stride + j;
                        int d = i*stride + j;
                        if (!flip)
                        {
                            triangles.Add(a); triangles.Add(b); triangles.Add(c);
                            triangles.Add(c); triangles.Add(b); triangles.Add(d);
                        }
                        else
                        {
                            triangles.Add(a); triangles.Add(c); triangles.Add(b);
                            triangles.Add(c); triangles.Add(d); triangles.Add(b);
                        }
                    }
                }
            }
            var mesh = new Mesh { name = "NageelRecovery_" + (flip?"Left":"RoadOrRight") };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.SetUVs(0,uvs);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh source, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source,path);
                return source;
            }
            EditorUtility.CopySerialized(source,existing);
            UnityEngine.Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static GameObject MeshObject(string name, Mesh mesh, Material material)
        {
            var obj = new GameObject(name);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            return obj;
        }

        private static GameObject VisiblePrimitive(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent,false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col); // visual-only, physics at root/wheels
            return go;
        }

        private static void CreateDriveScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f,0.58f,0.62f,1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0024f;
            RenderSettings.fogColor = new Color(0.64f,0.73f,0.77f);

            var sunlight = new GameObject("HighlandSun");
            sunlight.transform.rotation = Quaternion.Euler(45f,-35f,0f);
            var light = sunlight.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            Material asphalt = MakeMaterial("HighlandAsphalt",new Color(.19f,.20f,.21f),true);
            Material rock = MakeMaterial("MountainRock",new Color(.45f,.39f,.31f),true);
            Material paint = MakeMaterial("DesertVehicle",new Color(.78f,.72f,.58f),false);
            Material glass = MakeMaterial("VehicleGlass",new Color(.11f,.23f,.29f),false);
            Material rubber = MakeMaterial("VehicleRubber",new Color(.035f,.035f,.04f),false);
            Material rail = MakeMaterial("GuardRailSteel",new Color(.70f,.72f,.70f),false);

            var centers = new List<Vector3>();
            for (float z=0;z<=Length;z+=Step) centers.Add(RecoveryTouchOverlay.RecoveryRoadPoint(z));
            var road = SaveMesh(BuildRibbon(centers,new[]{-4.5f,4.5f},new[]{0f,0f},false),
                Root+"/Generated/RecoveryRoad.asset");
            var hillside = SaveMesh(BuildRibbon(centers,new[]{-4.5f,-15f,-70f},
                new[]{-0.03f,3f,28f},true),Root+"/Generated/RecoveryHillside.asset");
            var cliffside = SaveMesh(BuildRibbon(centers,new[]{4.5f,15f,68f},
                new[]{-0.03f,-8f,-24f},false),Root+"/Generated/RecoveryCliffside.asset");
            MeshObject("DriveableAsphalt_1800m",road,asphalt);
            MeshObject("UphillRockTerrain",hillside,rock);
            MeshObject("ValleyCliffTerrain",cliffside,rock);

            var scenery = new GameObject("RoadsideGuardrailsAndRocks").transform;
            var rng = new System.Random(7031);
            for (int i=2;i<centers.Count-2;i+=3)
            {
                Vector3 pos=centers[i];
                Vector3 tangent=(centers[i+1]-centers[i-1]).normalized;
                Vector3 right=Vector3.Cross(Vector3.up,tangent).normalized;
                var guard=GameObject.CreatePrimitive(PrimitiveType.Cube);
                guard.name="CliffSafetyGuardrail";
                guard.transform.SetParent(scenery);
                guard.transform.position=pos+right*4.95f+Vector3.up*.65f;
                guard.transform.rotation=Quaternion.LookRotation(tangent,Vector3.up);
                guard.transform.localScale=new Vector3(.24f,1.0f,Step*3.0f);
                guard.GetComponent<Renderer>().sharedMaterial=rail;
                if (i%2==0)
                {
                    var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stone.name="MountainRock";
                    stone.transform.SetParent(scenery);
                    stone.transform.position=pos-right*(13f+(float)rng.NextDouble()*18f)
                        +Vector3.up*(3f+(float)rng.NextDouble()*5f);
                    stone.transform.localScale=new Vector3(5f+(float)rng.NextDouble()*9f,
                        4f+(float)rng.NextDouble()*6f,5f+(float)rng.NextDouble()*9f);
                    stone.transform.rotation=Quaternion.Euler(0f,(float)rng.NextDouble()*180f,10f);
                    stone.GetComponent<Renderer>().sharedMaterial=rock;
                }
            }

            GameObject car = CreateCar(paint,glass,rubber,rail);
            car.transform.position=RecoveryTouchOverlay.RecoveryRoadPoint(12f)+Vector3.up*1.6f;
            car.transform.rotation=Quaternion.LookRotation(
                RecoveryTouchOverlay.RecoveryRoadPoint(20f)-RecoveryTouchOverlay.RecoveryRoadPoint(12f),
                Vector3.up);

            var cameraGo = new GameObject("Nageel3DFollowCamera");
            cameraGo.tag="MainCamera";
            var camera=cameraGo.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView=66f; camera.farClipPlane=750f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position=car.transform.position+new Vector3(0f,3f,-8f);
            var follow=cameraGo.AddComponent<CameraController>();
            follow.vehicleTarget=car.transform;
            follow.vehicleRigidbody=car.GetComponent<Rigidbody>();
            car.GetComponent<RecoveryTouchOverlay>().drivingCamera=follow;

            PrefabUtility.SaveAsPrefabAsset(car,"Assets/Prefabs/NageelRecoverySUV.prefab");
            new GameObject("AddressablesLocalCatalogProbe").AddComponent<RecoveryAddressablesRuntime>();
            EditorSceneManager.SaveScene(scene,DriveScene);
        }

        private static GameObject CreateCar(Material body, Material glass, Material wheels, Material detail)
        {
            GameObject root = new GameObject("NageelRecoverySUV_Rigidbody_4Wheel");
            var rb=root.AddComponent<Rigidbody>();
            rb.mass=2150f; rb.interpolation=RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var collider=root.AddComponent<BoxCollider>();
            collider.size=new Vector3(2.10f,1.14f,4.45f);
            collider.center=new Vector3(0f,.18f,0f);

            VisiblePrimitive("SUV_MainBody",PrimitiveType.Cube,root.transform,
                new Vector3(0f,.18f,0f),new Vector3(2.05f,.96f,4.30f),body);
            VisiblePrimitive("SUV_Cabin",PrimitiveType.Cube,root.transform,
                new Vector3(0f,.98f,-.28f),new Vector3(1.85f,.70f,2.22f),body);
            VisiblePrimitive("SUV_Windshield",PrimitiveType.Cube,root.transform,
                new Vector3(0f,1.05f,.84f),new Vector3(1.60f,.49f,.085f),glass);
            VisiblePrimitive("SUV_Grille",PrimitiveType.Cube,root.transform,
                new Vector3(0f,.18f,2.18f),new Vector3(1.45f,.33f,.10f),detail);

            var controller=root.AddComponent<NageelCarController>();
            var fl=AddWheel(root.transform,"FrontLeft",-.90f,-.48f,1.43f,wheels);
            var fr=AddWheel(root.transform,"FrontRight",.90f,-.48f,1.43f,wheels);
            var rl=AddWheel(root.transform,"RearLeft",-.90f,-.48f,-1.44f,wheels);
            var rr=AddWheel(root.transform,"RearRight",.90f,-.48f,-1.44f,wheels);
            controller.frontLeftCollider=fl.Item1; controller.frontLeftMesh=fl.Item2;
            controller.frontRightCollider=fr.Item1; controller.frontRightMesh=fr.Item2;
            controller.rearLeftCollider=rl.Item1; controller.rearLeftMesh=rl.Item2;
            controller.rearRightCollider=rr.Item1; controller.rearRightMesh=rr.Item2;
            controller.suvBodyMesh=root.transform.Find("SUV_MainBody").gameObject;
            root.AddComponent<RecoveryTouchOverlay>();
            var source=root.AddComponent<AudioSource>();
            source.playOnAwake=false;
            root.AddComponent<RecoveryEngineSynth>().car=controller;
            return root;
        }

        private static Tuple<WheelCollider,Transform> AddWheel(Transform root,string name,
            float x,float y,float z,Material rubber)
        {
            var colliderGo=new GameObject(name+"_Physics");
            colliderGo.transform.SetParent(root,false);
            colliderGo.transform.localPosition=new Vector3(x,y,z);
            var wc=colliderGo.AddComponent<WheelCollider>();
            wc.radius=.43f; wc.mass=32f; wc.suspensionDistance=.33f;
            var wheelRoot=new GameObject(name+"_Visual").transform;
            wheelRoot.SetParent(root,false);
            wheelRoot.localPosition=new Vector3(x,y,z);
            VisiblePrimitive("Rubber",PrimitiveType.Cylinder,wheelRoot,
                Vector3.zero,new Vector3(.43f,.19f,.43f),rubber,Quaternion.Euler(0f,0f,90f));
            return Tuple.Create(wc,wheelRoot);
        }
    }
}
#endif
