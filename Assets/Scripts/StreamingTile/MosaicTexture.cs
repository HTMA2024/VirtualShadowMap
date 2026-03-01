using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveRendering
{
    public sealed class MosaicTexture : IDisposable
    {
        /// <summary>
        /// Tile dimension.
        /// </summary>
        private int m_TileDimension = 512;

        /// <summary>
        /// Grid count.
        /// </summary>
        private int m_LayoutCount = 8;

        /// <summary>
        /// Tile eviction queue.
        /// </summary>
        private EvictionCache m_RecycleQueue;

        /// <summary>
        /// Cell format.
        /// </summary>
        private StreamingTileFormat[] m_TileFormat;

        /// <summary>
        /// Cell surfaces.
        /// </summary>
        private RenderTexture[] m_TileSurfaces;

        public int layoutExtent { get { return m_LayoutCount; } }

        public int tileDimension { get { return m_TileDimension; } }

        public int atlasResolution { get { return m_LayoutCount * m_TileDimension; } }

        public MosaicTexture(int size, int count, StreamingTileFormat[] format)
        {
            m_TileDimension = size;
            m_LayoutCount = count;
            m_TileFormat = format;
            m_RecycleQueue = new EvictionCache(m_LayoutCount * m_LayoutCount);

            m_TileSurfaces = new RenderTexture[m_TileFormat.Length];

            for (int i = 0; i < m_TileFormat.Length; i++)
            {
                var texture = RenderTexture.GetTemporary(this.atlasResolution, this.atlasResolution, 16, m_TileFormat[i].renderFormat, m_TileFormat[i].gammaMode);
                texture.name = "TileTexture" + i;
                texture.useMipMap = false;
                texture.autoGenerateMips = false;
                texture.filterMode = m_TileFormat[i].filterSampling;
                texture.wrapMode = m_TileFormat[i].wrapBehavior;

                m_TileSurfaces[i] = texture;
            }
        }

        ~MosaicTexture()
        {
            this.Dispose();
        }

        public Vector2Int IndexToCoord(int id)
        {
            return new Vector2Int(id % layoutExtent, id / layoutExtent);
        }

        public int CoordToIndex(Vector2Int tile)
        {
            return tile.y * layoutExtent + tile.x;
        }

        public int AllocateTile()
        {
            return m_RecycleQueue.frontEntry;
        }

        public bool MarkActive(int tile)
        {
            return m_RecycleQueue.MarkActive(tile);
        }

        public RectInt ComputeTileRegion(Vector2Int tile)
{
            var size = m_TileDimension;
            return new RectInt(tile.x * size, tile.y * size, size, size);
}

        public RenderTexture FetchSurface(int index)
        {
            return m_TileSurfaces[index];
        }

        public Matrix4x4 ComputeTransform(int id)
        {
            return ComputeTransform(IndexToCoord(id));
        }

        public Matrix4x4 ComputeTransform(Vector2Int tile)
{
            var tileRect = ComputeTileRegion(tile);

            var tileX = tileRect.x / (float)atlasResolution * 2 - 1;
            var tileY = 1 - (tileRect.y + tileRect.height) / (float)atlasResolution * 2;
            var tileWidth = tileRect.width * 2 / (float)atlasResolution;
            var tileHeight = tileRect.height * 2 / (float)atlasResolution;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
            {
                tileY = tileRect.y / (float)atlasResolution * 2 - 1;
            }

            return Matrix4x4.TRS(new Vector3(tileX, tileY, 0), Quaternion.identity, new Vector3(tileWidth, tileHeight, 0));
        }

        public void ResetAll()
{
            m_RecycleQueue.ResetAll();
        }

public void Dispose()
        {
            for (int i = 0; i < m_TileSurfaces.Length; i++)
            {
                if (m_TileSurfaces[i] != null)
                {
                    RenderTexture.ReleaseTemporary(m_TileSurfaces[i]);
                    m_TileSurfaces[i] = null;
                }
            }

            GC.SuppressFinalize(this);
        }
}
}
