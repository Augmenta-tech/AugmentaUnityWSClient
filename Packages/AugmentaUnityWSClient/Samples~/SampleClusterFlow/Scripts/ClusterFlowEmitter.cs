using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

/// <summary>
/// Feeds the point cloud of one Augmenta cluster to a Visual Effect, and fades that effect in and
/// out as the cluster enters and leaves.
/// </summary>
public class ClusterFlowEmitter : MonoBehaviour
{
    [HideInInspector] public AugmentaCluster augmentaCluster;

    [Header("Emission")]
    [Tooltip("Particles emitted per second when the cluster is fully present")]
    public float maxSpawnRate = 6000f;
    [Tooltip("Longest lifetime a particle can have in the graph, in seconds. The emitter outlives its " +
             "cluster by that long, so the last particles die in the air instead of being cut off.")]
    public float maxParticleLifetime = 2.5f;

    [Header("Appearance")]
    [Tooltip("Time taken by the cloud to reach its full density when its cluster enters, in seconds")]
    public float appearDuration = .6f;
    [Tooltip("Time taken by the cloud to fade out when its cluster leaves, in seconds")]
    public float disappearDuration = .9f;

    [Header("Exposed properties")]
    [Tooltip("Graph property receiving the cluster points, in scene pivot space")]
    public ExposedProperty pointsProperty = "Points";
    [Tooltip("Graph property receiving the number of valid points in the buffer")]
    public ExposedProperty pointsCountProperty = "PointsCount";
    [Tooltip("Graph property receiving the matrix taking the points from scene pivot space to world space")]
    public ExposedProperty sceneToWorldTransformProperty = "SceneToWorldTransform";
    [Tooltip("Graph property receiving the current emission rate")]
    public ExposedProperty spawnRateProperty = "SpawnRate";
    [Tooltip("Graph property receiving the cluster color")]
    public ExposedProperty colorProperty = "Color";
    [Tooltip("Graph property receiving the current alpha")]
    public ExposedProperty alphaProperty = "Alpha";

    private VisualEffect effect;

    // Only ever grows: releasing it on an empty frame would leave a dead handle behind
    private int bufferCapacity = 512;
    private GraphicsBuffer pointsBuffer;

    private Color clusterColor = Color.white;

    // 0 = no particle emitted, 1 = fully present. Drives both the spawn rate and the brightness.
    private float presence;
    private float presenceTarget;

    // Time at which presence reached 0, used to keep the emitter alive while its last particles die
    private float zeroPresenceTime;

    /// <summary>
    /// True once the cloud has faded out and its last particles have died, and it can be destroyed.
    /// </summary>
    public bool hasDisappeared => presenceTarget <= 0f && presence <= 0f
                                  && Time.time - zeroPresenceTime > maxParticleLifetime;

    #region MonoBehaviour

    private void OnEnable()
    {
        effect = GetComponent<VisualEffect>();
        Assert.IsNotNull(effect);
        Assert.IsTrue(effect.HasGraphicsBuffer(pointsProperty));
        Assert.IsTrue(effect.HasUInt(pointsCountProperty));
        Assert.IsTrue(effect.HasMatrix4x4(sceneToWorldTransformProperty));
        Assert.IsTrue(effect.HasFloat(spawnRateProperty));
        Assert.IsTrue(effect.HasVector4(colorProperty));
        Assert.IsTrue(effect.HasFloat(alphaProperty));

        pointsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCapacity, 3 * sizeof(float));
        effect.SetGraphicsBuffer(pointsProperty, pointsBuffer);
        effect.SetUInt(pointsCountProperty, 0);
        effect.SetFloat(spawnRateProperty, 0f);
    }

    private void Update()
    {
        // MoveTowards, not SmoothDamp: the fade must reach exactly 0 in a known time, the owner
        // destroys the emitter on that
        float presenceDuration = presenceTarget > presence ? appearDuration : disappearDuration;
        float previousPresence = presence;
        presence = presenceDuration > 0f
            ? Mathf.MoveTowards(presence, presenceTarget, Time.deltaTime / presenceDuration)
            : presenceTarget;

        if (presence <= 0f && previousPresence > 0f)
        {
            zeroPresenceTime = Time.time;
        }

        float eased = Mathf.SmoothStep(0f, 1f, presence);

        // Fading the alpha as well as the rate, so the cloud dims instead of only thinning out
        effect.SetFloat(spawnRateProperty, maxSpawnRate * eased);
        effect.SetFloat(alphaProperty, eased);
    }

    private void OnDisable()
    {
        if (pointsBuffer != null)
        {
            pointsBuffer.Release();
            pointsBuffer = null;
        }
    }

    #endregion

    /// <summary>
    /// Position the emitter takes to follow the given cluster, used to match a cluster coming back
    /// with the emitter it flickered out of.
    /// </summary>
    public static Vector3 GetFollowPosition(AugmentaCluster augmentaCluster)
    {
        return augmentaCluster.transform.position;
    }

    /// <summary>
    /// Binds the emitter to a cluster and starts it from an empty cloud.
    /// </summary>
    public void Initialize(AugmentaCluster augmentaCluster, Color clusterColor)
    {
        this.augmentaCluster = augmentaCluster;
        this.clusterColor = clusterColor;
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);

        presence = 0f;
        presenceTarget = 1f;

        effect.SetFloat(spawnRateProperty, 0f);
        effect.SetVector4(colorProperty, clusterColor);

        // Push a first frame of points, so the particles born on the next frame have targets
        OnObjectUpdate(augmentaCluster);
    }

    /// <summary>
    /// Unbinds the emitter from its cluster. The effect keeps running on the points it last received.
    /// </summary>
    public void Shutdown()
    {
        if (!augmentaCluster)
        {
            return;
        }

        augmentaCluster.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaCluster = null;
    }

    /// <summary>
    /// Starts the disappearance. The emitter unbinds from its cluster but keeps running, frozen on
    /// the last points it received, until its last particles have died.
    /// </summary>
    public void BeginDisappear()
    {
        Shutdown();
        presenceTarget = 0f;

        // A cluster that leaves before it ever appeared never crosses zero in Update, so start
        // its grace period here instead
        if (presence <= 0f)
        {
            zeroPresenceTime = Time.time;
        }
    }

    /// <summary>
    /// Re-binds a disappearing emitter to a new cluster and fades it back in from its current
    /// density, keeping its color and its live particles so a flickering tracking does not restart
    /// the cloud.
    /// </summary>
    public void Revive(AugmentaCluster augmentaCluster)
    {
        this.augmentaCluster = augmentaCluster;
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);

        presenceTarget = 1f;

        OnObjectUpdate(augmentaCluster);
    }

    private void OnObjectUpdate(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaCluster);

        // Only used to match a returning cluster with this emitter: the particles are simulated in
        // world space from the buffer, so this transform does not move them
        transform.position = GetFollowPosition(augmentaCluster);

        var points = augmentaCluster.points;
        if (points.Count == 0)
        {
            // Keep the buffer and its contents, the live particles still need targets to drift to
            effect.SetUInt(pointsCountProperty, 0);
            return;
        }

        if (points.Count > bufferCapacity)
        {
            bufferCapacity = points.Count;
            pointsBuffer.Release();
            pointsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCapacity, 3 * sizeof(float));
            effect.SetGraphicsBuffer(pointsProperty, pointsBuffer);
        }

        // No Reinit here: the system keeps running, the new points are just fresh targets
        pointsBuffer.SetData(points.Array, points.Offset, 0, points.Count);
        effect.SetUInt(pointsCountProperty, (uint)points.Count);

        // Cluster points come in scene pivot space, the graph transforms them
        effect.SetMatrix4x4(sceneToWorldTransformProperty,
            augmentaCluster.GetParentScene().GetPivot().transform.localToWorldMatrix);
    }

    private static Vector3 ToVector3(Color color)
    {
        return new Vector3(color.r, color.g, color.b);
    }
}
