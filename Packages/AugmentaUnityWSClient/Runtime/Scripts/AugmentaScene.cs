using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

namespace AugmentaWebsocketClient
{
    public class AugmentaScene : AugmentaContainer
    {
        private Augmenta.Scene<Vector3> nativeScene { get { return this.nativeContainer as Augmenta.Scene<Vector3>; } }

        private GameObject objectsContainer;
        private GameObject childrenContainer;
        private GameObject pivot;

        public List<AugmentaCluster> clusters = new();
        public List<AugmentaPointCloud> pointClouds = new();

        public UnityEvent<AugmentaScene, AugmentaCluster> onClusterEntered = new();
        public UnityEvent<AugmentaScene, AugmentaCluster> onClusterLeft = new();

        public UnityEvent<AugmentaScene, AugmentaPointCloud> onPointCloudEntered = new();
        public UnityEvent<AugmentaScene, AugmentaPointCloud> onPointCloudLeft = new();

        public Vector3 size { get => nativeScene.size; }

        private void Awake()
        {
            this.pivot = new GameObject("ScenePivot");
            transform.SetParent(this.pivot.transform, false);

            this.objectsContainer = new GameObject("Objects");
            this.objectsContainer.transform.SetParent(this.transform, false);

            this.childrenContainer = new GameObject("Children");
            this.childrenContainer.transform.SetParent(this.transform, false);
        }

        internal override void Setup(Augmenta.Container<Vector3> nativeContainer, AugmentaClient client)
        {
            base.Setup(nativeContainer, client);
            this.nativeScene.onObjectEntered += OnObjectEntered;
            this.nativeScene.onObjectExited += OnObjectExited;
        }

        protected override void SetTransformFromNativeContainer()
        {
            this.pivot.transform.localPosition = this.nativeScene.position;
            this.pivot.transform.localEulerAngles = this.nativeContainer.rotation;

            this.transform.localPosition = this.nativeScene.size / 2;

            this.childrenContainer.transform.localPosition = -(this.nativeScene.size / 2);
            this.objectsContainer.transform.localPosition = -(this.nativeScene.size / 2);
        }

        protected override Transform GetChildrenHolderComponent()
        {
            return this.childrenContainer.transform;
        }

        public override Transform GetPivot()
        {
            return this.pivot.transform;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.lossyScale);
            Gizmos.color = Color.white * .5f;
            Gizmos.DrawWireCube(Vector3.zero, this.nativeScene.size);
        }

        private void OnObjectEntered(Augmenta.GenericObject<Vector3> enteredObject)
        {
            if (enteredObject.isCluster)
            {
                AugmentaCluster clusterComponent = new GameObject().AddComponent<AugmentaCluster>();
                clusterComponent.transform.SetParent(this.objectsContainer.transform, false);
                clusterComponent.Initialize(enteredObject, this.parentClientComponent.GetWorld(), this);
                this.clusters.Add(clusterComponent);

                onClusterEntered.Invoke(this, clusterComponent);
            }
            else
            {
                AugmentaPointCloud pcComponent = new GameObject().AddComponent<AugmentaPointCloud>();
                pcComponent.transform.SetParent(this.objectsContainer.transform, false);
                pcComponent.Initialize(enteredObject, this.parentClientComponent.GetWorld(), this);
                this.pointClouds.Add(pcComponent);
            
                onPointCloudEntered.Invoke(this, pcComponent);
            }
        }

        private void OnObjectExited(Augmenta.GenericObject<Vector3> exitedObject)
        {
            if (exitedObject.isCluster)
            {
                AugmentaCluster clusterComponent = this.clusters.Find(cluster => cluster.objectID == exitedObject.objectID);
                Assert.IsNotNull(clusterComponent);
                this.clusters.Remove(clusterComponent);
                
                this.onClusterLeft.Invoke(this, clusterComponent);
                Destroy(clusterComponent.gameObject);

            }
            else
            {
                AugmentaPointCloud pcComponent = this.pointClouds.Find(pc => pc.objectID == exitedObject.objectID);
                Assert.IsNotNull(pcComponent);
                this.pointClouds.Remove(pcComponent);

                this.onPointCloudLeft.Invoke(this, pcComponent);
                Destroy(pcComponent.gameObject);
            }
        }
    }
}