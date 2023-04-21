using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor.Rendering;
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
        // A decimeter seems reasonable.
        private const float MINIMUM_SCALE = 0.1f;

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

        private static readonly int _idSurfaceOffset = Shader.PropertyToID("_Offset");

        [Header("Decal Settings")]
        [SerializeField] private ProjectionAxis _axis = ProjectionAxis.Z;
        [SerializeField] private bool _excludeBackfacing;
        [SerializeField] private float _surfaceOffset = 0.001f;

        [Header("Debug Drawing")]
        [SerializeField] private bool _debugDrawVolume = true;
        [SerializeField] private bool _debugDrawAxis = true;
        [SerializeField] private bool _debugDrawBounds = false;
        [SerializeField] private bool _debugDrawSources = false;
        
        [SerializeField] private Color _debugVolumeColor = Color.cyan;
        [SerializeField] private Color _debugBoundsColor = Color.grey;
        [SerializeField] private Color _debugSourceColor = Color.yellow;
        
        private List<MeshFilter> _relevantMeshes = new List<MeshFilter>();
        private Mesh _decalMesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
    
        private void Start() {
            _decalMesh = new Mesh() {indexFormat = IndexFormat.UInt32};
            _renderer = GetComponent<MeshRenderer>();
            GetComponent<MeshFilter>().mesh = _decalMesh;
            
            UpdatePropertyBlock();
        }

        private void UpdatePropertyBlock() {
            if (_propertyBlock == null) {
                _propertyBlock = new MaterialPropertyBlock();
            }
            
            var offsetParams = _axis switch {
                ProjectionAxis.X => new Vector4(1, 0, 0, _surfaceOffset),
                ProjectionAxis.Y => new Vector4(0, 1, 0, _surfaceOffset),
                ProjectionAxis.Z => new Vector4(0, 0, 1, _surfaceOffset),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            _propertyBlock.SetVector(_idSurfaceOffset, offsetParams);
            _renderer.SetPropertyBlock(_propertyBlock, 0);
        }
    
        private void Update() {
            UpdatePropertyBlock();
            
            FindRelevantMeshRenderers(_relevantMeshes);

            if (_relevantMeshes.Count < 0) {
                return;
            }

            var sw = new Stopwatch();
            sw.Start();
            
            DecalMeshBuilder.BeginDecalMesh(ref _decalMesh, _axis, _excludeBackfacing);

            var triangleCount = 0u;
            foreach (var meshFilter in _relevantMeshes) {
                var meshToWorld = meshFilter.transform.localToWorldMatrix;
                var worldToDecal = transform.worldToLocalMatrix;

                triangleCount += meshFilter.sharedMesh.GetIndexCount(0) / 3;

                DecalMeshBuilder.ProcessSourceMesh(meshToWorld, worldToDecal, meshFilter.sharedMesh);
            }

            DecalMeshBuilder.EndDecalMesh(ref _decalMesh);

            sw.Stop();
            Debug.Log($"Decal Mesh Time: {sw.ElapsedMilliseconds}ms");
            Debug.Log($"Meshes Processed: {_relevantMeshes.Count}");
            Debug.Log($"Triangles Processed: {triangleCount}");
            Debug.Log($"Decal Triangles: {_decalMesh.GetIndexCount(0) / 3}");
        }

        /// <summary>
        /// This was built with the path of least resistance to a working example.
        /// This search would likely be extremely slow on large and complex scenes.
        /// Ideally, the decal or some managing system would query meshes intersecting the volume.
        /// </summary>
        private void FindRelevantMeshRenderers(in List<MeshFilter> meshes) {
            meshes.Clear();
        
            var bounds = GetBounds();
            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

            foreach (var meshRenderer in renderers) {
                // Skip self.
                if (meshRenderer == _renderer) {
                    continue;
                }
                
                // Skip other mesh decals.
                if (meshRenderer.GetComponent<MeshDecal>()) {
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
        
        /// <summary>
        /// Get the bounding box of this decal.
        /// </summary>
        /// <returns></returns>
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
    
        #if UNITY_EDITOR
        private void OnDrawGizmos() {
            // Just clamp scale here.
            var scale = transform.localScale;
            transform.localScale = new Vector3(
                Mathf.Max(MINIMUM_SCALE, scale.x),
                Mathf.Max(MINIMUM_SCALE, scale.y),
                Mathf.Max(MINIMUM_SCALE, scale.z)
            );

            // Draw the projection volume.
            if (_debugDrawVolume) {
                Gizmos.color = _debugVolumeColor;
                for (var i = 0; i < BOX_EDGES.Length; i += 2) {
                    var p0 = transform.TransformPoint(BOX_CORNERS[BOX_EDGES[i]]);
                    var p1 = transform.TransformPoint(BOX_CORNERS[BOX_EDGES[i + 1]]);

                    Gizmos.DrawLine(p0, p1);
                }
            }

            // Draw projection axis.
            if (_debugDrawAxis) {
                Gizmos.color = _axis switch {
                    ProjectionAxis.X => Color.red,
                    ProjectionAxis.Y => Color.green,
                    ProjectionAxis.Z => Color.blue,
                    _ => throw new ArgumentOutOfRangeException()
                };

                var axis = _axis switch {
                    ProjectionAxis.X => transform.right,
                    ProjectionAxis.Y => transform.up,
                    ProjectionAxis.Z => transform.forward,
                    _ => throw new ArgumentOutOfRangeException()
                };
                
                var a0 = transform.TransformPoint(axis * 0.5f) + axis * 0.5f;
                var a1 = transform.TransformPoint(-axis * 0.5f) - axis * 0.5f;
                Gizmos.DrawLine(a0, a1);
            }

            // Only draw the bounding box if the decal is rotated.
            if (_debugDrawBounds) {
                var angles = transform.rotation.eulerAngles;
                if (angles.x + angles.y + angles.z > 0.001f) {
                    Gizmos.color = Color.grey;
                    var bounds = GetBounds();
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }

            // Draw bounds of relevant meshes.
            if (_debugDrawSources) {
                Gizmos.color = _debugSourceColor;
                foreach (var meshFilter in _relevantMeshes) {
                    Gizmos.DrawWireCube(meshFilter.transform.position,
                        meshFilter.GetComponent<MeshRenderer>().bounds.size);
                }
            }
        }
        #endif
    }
}
