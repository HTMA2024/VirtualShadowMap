using UnityEngine;

namespace AdaptiveRendering
{
#if UNITY_2018_3_OR_NEWER
    [ExecuteAlways]
#else
    [ExecuteInEditMode]
#endif
    [AddComponentMenu("Rendering/Virtual Shadow Debug", 1001)]
    public class VirtualShadowDebug : MonoBehaviour
    {
        [Header("Split View: Left = CSM, Right = VSM")]
        public bool splitView = false;

        [Header("Split Position (0~1)")]
        [Range(0f, 1f)]
        public float splitPosition = 0.5f;

        private static readonly int _VirtualShadowDebugSplit = Shader.PropertyToID("_CascadedOcclusionDebugSplit");
        private static readonly int _VirtualShadowDebugSplitPos = Shader.PropertyToID("_CascadedOcclusionDebugSplitPos");

        void Update()
        {
            Shader.SetGlobalFloat(_VirtualShadowDebugSplit, splitView ? 1f : 0f);
            Shader.SetGlobalFloat(_VirtualShadowDebugSplitPos, splitPosition);
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(_VirtualShadowDebugSplit, 0f);
        }
    }
}
