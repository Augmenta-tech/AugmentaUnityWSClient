using System.Collections.Generic;
using System.Runtime.InteropServices;
using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

/// <summary>
/// Uploads every tracked cluster of an Augmenta scene into a single GraphicsBuffer, and hands it to
/// one Visual Effect covering the whole space. The graph reads that buffer from the Custom HLSL
/// functions of AugmentaClusterField.hlsl, so every cluster deforms the same particle field instead
/// of each getting its own system.
///
/// Cluster data is uploaded in world space, so the effect has to run in world space too.
/// </summary>
[RequireComponent(typeof(VisualEffect))]
public class ClusterFieldManager : MonoBehaviour
{
    public AugmentaClient augmentaClient;

    [Header("Appearance")]
    [Tooltip("Time taken by an entering cluster to reach its full influence, in seconds")]
    public float appearDuration = .5f;
    [Tooltip("Time taken by a leaving cluster to lose its influence, in seconds. The cluster keeps " +
             "acting on the field, frozen on its last position, until then.")]
    public float disappearDuration = .8f;

    [Header("Exposed properties")]
    [Tooltip("Graph property receiving the cluster buffer")]
    public ExposedProperty clustersProperty = "Clusters";
    [Tooltip("Graph property receiving the number of valid clusters in the buffer")]
    public ExposedProperty clusterCountProperty = "ClusterCount";

    /// <summary>
    /// One cluster as uploaded to the graph.
    /// The padding fields only keep the struct on float4 boundaries.
    /// </summary>
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    [StructLayout(LayoutKind.Sequential)]
    public struct AugmentaClusterData
    {
        public Vector3 position;
        public float influence;
        public Vector3 velocity;
        public float padding0;
        public Vector3 size;
        public float padding1;
    }

    /// <summary>
    /// One cluster as the field sees it. Values are cached every frame so a cluster that left keeps
    /// acting on the field while its influence fades: the client destroys the AugmentaCluster right
    /// after announcing that it left, so its reference cannot be read anymore by then.
    /// </summary>
    private class ClusterEntry
    {
        public AugmentaCluster cluster;
        public AugmentaClusterData data;
        public float influenceTarget;
    }

    private VisualEffect effect;

    private AugmentaScene augmentaScene;

    private List<ClusterEntry> entries = new();

    // Only ever grows: releasing it on an empty frame would leave a dead handle behind
    private int bufferCapacity = 32;
    private GraphicsBuffer clustersBuffer;
    private AugmentaClusterData[] clusterData;

    #region MonoBehaviour

    private void OnEnable()
    {
        Assert.IsNotNull(augmentaClient);

        effect = GetComponent<VisualEffect>();
        Assert.IsTrue(effect.HasGraphicsBuffer(clustersProperty));
        Assert.IsTrue(effect.HasUInt(clusterCountProperty));

        AllocateBuffer(bufferCapacity);
        effect.SetUInt(clusterCountProperty, 0);

        augmentaClient.onWorldRegistered.AddListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.AddListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.AddListener(OnWorldUnregistered);

        if (augmentaClient.IsWorldRegistered())
        {
            // If the client was already connected, initialize now,
            // because we won't receive the "Registered" event later
            OnWorldRegistered(augmentaClient.GetWorld());
        }
    }

    private void Update()
    {
        UpdateEntries();
        UploadEntries();
    }

    private void OnDisable()
    {
        if (augmentaScene)
        {
            ShutdownScene();
        }
        entries.Clear();

        augmentaClient.onWorldRegistered.RemoveListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.RemoveListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.RemoveListener(OnWorldUnregistered);

        if (clustersBuffer != null)
        {
            clustersBuffer.Release();
            clustersBuffer = null;
        }
    }

    #endregion

    private void OnWorldRegistered(AugmentaWorld world)
    {
        InitializeScene();
    }

    private void OnWorldUpdated(AugmentaWorld world)
    {
        var newScene = world.GetScene(0);
        if (newScene != augmentaScene)
        {
            ShutdownScene();
            InitializeScene();
        }
    }

    private void OnWorldUnregistered(AugmentaWorld world)
    {
        ShutdownScene();
    }

    private void OnClusterEnteredScene(AugmentaScene augmentaScene, AugmentaCluster cluster)
    {
        entries.Add(new ClusterEntry
        {
            cluster = cluster,
            data = ReadCluster(cluster, 0f),
            influenceTarget = 1f,
        });
    }

    private void OnClusterLeftScene(AugmentaScene augmentaScene, AugmentaCluster cluster)
    {
        ClusterEntry entry = entries.Find(candidate => candidate.cluster == cluster);
        Assert.IsNotNull(entry);

        // The cluster GameObject is destroyed right after this, so the entry keeps running on the
        // values it last read while its influence fades out
        entry.cluster = null;
        entry.influenceTarget = 0f;
    }

    private void InitializeScene()
    {
        // In this example we assume we're interested in the first scene
        augmentaScene = augmentaClient.GetWorld().GetScene(0);
        augmentaScene.onClusterEntered.AddListener(OnClusterEnteredScene);
        augmentaScene.onClusterLeft.AddListener(OnClusterLeftScene);

        // Create existing objects
        foreach (var cluster in augmentaScene.clusters)
        {
            OnClusterEnteredScene(augmentaScene, cluster);
        }
    }

    private void ShutdownScene()
    {
        augmentaScene.onClusterEntered.RemoveListener(OnClusterEnteredScene);
        augmentaScene.onClusterLeft.RemoveListener(OnClusterLeftScene);
        augmentaScene = null;

        // Fade the clusters out rather than dropping them, so a reconnection does not snap the
        // whole field back to rest
        foreach (var entry in entries)
        {
            entry.cluster = null;
            entry.influenceTarget = 0f;
        }
    }

    /// <summary>
    /// Refreshes the cached data of every live cluster, ramps the influences, and drops the entries
    /// that finished fading out.
    /// </summary>
    private void UpdateEntries()
    {
        for (int i = entries.Count - 1; i >= 0; --i)
        {
            ClusterEntry entry = entries[i];
            float influence = entry.data.influence;

            if (entry.cluster)
            {
                entry.data = ReadCluster(entry.cluster, influence);
            }

            float duration = entry.influenceTarget > influence ? appearDuration : disappearDuration;
            entry.data.influence = duration > 0f
                ? Mathf.MoveTowards(influence, entry.influenceTarget, Time.deltaTime / duration)
                : entry.influenceTarget;

            if (!entry.cluster && entry.data.influence <= 0f)
            {
                entries.RemoveAt(i);
            }
        }
    }

    private void UploadEntries()
    {
        if (entries.Count > bufferCapacity)
        {
            clustersBuffer.Release();
            AllocateBuffer(entries.Count);
        }

        for (int i = 0; i < entries.Count; ++i)
        {
            clusterData[i] = entries[i].data;
        }

        if (entries.Count > 0)
        {
            clustersBuffer.SetData(clusterData, 0, 0, entries.Count);
        }

        effect.SetUInt(clusterCountProperty, (uint)entries.Count);
    }

    private void AllocateBuffer(int capacity)
    {
        bufferCapacity = capacity;
        clusterData = new AugmentaClusterData[bufferCapacity];
        clustersBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCapacity,
                                            Marshal.SizeOf<AugmentaClusterData>());
        effect.SetGraphicsBuffer(clustersProperty, clustersBuffer);
    }

    private static AugmentaClusterData ReadCluster(AugmentaCluster cluster, float influence)
    {
        return new AugmentaClusterData
        {
            // boxCenter is already in world space, but velocity comes straight from the server, in
            // the same space as the cluster points
            position = cluster.boxCenter,
            influence = influence,
            velocity = cluster.transform.parent.localToWorldMatrix.MultiplyVector(cluster.velocity),
            size = cluster.boxSize,
        };
    }
}
