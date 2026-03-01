using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveRendering
{
    public sealed class IndirectionTexture : IDisposable
    {
        /// <summary>
        /// ������ҳ��Ѱַ��ͼ
        /// </summary>
        private RenderTexture m_LookupSurface;

        /// <summary>
        /// ��ǰ����ļ����TileNode
        /// </summary>
        private List<TileNode> m_PendingEntries;

        /// <summary>
        /// ������ʷ֡����
        /// </summary>
        private int m_PreviousFrameTick = 0;

        /// <summary>
        /// ��ǰ֡�����Tile�б�
        /// </summary>
        private List<TileNode> m_ActiveTiles;

        /// <summary>
        /// ��ǰ֡�����Tiled����
        /// </summary>
        private Vector4[] m_TileCoords;

        /// <summary>
        /// ��ǰ֡�����Tiled����
        /// </summary>
        private Matrix4x4[] m_TileTransforms;

        /// <summary>
        /// ����Lookup��ʵ����
        /// </summary>
        private MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

        /// <summary>
        /// ҳ����?
        /// </summary>
        private int m_TableSize;

        /// <summary>
        /// �ж��Ƿ���Ҫ�ػ�
        /// </summary>
        private bool m_IsDirty = false;

        /// <summary>
        /// ҳ����?
        /// </summary>
        public int tableDimension { get { return m_TableSize; } }

        /// <summary>
        /// ��ǰ�����Tiled����
        /// </summary>
        public int visibleTileCount { get { return m_ActiveTiles.Count; } }

        /// <summary>
        /// ��ǰ�����Tiled����
        /// </summary>
        public Vector4[] visibleTileCoords { get { return m_TileCoords; } }

        /// <summary>
        /// ��ǰ�����Tiled����
        /// </summary>
        public Matrix4x4[] visibleTileTransforms { get { return m_TileTransforms; } }

        public IndirectionTexture(int pageSize, int maxMosaicPool)
        {
            var tilingCount = Mathf.CeilToInt(Mathf.Sqrt(maxMosaicPool));

            m_TableSize = pageSize;
            m_PendingEntries = new List<TileNode>();
            m_PreviousFrameTick = 0;
            m_ActiveTiles = new List<TileNode>();
            m_TileCoords = new Vector4[tilingCount * tilingCount];
            m_TileTransforms = new Matrix4x4[tilingCount * tilingCount];

            m_LookupSurface = RenderTexture.GetTemporary(pageSize, pageSize, 16, RenderTextureFormat.ARGBHalf);
            m_LookupSurface.name = "IndirectionTexture";
            m_LookupSurface.filterMode = FilterMode.Point;
            m_LookupSurface.wrapMode = TextureWrapMode.Clamp;
        }

        ~IndirectionTexture()
        {
            this.Dispose();
        }

        public RenderTexture FetchSurface()
        {
            return m_LookupSurface;
        }

        public void Enqueue(TileNode page)
        {
            m_PendingEntries.Add(page);

            if (page.isResident)
            {
                if (!m_ActiveTiles.Contains(page))
                    m_IsDirty = true;
            }
        }

        public void ResetAll()
        {
            m_PendingEntries.Clear();
        }

        public bool RefreshActiveTiles(MosaicTexture tiledTexture)
        {
            bool isDirty = m_IsDirty;

            if (!isDirty)
            {
                foreach (var kv in m_PendingEntries)
                {
                    var page = kv;
                    if (page.isResident)
                    {
                        if (page.payload.lastTouchFrame >= m_PreviousFrameTick)
                        {
                            isDirty = true;
                            break;
                        }
                    }
                    else
                    {
                        if (m_ActiveTiles.Contains(page))
                        {
                            isDirty = true;
                            break;
                        }
                    }
                }
            }

            if (isDirty)
            {
                m_ActiveTiles.Clear();

                foreach (var page in m_PendingEntries)
                {
                    if (page.isResident)
                        m_ActiveTiles.Add(page);
                }

                m_ActiveTiles.Sort((a, b) => { return -a.mipLevel.CompareTo(b.mipLevel); });

                var length = Mathf.Min(m_ActiveTiles.Count, m_TileCoords.Length);

                for (int i = 0; i < length; i++)
                {
                    var page = m_ActiveTiles[i];
                    var pageIndex = tiledTexture.IndexToCoord(page.payload.tileIndex);

                    m_TileCoords[i] = new Vector4(pageIndex.x, pageIndex.y, page.mipLevel, 1 << page.mipLevel);
                    m_TileTransforms[i] = page.ComputeTransform(m_TableSize, Vector2.zero);
                }

                m_IsDirty = true;
            }

            m_PreviousFrameTick = Time.frameCount;

            return m_IsDirty;
        }

        public void RebuildIndirectionSurface(CommandBuffer cmd, Material material)
        {
            if (this.m_IsDirty)
            {
                m_PropertyBlock.Clear();
                m_PropertyBlock.SetVectorArray(ShaderConstants._TiledIndex, this.visibleTileCoords);

                cmd.SetRenderTarget(this.FetchSurface(), RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmd.ClearRenderTarget(true, true, Color.clear);
                cmd.DrawMeshInstanced(
                    StreamingTileUtilities.fullscreenMesh,
                    0,
                    material,
                    0,
                    this.visibleTileTransforms,
                    this.visibleTileCount,
                    m_PropertyBlock);

                this.m_IsDirty = false;
            }
        }

        public void Dispose()
        {
            if (m_LookupSurface != null)
            {
                RenderTexture.ReleaseTemporary(m_LookupSurface);
                m_LookupSurface = null;
            }

            GC.SuppressFinalize(this);
        }

        static class ShaderConstants
        {
            public static readonly int _TiledIndex = Shader.PropertyToID("_TiledIndex");
        }
    }
}
