using Augmenta;
using AugmentaWebsocketClient;
using UnityEngine;

namespace AugmentaWebsocketClient
{
    public class AugmentaPointCloud : AugmentaObject
    {
        /// <summary>
        /// The point cloud's point array. Points coordinates are relative to the parent augmenta scene's pivot !
        /// </summary>
        /// <todo>
        /// Should points be transformed to be in local or world space ? This could be computationnaly intensive 
        /// </todo>
        public System.ArraySegment<Vector3> points { get { return nativeObject.points; } }

        void OnDrawGizmos()
        {
            if (!drawDebug || nativeObject == null)
            {
                return;
            }

            // Draw point cloud
            // Points coordinates are in augmenta-world-relative coordinates
            Gizmos.matrix = transform.parent.localToWorldMatrix;
            Gizmos.color = Color.HSVToRGB(objectID * .1f % 1, 1, 1); ;
            foreach (var p in points)
            {
                Gizmos.DrawSphere(p, .01f);
            }
        }
    }
}