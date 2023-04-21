using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

namespace MeshDecals.Scripts {
    public static class DecalMeshUtility {
        /// <summary>
        /// Separating axis test for the triangle vs unit cube.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SeparatingAxisTest(in Vector3 t0, in Vector3 t1, in Vector3 t2, in Vector3 a, in Vector3 e) {
            var r = Vector3.right;
            var u = Vector3.up;
            var f = Vector3.forward;
            
            var p0 = Vector3.Dot(t0, a);
            var p1 = Vector3.Dot(t1, a);
            var p2 = Vector3.Dot(t2, a);
            
            var i = e.x * Mathf.Abs(Vector3.Dot(r, a)) +
                    e.y * Mathf.Abs(Vector3.Dot(u, a)) +
                    e.z * Mathf.Abs(Vector3.Dot(f, a));
            
            return Mathf.Min(p0, p2) <= i || Mathf.Max(p0, p1) >= -i;
        }
        
        /// <summary>
        /// Determines if a triangle intersects a bounding box.
        /// References:
        /// https://fileadmin.cs.lth.se/cs/Personal/Tomas_Akenine-Moller/code/tribox_tam.pdf
        /// https://gdbooks.gitbooks.io/3dcollisions/content/Chapter4/aabb-triangle.html
        /// </summary>
        public static bool TriangleIntersectsBox(in Vector3 t0, in Vector3 t1, in Vector3 t2, in BoundingBox box) {
            // Early intersection test.
            // An intersection exists if any vertices lie within the cube.
            if (box.ContainsPoint(t0) || box.ContainsPoint(t1) || box.ContainsPoint(t2)) {
                return true;
            }
            
            // Early rejection tests.
            // Reject the triangle early if neither the bounds nor plane of the triangle do not cross the cube.
            {
                // Compute bounds of the triangle.
                var min = Vector3.Min(Vector3.Min(t0, t1), t2);
                var max = Vector3.Max(Vector3.Max(t0, t1), t2);
                var bounds = new BoundingBox(min, max);

                // No intersection of the triangle bounds does not intersect the cube.
                if (!bounds.Intersects(box)) {
                    return false;
                }

                // Compute plane of triangle as normal and distance.
                var n = Vector3.Cross(t1 - t0, t2 - t0);
                var d = Vector3.Dot(n, t0);

                // Compute projection interval of the cube onto the plane.
                var extents = box.Extents;
                var pi = extents.x * Mathf.Abs(n.x) + extents.y * Mathf.Abs(n.y) + extents.z * Mathf.Abs(n.z);

                // No intersection if plane distance is outside [-pi, pi].
                if (Mathf.Abs(d) > pi) {
                    return false;
                }
            }
            
            // Cover all the other cases with separating axis theorem.
            {
                // Compute the edges of the triangle.
                var te0 = t1 - t0;
                var te1 = t2 - t1;
                var te2 = t0 - t2;
                
                // Face normals of the cube.
                var cf0 = Vector3.right;
                var cf1 = Vector3.up;
                var cf2 = Vector3.forward;
                
                // Compute the 9 separating axes.
                var ax0 = Vector3.Cross(cf0, te0);
                var ax1 = Vector3.Cross(cf0, te1);
                var ax2 = Vector3.Cross(cf0, te2);

                var ax3 = Vector3.Cross(cf1, te0);
                var ax4 = Vector3.Cross(cf1, te1);
                var ax5 = Vector3.Cross(cf1, te2);
                
                var ax6 = Vector3.Cross(cf2, te0);
                var ax7 = Vector3.Cross(cf2, te1);
                var ax8 = Vector3.Cross(cf2, te2);
                
                // Test each axis.
                var extents = box.Extents;
                if (!SeparatingAxisTest(t0, t1, t2, ax0, extents)) { return false; }
                if (!SeparatingAxisTest(t0, t1, t2, ax1, extents)) { return false; }
                if (!SeparatingAxisTest(t0, t1, t2, ax2, extents)) { return false; }
                
                if (!SeparatingAxisTest(t0, t1, t2, ax3, extents)) { return false; }
                if (!SeparatingAxisTest(t0, t1, t2, ax4, extents)) { return false; }
                if (!SeparatingAxisTest(t0, t1, t2, ax5, extents)) { return false; }
                
                if (!SeparatingAxisTest(t0, t1, t2, ax6, extents)) { return false; }
                if (!SeparatingAxisTest(t0, t1, t2, ax7, extents)) { return false; }
                
                // Intersection if all tests have passed at this point.
                return SeparatingAxisTest(t0, t1, t2, ax8, extents);
            }
        }
        
        /// <summary>
        /// Performs fan triangulation on a convex polygon defined as a set of vertices in clockwise order.
        /// </summary>
        public static void TriangulatePolygonFan(in List<Vector3> polygon, in List<Vector3> result, out int count) {
            // Origin of the triangle fan.
            var v0 = polygon[0];
            
            // Walk each vertex, find the current edge, and create the triangle.
            count = 0;
            for (var i = 1; i < polygon.Count - 1; i++) {
                var v1 = polygon[i];
                var v2 = polygon[i + 1];
                
               result.Add(v0);
               result.Add(v1);
               result.Add(v2);

                count += 3;
            }
        }
        
        /// <summary>
        /// Computes the face normal of a given triangle.
        /// </summary>
        public static Vector3 TriangleNormal(in Vector3 v0, in Vector3 v1, in Vector3 v2, bool normalize = true) {
            var e0 = v1 - v0;
            var e1 = v2 - v0;
            var n = Vector3.Cross(e0, e1);

            return normalize ? n.normalized : n;
        }
    }
}