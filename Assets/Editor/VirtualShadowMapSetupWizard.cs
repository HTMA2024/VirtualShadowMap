using UnityEngine;
using UnityEditor;
using AdaptiveRendering;

namespace AdaptiveRenderingEditor
{
    public class VirtualShadowMapSetupWizard : EditorWindow
    {
        private Light directionalLight;
        private Camera mainCamera;
        private OcclusionQualityPreset quality = OcclusionQualityPreset.Medium;
        private Vector2 scrollPos;

        [MenuItem("Window/Cascaded Occlusion/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<VirtualShadowMapSetupWizard>("VSM Setup", true);
            window.minSize = new Vector2(450, 500);
            window.maxSize = new Vector2(800, 1000);
        }

        void OnEnable()
        {
            AutoDetectComponents();
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            DrawHeader();
            EditorGUILayout.Space(15);

            DrawStep1();
            EditorGUILayout.Space(15);

            DrawStep2();
            EditorGUILayout.Space(15);

            DrawStep3();
            EditorGUILayout.Space(20);

            DrawSetupButton();
            EditorGUILayout.Space(15);

            DrawHelpSection();
            EditorGUILayout.Space(10);

            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            var headerBg = MakeTex(2, 2, new Color(0.2f, 0.3f, 0.4f, 0.3f));
            var rect = GUILayoutUtility.GetRect(0, 70, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, headerBg);
            
            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 18;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);
            
            var labelRect = new Rect(rect.x, rect.y + 15, rect.width, 30);
            GUI.Label(labelRect, "Virtual Shadow Maps Setup", titleStyle);
            
            var subStyle = new GUIStyle(EditorStyles.miniLabel);
            subStyle.alignment = TextAnchor.MiddleCenter;
            subStyle.normal.textColor = Color.gray;
            var subRect = new Rect(rect.x, rect.y + 45, rect.width, 20);
            GUI.Label(subRect, "Quick Configuration Wizard", subStyle);
        }

        void DrawStep1()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var stepStyle = new GUIStyle(EditorStyles.boldLabel);
            stepStyle.fontSize = 13;
            EditorGUILayout.LabelField("Step 1: Directional Light", stepStyle);
            EditorGUILayout.Space(5);
            
            directionalLight = (Light)EditorGUILayout.ObjectField("Light", directionalLight, typeof(Light), true);
            
            if (directionalLight != null && directionalLight.type != LightType.Directional)
            {
                EditorGUILayout.HelpBox("âš?Light must be Directional type!", MessageType.Error);
                directionalLight = null;
            }
            else if (directionalLight == null)
            {
                EditorGUILayout.HelpBox("Select a Directional Light or click Auto-detect", MessageType.Info);
                if (GUILayout.Button("Auto-detect Directional Light", GUILayout.Height(25)))
                {
                    AutoDetectLight();
                }
            }
            else
            {
                var vsm = directionalLight.GetComponent<VirtualShadowMaps>();
                if (vsm != null)
                {
                    EditorGUILayout.HelpBox("âœ?VirtualShadowMaps already attached", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"âœ?Selected: {directionalLight.name}", MessageType.None);
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        void DrawStep2()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var stepStyle = new GUIStyle(EditorStyles.boldLabel);
            stepStyle.fontSize = 13;
            EditorGUILayout.LabelField("Step 2: Main Camera", stepStyle);
            EditorGUILayout.Space(5);
            
            mainCamera = (Camera)EditorGUILayout.ObjectField("Camera", mainCamera, typeof(Camera), true);
            
            if (mainCamera == null)
            {
                EditorGUILayout.HelpBox("Select a Camera or click Auto-detect", MessageType.Info);
                if (GUILayout.Button("Auto-detect Main Camera", GUILayout.Height(25)))
                {
                    AutoDetectCamera();
                }
            }
            else
            {
                var vsmCam = mainCamera.GetComponent<VirtualShadowCamera>();
                if (vsmCam != null)
                {
                    EditorGUILayout.HelpBox("âœ?VirtualShadowCamera already attached", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"âœ?Selected: {mainCamera.name}", MessageType.None);
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        void DrawStep3()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var stepStyle = new GUIStyle(EditorStyles.boldLabel);
            stepStyle.fontSize = 13;
            EditorGUILayout.LabelField("Step 3: Quality Preset", stepStyle);
            EditorGUILayout.Space(5);
            
            quality = (OcclusionQualityPreset)EditorGUILayout.EnumPopup("Quality", quality);
            
            EditorGUILayout.Space(5);
            
            var desc = GetQualityDescription(quality);
            var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            descStyle.fontSize = 11;
            EditorGUILayout.LabelField(desc, descStyle);
            
            EditorGUILayout.EndVertical();
        }

        void DrawSetupButton()
        {
            bool canSetup = directionalLight != null && mainCamera != null;
            
            GUI.enabled = canSetup;
            
            var prevBgColor = GUI.backgroundColor;
            GUI.backgroundColor = canSetup ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
            
            if (GUILayout.Button("Setup Virtual Shadow Maps", GUILayout.Height(45)))
            {
                SetupVSM();
            }
            
            GUI.backgroundColor = prevBgColor;
            GUI.enabled = true;
            
            if (!canSetup)
            {
                EditorGUILayout.HelpBox("Please complete Steps 1 and 2 before setup", MessageType.Warning);
            }
        }

        void DrawHelpSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("What will be configured:", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.LabelField("â€?VirtualShadowMaps component on Directional Light", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("â€?VirtualShadowCamera component on Main Camera", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("â€?VirtualShadowVolume GameObject (if not exists)", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("â€?Quality settings applied", EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("After setup:", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.LabelField("1. Select VirtualShadowMaps to adjust settings", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("2. Use Baking Tools to generate shadow maps", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("3. Test in Play mode or Scene view", EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.EndVertical();
        }

        void AutoDetectComponents()
        {
            AutoDetectLight();
            AutoDetectCamera();
        }

        void AutoDetectLight()
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    directionalLight = light;
                    break;
                }
            }
        }

        void AutoDetectCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameras = FindObjectsOfType<Camera>();
                if (cameras.Length > 0)
                    mainCamera = cameras[0];
            }
        }

        void SetupVSM()
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            // Setup Light
            var vsm = directionalLight.GetComponent<VirtualShadowMaps>();
            bool vsmAdded = false;
            if (vsm == null)
            {
                vsm = Undo.AddComponent<VirtualShadowMaps>(directionalLight.gameObject);
                vsmAdded = true;
                Debug.Log($"[VSM Setup] Added VirtualShadowMaps to {directionalLight.name}");
            }
            
            Undo.RecordObject(vsm, "Setup VSM Quality");
            vsm.SetQualityLevel(quality);
            EditorUtility.SetDirty(vsm);

            // Setup Camera
            var vsmCam = mainCamera.GetComponent<VirtualShadowCamera>();
            bool vsmCamAdded = false;
            if (vsmCam == null)
            {
                vsmCam = Undo.AddComponent<VirtualShadowCamera>(mainCamera.gameObject);
                vsmCamAdded = true;
                Debug.Log($"[VSM Setup] Added VirtualShadowCamera to {mainCamera.name}");
            }

            // Setup Volume (optional)
            var volume = FindObjectOfType<VirtualShadowVolume>();
            bool volumeAdded = false;
            if (volume == null)
            {
                var volumeGO = new GameObject("Virtual Shadow Volume");
                Undo.RegisterCreatedObjectUndo(volumeGO, "Create VSM Volume");
                volume = volumeGO.AddComponent<VirtualShadowVolume>();
                volumeAdded = true;
                Debug.Log("[VSM Setup] Created VirtualShadowVolume");
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Build result message
            string message = "Virtual Shadow Maps has been configured!\n\n";
            
            if (vsmAdded || vsmCamAdded || volumeAdded)
            {
                message += "Components added:\n";
                if (vsmAdded)
                    message += $"âœ?VirtualShadowMaps on {directionalLight.name}\n";
                if (vsmCamAdded)
                    message += $"âœ?VirtualShadowCamera on {mainCamera.name}\n";
                if (volumeAdded)
                    message += "âœ?VirtualShadowVolume GameObject\n";
            }
            else
            {
                message += "All components already exist.\n";
            }
            
            message += $"\nQuality: {quality}\n\n";
            message += "Next steps:\n";
            message += "1. Select the Directional Light to adjust settings\n";
            message += "2. Use Baking Tools to generate shadow maps\n";
            message += "3. Test in Play mode";

            EditorUtility.DisplayDialog("Setup Complete", message, "OK");
            
            // Select the light for easy access
            Selection.activeGameObject = directionalLight.gameObject;
        }

        string GetQualityDescription(OcclusionQualityPreset level)
        {
            switch (level)
            {
                case OcclusionQualityPreset.Low:
                    return "Fast performance, suitable for mobile or low-end hardware. Lower shadow quality but better frame rate.";
                case OcclusionQualityPreset.Medium:
                    return "Balanced performance and quality for most use cases. Good compromise between quality and performance.";
                case OcclusionQualityPreset.High:
                    return "Better shadows, requires more memory and performance. Suitable for high-end PC and consoles.";
                case OcclusionQualityPreset.Ultra:
                    return "Best shadow quality, high memory and performance cost. For high-end hardware only.";
                default:
                    return "";
            }
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
