using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace AdaptiveRendering
{
    public sealed class StreamingTileMap2D : IDisposable
    {
        /// <summary>
        /// RT Job����
        /// </summary>
        private TileRequestJob m_StreamingScheduler;

        /// <summary>
        /// ҳ��
        /// </summary>
        private TileIndexTable m_NodeIndex;

        /// <summary>
        /// ƽ����ͼ����
        /// </summary>
        private MosaicTexture m_TileSurface;

        /// <summary>
        /// ������ҳ��Ѱַ��ͼ
        /// </summary>
        private RenderTexture m_LookupSurface;

        /// <summary>
        /// ������ʷ֡����
        /// </summary>
        private int m_PreviousFrameTick = 0;

        /// <summary>
        /// ��ǰ֡�����TileNode�б�
        /// </summary>
        private List<TileNode> m_DirtyNodes = new List<TileNode>();

        /// <summary>
        /// ��ǰ֡�����Tile�б�
        /// </summary>
        private List<TileNode> m_ActiveTiles = new List<TileNode>();

        /// <summary>
        /// ��ǰ֡�����Tiled����
        /// </summary>
        private Vector4[] m_TileCoords;

        /// <summary>
        /// ��ǰ֡�����Tiled����
        /// </summary>
        private Matrix4x4[] m_TileTransforms;

        /// <summary>
        /// ����Tile�ĳߴ�.
        /// </summary>
        public int tileDimension { get { return m_TileSurface.tileDimension; } }

        /// <summary>
        /// ����ߴ�?
        /// ����ߴ��ʾ��������������Tile������.
        /// </summary>
        public int layoutExtent { get { return m_TileSurface.layoutExtent; } }
        
        /// <summary>
        /// Tile ����Ŀ��.
        /// </summary>
        public int atlasResolution { get { return m_TileSurface.atlasResolution; } }

        /// <summary>
        /// ҳ����?
        /// </summary>
        public TileIndexTable tileDirectory { get { return m_NodeIndex; } }

        /// <summary>
        /// ҳ����?
        /// </summary>
        public int tableDimension { get { return m_NodeIndex.tableDimension; } }

        /// <summary>
        /// ҳ��
        /// </summary>
        public int maxLevelDepth { get { return m_NodeIndex.maxLevelDepth; } }

        /// <summary>
        /// �ж��Ƿ���Ҫ�ػ�
        /// </summary>
        public bool requiresRefresh { get { return m_DirtyNodes.Count > 0; } }

        /// <summary>
        /// ��ǰ�����Tile
        /// </summary>
        public Dictionary<int, TileNode> activeNodes { get { return m_NodeIndex.activeNodes; } }

        /// <summary>
        /// ��ǰ�����TileNode����
        /// </summary>
        public int activeNodeCount { get { return m_NodeIndex.activeNodes.Count; } }

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

        public StreamingTileMap2D(int tileSize, int tilingCount, StreamingTileFormat[] formats, int pageSize, int maxLevel)
        {
            m_StreamingScheduler = new TileRequestJob();

            m_NodeIndex = new TileIndexTable(pageSize, maxLevel);
            m_TileSurface = new MosaicTexture(tileSize, tilingCount, formats);

            m_TileCoords = new Vector4[tilingCount * tilingCount];
            m_TileTransforms = new Matrix4x4[tilingCount * tilingCount];

            m_LookupSurface = RenderTexture.GetTemporary(pageSize, pageSize, 16, RenderTextureFormat.ARGBHalf);
            m_LookupSurface.name = "IndirectionTexture";
            m_LookupSurface.filterMode = FilterMode.Point;
            m_LookupSurface.wrapMode = TextureWrapMode.Clamp;
        }

        ~StreamingTileMap2D()
        {
            this.Dispose();
        }

        public RenderTexture FetchSurface(int index)
        {
            return m_TileSurface.FetchSurface(index);
        }

        public MosaicTexture FetchMosaicSurface()
        {
            return m_TileSurface;
        }

        public RenderTexture FetchIndirectionSurface()
        {
            return m_LookupSurface;
        }

        /// <summary>
        /// ��������з���һ��δ��ʹ�õ�Tile
        /// </summary>
        public int AllocateTile()
        {
            return m_TileSurface.AllocateTile();
        }

        /// <summary>
        /// ��ȡ��TileTexture�ϻ��Ƶľ���
        /// </summary>
        public Matrix4x4 ComputeTileTransform(int tile)
        {
            return m_TileSurface.ComputeTransform(tile);
        }

        /// <summary>
        /// ��ȡ��IndirectionTexture�ϻ��Ƶľ���
        /// </summary>
        public Matrix4x4 ComputeIndirectionTransform(TileNode page)
        {
			var table = m_NodeIndex.levelHierarchy[page.mipLevel];
            return page.ComputeTransform(tableDimension, -table.viewportShift * table.cellExtent);
        }

        /// <summary>
        /// ��ȡҳ��
        /// </summary>
        public TileNode QueryTileNode(int x, int y, int mip)
        {
            Debug.Assert(x >= 0 && x < m_NodeIndex.tableDimension);
            Debug.Assert(y >= 0 && y < m_NodeIndex.tableDimension);
            Debug.Assert(mip >= 0 && mip < m_NodeIndex.maxLevelDepth);

            return m_NodeIndex.QueryTileNode(x, y, mip);
        }

        /// <summary>
        /// ����ҳ��
        /// </summary>
        public void EnableTileNode(int tile, TileNode page)
        {
            if (m_TileSurface.MarkActive(tile))
            {
                if (page.payload.queuedRequest != null)
                {
                    m_StreamingScheduler.CancelRequest(page.payload.queuedRequest.Value);
                    page.payload.queuedRequest = null;
                }

                m_NodeIndex.EnableTileNode(tile, page);
            }
        }

        /// <summary>
        /// ж��ҳ��
        /// </summary>
        public void RevokeTileNode(int x, int y, int mip)
        {
            Debug.Assert(x >= 0 && x < m_NodeIndex.tableDimension);
            Debug.Assert(y >= 0 && y < m_NodeIndex.tableDimension);
            Debug.Assert(mip >= 0 && mip < m_NodeIndex.maxLevelDepth);

            var page = m_NodeIndex.LocateTileNode(x, y, mip);
            if (page != null)
            {
                if (page.payload.queuedRequest != null)
                {
                    m_StreamingScheduler.CancelRequest(page.payload.queuedRequest.Value);
                    page.payload.queuedRequest = null;
                }

                if (page.payload.isResident)
                {
                    m_NodeIndex.RevokeTileNode(page.payload.tileIndex);
                }
            }
        }

        /// <summary>
        /// ж�����е�ҳ��
        /// </summary>
        public void RevokeTileNodes()
        {
            Assert.IsTrue(m_NodeIndex != null);
            m_NodeIndex.RevokeTileNodes();
        }

        /// <summary>
        /// �ƶ�ҳ��
        /// </summary>
        public void ShiftTileIndex(Vector2Int offset)
        {
            this.PurgeRequests();

            m_NodeIndex.AdjustViewport(offset);
        }

        /// <summary>
        /// ����ҳ����أ����û���ҵ�ҳ�����null
        /// </summary>
        public TileNode SubmitRequest(int x, int y, int mip)
        {
            Assert.IsTrue(x >= 0 && x < m_NodeIndex.tableDimension);
            Assert.IsTrue(y >= 0 && y < m_NodeIndex.tableDimension);
            Assert.IsTrue(mip >= 0 && mip < m_NodeIndex.maxLevelDepth);

            var page = m_NodeIndex.LocateTileNode(x, y, mip);
            if (page != null)
            {
                if (!page.payload.isResident)
                {
                    if (page.payload.queuedRequest == null)
                        page.payload.queuedRequest = m_StreamingScheduler.SubmitRequest(x, y, page.mipLevel);
                }
                else
                {
                    m_TileSurface.MarkActive(page.payload.tileIndex);
                }

                return page;
            }

            return null;
        }

        /// <summary>
        /// ����ҳ��
        /// </summary>
        public void SubmitRequest(Color32[] pageData)
        {
            foreach (var data in pageData)
            {
                if (data.a == 0)
                    continue;

                SubmitRequest(data.r, data.g, data.b);
            }
        }

        /// <summary>
        /// ����LOD�µ�����ҳ��
        /// </summary>
        public void SubmitRequestByMip(int mip)
        {
            if (mip <= maxLevelDepth)
            {
                var cellSize = 1 << mip;
                var cellCount = tableDimension / cellSize;

                for (int i = 0; i < cellCount; i++)
                {
                    for (int j = 0; j < cellCount; j++)
                    {
                        this.SubmitRequest(i, j, mip);
                    }
                }
            }
        }

        public int PendingRequestCount()
        {
            return m_StreamingScheduler.queuedCount;
        }

        public TileRequestData? PeekRequest()
        {
            return m_StreamingScheduler.PeekRequest();
        }

        public void OrderRequests()
        {
            m_StreamingScheduler.OrderRequests();
        }

        public void OrderRequests(Comparison<TileRequestData> comparison)
        {
            m_StreamingScheduler.OrderRequests(comparison);
        }

        public void CancelRequest(TileRequestData req)
        {
            var page = m_NodeIndex.QueryTileNode(req.gridColumn, req.gridRow, req.lodTier);
            if (page != null)
            {
                if (page.payload.queuedRequest.Equals(req))
                {
                    m_StreamingScheduler.CancelRequest(req);
                    page.payload.queuedRequest = null;
                }
            }
        }

        public void CancelRequest(int x, int y, int mip)
        {
            Debug.Assert(x >= 0 && x < m_NodeIndex.tableDimension);
            Debug.Assert(y >= 0 && y < m_NodeIndex.tableDimension);
            Debug.Assert(mip >= 0 && mip < m_NodeIndex.maxLevelDepth);

            var req = m_StreamingScheduler.LocateRequest(x, y, mip);
            if (req != null)
                this.CancelRequest(req.Value);
        }

        public void PurgeRequests()
        {
            Debug.Assert(m_StreamingScheduler != null);

            m_StreamingScheduler.PurgeAll((req) => 
            {
                var page = m_NodeIndex.QueryTileNode(req.gridColumn, req.gridRow, req.lodTier);
                if (page != null)
                {
                    if (page.payload.queuedRequest.Equals(req))
                        page.payload.queuedRequest = null;
                }
            });
        }

        public bool RefreshActiveTiles()
        {
            m_DirtyNodes.Clear();

            foreach (var kv in m_NodeIndex.activeNodes)
            {
                var page = kv.Value;
                if (page.payload.lastTouchFrame < m_PreviousFrameTick)
                    continue;

                m_DirtyNodes.Add(page);
            }

            if (m_DirtyNodes.Count > 0)
            {
                m_ActiveTiles.Clear();

                foreach (var kv in m_NodeIndex.activeNodes)
                    m_ActiveTiles.Add(kv.Value);

                m_ActiveTiles.Sort((a, b) => { return -a.mipLevel.CompareTo(b.mipLevel); });

                for (int i = 0; i < m_ActiveTiles.Count; i++)
                {
                    var page = m_ActiveTiles[i];
                    var pageIndex = m_TileSurface.IndexToCoord(page.payload.tileIndex);

                    m_TileCoords[i] = new Vector4(pageIndex.x, pageIndex.y, page.mipLevel, 1 << page.mipLevel);
                    m_TileTransforms[i] = ComputeIndirectionTransform(page);
                }
            }

            m_PreviousFrameTick = Time.frameCount;

            return m_DirtyNodes.Count > 0;
        }

        public void RebuildIndirectionSurface(CommandBuffer cmd, Material material, MaterialPropertyBlock materialPropertyBlock)
        {
            if (this.requiresRefresh)
            {
                cmd.SetRenderTarget(this.FetchIndirectionSurface(), RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmd.ClearRenderTarget(true, true, Color.clear);
                cmd.DrawMeshInstanced(
                    StreamingTileUtilities.fullscreenMesh,
                    0,
                    material,
                    0,
                    this.visibleTileTransforms,
                    this.visibleTileCount,
                    materialPropertyBlock);
            }
        }

        public void ResetAll()
        {
            Assert.IsTrue(m_NodeIndex != null && m_StreamingScheduler != null && m_TileSurface != null);

            m_NodeIndex.RevokeTileNodes();
            m_StreamingScheduler.PurgeAll();
            m_TileSurface.ResetAll();
        }

        public void Dispose()
        {
            if (m_TileSurface != null)
            {
                m_TileSurface.Dispose();
                m_TileSurface = null;
            }

            if (m_LookupSurface != null)
            {
                RenderTexture.ReleaseTemporary(m_LookupSurface);
                m_LookupSurface = null;
            }

            if (m_StreamingScheduler != null)
            {
                m_StreamingScheduler.PurgeAll();
                m_StreamingScheduler = null;
            }

            if (m_NodeIndex != null)
            {
                m_NodeIndex.RevokeTileNodes();
                m_NodeIndex = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
