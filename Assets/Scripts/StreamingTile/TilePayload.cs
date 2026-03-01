using System;

namespace AdaptiveRendering
{
    /// <summary>
    /// ҳ������
    /// </summary>
    public sealed class TilePayload
    {
        private static int s_InvalidTileIndex = -1;

        /// <summary>
        /// ��Ӧƽ����ͼ�е�id
        /// </summary>
		public int tileIndex = s_InvalidTileIndex;

        /// <summary>
        /// �����֡���
        /// </summary>
        public int lastTouchFrame;

        /// <summary>
        /// ��Ⱦ����
        /// </summary>
		public TileRequestData? queuedRequest;

        /// <summary>
        /// �Ƿ��ڿ���״̬
        /// </summary>
		public bool isResident { get { return tileIndex != s_InvalidTileIndex; } }

        /// <summary>
        /// ����ҳ������
        /// </summary>
        public void ClearTileSlot()
        {
            tileIndex = s_InvalidTileIndex;
        }
    }
}