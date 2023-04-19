using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeshDecals.Scripts {
    public static class DecalMeshBuilder {
        // Unit rectangle centered on origin for clipping.
        private static readonly List<Vector3> CLIP_RECT = new List<Vector3>() {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
        };
        
        // Mesh work buffers.
        private static readonly List<Vector3> _vertexWorkBuffer = new List<Vector3>();
        private static readonly List<int> _indexWorkBuffer = new List<int>();
        private static readonly List<Vector3> _polygonWorkBuffer = new List<Vector3>();
        
        // Sutherlan-Hodgmann work buffers.
        private static readonly List<Vector3> _inputList = new List<Vector3>();
        private static readonly List<Vector3> _outputList = new List<Vector3>();
        
        // Result work buffers.
        private static readonly List<Vector3> _resultVertices = new List<Vector3>();
        private static readonly List<int> _resultTriangles = new List<int>();

        /// <summary>
        /// Swizzles vertices such that the coordinates for the desired clipping axis end up in XY.
        /// Allows the clipping algorithm to only ever operate on X/Y without generalizing it to 3D.
        /// </summary>
        private static Vector3 Swizzle(in Vector3 v, ProjectionAxis axis) {
            return axis switch {
                ProjectionAxis.Z => v,
                ProjectionAxis.Y => new Vector3(v.x, v.z, v.y),
                ProjectionAxis.X => new Vector3(v.z, v.y, v.x),
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            };
        }

        /// <summary>
        /// Determines if a point lies on the right-hand side of an edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPointInside(in Vector3 p, in Vector3 s0, in Vector3 s1) {
            // Cross product.
            var k = (s1.x - s0.x) * (p.y - s0.y) - (s1.y - s0.y) * (p.x - s0.x);
            return k <= 0;
        }

        /// <summary>
        /// Finds the intersection of two lines each defined as a pair of points.
        /// This does not check for parallel lines as the algorithm only calls this when the lines are known to intersect.
        /// We use use the same T to interpolate the missing coordinate (always mapped to Z here).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 IntersectLines(
            in Vector3 a0, in Vector3 a1,
            in Vector3 b0, in Vector3 b1
        ) {
            // Compute denominator for determinants.
            var d = (a0.x - a1.x) * (b0.y - b1.y) - (a0.y - a1.y) * (b0.x - b1.x);

            // Compute determinate.
            var t = ((a0.x - b0.x) * (b0.y - b1.y) - (a0.y - b0.y) * (b0.x - b1.x)) / d;

            // Compute intersection.
            return a0 + t * (a1 - a0);
        }

        /// <summary>
        /// Clips a polygon against the unit square centered on origin using Sutherland-Hodgmann.
        /// References:
        /// https://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman_algorithm
        /// </summary>
        private static void ClipPolygons(in List<Vector3> subject, in List<Vector3> clip, ProjectionAxis axis) {
            _inputList.Clear();
            _outputList.Clear();

            for (var i = 0; i < subject.Count; i++) {
                _outputList.Add(Swizzle(subject[i], axis));
            }

            for (var i = 0; i < clip.Count; i++) {
                _inputList.Clear();
                _inputList.AddRange(_outputList);
                _outputList.Clear();

                if (_inputList.Count == 0) {
                    continue;
                }
                
                var c0 = clip[i];
                var c1 = clip[(i + 1) % clip.Count];

                var s0 = _inputList[_inputList.Count - 1];
                for (var j = 0; j < _inputList.Count; j++) {
                    var s1 = _inputList[j];

                    if (IsPointInside(s1, c0, c1)) {
                        if (!IsPointInside(s0, c0, c1)) {
                            var p = IntersectLines(s0, s1, c0, c1);
                            _outputList.Add(p);
                        }
                        
                        _outputList.Add(s1);
                    }
                    else if (IsPointInside(s0, c0, c1)) {
                        var p = IntersectLines(s0, s1, c0, c1);
                        _outputList.Add(p);
                    }

                    s0 = s1;
                }
            }
            
            // Skip if we get a degenerate polygon.
            _polygonWorkBuffer.Clear();
            if (_outputList.Count < 3) {
                return;
            }
            
            for (var i = 0; i < _outputList.Count; i++) {
                var vertex = _outputList[i];
                _polygonWorkBuffer.Add(Swizzle(vertex, axis));
            }
        }
        
        /// <summary>
        /// Rounds given vertex to use as a key for welding.
        /// TODO: Is it faster to return a primitive type for the dictionary?
        /// </summary>
        private static Vector3 RoundVertex(Vector3 v, float tolerance) {
            var it = 1.0f / tolerance;
            v.x = Mathf.Round(v.x * it) * tolerance;
            v.y = Mathf.Round(v.y * it) * tolerance;
            v.z = Mathf.Round(v.z * it) * tolerance;

            return v;
        }
        
        /// <summary>
        /// Welds vertices within a certain tolerance.
        /// </summary>
        private static void WeldVertices(
            in List<Vector3> srcVerts,
            in List<int> srcTris,
            out Vector3[] newVerts,
            float tolerance = float.Epsilon
        ) {
            var duplicateTable = new Dictionary<Vector3, int>();
            var vertexMap = new List<int>();
            var triangleMap = new int[srcVerts.Count];

            for (var i = 0; i < srcVerts.Count; i++) {
                var vertex = srcVerts[i];
                var key = RoundVertex(vertex, tolerance);
                
                if (!duplicateTable.ContainsKey(key)) {
                    var count = vertexMap.Count;
                    duplicateTable.Add(key, count);
                    triangleMap[i] = count;
                    vertexMap.Add(i);

                    continue;
                }

                triangleMap[i] = duplicateTable[key];
            }
            
            // Create the new vertex array and map vertices.
            newVerts = new Vector3[vertexMap.Count];
            for (var i = 0; i < vertexMap.Count; i++) {
                var mi = vertexMap[i];
                newVerts[i] = srcVerts[mi];
            }
     
            // We can update the original triangle list in place.
            for (var i = 0; i < srcTris.Count; i++) {
                srcTris[i] = triangleMap[srcTris[i]];
            }
        }
        
        /// <summary>
        /// Sets everything up to build a decal mesh.
        /// </summary>
        public static void BeginDecalMesh(ref Mesh mesh) {
            if (!mesh) {
                mesh = new Mesh() { indexFormat = IndexFormat.UInt32 };
            }
            
            mesh.Clear();
            
            _resultVertices.Clear();
            _resultTriangles.Clear();
        }
        
        /// <summary>
        /// Processes a given source mesh and adds any resulting triangles to the decal mesh.
        /// </summary>
        public static bool ProcessSourceMesh(Matrix4x4 srcToWorld, Matrix4x4 worldToDecal, in Mesh src) {
            if (!src) {
                throw new ArgumentNullException(nameof(src));
            }
            
            if (src.GetIndexCount(0) < 3) {
                return false;
            }
            
            // Clear work buffers.
            _vertexWorkBuffer.Clear();
            _indexWorkBuffer.Clear();
            
            // TODO: Handle sub-meshes.
            src.GetVertices(_vertexWorkBuffer);
            src.GetTriangles(_indexWorkBuffer, 0);

            // Walk each triangle in the source mesh.
            var bounds = BoundingBox.UnitCube();
            for (var i = 0; i < _indexWorkBuffer.Count; i += 3) {
                // Fetch vertex positions for the triangle.
                var v0 = _vertexWorkBuffer[_indexWorkBuffer[i]];
                var v1 = _vertexWorkBuffer[_indexWorkBuffer[i + 1]];
                var v2 = _vertexWorkBuffer[_indexWorkBuffer[i + 2]];
                
                // Source to world.
                v0 = srcToWorld.MultiplyPoint(v0);
                v1 = srcToWorld.MultiplyPoint(v1);
                v2 = srcToWorld.MultiplyPoint(v2);
                
                // World to decal.
                v0 = worldToDecal.MultiplyPoint(v0);
                v1 = worldToDecal.MultiplyPoint(v1);
                v2 = worldToDecal.MultiplyPoint(v2);
                
                // If the triangles is fully enclosed, just add it and continue to the next.
                if (bounds.ContainsPoint(v0) && bounds.ContainsPoint(v1) && bounds.ContainsPoint(v2)) {
                    _resultVertices.Add(v0);
                    _resultVertices.Add(v1);
                    _resultVertices.Add(v2);

                    continue;
                }

                // Skip if the triangle does not intersect the cube.
                // TODO: It's actually slower to try and skip triangles with an intersection test.
                /*if (!DecalMeshUtility.TriangleIntersectsUnitCube(v0, v1, v2)) {
                    continue;
                }*/

                // Add triangle to polygon work buffer.
                _polygonWorkBuffer.Clear();
                _polygonWorkBuffer.Add(v0);
                _polygonWorkBuffer.Add(v1);
                _polygonWorkBuffer.Add(v2);
                
                // Clip triangle along one axis first.
                // This gets the 4 planes of the AABB around that axis.
                // Z is chosen here as it does not require a swizzle to move coordinates into XY positions.
                ClipPolygons(_polygonWorkBuffer, CLIP_RECT, ProjectionAxis.Z);
                
                // Skip triangle if clip resulted in a degenerate polygon.
                if (_polygonWorkBuffer.Count == 0) {
                    continue;
                }
                
                // Clip resulting polygon again in an axis orthogonal to the first.
                // This handles the remaining two planes of the AABB.
                // X is chosen here but Y would work just as well.
                // TODO: This actually does 4 plane clips and could be reduced to two.
                ClipPolygons(_polygonWorkBuffer, CLIP_RECT, ProjectionAxis.X);
                
                // Skip if we get a degenerate polygon.
                if (_polygonWorkBuffer.Count == 0) {
                    continue;
                }
                
                // Triangulate the final polygon and add the vertices to the result.
                // TODO: Better triangulation methods?
                DecalMeshUtility.TriangulatePolygonFan(_polygonWorkBuffer, _resultVertices, out var count);
            }

            return _resultVertices.Count >= 3;
        }
        
        /// <summary>
        /// Builds the final decal mesh.
        /// </summary>
        public static bool EndDecalMesh(ref Mesh result) {
            if (_resultVertices.Count < 3) {
                return false;
            }
            
            // Triangles are all in sequence so just populate the index buffer with [0..VertexCount - 1].
            for (var i = 0; i < _resultVertices.Count; i++) {
                _resultTriangles.Add(i);
            }
            
            // TODO: Pack triangles from source meshes into sub-meshes.

            // Weld duplicate vertices within a certain tolerance.
            WeldVertices(_resultVertices, _resultTriangles, out var finalVertices, 0.0001f);
            
            // TODO: Decimation or simplification pass to clean up unnecessary triangles.
            // TODO: Upload texture coordinates for plane of projection selected by the user.
            
            // Set final mesh data.
            result.SetVertices(finalVertices);
            result.SetIndices(_resultTriangles, MeshTopology.Triangles, 0);
            
            result.RecalculateBounds();
            result.RecalculateNormals();
            result.Optimize();
            
            return true;
        }
    }
}