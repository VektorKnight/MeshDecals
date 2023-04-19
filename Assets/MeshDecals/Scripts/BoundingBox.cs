using UnityEngine;

namespace MeshDecals.Scripts {
    /// <summary>
    /// Basic bounding box. Unity's wa sa bit slow so I just made one.
    /// Not entirely necessary.
    /// </summary>
    public readonly struct BoundingBox {
        public readonly Vector3 Min, Max;

        public BoundingBox(Vector3 min, Vector3 max) : this() {
            Min = min;
            Max = max;
        }
        
        /// <summary>
        /// Whether or not this bounding box intersects another box.
        /// </summary>
        public bool Intersects(in BoundingBox other) {
            if (Max.x < other.Min.x || Min.x > other.Max.x) { return false; }
            if (Max.y < other.Min.y || Min.y > other.Max.y) { return false; }
            return (Max.z > other.Min.z) && (Min.z < other.Max.z);
        }

        public bool ContainsPoint(Vector3 p) {
            if (Max.x < p.x || Min.x > p.x) { return false; }
            if (Max.y < p.y || Min.y > p.y) { return false; }
            return (Max.z > p.z) && (Min.z < p.z);
        }

        public static BoundingBox UnitCube() {
            var extent = new Vector3(0.5f, 0.5f, 0.5f);
            return new BoundingBox(-extent, extent);
        }
    }
}