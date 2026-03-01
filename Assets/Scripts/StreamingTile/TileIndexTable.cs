using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveRendering
{
    /// <summary>
    /// ҳ��
    /// </summary>
    public sealed class TileIndexTable
    {
        /// <summary>
        /// ҳ��ߴ�?
        /// </summary>
        private int m_TableSize;

        /// <summary>
        /// ҳ��ߴ�?
        /// </summary>
        private int m_MaxLevelCount;

        /// <summary>
        /// ҳ��㼶��?
        /// </summary>
        private TileLevelTable[] m_TierStructure;

        /// <summary>
        /// ��ǰ��Ծ��ҳ��
        /// </summary>
        private Dictionary<int, TileNode> m_ActiveEntries = new Dictionary<int, TileNode>();

        /// <summary>
        /// ҳ��ߴ�?
        /// </summary>
        public int tableDimension { get => m_TableSize; }

        /// <summary>
        /// ���mipmap�ȼ�
        /// </summary>
        public int maxLevelDepth { get => m_MaxLevelCount; }

        /// <summary>
        /// ҳ��㼶��?
        /// </summary>
        public TileLevelTable[] levelHierarchy { get => m_TierStructure; }

        /// <summary>
        /// ��ǰ��Ծ��ҳ��
        /// </summary>
        public Dictionary<int, TileNode> activeNodes { get => m_ActiveEntries; }

        public TileIndexTable(int pageSize = 256, int maxLevel = 8)
        {
            m_TableSize = pageSize;
            m_MaxLevelCount = Math.Clamp(maxLevel, 1, (int)Mathf.Log(pageSize, 2) + 1);

            m_TierStructure = new TileLevelTable[m_MaxLevelCount];

            for (int i = 0; i < m_MaxLevelCount; i++)
                m_TierStructure[i] = new TileLevelTable(pageSize, i);
        }

        public TileNode QueryTileNode(int x, int y, int mip)
        {
            Debug.Assert(x >= 0 && x < m_TableSize);
            Debug.Assert(y >= 0 && y < m_TableSize);
            Debug.Assert(mip >= 0 && mip < m_MaxLevelCount);

            if (mip >= 0 && mip < m_MaxLevelCount) 
                return m_TierStructure[mip].Retrieve(x, y);

            return null;
        }

        public TileNode QueryNearestTileNode(int x, int y, int mip)
        {
            Debug.Assert(x >= 0 && x < m_TableSize);
            Debug.Assert(y >= 0 && y < m_TableSize);
            Debug.Assert(mip >= 0 && mip < m_MaxLevelCount);

            return m_TierStructure[mip].RetrieveNearest(x, y);
        }

        public TileNode LocateTileNode(int x, int y, int mip)
		{
            if (mip >= m_MaxLevelCount || mip < 0 || x < 0 || y < 0 || x >= tableDimension || y >= tableDimension)
                return null;

            return m_TierStructure[mip].Retrieve(x, y);
        }

        public void AdjustViewport(Vector2Int offset)
        {
            for (int i = 0; i < m_MaxLevelCount; i++)
                m_TierStructure[i].AdjustViewport(offset, RevokeTileNode);

            foreach (var kv in m_ActiveEntries)
                m_ActiveEntries[kv.Key].payload.lastTouchFrame = Time.frameCount;
        }

        /// <summary>
        /// ��ҳ����Ϊ��Ծ״̬
        /// </summary>
        public void EnableTileNode(int tile, TileNode page)
        {
            if (m_ActiveEntries.TryGetValue(tile, out var node))
            {
                if (node != page)
                {
                    node.payload.ClearTileSlot();
                    m_ActiveEntries.Remove(tile);
                }
            }

            page.payload.tileIndex = tile;
            page.payload.queuedRequest = null;
            page.payload.lastTouchFrame = Time.frameCount;

            m_ActiveEntries[tile] = page;
        }

        /// <summary>
        /// ��ҳ����Ϊ�ǻ�Ծ״̬
        /// </summary>
        public void RevokeTileNode(int tile)
        {
            if (m_ActiveEntries.TryGetValue(tile, out var node))
            {
                node.payload.ClearTileSlot();
                m_ActiveEntries.Remove(tile);
            }
        }

        public void RevokeTileNodes()
        {
            foreach (var it  in m_ActiveEntries)
                it.Value.payload.ClearTileSlot();

            m_ActiveEntries?.Clear();
        }

        public void ResetViewportOffset()
        {
            for (int i = 0; i < m_MaxLevelCount; i++)
                m_TierStructure[i].ResetViewportOffset();
        }
    }
}
