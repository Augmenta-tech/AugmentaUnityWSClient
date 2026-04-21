using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

[RequireComponent(typeof(VisualEffect))]
public class MyPointCloudComponent : MonoBehaviour
{
    [HideInInspector]
    public AugmentaPointCloud augmentaPointCloud;
    private VisualEffect effect;

    private int bufferCapacity = 100000;
    private GraphicsBuffer pointsBuffer;
    private ExposedProperty pointsProperty = "Points";
    private ExposedProperty pointsCountProperty = "PointsCount";
    private ExposedProperty sceneToWorldTransformProperty = "SceneToWorldTransform";

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

    public void Initialize(AugmentaPointCloud obj)
    {
        this.augmentaPointCloud = obj;
        augmentaPointCloud.onEnter.AddListener(OnObjectEnter);
        augmentaPointCloud.onUpdate.AddListener(OnObjectUpdate);
        augmentaPointCloud.onLeave.AddListener(OnObjectLeave);
    }

    public void Shutdown()
    {
        augmentaPointCloud.onEnter.RemoveListener(OnObjectEnter);
        augmentaPointCloud.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaPointCloud.onLeave.RemoveListener(OnObjectLeave);
        augmentaPointCloud = null;
    }

    void OnObjectUpdate(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaPointCloud);

        // Grow buffer if necessary
        if (augmentaPointCloud.points.Count > this.bufferCapacity)
        {
            this.bufferCapacity = augmentaPointCloud.points.Count;

            pointsBuffer.Release();
            pointsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, this.bufferCapacity, 3 * sizeof(float));
        }

        this.effect.Reinit();

        // Copy updated point count to GPU
        this.effect.SetUInt(pointsCountProperty, (uint)augmentaPointCloud.points.Count);

        // Copy the updated points buffer to the GPU one
        pointsBuffer.SetData(augmentaPointCloud.points.Array, 0, 0, augmentaPointCloud.points.Count); 
        this.effect.SetGraphicsBuffer(pointsProperty, pointsBuffer);

        // Point Clouds come in scene-pivot-relative coordinates, so we need to transform them in the shader
        this.effect.SetMatrix4x4(sceneToWorldTransformProperty, obj.GetParentScene().GetPivot().transform.localToWorldMatrix);
    }

    private void OnObjectLeave(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaPointCloud);
        Debug.Log("Point Cloud object leaving. Bye !");

        augmentaPointCloud.onEnter.RemoveListener(OnObjectEnter);
        augmentaPointCloud.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaPointCloud.onLeave.RemoveListener(OnObjectLeave);

        // In this sample, object cleanup is handled by the MyAugmentaManager component
        //Destroy(gameObject);
    }

    private void OnObjectEnter(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaPointCloud);
        Debug.Log("Point Cloud object entered. Hello !");
    }
}
