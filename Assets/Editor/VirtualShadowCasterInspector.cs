using UnityEngine;
using UnityEditor;
using AdaptiveRendering;

namespace AdaptiveRenderingEditor
{
    [CustomEditor(typeof(VirtualShadowCaster))]
    [CanEditMultipleObjects]
    public class VirtualShadowCasterInspector : Editor
    {
        private SerializedProperty castShadowProp;
        
        private static GUIStyle headerStyle;
        private static GUIStyle boxStyle;
        private static Texture2D headerBg;

        void OnEnable()
        {
            castShadowProp = serializedObject.FindProperty("castShadow");
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            
            var caster = target as VirtualShadowCaster;
            
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(5);

            DrawStatusBox(caster);
            EditorGUILayout.Space(10);

            DrawSettings();
            EditorGUILayout.Space(5);

            DrawInfo(caster);

            serializedObject.ApplyModifiedProperties();
        }

        void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 14;
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.normal.textColor = new Color(1f, 0.9f, 0.7f);
                headerStyle.padding = new RectOffset(0, 0, 10, 10);
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox);
                boxStyle.padding = new RectOffset(10, 10, 10, 10);
            }

            if (headerBg == null)
            {
                headerBg = MakeTex(2, 2, new Color(0.4f, 0.35f, 0.2f, 0.3f));
            }
        }

        void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, headerBg);
            
            var labelRect = new Rect(rect.x, rect.y + 10, rect.width, 40);
            GUI.Label(labelRect, "Virtual Shadow Caster", headerStyle);
            
            var subLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            subLabelStyle.alignment = TextAnchor.MiddleCenter;
            subLabelStyle.normal.textColor = Color.gray;
            var subRect = new Rect(rect.x, rect.y + 35, rect.width, 20);
            GUI.Label(subRect, "Shadow Casting Control", subLabelStyle);
        }

        void DrawStatusBox(VirtualShadowCaster caster)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(80));
            
            var statusColor = caster.castShadow ? new Color(0.3f, 0.8f, 0.3f) : Color.gray;
            var prevColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label("", GUILayout.Width(20));
            GUI.color = prevColor;
            
            EditorGUILayout.LabelField(caster.castShadow ? "Casting Shadows" : "Not Casting", GUILayout.Width(120));
            
            GUILayout.FlexibleSpace();
            
            var renderer = caster.GetComponent<Renderer>();
            if (renderer != null)
            {
                EditorGUILayout.LabelField($"Renderer: {renderer.GetType().Name}", GUILayout.Width(150));
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        void DrawSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Occlusion Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(castShadowProp, new GUIContent("Cast Shadow", "Enable/disable shadow casting for this object"));
            
            EditorGUILayout.HelpBox(
                "When enabled, this object will cast shadows in the Virtual Shadow Map system.\n\n" +
                "Note: The object's Renderer must also have 'Cast Shadows' enabled in its settings.",
                MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        void DrawInfo(VirtualShadowCaster caster)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Component Info", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            var renderer = caster.GetComponent<Renderer>();
            if (renderer != null)
            {
                EditorGUILayout.LabelField("Renderer Type", renderer.GetType().Name);
                EditorGUILayout.LabelField("Unity Shadow Mode", renderer.shadowCastingMode.ToString());
                
                if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    EditorGUILayout.HelpBox(
                        "Warning: Renderer's shadow casting is disabled. Enable it in the Renderer component for shadows to work.",
                        MessageType.Warning);
                }
                
                var bounds = renderer.bounds;
                EditorGUILayout.LabelField("Bounds Size", bounds.size.ToString("F2"));
            }
            else
            {
                EditorGUILayout.HelpBox("No Renderer component found!", MessageType.Error);
            }
            
            EditorGUILayout.EndVertical();
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
