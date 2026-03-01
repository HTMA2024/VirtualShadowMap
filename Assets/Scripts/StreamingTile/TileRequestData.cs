using System;
using UnityEngine.Serialization;

namespace AdaptiveRendering
{
    /// <summary>
    /// ��Ⱦ������.
    /// </summary>
    [Serializable]
    public struct TileRequestData : IEquatable<TileRequestData>
    {
        /// <summary>
        /// ҳ��X����
        /// </summary>
        [FormerlySerializedAs("columnIndex")]
        public int gridColumn;

        /// <summary>
        /// ҳ��Y����
        /// </summary>
        [FormerlySerializedAs("rowIndex")]
        public int gridRow;

        /// <summary>
        /// mipmap�ȼ�
        /// </summary>
        [FormerlySerializedAs("detailLevel")]
        public int lodTier;

        /// <summary>
        /// ҳ���С
        /// </summary>
        public int nodeExtent { get { return 1 << lodTier; } }

        /// <summary>
        /// ���캯��
        /// </summary>
        public TileRequestData(int x, int y, int mip)
        {
            gridColumn = x;
            gridRow = y;
            lodTier = mip;
        }

        public override int GetHashCode()
        {
            return (gridRow * short.MaxValue + gridColumn) * (1 + lodTier);
        }

        public bool Equals(TileRequestData other)
        {
            return this.gridColumn == other.gridColumn && this.gridRow == other.gridRow && this.lodTier == other.lodTier;
        }
    }
}