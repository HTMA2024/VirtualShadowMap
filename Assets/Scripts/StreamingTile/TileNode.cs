using UnityEngine;

namespace AdaptiveRendering
{
    public sealed class TileNode
    {
        public int column { get; }

        public int rank { get; }

        public int mipLevel { get; }

        public int nodeExtent { get { return 1 << mipLevel; } }

        /// <summary>
        /// ��Ӧƽ����ͼ�е�id
        /// </summary>
        public int tileSlot { get { return payload.tileIndex; } }

        /// <summary>
        /// �Ƿ��ڿ���״̬
        /// </summary>
		public bool isResident { get { return payload.isResident; } }

        public TilePayload payload { get; }

        public TileNode(int x, int y, int mip)
        {
            this.column = x;
            this.rank = y;
            this.mipLevel = mip;
            this.payload = new TilePayload();
        }

        public RectInt ComputeBounds()
        {
            var cellSize = 1 << mipLevel;
            return new RectInt(column * cellSize, rank * cellSize, cellSize, cellSize);
        }

        /// <summary>
        /// ��ȡ��IndirectionTexture�ϻ��Ƶľ���
        /// </summary>
        public Matrix4x4 ComputeTransform(int pageSize, Vector2 offset)
        {
            var rect = ComputeBounds();
            var lb = rect.position + offset;

            while (lb.x < 0) lb.x += pageSize;
            while (lb.y < 0) lb.y += pageSize;

            var tileRect = new Rect(lb.x, lb.y, rect.width, rect.height);

            var size = tileRect.width / pageSize;
            var position = new Vector3(tileRect.x / pageSize, tileRect.y / pageSize, 0);

            return StreamingTileUtilities.ComputeScaleBiasMatrix(position, new Vector3(size, size, size));
        }
    }
}