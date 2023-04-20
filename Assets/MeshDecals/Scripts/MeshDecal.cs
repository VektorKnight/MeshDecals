using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace MeshDecals.Scripts {
    /// <summary>
    /// Hastily constructed to prove functionality.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class MeshDecal : MonoBehaviour {
        private static readonly Vector3[] BOX_CORNERS = new[] {
            new Vector3(-0.5f, -0.5f, -0.5f), // BL
            new Vector3(-0.5f, 0.5f, -0.5f),  // TL
            new Vector3(0.5f, 0.5f, -0.5f),   // TR
            new Vector3(0.5f, -0.5f, -0.5f),  // BR
        
            new Vector3(-0.5f, -0.5f, 0.5f), // BL
            new Vector3(-0.5f, 0.5f, 0.5f),  // TL
            new Vector3(0.5f, 0.5f, 0.5f),   // TR
            new Vector3(0.5f, -0.5f, 0.5f),  // BR
        };

        private static readonly int[] BOX_EDGES = new[] {
            0, 1, 1, 2, 2, 3, 3, 0,
        
            4, 5, 5, 6, 6, 7, 7, 4,
        
            0, 4, 1, 5, 2, 6, 3, 7
        };

        [Header("Decal Settings")]
        [SerializeField] private ProjectionAxis _axis = ProjectionAxis.Z;
        [SerializeField] private bool _excludeBackfacing;

        private List<MeshFilter> _relevantMeshes = new List<MeshFilter>();
    
        // Mesh work buffers.
        private Mesh _decalMesh;

        private MeshRenderer _renderer;
    
        private void Start() {
            _decalMesh = new Mesh() {indexFormat = IndexFormat.UInt32};
            _renderer = GetComponent<MeshRenderer>();
            GetComponent<MeshFilter>().mesh = _decalMesh;
        }
    
        private void Update() {
            //if (transform.hasChanged) {
            FindRelevantMeshRenderers(_relevantMeshes);

            if (_relevantMeshes.Count < 0) {
                return;
            }

            var sw = new Stopwatch();
            sw.Start();
            
            //Profiler.BeginSample("Decal Mesh");
            DecalMeshBuilder.BeginDecalMesh(ref _decalMesh, _axis);

            var triangleCount = 0u;
            foreach (var meshFilter in _relevantMeshes) {
                var meshToWorld = meshFilter.transform.localToWorldMatrix;
                var worldToDecal = transform.worldToLocalMatrix;

                triangleCount += meshFilter.sharedMesh.GetIndexCount(0) / 3;

                DecalMeshBuilder.ProcessSourceMesh(meshToWorld, worldToDecal, meshFilter.sharedMesh);
            }

            DecalMeshBuilder.EndDecalMesh(ref _decalMesh);

            //Profiler.EndSample();
            
            sw.Stop();
            Debug.Log($"Decal Mesh Time: {sw.ElapsedMilliseconds}ms");
            Debug.Log($"Meshes Processed: {_relevantMeshes.Count}");
            Debug.Log($"Triangles Processed: {triangleCount}");
            Debug.Log($"Decal Triangles: {_decalMesh.GetIndexCount(0) / 3}");
            //}
        }

        void FindRelevantMeshRenderers(in List<MeshFilter> meshes) {
            meshes.Clear();
        
            var bounds = GetBounds();
            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

            foreach (var meshRenderer in renderers) {
                // Skip self.
                if (meshRenderer == _renderer) {
                    continue;
                }
            
                // Not intersecting the decal bounding box.
                if (!meshRenderer.bounds.Intersects(bounds)) {
                    continue;
                }
            
                // Must have a mesh filter with a valid mesh.
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter && meshFilter.sharedMesh) {
                    meshes.Add(meshFilter);
                }
            }
        }

        private Bounds GetBounds() {
            // Transform all corners to world space then find the min/max.
            var min = transform.position;
            var max = min;

            foreach (var corner in BOX_CORNERS) {
                min = Vector3.Min(min, transform.TransformPoint(corner));
                max = Vector3.Max(max, transform.TransformPoint(corner));
            }

            return new Bounds(transform.position, max - min);
        }
    
        private void OnDrawGizmos() {
            // Draw the projection volume.
            Gizmos.color = Color.cyan;
            for (var i = 0; i < BOX_EDGES.Length; i += 2) {
                var p0 = transform.TransformPoint(BOX_CORNERS[BOX_EDGES[i]]);
                var p1 = transform.TransformPoint(BOX_CORNERS[BOX_EDGES[i + 1]]);
            
                Gizmos.DrawLine(p0, p1);
            }
        
            // Draw projection axis.
            Gizmos.color = _axis switch {
                ProjectionAxis.X => Color.red,
                ProjectionAxis.Y => Color.green,
                ProjectionAxis.Z => Color.blue,
                _ => throw new ArgumentOutOfRangeException()
            };
        
            var axis = _axis switch {
                ProjectionAxis.X => Vector3.right,
                ProjectionAxis.Y => Vector3.up,
                ProjectionAxis.Z => Vector3.forward,
                _ => throw new ArgumentOutOfRangeException()
            };
        
            var a0 = transform.TransformPoint(-axis);
            var a1 = transform.TransformPoint(axis);
            Gizmos.DrawLine(a0, a1);
        
            // Only draw the bounding box if the decal is rotated.
            return;
            var angles = transform.rotation.eulerAngles;
            if (angles.x + angles.y + angles.z > 0.001f) {
                Gizmos.color = Color.grey;
                var bounds = GetBounds();
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        
            // Draw bounds of relevant meshes.
            return;
            Gizmos.color = Color.yellow;
            foreach (var meshFilter in _relevantMeshes) {
                Gizmos.DrawWireCube(meshFilter.transform.position, meshFilter.GetComponent<MeshRenderer>().bounds.size);
            }
        }
    }
}
