using UnityEngine;
using UnityEditor;
using AdaptiveRendering;

namespace AdaptiveRenderingEditor
{
    [CustomEditor(typeof(VirtualShadowVolume))]
    public class VirtualShadowVolumeInspector : Editor
    {
        private SerializedProperty boundsProp;
        private SerializedProperty renderersProp;
        
        private bool showGizmos = true;
        
        private static GUIStyle headerStyle;
        private static GUIStyle boxStyle;
        private static Texture2D headerBg;

        void OnEnable()
        {
            boundsProp = serializedObject.FindProperty("bounds");
            renderersProp = serializedObject.FindProperty("m_Renderers");
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            
            var volume = target as VirtualShadowVolume;
            
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(5);

            DrawBoundsSection(volume);
            EditorGUILayout.Space(5);

            DrawToolsSection(volume);
            EditorGUILayout.Space(5);

            DrawGizmosSection();

            serializedObject.ApplyModifiedProperties();
        }

        void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 14;
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.normal.textColor = new Color(0.8f, 1f, 0.8f);
                headerStyle.padding = new RectOffset(0, 0, 10, 10);
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox);
                boxStyle.padding = new RectOffset(10, 10, 10, 10);
            }

            if (headerBg == null)
            {
                headerBg = MakeTex(2, 2, new Color(0.2f, 0.4f, 0.2f, 0.3f));
            }
        }

        void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, headerBg);
            
            var labelRect = new Rect(rect.x, rect.y + 10, rect.width, 40);
            GUI.Label(labelRect, "Virtual Shadow Volume", headerStyle);
            
            var subLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            subLabelStyle.alignment = TextAnchor.MiddleCenter;
            subLabelStyle.normal.textColor = Color.gray;
            var subRect = new Rect(rect.x, rect.y + 35, rect.width, 20);
            GUI.Label(subRect, "Shadow Coverage Area", subLabelStyle);
        }

        void DrawBoundsSection(VirtualShadowVolume volume)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Volume Bounds", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(boundsProp, new GUIContent("Bounds", "Shadow volume bounds"));
            
            EditorGUILayout.Space(5);
            
            var bounds = volume.bounds;
            EditorGUILayout.LabelField("Center", bounds.center.ToString("F2"));
            EditorGUILayout.LabelField("Size", bounds.size.ToString("F2"));
            EditorGUILayout.LabelField("Volume", $"{bounds.size.x * bounds.size.y * bounds.size.z:F1} units³");
            
            EditorGUILayout.Space(5);
            
            var renderers = volume.renderers;
            if (renderers != null && renderers.Length > 0)
            {
                EditorGUILayout.LabelField("Collected Renderers", $"{renderers.Length} objects");
            }
            else
            {
                EditorGUILayout.HelpBox("No renderers collected. Click 'Collect Scene Objects' to scan the scene.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawToolsSection(VirtualShadowVolume volume)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Quick Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            if (GUILayout.Button("Collect Scene Objects", GUILayout.Height(35)))
            {
                Undo.RecordObject(volume, "Collect Scene Objects");
                volume.Collect();
                EditorUtility.SetDirty(volume);
            }
            
            EditorGUILayout.HelpBox("Scans the scene for static objects and LOD groups to include in shadow volume.", MessageType.None);
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Fit to Scene Bounds", GUILayout.Height(30)))
            {
                FitToSceneBounds(volume);
            }
            
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Expand by 10%", GUILayout.Height(25)))
            {
                ExpandBounds(volume, 1.1f);
            }
            if (GUILayout.Button("Expand by 50%", GUILayout.Height(25)))
            {
                ExpandBounds(volume, 1.5f);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.Height(25)))
            {
                Undo.RecordObject(volume, "Clear Volume");
                volume.Clear();
                EditorUtility.SetDirty(volume);
            }
            if (GUILayout.Button("Reset to Default", GUILayout.Height(25)))
            {
                ResetBounds(volume);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawGizmosSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            showGizmos = EditorGUILayout.Toggle("Show Gizmos in Scene", showGizmos);
            
            if (showGizmos)
            {
                EditorGUILayout.HelpBox(
                    "Green wireframe box shows the shadow volume bounds in the Scene view.\n" +
                    "All shadow casters within this volume will be included in VSM.",
                    MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }

        void FitToSceneBounds(VirtualShadowVolume volume)
        {
            Undo.RecordObject(volume, "Fit Volume to Scene");
            
            var renderers = FindObjectsOfType<Renderer>();
            if (renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("No Renderers", "No renderers found in scene to calculate bounds.", "OK");
                return;
            }
            
            Bounds sceneBounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                sceneBounds.Encapsulate(renderer.bounds);
            }
            
            // Add some padding
            sceneBounds.Expand(sceneBounds.size.magnitude * 0.1f);
            
            volume.bounds = sceneBounds;
            
            EditorUtility.SetDirty(volume);
        }

        void ExpandBounds(VirtualShadowVolume volume, float factor)
        {
            Undo.RecordObject(volume, "Expand Volume");
            
            var bounds = volume.bounds;
            var center = bounds.center;
            var size = bounds.size * factor;
            
            bounds.center = center;
            bounds.size = size;
            volume.bounds = bounds;
            
            EditorUtility.SetDirty(volume);
        }

        void ResetBounds(VirtualShadowVolume volume)
        {
            Undo.RecordObject(volume, "Reset Volume");
            
            volume.bounds = new Bounds(Vector3.zero, new Vector3(200, 200, 200));
            
            EditorUtility.SetDirty(volume);
        }

        void OnSceneGUI()
        {
            if (!showGizmos)
                return;
            
            var volume = target as VirtualShadowVolume;
            var bounds = volume.bounds;
            
            Handles.color = new Color(0.3f, 1f, 0.3f, 0.8f);
            Handles.DrawWireCube(bounds.center, bounds.size);
            
            // Draw corner handles for interactive editing
            Handles.color = new Color(0.3f, 1f, 0.3f, 0.5f);
            
            EditorGUI.BeginChangeCheck();
            
            // Center handle
            var newCenter = Handles.PositionHandle(bounds.center, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(volume, "Adjust Volume Center");
                var newBounds = volume.bounds;
                newBounds.center = newCenter;
                volume.bounds = newBounds;
                EditorUtility.SetDirty(volume);
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
