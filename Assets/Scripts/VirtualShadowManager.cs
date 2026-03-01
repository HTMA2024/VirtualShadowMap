using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdaptiveRendering
{
    public sealed class VirtualShadowManager : IDisposable
    {
        static readonly Lazy<VirtualShadowManager> m_Instance = new Lazy<VirtualShadowManager>(() => new VirtualShadowManager());

        public static VirtualShadowManager instance => m_Instance.Value;

        /// <summary>
        /// 线程同步锁，确保多线程环境下的数据安全
        /// </summary>
        private readonly object m_Lock = new object();

        private List<VirtualShadowMaps> m_VirtualShadowMaps;

        private Dictionary<Camera, VirtualShadowCamera> m_VirtualShadowCameras;

        private bool m_VolumeUpdateNeeded;

        public Dictionary<Camera, VirtualShadowCamera> cameras { get => m_VirtualShadowCameras; }

        readonly List<VirtualShadowVolume> m_Volumes;

        private List<Renderer> m_Renderers = new List<Renderer>();

        private Bounds m_Bounds;

        public int casterCount
        {
            get
            {
                return m_Renderers != null ? m_Renderers.Count : 0;
            }
        }

        VirtualShadowManager()
        {
            m_Volumes = new List<VirtualShadowVolume>();
            m_VirtualShadowMaps = new List<VirtualShadowMaps>();
            m_VirtualShadowCameras = new Dictionary<Camera, VirtualShadowCamera>();
            m_VolumeUpdateNeeded = true;
        }

        internal void SetDirty()
        {
            m_VolumeUpdateNeeded = true;
        }

        public void SetCameraDirty()
        {
            foreach (var it in m_VirtualShadowCameras)
            {
                if (it.Value != null)
                    it.Value.SetDirty();
            }
        }

        public VirtualShadowMaps First()
        {
            return m_VirtualShadowMaps.Count > 0 ? m_VirtualShadowMaps.First() : null;
        }

        public void Register(VirtualShadowMaps shadowMaps)
        {
            lock (m_Lock)
            {
                foreach (var it in m_VirtualShadowCameras)
                {
                    if (it.Value != null)
                        it.Value.SetDirty();
                }

                m_VirtualShadowMaps.Add(shadowMaps);
            }
        }

        public void Unregister(VirtualShadowMaps shadowMaps)
        {
            lock (m_Lock)
            {
                m_VirtualShadowMaps.Remove(shadowMaps);
            }
        }

        internal void Register(VirtualShadowVolume volume)
        {
            lock (m_Lock)
            {
                if (!m_Volumes.Contains(volume))
                {
                    m_Volumes.Add(volume);
                    m_VolumeUpdateNeeded = true;
                }
            }
        }

        internal void Unregister(VirtualShadowVolume volume)
        {
            lock (m_Lock)
            {
                m_Volumes.Remove(volume);
                m_VolumeUpdateNeeded = true;
            }
        }

        public void Register(VirtualShadowCamera camera)
        {
            lock (m_Lock)
            {
                if (!m_VirtualShadowCameras.ContainsKey(camera.GetCamera()))
                    m_VirtualShadowCameras.Add(camera.GetCamera(), camera);
            }
        }

        public void Unregister(VirtualShadowCamera camera)
        {
            lock (m_Lock)
            {
                m_VirtualShadowCameras.Remove(camera.GetCamera());
            }
        }

        public bool TryGetCamera(Camera camera, out VirtualShadowCamera value)
        {
            return m_VirtualShadowCameras.TryGetValue(camera, out value);
        }

        public Dictionary<Camera, VirtualShadowCamera> GetCameras()
        {
            return m_VirtualShadowCameras;
        }

        internal List<Renderer> GetAllRenderers()
        {
            var renderers = new List<Renderer>();
            var allRenderers = new List<Renderer>();

            foreach (var lodGroup in GameObject.FindObjectsOfType<LODGroup>())
            {
                foreach (var lod in lodGroup.GetLODs())
                {
                    foreach (var renderer in lod.renderers)
                    {
                        if (renderer != null)
                            allRenderers.Add(renderer);
                    }
                }
            }

            foreach (var lodGroup in GameObject.FindObjectsOfType<LODGroup>())
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
                                    renderers.Add(renderer);
                            }
                        }
                    }
                }
            }

            foreach (var renderer in GameObject.FindObjectsOfType<MeshRenderer>())
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

            return renderers;
        }

        internal bool UpdateCasters(bool forceUpdate = false)
        {
            if (!m_VolumeUpdateNeeded && !forceUpdate)
                return false;

            if (m_Volumes.Count > 0)
            {
                m_Renderers = new List<Renderer>();
                m_Renderers.Clear();

                foreach (var volume in m_Volumes)
                {
                    if (volume.renderers == null)
                        continue;

                    m_Renderers.AddRange(volume.renderers);
                }
            }
            else
            {
                m_Renderers = GetAllRenderers();
            }

            m_Bounds = VirtualShadowMapsUtilities.CalculateBoundingBox(m_Renderers);
            m_VolumeUpdateNeeded = false;

            return true;
        }

        public List<Renderer> GetRenderers(bool forceUpdate = false)
        {
            this.UpdateCasters(forceUpdate);
            return m_Renderers;
        }

        internal Bounds GetBounds()
        {
            this.UpdateCasters();
            return m_Bounds;
        }

        public void Release()
        {
            m_Renderers = null;
            m_VolumeUpdateNeeded = false;
        }

        public void Dispose()
        {
            this.Release();
            GC.SuppressFinalize(this);
        }
    }
}