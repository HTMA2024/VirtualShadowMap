using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveRendering
{
    public static class StreamingTileUtilities
    {
        /// <summary>
        /// ����Tile��
        /// </summary>
        private static Mesh m_FullscreenQuad = null;

        public static Mesh fullscreenMesh
        {
            get
            {
                if (m_FullscreenQuad != null)
                    return m_FullscreenQuad;

                m_FullscreenQuad = new Mesh() { name = "Fullscreen Quad" };
                m_FullscreenQuad.SetVertices(new List<Vector3>() {
                    new Vector3(0, 1, 0.0f),
                    new Vector3(0, 0, 0.0f),
                    new Vector3(1, 0, 0.0f),
                    new Vector3(1, 1, 0.0f)
                });

                m_FullscreenQuad.SetUVs(0, new List<Vector2>()
                {
                    new Vector2(0, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 0)
                });

                m_FullscreenQuad.SetIndices(new[] { 0, 1, 2, 2, 3, 0 }, MeshTopology.Triangles, 0, false);
                m_FullscreenQuad.UploadMeshData(true);

                return m_FullscreenQuad;
            }
        }

        public static Matrix4x4 ComputeScaleBiasMatrix()
        {
            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 1.0f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            textureScaleAndBias.m23 = 0.0f;

            return textureScaleAndBias;
        }

        public static Matrix4x4 ComputeScaleBiasMatrix(Matrix4x4 proj, Matrix4x4 view)
        {
            Matrix4x4 worldToShadow = proj * view;
            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 1.0f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            textureScaleAndBias.m23 = 0.0f;

            return textureScaleAndBias * worldToShadow;
        }

        public static Matrix4x4 ComputeScaleBiasMatrix(Vector3 offset, Vector3 scale)
        {
            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = scale.x;
            textureScaleAndBias.m11 = scale.x;
            textureScaleAndBias.m22 = scale.x;
            textureScaleAndBias.m03 = offset.x;
            textureScaleAndBias.m13 = offset.y;
            textureScaleAndBias.m23 = offset.z;

            return textureScaleAndBias;
        }
    }
}
