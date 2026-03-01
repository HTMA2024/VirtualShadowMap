using UnityEngine;

namespace AdaptiveRendering
{
    public struct StreamingTileFormat
    {
        public RenderTextureFormat renderFormat;
        public RenderTextureReadWrite gammaMode;
        public FilterMode filterSampling;
        public TextureWrapMode wrapBehavior;

        public StreamingTileFormat(RenderTextureFormat format, FilterMode filterMode = FilterMode.Bilinear, TextureWrapMode wrapMode = TextureWrapMode.Clamp, RenderTextureReadWrite readWrite = RenderTextureReadWrite.Linear)
        {
            this.renderFormat = format;
            this.wrapBehavior = wrapMode;
            this.filterSampling = filterMode;
            this.gammaMode = readWrite;
        }
    }
}