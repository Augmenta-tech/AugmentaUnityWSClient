using UnityEngine;

namespace AugmentaWebsocketClient
{
    class AugmentaShape
    {
        protected Augmenta.Shape<Vector3> nativeShape;

        internal void Setup(Augmenta.Shape<Vector3> nativeShape)
        {
            this.nativeShape = nativeShape;
        }
    }

    class AugmentaBoxShape : AugmentaShape
    {
        private Augmenta.BoxShape<Vector3> nativeBox { get { return nativeShape as Augmenta.BoxShape<Vector3>; } }

        public Vector3 size { get { return nativeBox.size; } }
    }

    class AugmentaSphereShape : AugmentaShape
    {
        private Augmenta.SphereShape<Vector3> nativeSphere { get { return nativeShape as Augmenta.SphereShape<Vector3>; } }

        public float radius { get { return nativeSphere.radius; } }
    }

    class AugmentaCylinderShape : AugmentaShape
    {
        private Augmenta.CylinderShape<Vector3> nativeCylinder { get { return nativeShape as Augmenta.CylinderShape<Vector3>; } }
        
        public float radius { get { return nativeCylinder.radius; } }
        public float height { get { return nativeCylinder.height; } }
    }

}