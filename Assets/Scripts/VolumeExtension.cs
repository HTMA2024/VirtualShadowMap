using UnityEngine;

namespace AdaptiveRendering
{
    public static class VolumeExtension
    {
        public static Vector3 ExtractVertex(this Bounds aabb, int index)
        {
            Debug.Assert(index >= 0 && index <= 7);
            float X = (((index & 1) != 0) ^ ((index & 2) != 0)) ? (aabb.max.x) : (aabb.min.x);
            float Y = ((index / 2) % 2 == 0) ? (aabb.min.y) : (aabb.max.y);
            float Z = (index < 4) ? (aabb.min.z) : (aabb.max.z);
            return new Vector3(X, Y, Z);
        }

        public static Bounds ProjectToLocalSpace(this Bounds bounds, Matrix4x4 worldToLocalMatrix)
        {
            var boundsInLightSpace = new Bounds();
            boundsInLightSpace.max = Vector3.negativeInfinity;
            boundsInLightSpace.min = Vector3.positiveInfinity;

            for (var i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.ExtractVertex(i);
                Vector3 localPosition = worldToLocalMatrix.MultiplyPoint(corner);

                boundsInLightSpace.Encapsulate(localPosition);
            }

            return boundsInLightSpace;
        }
    }
}