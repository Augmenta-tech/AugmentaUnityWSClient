using Augmenta;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

namespace AugmentaWebsocketClient
{
    public class AugmentaObject : MonoBehaviour
    {
        private GenericObject<Vector3> nativeObject = null;
        private AugmentaContainer parentWorld = null;
        private AugmentaScene parentScene = null;

        public UnityEvent<AugmentaObject> onEnter = new();
        public UnityEvent<AugmentaObject> onUpdate = new();
        public UnityEvent<AugmentaObject> onLeave = new();

        public int objectID { get { return nativeObject.objectID; } }

        /// <summary>
        /// The point cloud's point array. Points coordinates are relative to the parent augmenta scene's pivot !
        /// </summary>
        /// <todo>
        /// Should points be transformed to be in local or world space ? This could be computationnaly intensive 
        /// </todo>
        public Vector3[] points { get { return nativeObject.points.ToArray(); } }

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
        /// Average position of a cluster's point cloud's points in local space
        /// </summary>
        public Vector3 localCentroid
        {
            get
            {
                return this.nativeObject.centroid;
            }
        }

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

        public Vector3 boxSize { get { return nativeObject.boxSize; } }
        public float weight { get { return nativeObject.weight; } }

        [Header("Debug")]
        public bool drawDebug = true;

        private void ApplyNativeObjectTransform()
        {
            if (this.nativeObject.isCluster)
            {
                this.transform.localPosition = this.nativeObject.boxCenter;
            }

            // TODO: Use quaternions; but I think we'll need a sdk update for that
            this.transform.localEulerAngles = this.nativeObject.rotation;
        }

        internal void Initialize(GenericObject<Vector3> augmentaObject, AugmentaContainer parentWorld, AugmentaScene parentScene)
        {
            Assert.IsNull(nativeObject);

            this.parentWorld = parentWorld;
            this.parentScene = parentScene;

            this.nativeObject = augmentaObject;
            this.nativeObject.onUpdate += OnNativeObjectUpdated;
            this.nativeObject.onEnter += OnNativeObjectEntered;
            this.nativeObject.onLeave += OnNativeObjectLeft;

            this.name = nativeObject.isCluster ? "Cluster" : "Point Cloud";
            this.name += " " + this.objectID;

            ApplyNativeObjectTransform();
        }

        private void OnNativeObjectUpdated(Augmenta.GenericObject<Vector3> nativeObject)
        {
            Assert.AreEqual(nativeObject, this.nativeObject);
            ApplyNativeObjectTransform();
            onUpdate?.Invoke(this);
        }

        private void OnNativeObjectEntered(Augmenta.GenericObject<Vector3> nativeObject)
        {
            Assert.AreEqual(nativeObject, this.nativeObject);
            onEnter?.Invoke(this);
        }

        private void OnNativeObjectLeft(Augmenta.GenericObject<Vector3> nativeObject)
        {
            Assert.AreEqual(nativeObject, this.nativeObject);
            onLeave?.Invoke(this);
        }

        public AugmentaScene GetParentScene()
        {
            return this.parentScene;
        }

        void OnDrawGizmos()
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