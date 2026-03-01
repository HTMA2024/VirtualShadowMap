using System;
using UnityEngine;

namespace AdaptiveRendering
{
    public sealed class TileLevelTable
    {
        private TileNode[,] m_CellMatrix;

        private Vector2Int m_ScrollOffset;

        public TileNode[,] nodeGrid { get => m_CellMatrix; }

        public Vector2Int viewportShift { get => m_ScrollOffset; }

        public int mipLevel { get; }
        public int gridCellCount { get; }
        public int cellExtent { get; }

        public TileLevelTable(int pageSize, int mip)
        {
            mipLevel = mip;
            cellExtent = 1 << mipLevel;
            gridCellCount = pageSize / cellExtent;

            m_ScrollOffset = Vector2Int.zero;
            m_CellMatrix = new TileNode[gridCellCount, gridCellCount];

            for (int i = 0; i < gridCellCount; i++)
            {
                for(int j = 0; j < gridCellCount; j++)
                {
                    m_CellMatrix[i, j] = new TileNode(i, j, mipLevel);
                }
            }
        }

        public void ResetViewportOffset()
        {
            m_ScrollOffset = Vector2Int.zero;
        }

        public void AdjustViewport(Vector2Int offset, Action<int> InvalidatePage)
        {
            if (Mathf.Abs(offset.x) >= gridCellCount || Mathf.Abs(offset.y) > gridCellCount || offset.x % cellExtent != 0 || offset.y % cellExtent != 0)
            {
                for (int i = 0; i < gridCellCount; i++)
				{
                    for (int j = 0; j < gridCellCount; j++)
                    {
                        var transXY = GetTransXY(i, j);
                        ref var page = ref m_CellMatrix[transXY.x, transXY.y];
                        page.payload.queuedRequest = null;

                        if (page.payload.isResident)
                        {
                            InvalidatePage(page.payload.tileIndex);
                        }
                    }
                }

                m_ScrollOffset = Vector2Int.zero;
                return;
            }

            offset.x /= cellExtent;
            offset.y /= cellExtent;

            #region clip map
            if (offset.x > 0)
            {
                for (int i = 0;i < offset.x; i++)
                {
                    for (int j = 0;j < gridCellCount;j++)
                    {
                        var transXY = GetTransXY(i, j);
                        m_CellMatrix[transXY.x, transXY.y].payload.queuedRequest = null;
                        InvalidatePage(m_CellMatrix[transXY.x, transXY.y].payload.tileIndex);
                    }
                }
            }
            else if (offset.x < 0)
            {
                for (int i = 1; i <= -offset.x; i++)
                {
                    for (int j = 0; j < gridCellCount; j++)
                    {
                        var transXY = GetTransXY(gridCellCount - i, j);
                        ref var page = ref m_CellMatrix[transXY.x, transXY.y];
                        page.payload.queuedRequest = null;
                        
                        if (page.payload.isResident)
						{
                            InvalidatePage(page.payload.tileIndex);
                        }
                    }
                }
            }
            if (offset.y > 0)
            {
                for (int i = 0; i < offset.y; i++)
                {
                    for (int j = 0; j < gridCellCount; j++)
                    {
                        var transXY = GetTransXY(j, i);
                        m_CellMatrix[transXY.x, transXY.y].payload.queuedRequest = null;
                        InvalidatePage(m_CellMatrix[transXY.x, transXY.y].payload.tileIndex);
                    }
                }
            }
            else if (offset.y < 0)
            {
                for (int i = 1; i <= -offset.y; i++)
                {
                    for (int j = 0; j < gridCellCount; j++)
                    {
                        var transXY = GetTransXY(j, gridCellCount - i);
                        m_CellMatrix[transXY.x, transXY.y].payload.queuedRequest = null;
                        InvalidatePage(m_CellMatrix[transXY.x, transXY.y].payload.tileIndex);
                    }
                }
            }
            #endregion

            m_ScrollOffset += offset;
            
            while(m_ScrollOffset.x < 0) m_ScrollOffset.x += gridCellCount;
            while (m_ScrollOffset.y < 0) m_ScrollOffset.y += gridCellCount;

            m_ScrollOffset.x %= gridCellCount;
            m_ScrollOffset.y %= gridCellCount;
        }

        // ȡx/y/mip��ȫһ�µ�node��û�оͷ���null
        public TileNode Retrieve(int x, int y)
        {
            if (x < 0 || y < 0 || x >= gridCellCount || y >= gridCellCount)
                return null;

            return m_CellMatrix[x, y];
        }

        public TileNode RetrieveNearest(int x, int y)
        {
            if (x < 0 || y < 0 || x >= gridCellCount || y >= gridCellCount)
            {
                x /= cellExtent;
                y /= cellExtent;

                x = (x + m_ScrollOffset.x) % gridCellCount;
                y = (y + m_ScrollOffset.y) % gridCellCount;
            }

            return Retrieve(x, y);
        }        

        public RectInt ComputeInverseRegion(RectInt rect)
        {
            return new RectInt( rect.xMin - m_ScrollOffset.x,
                                rect.yMin - m_ScrollOffset.y,
                                rect.width,
                                rect.height);
        }

        private Vector2Int GetTransXY(int x, int y)
        {
            return new Vector2Int((x + m_ScrollOffset.x) % gridCellCount,
                                  (y + m_ScrollOffset.y) % gridCellCount);
        }
    }
}
