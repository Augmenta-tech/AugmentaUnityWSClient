using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

public class MyClusterComponent : MonoBehaviour
{
    [HideInInspector] public AugmentaCluster augmentaCluster;

    [Tooltip("Feed the cluster point cloud to a VisualEffect on this GameObject. When disabled, no VisualEffect is required.")]
    public bool usePointCloud = true;

    private VisualEffect effect;
    private bool pointCloudEnabled;

    private int bufferCapacity = 500;
    private GraphicsBuffer pointsBuffer;
    private ExposedProperty pointsProperty = "Points";
    private ExposedProperty pointsCountProperty = "PointsCount";
    private ExposedProperty sceneToWorldTransformProperty = "SceneToWorldTransform";

    private bool hasObjectChanged = false;

    void OnEnable()
    {
        if (!usePointCloud)
        {
            pointCloudEnabled = false;
            return;
        }

        effect = GetComponent<VisualEffect>();
        pointCloudEnabled = effect != null;

        if (!pointCloudEnabled)
        {
            Debug.LogWarning($"{nameof(MyClusterComponent)} on {name} has usePointCloud enabled but no VisualEffect component: point cloud rendering is skipped.", this);
            return;
        }

        Assert.IsTrue(effect.HasGraphicsBuffer(pointsProperty));
        Assert.IsTrue(effect.HasUInt(pointsCountProperty));
        Assert.IsTrue(effect.HasMatrix4x4(sceneToWorldTransformProperty));

        pointsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCapacity, 3 * sizeof(float));
        effect.SetGraphicsBuffer(pointsProperty, pointsBuffer);
    }

    void OnDisable()
    {
        if (pointsBuffer != null)
        {
            pointsBuffer.Release();
            pointsBuffer = null;
        }
    }

    private void Update()
    {
        if (!hasObjectChanged)
        {
            return;
        }
        //// Update Box Info
        transform.position = augmentaCluster.transform.position;
        transform.rotation = augmentaCluster.transform.rotation;
        transform.localScale = augmentaCluster.boxSize;

        //// Update Point Cloud Info
        if (!pointCloudEnabled)
        {
            return;
        }

        if (augmentaCluster.points.Count == 0)
        {
            if (pointsBuffer != null)
            {
                pointsBuffer.Release();
                pointsBuffer = null;
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
        this.effect.SetMatrix4x4(sceneToWorldTransformProperty, augmentaCluster.GetParentScene().GetPivot().transform.localToWorldMatrix);

        hasObjectChanged = false;
    }

    public void Initialize(AugmentaCluster obj)
    {
        augmentaCluster = obj;
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);

        transform.position = augmentaCluster.transform.position;
        transform.rotation = augmentaCluster.transform.rotation;
        transform.localScale = augmentaCluster.boxSize;
    }

    public void Shutdown()
    {
        augmentaCluster.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaCluster = null;
    }

    private void OnObjectUpdate(AugmentaObject obj)
    {
        hasObjectChanged = true;
    }
}
