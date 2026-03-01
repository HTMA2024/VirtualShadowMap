using UnityEngine;
using UnityEditor;
using AdaptiveRendering;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace AdaptiveRenderingEditor
{
    [CustomEditor(typeof(VirtualShadowMaps))]
    [CanEditMultipleObjects]
    public class VirtualShadowMapsInspector : Editor
    {
        private SerializedProperty shadowDataProp;
        private SerializedProperty maxMipLevelProp;
        private SerializedProperty maxResolutionProp;
        private SerializedProperty maxTilePoolProp;
        private SerializedProperty biasProp;
        private SerializedProperty normalBiasProp;
        private SerializedProperty softnessProp;
        private SerializedProperty softnessNearProp;
        private SerializedProperty softnessFarProp;
        private SerializedProperty pcssFilterProp;
        private SerializedProperty updateIntervalProp;
        private SerializedProperty maxPageRequestLimitProp;
        private SerializedProperty drawLookupMaterialProp;
        private SerializedProperty drawTileMaterialProp;
        private SerializedProperty castMaterialProp;
        private SerializedProperty minMaxDepthComputeProp;
        private SerializedProperty regionCenterProp;
        private SerializedProperty regionSizeProp;
        private SerializedProperty paddingProp;

        private bool showAdvanced = false;
        private bool showMaterials = false;
        private bool showStats = true;
        private bool showDebug = false;
        private bool showBaking = true;

        private static GUIStyle headerStyle;
        private static GUIStyle boxStyle;
        private static Texture2D headerBg;

        private VirtualShadowMaps m_VirtualShadowMaps { get { return target as VirtualShadowMaps; } }

        void OnEnable()
        {
            shadowDataProp = serializedObject.FindProperty("shadowData");
            maxMipLevelProp = serializedObject.FindProperty("maxMipLevel");
            maxResolutionProp = serializedObject.FindProperty("maxResolution");
            maxTilePoolProp = serializedObject.FindProperty("maxTilePool");
            biasProp = serializedObject.FindProperty("bias");
            normalBiasProp = serializedObject.FindProperty("normalBias");
            softnessProp = serializedObject.FindProperty("softnesss");
            softnessNearProp = serializedObject.FindProperty("softnessNear");
            softnessFarProp = serializedObject.FindProperty("softnessFar");
            pcssFilterProp = serializedObject.FindProperty("pcssFilter");
            updateIntervalProp = serializedObject.FindProperty("updateInterval");
            maxPageRequestLimitProp = serializedObject.FindProperty("maxPageRequestLimit");
            drawLookupMaterialProp = serializedObject.FindProperty("drawLookupMaterial");
            drawTileMaterialProp = serializedObject.FindProperty("drawTileMaterial");
            castMaterialProp = serializedObject.FindProperty("castMaterial");
            minMaxDepthComputeProp = serializedObject.FindProperty("minMaxDepthCompute");
            regionCenterProp = serializedObject.FindProperty("regionCenter");
            regionSizeProp = serializedObject.FindProperty("regionSize");
            paddingProp = serializedObject.FindProperty("padding");
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            
            var vsm = target as VirtualShadowMaps;
            
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(5);

            DrawStatusBox(vsm);
            EditorGUILayout.Space(10);

            DrawQualitySection(vsm);
            EditorGUILayout.Space(5);

            DrawCoreSettings();
            EditorGUILayout.Space(5);

            DrawShadowParameters();
            EditorGUILayout.Space(5);

            DrawFilteringSection();
            EditorGUILayout.Space(5);

            DrawAdvancedSection();
            EditorGUILayout.Space(5);

            DrawMaterialsSection();
            EditorGUILayout.Space(5);

            DrawBakingSection();
            EditorGUILayout.Space(5);

            DrawStatsSection(vsm);
            EditorGUILayout.Space(5);

            DrawDebugSection(vsm);

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
                headerBg = MakeTex(2, 2, new Color(0.2f, 0.3f, 0.4f, 0.3f));
            }
        }

        void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, headerBg);
            
            var labelRect = new Rect(rect.x, rect.y + 10, rect.width, 40);
            GUI.Label(labelRect, "Cascaded Occlusion Maps", headerStyle);
            
            var subLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            subLabelStyle.alignment = TextAnchor.MiddleCenter;
            subLabelStyle.normal.textColor = Color.gray;
            var subRect = new Rect(rect.x, rect.y + 35, rect.width, 20);
            GUI.Label(subRect, "Light Component", subLabelStyle);
        }

        void DrawStatusBox(VirtualShadowMaps vsm)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(80));
            
            var statusColor = vsm.shadowOn ? new Color(0.3f, 0.8f, 0.3f) : Color.gray;
            var prevColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label("", GUILayout.Width(20));
            GUI.color = prevColor;
            
            EditorGUILayout.LabelField(vsm.shadowOn ? "Active" : "Inactive", GUILayout.Width(60));
            
            GUILayout.FlexibleSpace();
            
            if (vsm.shadowOn)
            {
                EditorGUILayout.LabelField($"Pages: {vsm.activePageCount}/{vsm.maxTilePool}", GUILayout.Width(100));
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        void DrawQualitySection(VirtualShadowMaps vsm)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quality Presets", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            
            var currentLevel = vsm.CurrentLevel;
            
            if (QualityButton("Low", currentLevel == OcclusionQualityPreset.Low))
            {
                Undo.RecordObject(vsm, "Change VSM Quality");
                vsm.SetQualityLevel(OcclusionQualityPreset.Low);
                EditorUtility.SetDirty(vsm);
            }
            
            if (QualityButton("Medium", currentLevel == OcclusionQualityPreset.Medium))
            {
                Undo.RecordObject(vsm, "Change VSM Quality");
                vsm.SetQualityLevel(OcclusionQualityPreset.Medium);
                EditorUtility.SetDirty(vsm);
            }
            
            if (QualityButton("High", currentLevel == OcclusionQualityPreset.High))
            {
                Undo.RecordObject(vsm, "Change VSM Quality");
                vsm.SetQualityLevel(OcclusionQualityPreset.High);
                EditorUtility.SetDirty(vsm);
            }
            
            if (QualityButton("Ultra", currentLevel == OcclusionQualityPreset.Ultra))
            {
                Undo.RecordObject(vsm, "Change VSM Quality");
                vsm.SetQualityLevel(OcclusionQualityPreset.Ultra);
                EditorUtility.SetDirty(vsm);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(GetQualityDescription(currentLevel), MessageType.None);
            
            EditorGUILayout.EndVertical();
        }

        bool QualityButton(string label, bool isActive)
        {
            var prevColor = GUI.backgroundColor;
            if (isActive)
                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            
            var result = GUILayout.Button(label, GUILayout.Height(25));
            
            GUI.backgroundColor = prevColor;
            return result;
        }

        string GetQualityDescription(OcclusionQualityPreset level)
        {
            switch (level)
            {
                case OcclusionQualityPreset.Low:
                    return "Fast performance, suitable for mobile or low-end hardware.";
                case OcclusionQualityPreset.Medium:
                    return "Balanced performance and quality for most use cases.";
                case OcclusionQualityPreset.High:
                    return "Better shadows, requires more memory and performance.";
                case OcclusionQualityPreset.Ultra:
                    return "Best shadow quality, high memory and performance cost.";
                default:
                    return "";
            }
        }

        void DrawCoreSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Core Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(shadowDataProp, new GUIContent("Shadow Data", "Pre-baked shadow data asset (optional)"));
            
            EditorGUI.BeginChangeCheck();
            
            // Show read-only computed values
            var vsm = target as VirtualShadowMaps;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField(new GUIContent("TileNode Size (computed)", "Computed from TileNode Level: 2^(pageLevel-1)"), vsm.pageSize);
            EditorGUI.EndDisabledGroup();
            
            // Editable fields
            EditorGUILayout.PropertyField(maxMipLevelProp, new GUIContent("Max Mip Level", "Number of mipmap levels (1-8)"));
            EditorGUILayout.PropertyField(maxTilePoolProp, new GUIContent("Max Tile Pool", "Maximum number of shadow tiles"));
            
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(vsm);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawShadowParameters()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Shadow Parameters", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(maxResolutionProp, new GUIContent("Max Resolution", "Resolution of individual shadow maps"));
            EditorGUILayout.PropertyField(regionCenterProp, new GUIContent("Region Center", "Center of shadow coverage area"));
            EditorGUILayout.PropertyField(regionSizeProp, new GUIContent("Region Size", "Size of shadow coverage area"));
            EditorGUILayout.PropertyField(paddingProp, new GUIContent("Padding", "Shadow map padding"));
            EditorGUILayout.Slider(biasProp, 0f, 2f, new GUIContent("Depth Bias", "Offset to prevent shadow acne"));
            EditorGUILayout.Slider(normalBiasProp, 0f, 3f, new GUIContent("Normal Bias", "Normal-based bias offset"));

            EditorGUILayout.EndVertical();
        }

        void DrawFilteringSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Filtering", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(pcssFilterProp, new GUIContent("PCSS Filter", "Percentage-Closer Soft Shadows"));
            
            EditorGUI.BeginDisabledGroup(!pcssFilterProp.boolValue);
            EditorGUILayout.Slider(softnessProp, 0f, 5f, new GUIContent("Softness", "Overall shadow softness"));
            EditorGUILayout.Slider(softnessNearProp, 0f, 5f, new GUIContent("Softness Near", "Softness multiplier for near shadows"));
            EditorGUILayout.Slider(softnessFarProp, 0f, 5f, new GUIContent("Softness Far", "Softness multiplier for far shadows"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        void DrawAdvancedSection()
        {
            showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvanced, "Advanced Settings");
            if (showAdvanced)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                EditorGUILayout.PropertyField(updateIntervalProp, new GUIContent("Update Interval", "Light rotation threshold for updates (degrees)"));
                EditorGUILayout.PropertyField(maxPageRequestLimitProp, new GUIContent("Max TileNode Requests", "Maximum page requests per frame"));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawMaterialsSection()
        {
            showMaterials = EditorGUILayout.BeginFoldoutHeaderGroup(showMaterials, "Materials & Resources");
            if (showMaterials)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                EditorGUILayout.PropertyField(castMaterialProp, new GUIContent("Cast Material", "Material for shadow casting"));
                EditorGUILayout.PropertyField(drawTileMaterialProp, new GUIContent("Tile Material", "Material for drawing shadow tiles"));
                EditorGUILayout.PropertyField(drawLookupMaterialProp, new GUIContent("Lookup Material", "Material for drawing lookup texture"));
                EditorGUILayout.PropertyField(minMaxDepthComputeProp, new GUIContent("MinMax Depth Compute", "Compute shader for depth calculations"));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawStatsSection(VirtualShadowMaps vsm)
        {
            showStats = EditorGUILayout.BeginFoldoutHeaderGroup(showStats, "Runtime Statistics");
            if (showStats)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField("Shadow State", vsm.shadowOn ? "Active" : "Inactive");
                EditorGUILayout.LabelField("Quality Level", vsm.CurrentLevel.ToString());
                EditorGUILayout.LabelField("TileNode Level", vsm.pageLevel.ToString());
                EditorGUILayout.LabelField("TileNode Size", vsm.pageSize.ToString());
                EditorGUILayout.LabelField("Active Pages", $"{vsm.activePageCount} / {vsm.maxTilePool}");
                EditorGUILayout.LabelField("Request Count", vsm.PendingRequestCount().ToString());
                EditorGUILayout.LabelField("Frame Requests", vsm.FrameRequestCount().ToString());
                
                var bounds = vsm.boundsInLightSpace;
                EditorGUILayout.LabelField("Light Space Bounds", $"({bounds.size.x:F1}, {bounds.size.y:F1}, {bounds.size.z:F1})");
                
                var texture = vsm.FetchSurface();
                if (texture != null)
                {
                    EditorGUILayout.LabelField("Tile Texture", $"{texture.width}x{texture.height}");
                }
                
                var bufferSize = vsm.GetLightBufferSize();
                if (bufferSize >= 0)
                {
                    EditorGUILayout.LabelField("Command Buffer Size", $"{bufferSize / 1024f:F2} KB");
                }
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawDebugSection(VirtualShadowMaps vsm)
        {
            showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "Debug Tools");
            if (showDebug)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                if (GUILayout.Button("Setup Wizard", GUILayout.Height(30)))
                {
                    VirtualShadowMapSetupWizard.ShowWindow();
                }
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Force Rebuild", GUILayout.Height(25)))
                {
                    var cameras = FindObjectsOfType<VirtualShadowCamera>();
                    foreach (var cam in cameras)
                    {
                        cam.SetDirty();
                    }
                    EditorUtility.SetDirty(vsm);
                }
                
                EditorGUILayout.Space(10);
                
                var tileTexture = vsm.isActiveAndEnabled && vsm.shadowOn ? vsm.FetchSurface() : null;
                if (tileTexture)
                {
                    EditorGUILayout.LabelField("Tile Texture Preview", EditorStyles.boldLabel);
                    Rect lastRect = GUILayoutUtility.GetAspectRect(1.0f);
                    EditorGUI.DrawPreviewTexture(lastRect, tileTexture);
                }
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawBakingSection()
        {
            showBaking = EditorGUILayout.BeginFoldoutHeaderGroup(showBaking, "Baking Tools");
            if (showBaking)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                var prevBgColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("Generate Shadow Maps", GUILayout.Height(40)))
                {
                    GenerateShadowMaps();
                }
                GUI.backgroundColor = prevBgColor;
                
                EditorGUILayout.HelpBox("Bakes all shadow pages to disk and creates VirtualShadowData asset.", MessageType.Info);
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("Caster Management", EditorStyles.boldLabel);
                
                if (VirtualShadowManager.instance.casterCount > 0)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.IntField("Num Caster", VirtualShadowManager.instance.casterCount);
                    EditorGUI.EndDisabledGroup();
                }
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Collect Caster", GUILayout.Height(30)))
                {
                    CollectCaster();
                }
                if (GUILayout.Button("Clear Caster", GUILayout.Height(30)))
                {
                    ClearCaster();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("Shadow Casting Mode", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Turn On Shadows", GUILayout.Height(25)))
                {
                    TurnOnShadows();
                }
                if (GUILayout.Button("Turn Off Shadows", GUILayout.Height(25)))
                {
                    TurnOffShadows();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.HelpBox("Controls Unity's shadow casting mode for all renderers (ignores ShadowOnly).", MessageType.None);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        public bool SaveRenderTexture(RenderTexture renderTexture, string filePath)
        {
            RenderTexture savedRT = RenderTexture.active;

            Graphics.SetRenderTarget(renderTexture);

            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, GraphicsFormat.R16_SFloat, TextureCreationFlags.None);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
            texture.Apply();

            Graphics.SetRenderTarget(savedRT);

            var tileNums = texture.CountActiveTiles();
            if (tileNums > 0)
            {
                byte[] bytes = texture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                File.WriteAllBytes(filePath, bytes);
            }

            if (Application.isEditor)
                UnityEngine.Object.DestroyImmediate(texture);
            else
                UnityEngine.Object.Destroy(texture);

            return (tileNums > 0) ? true : false;
        }

        public void GenerateShadowMaps()
        {
            var scene = SceneManager.GetActiveScene();
            var sceneName = Path.GetFileNameWithoutExtension(scene.path) + "_Shadow";
            var fileroot = Path.GetDirectoryName(scene.path);

            if (!AssetDatabase.IsValidFolder(Path.Join(fileroot, sceneName)))
                AssetDatabase.CreateFolder(fileroot, sceneName);

            m_VirtualShadowMaps.shadowData = null;

            Dictionary<TileRequestData, string> texAssets = new Dictionary<TileRequestData, string>();
            Dictionary<TileRequestData, Matrix4x4> texLightProjecionMatrix = new Dictionary<TileRequestData, Matrix4x4>();

            using (var baker = new VirtualShadowMapBaker(m_VirtualShadowMaps))
            {
                var pageTable = new TileIndexTable(m_VirtualShadowMaps.pageSize, m_VirtualShadowMaps.pageLevel);
                var requestPageJob = new TileRequestJob();

                for (int i = 0; i < pageTable.maxLevelDepth; i++)
                {
                    foreach (var page in pageTable.levelHierarchy[i].nodeGrid)
                        requestPageJob.SubmitRequest(page.column, page.rank, page.mipLevel);
                }

                requestPageJob.OrderRequests();

                var totalRequestCount = requestPageJob.queuedCount;
                var shadowMap = OcclusionTexturePool.Acquire(m_VirtualShadowMaps.shadowSize);

                for (var i = 0; i < totalRequestCount; i++)
                {
                    var request = requestPageJob.PeekRequest().Value;
                    var pageName = request.lodTier + "-" + request.gridColumn + "-" + request.gridRow;
                    var outpath = Path.Join(fileroot, sceneName, "ShadowTexBytes-" + pageName + ".exr");
                    
                    baker.RenderNow(shadowMap, request.gridColumn, request.gridRow, request.lodTier);

                    if (this.SaveRenderTexture(shadowMap, outpath))
                    {
                        texAssets[request] = outpath;
                        texLightProjecionMatrix[request] = baker.lightProjecionMatrix;
                    }

                    requestPageJob.CancelRequest(request);

                    if (EditorUtility.DisplayCancelableProgressBar("VirtualShadowMaps Baker", "Processing index:" + i + " total:" + totalRequestCount, i / (float)totalRequestCount))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }

                OcclusionTexturePool.Reclaim(shadowMap);
            }

            EditorUtility.ClearProgressBar();

            AssetDatabase.Refresh();

            var m_VirtualShadowData = ScriptableObject.CreateInstance<VirtualShadowData>();
            m_VirtualShadowData.regionCenter = m_VirtualShadowMaps.regionCenter;
            m_VirtualShadowData.regionSize = m_VirtualShadowMaps.regionSize;
            m_VirtualShadowData.pageSize = m_VirtualShadowMaps.pageSize;
            m_VirtualShadowData.maxMipLevel = m_VirtualShadowMaps.pageLevel;
            m_VirtualShadowData.maxResolution = m_VirtualShadowMaps.shadowSize;
            m_VirtualShadowData.bounds = m_VirtualShadowMaps.CalculateBoundingBox();
            m_VirtualShadowData.worldToLocalMatrix = m_VirtualShadowMaps.GetLightTransform().worldToLocalMatrix;
            m_VirtualShadowData.localToWorldMatrix = m_VirtualShadowMaps.GetLightTransform().localToWorldMatrix;

            foreach (var it in texAssets)
                m_VirtualShadowData.SetTexAsset(it.Key, AssetDatabase.AssetPathToGUID(it.Value));

            foreach (var it in texLightProjecionMatrix)
                m_VirtualShadowData.SetMatrix(it.Key, it.Value);

            m_VirtualShadowData.SetupTextureImporter();
            m_VirtualShadowData.SaveAs(Path.Join(fileroot, sceneName));

            m_VirtualShadowMaps.shadowData = m_VirtualShadowData;

            RefreshSceneCameras();
            TurnOffShadows();

            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Bake Complete", 
                $"Shadow maps have been generated!\n\nOutput: {Path.Join(fileroot, sceneName)}\n\nVirtualShadowData asset created and assigned.",
                "OK");
        }

        public void RefreshSceneCameras()
        {
            foreach (var cam in SceneView.GetAllSceneCameras())
            {
                if (cam.cameraType == CameraType.SceneView)
                {
                    if (cam.gameObject.TryGetComponent<VirtualShadowCamera>(out var virtualShadowCamera))
                        virtualShadowCamera.SetDirty();
                }
            }
        }

        public void TurnOnShadows()
        {
            var renderers = VirtualShadowManager.instance.GetRenderers();

            foreach (var it in renderers)
            {
                if (it.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    it.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    EditorUtility.SetDirty(it);
                }
            }
        }

        public void TurnOffShadows()
        {
            var renderers = VirtualShadowManager.instance.GetRenderers();

            foreach (var it in renderers)
            {
                if (it.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On)
                {
                    it.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    EditorUtility.SetDirty(it);
                }
            }
        }

        internal VirtualShadowVolume GetVolume(ref GameObject[] gameObjects)
        {
            foreach (var obj in gameObjects)
            {
                var layer = obj.GetComponentInChildren<VirtualShadowVolume>();
                if (layer != null)
                    return layer;
            }

            return null;
        }

        public void CollectCaster()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != null)
                {
                    var gameObjects = scene.GetRootGameObjects();
                    var volume = GetVolume(ref gameObjects);
                    if (volume != null)
                    {
                        volume.Collect();
                        EditorUtility.SetDirty(volume);
                    }
                    else
                    {
                        var volumeObj = new GameObject("Virtual-shadow Volume");
                        SceneManager.MoveGameObjectToScene(volumeObj, scene);

                        volume = volumeObj.AddComponent<VirtualShadowVolume>();
                        volume.Collect();
                        EditorUtility.SetDirty(volume);
                    }
                }
            }

            m_VirtualShadowMaps.CalculateRegionBox();
            Repaint();
        }

        public void ClearCaster()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != null)
                {
                    var gameObjects = scene.GetRootGameObjects();
                    var volume = GetVolume(ref gameObjects);
                    if (volume != null)
                    {
                        volume.Clear();
                        DestroyImmediate(volume.gameObject);
                        EditorUtility.SetDirty(gameObjects[0]);
                    }
                }
            }

            Repaint();
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
