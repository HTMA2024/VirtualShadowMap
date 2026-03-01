using UnityEngine;
using UnityEditor;
using AdaptiveRendering;

namespace AdaptiveRenderingEditor
{
    [CustomEditor(typeof(VirtualShadowDebug))]
    public class VirtualShadowDebugInspector : Editor
    {
        private SerializedProperty splitViewProp;
        private SerializedProperty splitPositionProp;
        
        private static GUIStyle headerStyle;
        private static GUIStyle boxStyle;
        private static Texture2D headerBg;

        void OnEnable()
        {
            splitViewProp = serializedObject.FindProperty("splitView");
            splitPositionProp = serializedObject.FindProperty("splitPosition");
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(5);

            DrawSplitViewSection();
            EditorGUILayout.Space(5);

            DrawInstructions();

            serializedObject.ApplyModifiedProperties();
        }

        void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 14;
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
                headerStyle.padding = new RectOffset(0, 0, 10, 10);
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox);
                boxStyle.padding = new RectOffset(10, 10, 10, 10);
            }

            if (headerBg == null)
            {
                headerBg = MakeTex(2, 2, new Color(0.4f, 0.3f, 0.2f, 0.3f));
            }
        }

        void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, headerBg);
            
            var labelRect = new Rect(rect.x, rect.y + 10, rect.width, 40);
            GUI.Label(labelRect, "Virtual Shadow Debug", headerStyle);
            
            var subLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            subLabelStyle.alignment = TextAnchor.MiddleCenter;
            subLabelStyle.normal.textColor = Color.gray;
            var subRect = new Rect(rect.x, rect.y + 35, rect.width, 20);
            GUI.Label(subRect, "Debug Visualization", subLabelStyle);
        }

        void DrawSplitViewSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Split View Comparison", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(splitViewProp, new GUIContent("Enable Split View", "Compare Unity CSM (left) vs VSM (right)"));
            
            EditorGUI.BeginDisabledGroup(!splitViewProp.boolValue);
            EditorGUILayout.Slider(splitPositionProp, 0f, 1f, new GUIContent("Split Position", "Horizontal position of the split line"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        void DrawInstructions()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.LabelField("How to Use", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.HelpBox(
                "Split View Mode:\n" +
                "â€?Left side: Unity's built-in Cascaded Shadow Maps (CSM)\n" +
                "â€?Right side: Virtual Shadow Maps (VSM)\n\n" +
                "This allows you to compare shadow quality and performance between the two systems in real-time.",
                MessageType.Info);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Tips:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("â€?Adjust split position to see more of one side", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("â€?Move camera to see shadow differences", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("â€?Works in both Edit and Play mode", EditorStyles.wordWrappedMiniLabel);
            
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
