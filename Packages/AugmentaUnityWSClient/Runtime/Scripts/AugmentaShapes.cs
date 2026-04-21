using UnityEngine;

namespace AugmentaWebsocketClient
{
    public class AugmentaShape
    {
        protected Augmenta.Shape<Vector3> nativeShape;

        internal void Setup(Augmenta.Shape<Vector3> nativeShape)
        {
            this.nativeShape = nativeShape;
        }
    }

    public class AugmentaBoxShape : AugmentaShape
    {
        private Augmenta.BoxShape<Vector3> nativeBox { get { return nativeShape as Augmenta.BoxShape<Vector3>; } }

        public Vector3 size { get { return nativeBox.size; } }
    }

    public class AugmentaSphereShape : AugmentaShape
    {
        private Augmenta.SphereShape<Vector3> nativeSphere { get { return nativeShape as Augmenta.SphereShape<Vector3>; } }

        public float radius { get { return nativeSphere.radius; } }
    }

    public class AugmentaCylinderShape : AugmentaShape
    {
        private Augmenta.CylinderShape<Vector3> nativeCylinder { get { return nativeShape as Augmenta.CylinderShape<Vector3>; } }
        
        public float radius { get { return nativeCylinder.radius; } }
        public float height { get { return nativeCylinder.height; } }
    }

}