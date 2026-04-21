using Augmenta;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

namespace AugmentaWebsocketClient
{
    public class AugmentaZone : AugmentaContainer
    {
        private Augmenta.Zone<Vector3> nativeZone { get { return this.nativeContainer as Augmenta.Zone<Vector3>; } }

        private GameObject pivot;
        private GameObject childrenContainer;

        public AugmentaShape shape;

        /// <summary>
        /// Number of objects present in the zone
        /// </summary>
        public int presence { get { return nativeZone.presence; } set { nativeZone.presence = value; } }

        /// <summary>
        /// Reserved for future implementation
        /// </summary>
        private float density { get { return nativeZone.density; } }

        /// <summary>
        /// Value of the zone's slider
        /// </summary>
        public float sliderValue { get { return nativeZone.sliderValue; } }

        /// <summary>
        /// Value of the zone's 2D pad
        /// </summary>
        public Vector2 padXY { get { return new Vector2(nativeZone.padX, nativeZone.padY); } }

        /// <summary>
        /// The point cloud present in the zone. Note that this requires a specific setup on the server side
        /// </summary>
        public System.ArraySegment<Vector3> points { get { return nativeZone.points; } }

        public UnityEvent<AugmentaZone, int> onObjectsEntered = new();
        public UnityEvent<AugmentaZone, int> onObjectsExited = new();
        public UnityEvent<AugmentaZone, int> onPresenceUpdated = new();
        public UnityEvent<AugmentaZone, float> onSliderUpdated = new();
        public UnityEvent<AugmentaZone, float, float> onXYPadUpdated = new();
        public UnityEvent<AugmentaZone> onPointCloudUpdated = new();

        private void Awake()
        {
            this.pivot = new GameObject("ZonePivot");
            transform.SetParent(this.pivot.transform, false);

            this.childrenContainer = new GameObject("Children");
            this.childrenContainer.transform.SetParent(this.transform, false);
        }

        internal override void Setup(Augmenta.Container<Vector3> nativeContainer, AugmentaClient client)
        {
            base.Setup(nativeContainer, client);

            switch (this.nativeZone.shape.shapeType)
            {
                case Augmenta.Shape<Vector3>.ShapeType.Box:
                    this.shape = new AugmentaBoxShape();
                    shape.Setup(this.nativeZone.shape);
                    break;

                case Augmenta.Shape<Vector3>.ShapeType.Sphere:
                    this.shape = new AugmentaSphereShape();
                    shape.Setup(this.nativeZone.shape);
                    break;

                case Augmenta.Shape<Vector3>.ShapeType.Cylinder:
                    this.shape = new AugmentaCylinderShape();
                    shape.Setup(this.nativeZone.shape);
                    break;

                default:
                    Debug.Log("Unsupported zone shape: Unknown");
                    break;
            }

            this.nativeZone.onObjectsEntered += OnNativeZoneObjectsEntered;
            this.nativeZone.onObjectsExited += OnNativeZoneObjectsExited;
            this.nativeZone.onPresenceUpdated += OnNativeZonePresenceUpdated;
            this.nativeZone.onSliderUpdated += OnNativeZoneSliderUpdated;
            this.nativeZone.onXYPadUpdated += OnNativeZoneXYPadUpdated;
            this.nativeZone.onPointCloudUpdated += OnNativeZonePointCloudUpdated;
        }

        protected override void SetTransformFromNativeContainer()
        {
            this.pivot.transform.localPosition = this.nativeZone.position;
            this.pivot.transform.localEulerAngles = this.nativeZone.rotation;

            switch (this.nativeZone.shape.shapeType)
            {
                case Augmenta.Shape<Vector3>.ShapeType.Box:
                    Augmenta.BoxShape<Vector3> boxShape = this.nativeZone.shape as Augmenta.BoxShape<Vector3>;

                    this.transform.localPosition = (boxShape.size / 2);
                    this.childrenContainer.transform.localPosition = -(boxShape.size / 2);

                    break;

                case Augmenta.Shape<Vector3>.ShapeType.Sphere:
                    break;

                case Augmenta.Shape<Vector3>.ShapeType.Cylinder:
                    break;

                default:
                    Debug.Log("Unsupported zone shape: Unknown");
                    break;
            }
        }

        protected override Transform GetChildrenHolderComponent()
        {
            return this.childrenContainer.transform;
        }

        public override Transform GetPivot()
        {
            return this.pivot.transform;
        }

        private void OnNativeZoneObjectsEntered(Zone<Vector3> zone, int count)
        {
            Assert.AreEqual(zone, nativeZone);
            onObjectsEntered.Invoke(this, count);
        }

        private void OnNativeZoneObjectsExited(Zone<Vector3> zone, int count)
        {
            Assert.AreEqual(zone, nativeZone);
            onObjectsExited.Invoke(this, count);
        }

        private void OnNativeZonePresenceUpdated(Zone<Vector3> zone, int presence)
        {
            Assert.AreEqual(zone, nativeZone);
            onPresenceUpdated.Invoke(this, presence);
        }

        private void OnNativeZoneSliderUpdated(Zone<Vector3> zone, float sliderValue)
        {
            Assert.AreEqual(zone, nativeZone);
            onSliderUpdated.Invoke(this, sliderValue);
        }

        private void OnNativeZoneXYPadUpdated(Zone<Vector3> zone, float xValue, float yValue)
        {
            Assert.AreEqual(zone, nativeZone);
            onXYPadUpdated.Invoke(this, xValue, yValue);
        }

        private void OnNativeZonePointCloudUpdated(Zone<Vector3> zone)
        {
            Assert.AreEqual(zone, nativeZone);
            onPointCloudUpdated.Invoke(this);
        }

        private void OnDrawGizmos()
        {
            if (!drawDebug || nativeZone == null)
            {
                return;
            }

            Color baseColor = new Color(nativeZone.color.R / 255f, nativeZone.color.G / 255f, nativeZone.color.B / 255f, nativeZone.color.A / 255f);
            Color brighter = new Color(.1f, .1f, .1f);

            Gizmos.color = baseColor;

            //transform for local space
            foreach (var p in points)
            {
                Gizmos.DrawSphere(p, .01f);
            }

            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            switch (nativeZone.shape.shapeType)
            {
                case Augmenta.Shape<Vector3>.ShapeType.Box:
                    Augmenta.BoxShape<Vector3> boxShape = this.nativeZone.shape as Augmenta.BoxShape<Vector3>;

                    // Draw the zone itself
                    Gizmos.matrix = this.transform.localToWorldMatrix;
                    Gizmos.color = baseColor;
                    Gizmos.DrawWireCube(Vector3.zero, boxShape.size);

                    // Draw pad
                    Vector3 padPosition = new Vector3(padXY.x * boxShape.size.x, 0, padXY.y * boxShape.size.z);
                    Gizmos.matrix = this.pivot.transform.localToWorldMatrix;
                    Gizmos.color = baseColor + brighter;
                    Gizmos.DrawLine(new Vector3(padPosition.x, 0, 0), new Vector3(padPosition.x, 0, boxShape.size.z));
                    Gizmos.DrawLine(new Vector3(0, 0, padPosition.z), new Vector3(boxShape.size.x, 0, padPosition.z));

                    // Draw slider
                    Vector3 sliderSize = boxShape.size;
                    switch (nativeZone.sliderAxis)
                    {
                        case 0:
                            sliderSize.x = sliderValue * boxShape.size.x;
                            break;

                        case 1:
                            sliderSize.y = sliderValue * boxShape.size.y;

                            break;

                        case 2:
                            sliderSize.z = sliderValue * boxShape.size.z;
                            break;
                    }

                    Gizmos.matrix = this.pivot.transform.localToWorldMatrix;
                    Gizmos.color = baseColor * new Color(1, 1, 1, .2f);
                    Gizmos.DrawCube(sliderSize / 2, sliderSize);

                    break;

                case Augmenta.Shape<Vector3>.ShapeType.Sphere:
                    Augmenta.SphereShape<Vector3> sphere = nativeZone.shape as Augmenta.SphereShape<Vector3>;
                    Gizmos.DrawWireSphere(Vector3.zero, sphere.radius);
                    break;
                case Augmenta.Shape<Vector3>.ShapeType.Cylinder:
                    // TODO
                    Augmenta.CylinderShape<Vector3> cylinder = nativeZone.shape as Augmenta.CylinderShape<Vector3>;
                    Vector3 halfSize = new Vector3(cylinder.radius, cylinder.height / 2, cylinder.radius);
                    Gizmos.DrawWireCube(halfSize, halfSize * 2);
                    break;
                case Augmenta.Shape<Vector3>.ShapeType.Cone:
                    // TODO:
                    Augmenta.ConeShape<Vector3> cone = nativeZone.shape as Augmenta.ConeShape<Vector3>;
                    Gizmos.DrawWireSphere(Vector3.zero, cone.radius);
                    break;
                case Augmenta.Shape<Vector3>.ShapeType.Mesh:
                    break;
            }
        }
    }
}