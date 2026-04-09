using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

[RequireComponent(typeof(VisualEffect))]
public class MyPointCloudComponent : MonoBehaviour
{
    private AugmentaObject augmentaObject;
    private VisualEffect effect;

    private int bufferCapacity = 100000;
    private GraphicsBuffer pointsBuffer;

    [SerializeField] private ExposedProperty pointsProperty = "Points";
    [SerializeField] private ExposedProperty pointsCountProperty = "PointsCount";
    [SerializeField] private ExposedProperty sceneToWorldTransformProperty = "SceneToWorldTransform";

    void OnEnable()
    {
        effect = GetComponent<VisualEffect>();
        Assert.IsTrue(effect.HasGraphicsBuffer(pointsProperty));
        Assert.IsTrue(effect.HasUInt(pointsCountProperty));
        Assert.IsTrue(effect.HasMatrix4x4(sceneToWorldTransformProperty));

        pointsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCapacity, 3 * sizeof(float));
        effect.SetGraphicsBuffer(pointsProperty, pointsBuffer);
    }

    void OnDisable()
    {
        pointsBuffer.Release();
    }

    public void Initialize(AugmentaObject obj)
    {
        this.augmentaObject = obj;
        augmentaObject.onEnter.AddListener(OnObjectEnter);
        augmentaObject.onUpdate.AddListener(OnObjectUpdate);
        augmentaObject.onLeave.AddListener(OnObjectLeave);
    }

    void OnObjectUpdate(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaObject);

        // Grow buffer if necessary
        if (obj.points.Length > this.bufferCapacity)
        {
            this.bufferCapacity = obj.points.Length;

            pointsBuffer.Release();
            pointsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, this.bufferCapacity, 3 * sizeof(float));
        }

        this.effect.Reinit();

        // Copy updated point count to GPU
        this.effect.SetUInt(pointsCountProperty, (uint)obj.points.Length);

        // Copy the updated points buffer to the GPU one
        pointsBuffer.SetData(obj.points);
        this.effect.SetGraphicsBuffer(pointsProperty, pointsBuffer);

        // Point Clouds come in scene-pivot-relative coordinates, so we need to transform them in the shader
        this.effect.SetMatrix4x4(sceneToWorldTransformProperty, obj.GetParentScene().GetPivot().transform.localToWorldMatrix);
    }

    private void OnObjectLeave(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaObject);
        Debug.Log("Point Cloud object leaving. Bye !");

        augmentaObject.onEnter.RemoveListener(OnObjectEnter);
        augmentaObject.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaObject.onLeave.RemoveListener(OnObjectLeave);

        Destroy(gameObject);
    }

    private void OnObjectEnter(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaObject);
        Debug.Log("Point Cloud object entered. Hello !");
    }
}
