using UnityEngine;

namespace AdaptiveRendering
{
    /// <summary>
    /// 提供�?GraphicsBuffer，替�?DX12Trick.EmptyBuffer
    /// </summary>
    public static class NullBufferProvider
    {
        private static GraphicsBuffer s_EmptyBuffer;

        public static GraphicsBuffer EmptyBuffer
        {
            get
            {
                if (s_EmptyBuffer == null || !s_EmptyBuffer.IsValid())
                {
                    s_EmptyBuffer = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured, 1, sizeof(float));
                }
                return s_EmptyBuffer;
            }
        }

        public static void Cleanup()
        {
            if (s_EmptyBuffer != null)
            {
                s_EmptyBuffer.Release();
                s_EmptyBuffer = null;
            }
        }
    }
}
