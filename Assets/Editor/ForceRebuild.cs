using UnityEditor;
using UnityEngine;

namespace AdaptiveRenderingEditor
{
    public class ForceRebuild
    {
        [MenuItem("Window/Cascaded Occlusion/Force Recompile")]
        public static void Recompile()
        {
            Debug.Log("[VSM] Forcing recompile...");
            AssetDatabase.Refresh();
            EditorUtility.RequestScriptReload();
            Debug.Log("[VSM] Recompile requested. Please wait...");
        }
    }
}
