using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

public class MyClusterComponent : MonoBehaviour
{
    [HideInInspector] public AugmentaCluster augmentaCluster;
    private VisualEffect effect;

    private int bufferCapacity = 500;
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

    public void Initialize(AugmentaCluster obj)
    {
        augmentaCluster = obj;
        augmentaCluster.onEnter.AddListener(OnObjectEnter);
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);
        augmentaCluster.onLeave.AddListener(OnObjectLeave);

        transform.position = augmentaCluster.transform.position;
        transform.rotation = augmentaCluster.transform.rotation;
        transform.localScale = augmentaCluster.boxSize;
    }

    private void OnObjectUpdate(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaCluster);

        //// Update Box Info
        transform.position = augmentaCluster.transform.position;
        transform.rotation = augmentaCluster.transform.rotation;
        transform.localScale = augmentaCluster.boxSize;

        //// Update Point Cloud Info
        if (augmentaCluster.points.Count == 0)
        {
            if (pointsBuffer != null)
            {
                pointsBuffer.Release();
            }
            bufferCapacity = 0;
            
            return;
        }

        // Grow buffer if necessary
        if (augmentaCluster.points.Count > this.bufferCapacity)
        {
            this.bufferCapacity = augmentaCluster.points.Count;

            if (pointsBuffer != null)
            {
                pointsBuffer.Release();
            }
            pointsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, this.bufferCapacity, 3 * sizeof(float));
        }

        this.effect.Reinit();

        // Copy updated point count to GPU
        this.effect.SetUInt(pointsCountProperty, (uint)augmentaCluster.points.Count);

        // Copy the updated points buffer to the GPU one
        pointsBuffer.SetData(augmentaCluster.points.Array, augmentaCluster.points.Offset, 0, augmentaCluster.points.Count);
        this.effect.SetGraphicsBuffer(pointsProperty, pointsBuffer);

        // Point Clouds come in scene-pivot-relative coordinates, so we need to transform them in the shader
        this.effect.SetMatrix4x4(sceneToWorldTransformProperty, obj.GetParentScene().GetPivot().transform.localToWorldMatrix);
    }

    private void OnObjectLeave(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaCluster);

        augmentaCluster.onEnter.RemoveListener(OnObjectEnter);
        augmentaCluster.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaCluster.onLeave.RemoveListener(OnObjectLeave);

        Debug.Log("Cluster object leaving. Bye !");

        // In this sample cleaning up leaving objects is done by the MyAugmentaManager component
        //Destroy(gameObject);
    }

    private void OnObjectEnter(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaCluster);
        Debug.Log("Cluster object entered. Hello !");
    }
}
