using Augmenta;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

namespace AugmentaWebsocketClient
{
    public abstract class AugmentaObject : MonoBehaviour
    {
        protected GenericObject<Vector3> nativeObject = null;
        private AugmentaContainer parentWorld = null;
        private AugmentaScene parentScene = null;

        /// <summary>
        /// Fired right after the object has entered the scene
        /// </summary>
        public UnityEvent<AugmentaObject> onEnter = new();

        /// <summary>
        /// Fired every time the client receives an update for this object
        /// </summary>
        public UnityEvent<AugmentaObject> onUpdate = new();

        /// <summary>
        /// Fired right before the object leaves the scene
        /// </summary>
        public UnityEvent<AugmentaObject> onLeave = new();

        public int objectID { get { return nativeObject.objectID; } }

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
            onUpdate.Invoke(this);
        }

        private void OnNativeObjectEntered(Augmenta.GenericObject<Vector3> nativeObject)
        {
            Assert.AreEqual(nativeObject, this.nativeObject);
            onEnter.Invoke(this);
        }

        private void OnNativeObjectLeft(Augmenta.GenericObject<Vector3> nativeObject)
        {
            Assert.AreEqual(nativeObject, this.nativeObject);
            onLeave.Invoke(this);
        }

        public AugmentaScene GetParentScene()
        {
            return this.parentScene;
        }
    }
}