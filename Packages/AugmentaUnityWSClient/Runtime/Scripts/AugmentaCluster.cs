using Augmenta;
using UnityEngine;

namespace AugmentaWebsocketClient
{
    /// <summary>
    /// Represents a tracked object in the space. This can be a person or an object, and is represented by a square box.
    /// You can use this component to get info about it: its position, size, etc.
    /// A cluster can optionnaly contain a points array: this is the point cloud that it contains (you'll need to enable the `cluster points` option from the client to see them)
    /// </summary>
    public class AugmentaCluster : AugmentaObject
    {
        /// <summary>
        /// The point cloud's point array. Points coordinates are relative to the parent augmenta scene's pivot !
        /// </summary>
        /// <todo>
        /// Should points be transformed to be in local or world space ? This could be computationnaly intensive 
        /// </todo>
        public System.ArraySegment<Vector3> points { get { return nativeObject.points; } }

        public GenericObject<Vector3>.State state { get { return nativeObject.state; } }

        /// <summary>
        /// Average position of a cluster's point cloud's points in world space
        /// </summary>
        public Vector3 centroid
        {
            get
            {
                return this.transform.localToWorldMatrix.MultiplyPoint(nativeObject.centroid);
            }
        }

        /// <summary>
        /// Average position of the cluster's point cloud's points in local space
        /// </summary>
        public Vector3 localCentroid
        {
            get
            {
                return this.nativeObject.centroid;
            }
        }

        /// <summary>
        /// Current velocity of the cluster
        /// </summary>
        public Vector3 velocity { get { return nativeObject.velocity; } }

        /// <summary>
        /// Center of the cluster bounds in world space. Equal to the component's transform position.
        /// </summary>
        public Vector3 boxCenter
        {
            get
            {
                return this.transform.position;
            }
        }

        /// <summary>
        /// Center of the cluster bounds in local space. Equal to the component's transform localPosition.
        /// </summary>
        public Vector3 localBoxCenter
        {
            get
            {
                return this.transform.localPosition;
            }
        }

        /// <summary>
        /// The cluster size as XYZ extents from its `boxCenter`
        /// </summary>
        public Vector3 boxSize { get { return nativeObject.boxSize; } }

        /// <summary>
        /// This value will go up and down from 0 to 1 during the lifetime of the cluster. It can be interpreted as a confidence score given by Augmenta on this cluster, i.e.
        /// a cluster with a weight close to 0 is more likely to be noise than a cluster with weight close to 1
        /// </summary>
        public float weight { get { return nativeObject.weight; } }

        private void OnDrawGizmos()
        {
            if (!drawDebug || nativeObject == null)
            {
                return;
            }

            bool aboutToLeave = state == GenericObject<Vector3>.State.Ghost || state == GenericObject<Vector3>.State.Leave;
            Color baseColor = aboutToLeave ? Color.gray / 2 : Color.HSVToRGB(objectID * .1f % 1, 1, 1);

            // Draw point cloud
            // Points coordinates are in augmenta-world-relative coordinates
            Gizmos.matrix = transform.parent.localToWorldMatrix;
            Gizmos.color = baseColor;
            foreach (var p in points)
            {
                Gizmos.DrawSphere(p, .01f);
            }

            // Draw cluster
            if (nativeObject.isCluster)
            {
                Gizmos.color = baseColor + Color.white * .3f;

                Gizmos.matrix = transform.parent.localToWorldMatrix;
                Gizmos.DrawWireSphere(this.localCentroid, .05f);

                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, boxSize);
            }
        }
    }
}