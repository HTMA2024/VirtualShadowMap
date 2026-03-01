using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdaptiveRendering
{
#if UNITY_2018_3_OR_NEWER
    [ExecuteAlways]
#else
    [ExecuteInEditMode]
#endif
    [AddComponentMenu("Rendering/Virtual Shadow Volume", 1000)]
public sealed class VirtualShadowVolume : MonoBehaviour
    {
        /// <summary>
        /// 所有静态物件的Bounds
        /// </summary>
        [SerializeField]
        private Bounds m_Bounds;
        public Bounds bounds;

        /// <summary>
        /// 所有静态物件的Renderers
        /// </summary>
        [HideInInspector]
        [SerializeField]
        private MeshRenderer[] m_Renderers;

        public MeshRenderer[] renderers { get => m_Renderers; }

        public void OnEnable()
        {
            VirtualShadowManager.instance.Register(this);
        }

        public void OnDisable()
        {
            VirtualShadowManager.instance.Unregister(this);
        }

        public void Clear()
        {
            m_Renderers = null;
            m_Bounds = new Bounds();
            VirtualShadowManager.instance.SetDirty();
        }

        public void Collect()
        {
            var obejcts = this.gameObject.scene.GetRootGameObjects();

            var renderers = new List<MeshRenderer>();
            var allRenderers = new List<MeshRenderer>();
            var lodGroups = new List<LODGroup>();

            foreach (var it in obejcts)
            {
                foreach (var lodGroup in it.GetComponentsInChildren<LODGroup>())
                {
                    lodGroups.Append(lodGroup);
                }
            }

            foreach (var lodGroup in lodGroups)
            {
                foreach (var lod in lodGroup.GetLODs())
                {
                    foreach (var renderer in lod.renderers)
                    {
                        if (renderer is MeshRenderer)
                            allRenderers.Add(renderer as MeshRenderer);
                    }
                }
            }

            foreach (var lodGroup in lodGroups)
            {
                var lods = lodGroup.GetLODs();
                if (lods.Length > 0)
                {
                    foreach (var renderer in lods[0].renderers)
                    {
                        if (renderer == null)
                            continue;

                        var isCastShadow = renderer.gameObject.isStatic;
                        if (renderer.TryGetComponent<VirtualShadowCaster>(out var virtualShadowCaster))
                            isCastShadow = virtualShadowCaster.castShadow;

                        if (isCastShadow)
                        {
                            if (renderer.TryGetComponent<MeshFilter>(out var meshFilter))
                            {
                                if (meshFilter.sharedMesh != null && renderer.sharedMaterial != null)
                                {
                                    if (renderer is MeshRenderer)
                                        renderers.Add(renderer as MeshRenderer);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var it in obejcts)
            {
                foreach (var renderer in it.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.enabled)
                    {
                        var isCastShadow = renderer.gameObject.isStatic;
                        if (renderer.TryGetComponent<VirtualShadowCaster>(out var virtualShadowCaster))
                            isCastShadow = virtualShadowCaster.castShadow;

                        if (isCastShadow)
                        {
                            if (renderer.TryGetComponent<MeshFilter>(out var meshFilter))
                            {
                                if (meshFilter.sharedMesh != null && renderer.sharedMaterial != null)
                                {
                                    if (!allRenderers.Contains(renderer))
                                        renderers.Add(renderer);
                                }
                            }
                        }
                    }
                }
            }

            m_Renderers = renderers.ToArray();

            if (m_Renderers.Length > 0)
            {
                this.m_Bounds.SetMinMax(Vector3.positiveInfinity, Vector3.negativeInfinity);

                foreach (var it in m_Renderers)
                    this.m_Bounds.Encapsulate(it.bounds);
            }
            else
            {
                m_Bounds = new Bounds();
            }

            VirtualShadowManager.instance.SetDirty();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Selection.activeGameObject == this.gameObject)
            {
                Gizmos.matrix = Matrix4x4.identity;

                var bounds = m_Bounds;
                Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.2f);
                Gizmos.DrawCube(bounds.center, bounds.size);
                Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.5f);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
#endif
    }
}
