using UnityEngine;
using UnityEditor;
using AdaptiveRendering;

namespace AdaptiveRenderingEditor
{
    [CustomEditor(typeof(VirtualShadowCamera))]
    [CanEditMultipleObjects]
    public class VirtualShadowCameraInspector : Editor
    {
        private SerializedProperty levelOfDetailProp;
        
        private bool showStats = true;
        private bool showDebug = false;
        
        private static GUIStyle headerStyle;
        private static GUIStyle boxStyle;
        private static Texture2D headerBg;

        void OnEnable()
        {
            levelOfDetailProp = serializedObject.FindProperty("levelOfDetail");
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            
            var vsmCam = target as VirtualShadowCamera;
            
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(5);

            DrawStatusBox(vsmCam);
            EditorGUILayout.Space(10);

            DrawSettings();
            EditorGUILayout.Space(5);

            DrawStatsSection(vsmCam);
            EditorGUILayout.Space(5);

            DrawDebugSection(vsmCam);

            serializedObject.ApplyModifiedProperties();
        }

        void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 14;
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);
                headerStyle.padding = new RectOffset(0, 0, 10, 10);
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox);
                boxStyle.padding = new RectOffset(10, 10, 10, 10);
            }

            if (headerBg == null)
            {
                headerBg = MakeTex(2, 2, new Color(0.2f, 0.4f, 0.3f, 0.3f));
            }
        }

        void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, headerBg);
            
            var labelRect = new Rect(rect.x, rect.y + 10, rect.width, 40);
            GUI.Label(labelRect, "Virtual Shadow Camera", headerStyle);
            
            var subLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            subLabelStyle.alignment = TextAnchor.MiddleCenter;
            subLabelStyle.normal.textColor = Color.gray;
            var subRect = new Rect(rect.x, rect.y + 35, rect.width, 20);
            GUI.Label(subRect, "Camera Component", subLabelStyle);
        }

        void DrawStatusBox(VirtualShadowCamera vsmCam)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel, GUILayout.Width(80));
            
            var cam = vsmCam.GetCamera();
            if (cam != null)
            {
                EditorGUILayout.LabelField(cam.name, GUILayout.Width(120));
            }
            
            GUILayout.FlexibleSpace();
            
            var lookupTex = vsmCam.GetLookupTexture();
            var statusColor = lookupTex != null ? new Color(0.3f, 0.8f, 0.3f) : Color.gray;
            var prevColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label("", GUILayout.Width(20));
            GUI.color = prevColor;
            
            EditorGUILayout.LabelField(lookupTex != null ? "Ready" : "Not Ready", GUILayout.Width(80));
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        void DrawSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.Slider(levelOfDetailProp, 0f, 10f, new GUIContent("Level of Detail", "Higher values load more shadow pages"));
            
            EditorGUILayout.HelpBox(
                "Level of Detail controls how many shadow pages are requested:\n" +
                "�?Lower values: Fewer pages, better performance, lower quality\n" +
                "�?Higher values: More pages, higher quality, more memory usage",
                MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        void DrawStatsSection(VirtualShadowCamera vsmCam)
        {
            showStats = EditorGUILayout.BeginFoldoutHeaderGroup(showStats, "Runtime Statistics");
            if (showStats)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                var cam = vsmCam.GetCamera();
                if (cam != null)
                {
                    EditorGUILayout.LabelField("Camera Name", cam.name);
                    EditorGUILayout.LabelField("Field of View", $"{cam.fieldOfView:F1}°");
                    EditorGUILayout.LabelField("Near Clip", $"{cam.nearClipPlane:F2}");
                    EditorGUILayout.LabelField("Far Clip", $"{cam.farClipPlane:F1}");
                }
                
                EditorGUILayout.Space(5);
                
                var lookupTex = vsmCam.GetLookupTexture();
                if (lookupTex != null)
                {
                    var rt = lookupTex as RenderTexture;
                    if (rt != null)
                    {
                        EditorGUILayout.LabelField("Indirection Texture", $"{rt.width}x{rt.height}");
                        EditorGUILayout.LabelField("Format", rt.format.ToString());
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Indirection Texture", $"{lookupTex.width}x{lookupTex.height}");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Indirection Texture", "Not Created");
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Position", vsmCam.worldSpaceCameraPosition.ToString("F2"));
                EditorGUILayout.LabelField("Direction", vsmCam.worldSpaceCameraDirection.ToString("F2"));
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawDebugSection(VirtualShadowCamera vsmCam)
        {
            showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "Debug Tools");
            if (showDebug)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                if (GUILayout.Button("Force Rebuild", GUILayout.Height(30)))
                {
                    vsmCam.SetDirty();
                    EditorUtility.SetDirty(vsmCam);
                }
                
                EditorGUILayout.Space(10);
                
                DrawTexturePreview(vsmCam);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawTexturePreview(VirtualShadowCamera vsmCam)
        {
            EditorGUILayout.LabelField("Texture Previews", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            // Lookup Texture
            var lookupTex = vsmCam.GetLookupTexture();
            if (lookupTex != null)
            {
                EditorGUILayout.LabelField("Lookup Texture (TileNode Table)", EditorStyles.miniBoldLabel);
                var rt = lookupTex as RenderTexture;
                if (rt != null)
                {
                    EditorGUILayout.LabelField($"Size: {rt.width}x{rt.height}, Format: {rt.format}", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"Size: {lookupTex.width}x{lookupTex.height}", EditorStyles.miniLabel);
                }
                
                var rect = GUILayoutUtility.GetRect(200, 200);
                EditorGUI.DrawPreviewTexture(rect, lookupTex, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox("Lookup Texture not available", MessageType.Info);
            }
            
            EditorGUILayout.Space(10);
            
            // Tile Texture (Shadow Atlas)
            var vsmLight = FindObjectOfType<VirtualShadowMaps>();
            if (vsmLight != null)
            {
                var tileTexture = vsmLight.isActiveAndEnabled && vsmLight.shadowOn ? vsmLight.FetchSurface() : null;
                if (tileTexture != null)
                {
                    EditorGUILayout.LabelField("Tile Texture (Shadow Atlas)", EditorStyles.miniBoldLabel);
                    var rt = tileTexture as RenderTexture;
                    if (rt != null)
                    {
                        EditorGUILayout.LabelField($"Size: {rt.width}x{rt.height}, Format: {rt.format}", EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Size: {tileTexture.width}x{tileTexture.height}", EditorStyles.miniLabel);
                    }
                    
                    var rect = GUILayoutUtility.GetRect(200, 200);
                    EditorGUI.DrawPreviewTexture(rect, tileTexture, null, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUILayout.HelpBox("Tile Texture not available (shadow may be off)", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("VirtualShadowMaps component not found in scene", MessageType.Warning);
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
