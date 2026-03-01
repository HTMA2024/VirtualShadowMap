using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveRendering
{
    public static class VirtualShadowMapsUtilities
    {
        public static Bounds CalculateBoundingBox(List<Renderer> renderers)
        {
            if (renderers != null && renderers.Count > 0)
            {
                Bounds aabb = new Bounds();
                aabb.max = Vector3.negativeInfinity;
                aabb.min = Vector3.positiveInfinity;

                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                        aabb.Encapsulate(renderer.bounds);
                }

                return aabb;
            }

            return new Bounds();
        }

        public static Bounds CalculateBoundingBox(List<Renderer> renderers, Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);

            Bounds aabb = new Bounds();
            aabb.max = Vector3.negativeInfinity;
            aabb.min = Vector3.positiveInfinity;

            foreach (var it in renderers)
            {
                if (it != null)
                {
                    if (GeometryUtility.TestPlanesAABB(planes, it.bounds))
                        aabb.Encapsulate(it.bounds);
                }
            }

            return aabb;
        }

        public static Bounds CalculateBoundingBox(List<MeshRenderer> renderers, Plane[] planes)
        {
            Bounds aabb = new Bounds();
            aabb.max = Vector3.negativeInfinity;
            aabb.min = Vector3.positiveInfinity;

            foreach (var it in renderers)
            {
                if (it != null)
                {
                    if (GeometryUtility.TestPlanesAABB(planes, it.bounds))
                        aabb.Encapsulate(it.bounds);
                }
            }

            return aabb;
        }

        public static float CameraSpaceDistance(Vector3 pos, Vector3 normal, Vector3 origion, Vector3 direction)
        {
            Plane pane = new Plane(normal, pos);
            Ray ray = new Ray(origion, direction);

            pane.Raycast(ray, out var enter);
            var point = ray.GetPoint(enter);

            return (ray.origin - point).magnitude;
        }

        public static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(pos);
            Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        public static float CalculateBiasScale(float orthographicSize, int tileSize)
        {
            float halfFrustumSize = orthographicSize;
            float halfTexelResolution = halfFrustumSize / (tileSize * 2);

            float biasScale = 10;
            biasScale *= halfTexelResolution;

            return biasScale;
        }
    }
}