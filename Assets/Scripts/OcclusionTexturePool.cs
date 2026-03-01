using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace AdaptiveRendering
{
    /// <summary>
    /// Command Buffer Pool
    /// </summary>
    public static class OcclusionTexturePool
    {
        /// <summary>
        /// Get a new Render Texture.
        /// </summary>
        /// <returns></returns>
        public static RenderTexture Acquire(TileResolution resolution)
        {
            var texture = RenderTexture.GetTemporary(resolution.AsPixelCount(), resolution.AsPixelCount(), 16, RenderTextureFormat.RGHalf);
            texture.name = "StaticShadowMap";
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        /// <summary>
        /// Get a new Render Texture and assign a name to it.
        /// Named Render Textures will add profiling makers implicitly for the texture execution.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static RenderTexture Acquire(TileResolution resolution, string name)
        {
            var texture = RenderTexture.GetTemporary(resolution.AsPixelCount(), resolution.AsPixelCount(), 16, RenderTextureFormat.RGHalf);
            texture.name = name;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        /// <summary>
        /// Release a Render Texture.
        /// </summary>
        /// <param name="buffer"></param>
        public static void Reclaim(RenderTexture texture)
        {
            RenderTexture.ReleaseTemporary(texture);
        }
    }
}