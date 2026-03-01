using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(OrbitViewController))]
public class OrbitViewControllerInspector : Editor
{
    private SerializedProperty translationRateProp;
    private SerializedProperty sprintFactorProp;
    private SerializedProperty crawlFactorProp;
    private SerializedProperty rotationGainProp;
    private SerializedProperty dampingFactorProp;
    private SerializedProperty scrollSpeedChangeProp;
    private SerializedProperty minSpeedProp;
    private SerializedProperty maxSpeedProp;
    
    private bool showMovement = true;
    private bool showLook = true;
    private bool showAdvanced = false;
    
    private static GUIStyle headerStyle;
    private static GUIStyle boxStyle;
    private static Texture2D headerBg;

    void OnEnable()
    {
        translationRateProp = serializedObject.FindProperty("moveSpeed");
        sprintFactorProp = serializedObject.FindProperty("boostMultiplier");
        crawlFactorProp = serializedObject.FindProperty("slowMultiplier");
        rotationGainProp = serializedObject.FindProperty("lookSensitivity");
        dampingFactorProp = serializedObject.FindProperty("smoothFactor");
        scrollSpeedChangeProp = serializedObject.FindProperty("scrollSpeedChange");
        minSpeedProp = serializedObject.FindProperty("minSpeed");
        maxSpeedProp = serializedObject.FindProperty("maxSpeed");
    }

    public override void OnInspectorGUI()
    {
        InitStyles();
        
        serializedObject.Update();

        DrawHeader();
        EditorGUILayout.Space(5);

        DrawControls();
        EditorGUILayout.Space(10);

        DrawMovementSection();
        EditorGUILayout.Space(5);

        DrawLookSection();
        EditorGUILayout.Space(5);

        DrawAdvancedSection();

        serializedObject.ApplyModifiedProperties();
    }

    void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);
            headerStyle.padding = new RectOffset(0, 0, 10, 10);
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(EditorStyles.helpBox);
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
        }

        if (headerBg == null)
        {
            headerBg = MakeTex(2, 2, new Color(0.2f, 0.3f, 0.5f, 0.3f));
        }
    }

    void DrawHeader()
    {
        var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, headerBg);
        
        var labelRect = new Rect(rect.x, rect.y + 10, rect.width, 40);
        GUI.Label(labelRect, "Free Camera", headerStyle);
        
        var subLabelStyle = new GUIStyle(EditorStyles.miniLabel);
        subLabelStyle.alignment = TextAnchor.MiddleCenter;
        subLabelStyle.normal.textColor = Color.gray;
        var subRect = new Rect(rect.x, rect.y + 35, rect.width, 20);
        GUI.Label(subRect, "First-Person Camera Controller", subLabelStyle);
    }

    void DrawControls()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);
        
        var controlStyle = new GUIStyle(EditorStyles.miniLabel);
        controlStyle.richText = true;
        
        EditorGUILayout.LabelField("<b>Movement:</b> WASD / Arrow Keys", controlStyle);
        EditorGUILayout.LabelField("<b>Up/Down:</b> Q / E", controlStyle);
        EditorGUILayout.LabelField("<b>Look:</b> Right Mouse Button + Drag", controlStyle);
        EditorGUILayout.LabelField("<b>Sprint:</b> Hold Shift", controlStyle);
        EditorGUILayout.LabelField("<b>Slow:</b> Hold Ctrl", controlStyle);
        EditorGUILayout.LabelField("<b>Speed:</b> Mouse Scroll Wheel", controlStyle);
        
        EditorGUILayout.EndVertical();
    }

    void DrawMovementSection()
    {
        showMovement = EditorGUILayout.BeginFoldoutHeaderGroup(showMovement, "Movement Settings");
        if (showMovement)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.Slider(translationRateProp, 0.1f, 100f, new GUIContent("Base Speed", "Normal movement speed"));
            EditorGUILayout.Slider(sprintFactorProp, 1f, 10f, new GUIContent("Sprint Multiplier", "Speed multiplier when holding Shift"));
            EditorGUILayout.Slider(crawlFactorProp, 0.1f, 1f, new GUIContent("Slow Multiplier", "Speed multiplier when holding Ctrl"));
            
            EditorGUILayout.Space(5);
            
            var currentSpeed = translationRateProp.floatValue;
            var sprintSpeed = currentSpeed * sprintFactorProp.floatValue;
            var slowSpeed = currentSpeed * crawlFactorProp.floatValue;
            
            EditorGUILayout.LabelField("Speed Preview:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Normal: {currentSpeed:F1} units/sec");
            EditorGUILayout.LabelField($"Sprint: {sprintSpeed:F1} units/sec");
            EditorGUILayout.LabelField($"Slow: {slowSpeed:F1} units/sec");
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawLookSection()
    {
        showLook = EditorGUILayout.BeginFoldoutHeaderGroup(showLook, "Look Settings");
        if (showLook)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.Slider(rotationGainProp, 0.1f, 10f, new GUIContent("Look Sensitivity", "Mouse sensitivity for camera rotation"));
            EditorGUILayout.Slider(dampingFactorProp, 0f, 0.5f, new GUIContent("Smooth Time", "Camera rotation smoothing (0 = instant)"));
            
            EditorGUILayout.Space(5);
            
            if (dampingFactorProp.floatValue > 0)
            {
                EditorGUILayout.HelpBox("Smoothing enabled - camera rotation will be interpolated", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("No smoothing - camera rotation will be instant", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawAdvancedSection()
    {
        showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvanced, "Advanced Settings");
        if (showAdvanced)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.Slider(scrollSpeedChangeProp, 0.1f, 10f, new GUIContent("Scroll Speed Change", "Speed adjustment per scroll wheel tick"));
            EditorGUILayout.Slider(minSpeedProp, 0.1f, 10f, new GUIContent("Min Speed", "Minimum allowed movement speed"));
            EditorGUILayout.Slider(maxSpeedProp, 10f, 500f, new GUIContent("Max Speed", "Maximum allowed movement speed"));
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Speed Range:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"{minSpeedProp.floatValue:F1} - {maxSpeedProp.floatValue:F1} units/sec");
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
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
