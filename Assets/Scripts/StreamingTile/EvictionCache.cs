namespace AdaptiveRendering
{
    public sealed class EvictionCache
    {
        public class NodeInfo
        {
            public int entryId = 0;
            public NodeInfo successor { get; set; }
            public NodeInfo predecessor { get; set; }
        }

        private NodeInfo [] m_SlotPool;
        private NodeInfo m_Head = null;
        private NodeInfo m_Tail = null;

        public int frontEntry { get { return m_Head.entryId; } }

        public EvictionCache(int count)
        {
            m_SlotPool = new NodeInfo[count];

            for (int i= 0;i < count;i++)
            {
                m_SlotPool[i] = new NodeInfo()
                {
                    entryId = i,
                };
            }

            for (int i = 0; i < count; i++)
            {
                m_SlotPool[i].successor = (i + 1 < count) ? m_SlotPool[i + 1] : null;
                m_SlotPool[i].predecessor = (i != 0) ? m_SlotPool[i - 1] : null;
            }

            m_Head = m_SlotPool[0];
            m_Tail = m_SlotPool[count - 1];
        }

        public void ResetAll()
		{
            if (m_SlotPool.Length > 0)
			{
                m_Head = m_SlotPool[0];
                m_Tail = m_SlotPool[m_SlotPool.Length - 1];
            }
        }

        public bool MarkActive(int id)
        {
            if (id < 0 || id >= m_SlotPool.Length)
                return false;

            var node = m_SlotPool[id];
            if (node == m_Tail)
            {
                return true;
            }

            Remove(node);
            AddLast(node);
            return true;
        }

        private void AddLast(NodeInfo node)
        {
            var lastTail = m_Tail;
            lastTail.successor = node;
            m_Tail = node;
            node.predecessor = lastTail;
        }

        private void Remove(NodeInfo node)
        {
            if (m_Head == node)
            {
                m_Head = node.successor;
                if (m_Head != null)
                    m_Head.predecessor = null;
            }
            else
            {
                node.predecessor.successor = node.successor;
                if (node.successor != null)
                    node.successor.predecessor = node.predecessor;
            }

            node.predecessor = null;
            node.successor = null;
        }
    }
}
