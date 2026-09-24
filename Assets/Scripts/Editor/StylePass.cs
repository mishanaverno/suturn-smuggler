using System;
using System.IO;
using Interior;
using OuterSpace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InteriorEditor
{
    /// <summary>Первый проход общего стиля кабины и вида за иллюминатором.</summary>
    public static class StylePass
    {
        const string Folder = "Assets/Art/Materials";
        const string InteriorPrefabs = "Assets/Resources/Prefabs/Interior";
        const string ExteriorPrefab = "Assets/Resources/Prefabs/Exterior/Bodies/space-body.prefab";
        const string ScenePath = "Assets/Scenes/SystemBuilder.unity";

        static Material shell, panel, recess, grip, accent, indicator;
        static Material saturn, moon, titan, sky;
        static RingStyleSet ringStyles;

        [MenuItem("Cockpit/Style: apply first pass")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "Materials");

            Shader matte = Shader.Find("Suturn/Stylized Matte");
            Shader body = Shader.Find("Suturn/Stylized Body");
            Shader titanShader = Shader.Find("Suturn/Titan Terraforming");
            Shader lamp = Shader.Find("Suturn/Indicator Solid");
            Shader rings = Shader.Find("Suturn/Stylized Ring");
            Shader skyShader = Shader.Find("Suturn/Space Sky");
            Shader saturnShader = Shader.Find("Suturn/Saturn");
            if (matte == null || body == null || titanShader == null || lamp == null || rings == null || skyShader == null || saturnShader == null)
                throw new InvalidOperationException("StylePass: stylized shaders have not imported.");

            shell = Matte("01 Shell Warm White", matte, "#C8CBC4", "#555D5B", .50f);
            panel = Matte("02 Panel Grey", matte, "#858F8C", "#323C3F", .42f);
            recess = Matte("03 Recess Graphite", matte, "#303D41", "#20292E", .55f);
            grip = Matte("04 Grip Dark", matte, "#4A585B", "#252E32", .50f);
            accent = Matte("05 Interaction Blue", matte, "#638F9B", "#2A4851", .48f);
            indicator = Material("06 Indicator", lamp);
            indicator.SetColor("_Color", Hex("#1F302F"));

            saturn = Material("07 Saturn", saturnShader);
            moon = Body("08 Icy Moon", body, "#AEBEC0", "#AEBEC0", "#111923", 0f);
            titan = Material("09 Titan", titanShader);
            titan.SetFloat("_PatternScale", 2f);
            titan.SetFloat("_BandStretch", 3f);
            titan.SetFloat("_Belts", .1f);
            titan.SetFloat("_ClearThreshold", .42f);
            titan.SetFloat("_RimPixels", 2f);
            titan.SetFloat("_SaturnFill", .18f);
            titan.SetFloat("_CloudDetail", .8f);
            titan.SetFloat("_HazeWidth", .03f);
            titan.SetFloat("_VeilStrength", .45f);
            titan.SetVector("_SaturnDirection", new Vector4(0, 0, 1, 0));
            titan.SetColor("_CloudLight", Hex("#F0C62A"));
            titan.SetColor("_CloudMid", Hex("#C8911C"));
            titan.SetColor("_CloudShadow", Hex("#3B3016"));
            titan.SetColor("_ClearDay", Hex("#5F6C1E"));
            titan.SetColor("_ClearNight", Hex("#12160A"));
            titan.SetColor("_RimColor", Hex("#F7D56A"));
            titan.SetColor("_SaturnColor", new Color(.38f, .29f, .12f));
            sky = Material("17 Space Sky", skyShader);
            ringStyles = new RingStyleSet
            {
                A = RingMaterial("10 Ring A", rings, "#D4C7AA", .46f),
                B = RingMaterial("11 Ring B", rings, "#E1D3B8", .72f),
                C = RingMaterial("12 Ring C", rings, "#CBBBA5", .26f),
                D = RingMaterial("13 Ring D", rings, "#B9A787", .08f),
                E = RingMaterial("14 Ring E", rings, "#B4C3C8", .015f),
                F = RingMaterial("15 Ring F", rings, "#D8C6A9", .32f),
                G = RingMaterial("16 Ring G", rings, "#B9B6AC", .025f)
            };

            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { InteriorPrefabs });
            int prefabCount = 0;
            foreach (string guid in prefabs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(InteriorPrefabs + "/", StringComparison.Ordinal)) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = ApplyToRenderers(root);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabCount++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject exterior = PrefabUtility.LoadPrefabContents(ExteriorPrefab);
            exterior.GetComponent<Renderer>().sharedMaterial = moon;
            PrefabUtility.SaveAsPrefabAsset(exterior, ExteriorPrefab);
            PrefabUtility.UnloadPrefabContents(exterior);

            var scene = EditorSceneManager.OpenScene(ScenePath);
            int sceneCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (ApplyToRenderers(root)) sceneCount++;

            foreach (ExteriorView exteriorView in UnityEngine.Object.FindObjectsByType<ExteriorView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                exteriorView.saturnMaterial = saturn;
                exteriorView.moonMaterial = moon;
                exteriorView.titanMaterial = titan;
                exteriorView.skyMaterial = sky;
                exteriorView.rings = ringStyles;
                EditorUtility.SetDirty(exteriorView);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"StylePass: {prefabCount} interior prefabs updated; {sceneCount} scene roots styled.");
        }

        [MenuItem("Cockpit/Style: capture material preview")]
        public static void Capture() => CaptureBody(false);

        [MenuItem("Cockpit/Style: capture Titan preview")]
        public static void CaptureTitan() => CaptureBody(true);

        [MenuItem("Cockpit/Style: capture Titan closeup")]
        public static void CaptureTitanCloseup() => CaptureTitanSphere(Quaternion.identity, "TitanCloseup.png");

        [MenuItem("Cockpit/Style: capture Titan far side")]
        public static void CaptureTitanFarSide() => CaptureTitanSphere(Quaternion.Euler(15, 160, 0), "TitanFarSide.png");

        static void CaptureTitanSphere(Quaternion rotation, string fileName)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Titan closeup sample";
            sphere.layer = 30;
            sphere.transform.rotation = rotation;
            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            sphereRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/09 Titan.mat");
            MaterialPropertyBlock saturnFill = new();
            saturnFill.SetVector("_SaturnDirection", new Vector4(.75f, .05f, -.66f, 0));
            sphereRenderer.SetPropertyBlock(saturnFill);

            GameObject lightObject = new("Preview sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("#FFF1DB");
            light.intensity = 1f;
            light.cullingMask = 1 << 30;
            lightObject.transform.rotation = Quaternion.LookRotation(new Vector3(.85f, -.15f, .35f));

            GameObject cameraObject = new("Preview camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -3);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = .67f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;

            RenderTexture target = new(800, 800, 24);
            Texture2D pixels = new(800, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Assets/Art/Previews");
                string path = "Assets/Art/Previews/" + fileName;
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                Debug.Log($"StylePass: preview saved to {path}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(sphere);
            }
        }

        static void CaptureBody(bool showTitan)
        {
            EditorSceneManager.OpenScene(ScenePath);
            Camera eye = GameObject.Find("EyeCam")?.GetComponent<Camera>();
            if (eye == null) throw new InvalidOperationException("StylePass: EyeCam was not found.");
            eye.transform.rotation *= Quaternion.Euler(8f, 0f, 0f);

            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sample.name = showTitan ? "Titan material sample (preview only)" :
                "Saturn material sample (preview only)";
            sample.transform.position = eye.transform.position + eye.transform.forward * 15f +
                eye.transform.right * 1f + eye.transform.up * 1.3f;
            sample.transform.localScale = Vector3.one * 4f;
            sample.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                Folder + (showTitan ? "/09 Titan.mat" : "/07 Saturn.mat"));
            if (showTitan)
            {
                MaterialPropertyBlock saturnFill = new();
                saturnFill.SetVector("_SaturnDirection", new Vector4(.75f, .05f, -.66f, 0));
                sample.GetComponent<Renderer>().SetPropertyBlock(saturnFill);
            }
            if (!showTitan)
            {
                // В игре кольца лежат в экваторе; для превью образец наклонён, чтобы их было видно.
                sample.transform.rotation = Quaternion.Euler(52f, 0f, 24f);
                StylizedRing.Create(sample.transform, 58232f, LoadRingStyles());
            }

            Texture2D screenGraphic = PreviewScreen();
            Material screenMaterial = new Material(Shader.Find("Sim/ScreenSurface"))
                { name = "Temporary screen preview", mainTexture = screenGraphic };
            foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
                if (renderer.name.ToLowerInvariant().Contains("screen")) renderer.sharedMaterial = screenMaterial;

            GameObject sunlight = new GameObject("Preview sun");
            Light light = sunlight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("#FFF0D7");
            light.intensity = .8f;
            sunlight.transform.rotation = showTitan ?
                Quaternion.LookRotation(eye.transform.forward * .35f + eye.transform.right * .9f) :
                Quaternion.LookRotation(eye.transform.forward + eye.transform.right * .5f);

            RenderTexture target = new RenderTexture(1280, 720, 24);
            Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = eye.targetTexture;
            CameraClearFlags oldClear = eye.clearFlags;
            Color oldBackground = eye.backgroundColor;
            try
            {
                eye.clearFlags = CameraClearFlags.SolidColor;
                eye.backgroundColor = Hex("#080D14");
                eye.targetTexture = target;
                eye.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Assets/Art/Previews");
                string path = showTitan ? "Assets/Art/Previews/TitanFirstPass.png" :
                    "Assets/Art/Previews/FirstPass.png";
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                Debug.Log($"StylePass: preview saved to {path}");
            }
            finally
            {
                eye.targetTexture = oldTarget;
                eye.clearFlags = oldClear;
                eye.backgroundColor = oldBackground;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(sample);
                UnityEngine.Object.DestroyImmediate(sunlight);
                UnityEngine.Object.DestroyImmediate(screenMaterial);
                UnityEngine.Object.DestroyImmediate(screenGraphic);
            }
        }

        static Texture2D PreviewScreen()
        {
            const int width = 512, height = 256;
            Texture2D texture = new(width, height, TextureFormat.RGB24, false);
            Color32 background = new(10, 26, 34, 255);
            Color32 grid = new(24, 48, 55, 255);
            Color32 line = new(80, 174, 156, 255);
            Color32 target = new(176, 205, 181, 255);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int dx = x - 256, dy = y - 128;
                int radius = dx * dx + dy * dy;
                bool orbit = Mathf.Abs(radius - 94 * 94) < 120 || Mathf.Abs(radius - 58 * 58) < 80;
                bool axes = Mathf.Abs(dx) < 1 || Mathf.Abs(dy) < 1;
                bool mark = (dx - 70) * (dx - 70) + (dy - 63) * (dy - 63) < 30;
                texture.SetPixel(x, y, mark ? target : orbit || axes ? line :
                    x % 32 == 0 || y % 32 == 0 ? grid : background);
            }
            texture.Apply();
            return texture;
        }

        static Material Matte(string name, Shader shader, string color, string shade, float shadowLevel)
        {
            Material material = Material(name, shader);
            material.SetColor("_Color", Hex(color));
            material.SetColor("_ShadeColor", Hex(shade));
            material.SetFloat("_ShadowLevel", shadowLevel);
            material.SetFloat("_MidLevel", .82f);
            material.SetFloat("_Edge", .35f);
            material.SetFloat("_Softness", .12f);
            return material;
        }

        static Material Body(string name, Shader shader, string color, string band, string night, float strength)
        {
            Material material = Material(name, shader);
            material.SetColor("_Color", Hex(color));
            material.SetColor("_BandColor", Hex(band));
            material.SetColor("_ShadeColor", Hex(night));
            material.SetFloat("_BandStrength", strength);
            material.SetFloat("_Edge", .06f);
            material.SetFloat("_Softness", .1f);
            return material;
        }

        static Material RingMaterial(string name, Shader shader, string color, float opacity)
        {
            Material material = Material(name, shader);
            Color tint = Hex(color);
            tint.a = opacity;
            material.SetColor("_Color", tint);
            return material;
        }

        static RingStyleSet LoadRingStyles() => new()
        {
            A = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/10 Ring A.mat"),
            B = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/11 Ring B.mat"),
            C = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/12 Ring C.mat"),
            D = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/13 Ring D.mat"),
            E = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/14 Ring E.mat"),
            F = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/15 Ring F.mat"),
            G = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/16 Ring G.mat")
        };

        static Material Material(string name, Shader shader)
        {
            string path = $"{Folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
                throw new ArgumentException(value);
            return color;
        }

        static bool ApplyToRenderers(GameObject root)
        {
            bool changed = false;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.layer == LayerMask.NameToLayer("Exterior")) continue;
                Material chosen = Choose(renderer);
                if (chosen == null) continue;
                Material current = renderer.sharedMaterial;
                if (current != null && current != chosen &&
                    current.shader.name != "Standard" && !current.shader.name.StartsWith("Suturn/", StringComparison.Ordinal) &&
                    current.name != "Default-Material") continue;
                if (current == chosen) continue;
                renderer.sharedMaterial = chosen;
                EditorUtility.SetDirty(renderer);
                changed = true;
            }
            foreach (PanelLamp lamp in root.GetComponentsInChildren<PanelLamp>(true))
            {
                Color on = Hex("#6AD2AA"), off = Hex("#1F302F");
                if (lamp.on == on && lamp.off == off) continue;
                lamp.on = on;
                lamp.off = off;
                EditorUtility.SetDirty(lamp);
                changed = true;
            }
            return changed;
        }

        static Material Choose(MeshRenderer renderer)
        {
            string name = renderer.name.ToLowerInvariant();
            if (name.Contains("screen") || name.Contains("label") || name.Contains("readout") ||
                name.Contains("table") || renderer.GetComponent("TextMeshPro") != null) return null;
            if (renderer.GetComponent<PanelLamp>() != null || name.StartsWith("light")) return indicator;
            if (name.Contains("cabin")) return shell;
            if (name.Contains("floor") || name.Contains("seat")) return recess;
            if (name.Contains("button")) return accent;
            if (name.Contains("switch") || name.Contains("knob") || name.Contains("wheel") ||
                name.Contains("stick") || name.Contains("lever") || name.Contains("cone")) return grip;
            if (name.Contains("monitor") || name.Contains("keyboard")) return recess;
            if (name.Contains("base") || name.Contains("block") || name.Contains("panel")) return panel;
            return null;
        }
    }
}
