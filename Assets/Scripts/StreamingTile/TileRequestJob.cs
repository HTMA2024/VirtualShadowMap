using System;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveRendering
{
    public sealed class TileRequestJob
    {
        /// <summary>
        /// ��������.
        /// </summary>
        public int queuedCount { get => m_PendingRequests.Count; }

        /// <summary>
        /// �ȴ����������?
        /// </summary>
        private List<TileRequestData> m_PendingRequests = new List<TileRequestData>();

        /// <summary>
        /// ��ȡ��һ����������
        /// </summary>
        public TileRequestData? PeekRequest()
        {
            return m_PendingRequests.Count > 0 ? m_PendingRequests.First() : null;
        }

        /// <summary>
        /// ����ҳ������
        /// </summary>
        public TileRequestData? LocateRequest(int x, int y, int mip)
        {
            foreach (var req in m_PendingRequests)
            {
                if (req.gridColumn == x && req.gridRow == y && req.lodTier == mip)
                    return req;
            }

            return null;
        }

        /// <summary>
        /// �Ƴ�ҳ������
        /// </summary>
        public void CancelRequest(TileRequestData req)
        {
            if (m_PendingRequests.Count == 0 || !m_PendingRequests.Contains(req))
            {
                throw new InvalidOperationException("Trying to release an object that has already been released to the pool.");
            }

            m_PendingRequests.Remove(req);
        }

        /// <summary>
        /// �½�ҳ������
        /// </summary>
        public TileRequestData? SubmitRequest(int x, int y, int mip)
        {
            // �Ƿ��Ѿ������������?
            if (this.LocateRequest(x, y, mip) == null)
			{
                // ����������б�?
                var request = new TileRequestData();
                request.gridColumn = x;
                request.gridRow = y;
                request.lodTier = mip;

                m_PendingRequests.Add(request);

                return request;
            }

            return null;
        }

        /// <summary>
        /// �������ȼ�������mip
        /// </summary>
        public void OrderRequests()
        {
            if (m_PendingRequests.Count > 0)
                m_PendingRequests.Sort((x, y) => { return y.lodTier.CompareTo(x.lodTier); });
        }

        /// <summary>
        /// �Զ�������
        /// </summary>
        public void OrderRequests(Comparison<TileRequestData> comparison)
        {
            if (m_PendingRequests.Count > 0)
                m_PendingRequests.Sort(comparison);
        }

        /// <summary>
        /// ������е�ҳ������?
        /// </summary>
        public void PurgeAll()
        {
            m_PendingRequests.Clear();
        }

        /// <summary>
        /// ������е�ҳ������?
        /// </summary>
        public void PurgeAll(Action<TileRequestData> cancelRequestPageJob)
        {
            if (cancelRequestPageJob != null)
			{
                foreach (var req in m_PendingRequests)
                    cancelRequestPageJob?.Invoke(req);
            }

            m_PendingRequests.Clear();
        }
    }
}
