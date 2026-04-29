using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace AugmentaWebsocketClient
{
    /// <summary>
    /// Represents an item from Augmenta's hierarchy (worlds, scenes, zones...)
    /// </summary>
    public class AugmentaContainer : MonoBehaviour
    {
        protected Augmenta.Container<Vector3> nativeContainer;
        protected AugmentaClient parentClientComponent;
        private List<AugmentaContainer> childrenContainers = new();

        public delegate void OnUpdateEvent(AugmentaContainer augmentaContainer);
        public event OnUpdateEvent onUpdate;

        [Header("Editor")]
        [Tooltip("Show gizmos in Scene Mode")]
        public bool showGizmos = true;

        internal virtual void Setup(Augmenta.Container<Vector3> nativeContainer, AugmentaClient parentClientComponent)
        {
            this.parentClientComponent = parentClientComponent;

            this.nativeContainer = nativeContainer;
            this.nativeContainer.onUpdate += OnNativeContainerUpdate;

            this.gameObject.name = this.nativeContainer.name;

            SetTransformFromNativeContainer();

            foreach (var baseChild in this.nativeContainer.children)
            {
                Augmenta.Container<Vector3> nativeChild = baseChild as Augmenta.Container<Vector3>;
                CreateChildComponent(nativeChild);
            }
        }

        private void OnNativeContainerUpdate(Augmenta.Container<Vector3> nativeContainer)
        {
            Assert.AreEqual(nativeContainer, this.nativeContainer);
            SetTransformFromNativeContainer();

            // Handle added children
            foreach (var baseChild in this.nativeContainer.children)
            {
                Augmenta.Container<Vector3> nativeChild = baseChild as Augmenta.Container<Vector3>;
                var associatedChild = this.GetChildrenHolderComponent().Find(nativeChild.name);
                if (associatedChild == null)
                {
                    CreateChildComponent(nativeChild);
                }
            }

            // Handle removed children
            List<AugmentaContainer> removedChildren = new();
            for (int childIdx = 0; childIdx < this.GetChildrenHolderComponent().childCount; ++childIdx)
            {
                var childTransform = this.GetChildrenHolderComponent().GetChild(childIdx);
                var childContainerComponent = childTransform.GetComponent<AugmentaContainer>();
                Assert.IsNotNull(childContainerComponent);

                bool childWasRemoved = true;
                foreach (var baseChild in this.nativeContainer.children)
                {
                    if (baseChild.name == childContainerComponent.name)
                    {
                        childWasRemoved = false;
                        break;
                    }
                }

                if (childWasRemoved)
                {
                    removedChildren.Add(childContainerComponent);
                }
            }
            foreach (var childToRemove in removedChildren)
            {
                Destroy(childToRemove);
            }

            onUpdate?.Invoke(this);
        }

        private void CreateChildComponent(Augmenta.Container<Vector3> childContainer)
        {
            switch (childContainer.containerType)
            {
                case Augmenta.ContainerType.Zone:
                    AugmentaZone zoneComponent = new GameObject().AddComponent<AugmentaZone>();
                    zoneComponent.Setup(childContainer, parentClientComponent);
                    AddChildZone(zoneComponent);

                    break;

                case Augmenta.ContainerType.Scene:
                    AugmentaScene sceneComponent = new GameObject().AddComponent<AugmentaScene>();
                    sceneComponent.Setup(childContainer, parentClientComponent);
                    AddChildScene(sceneComponent);

                    break;

                case Augmenta.ContainerType.Container:
                default:
                    AugmentaContainer containerComponent = new GameObject().AddComponent<AugmentaContainer>();
                    containerComponent.Setup(childContainer, parentClientComponent);
                    AddChildContainer(containerComponent);

                    break;
            }
        }

        protected virtual void SetTransformFromNativeContainer()
        {
            this.transform.localPosition = nativeContainer.position;
            this.transform.localEulerAngles = nativeContainer.rotation;
        }

        protected virtual void AddChildContainer(AugmentaContainer containerComponent)
        {
            childrenContainers.Add(containerComponent);
            containerComponent.GetPivot().SetParent(this.GetChildrenHolderComponent(), false);
        }

        protected virtual void AddChildZone(AugmentaZone zoneComponent)
        {
            childrenContainers.Add(zoneComponent);
            zoneComponent.GetPivot().SetParent(this.GetChildrenHolderComponent(), false);
        }

        protected virtual void AddChildScene(AugmentaScene sceneComponent)
        {
            childrenContainers.Add(sceneComponent);
            sceneComponent.GetPivot().SetParent(this.GetChildrenHolderComponent(), false);
        }

        protected virtual Transform GetChildrenHolderComponent()
        {
            return this.transform;
        }

        public virtual Transform GetPivot()
        {
            return this.transform;
        }

        public ref List<AugmentaContainer> GetChildrenContainers()
        {
            return ref childrenContainers;
        }

        public virtual void SetShowGizmos(bool show)
        {
            showGizmos = show;
            foreach (var child in childrenContainers)
            {
                child.SetShowGizmos(show);
            }
        }
    }
}