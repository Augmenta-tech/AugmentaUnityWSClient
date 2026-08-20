using System.Collections.Generic;
using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Listens to events fired by an Augmenta client, and create new custom GameObjects for each
/// incoming cluster.
/// </summary>
public class ClusterOrbitsManager : MonoBehaviour
{
    public AugmentaClient augmentaClient;
    public GameObject orbitingClusterPrefab;

    [Tooltip("Color given to each cluster, picked from its id. Stellar colors, from the hottest to the coolest star.")]
    public Color[] clusterColors =
    {
        new(.541f, .714f, 1f),    // Ice blue
        new(.749f, .831f, 1f),    // Blue white
        new(1f, .953f, .839f),    // Warm white
        new(1f, .851f, .541f),    // Gold
        new(1f, .722f, .420f),    // Amber
        new(1f, .569f, .333f),    // Orange
        new(1f, .420f, .353f),    // Ember red
    };

    [Tooltip("Maximum distance, in meters, at which an entering cluster revives a system that is " +
             "disappearing instead of spawning a new one on top of it. Absorbs tracking flicker.")]
    public float reviveDistance = 1f;

    // Walking the palette by a stride coprime with its length keeps consecutive cluster ids on
    // clearly different colors, instead of on the two neighbouring shades of the same one
    private const int paletteStride = 3;

    private GameObject clusterRoot;

    private AugmentaScene augmentaScene;

    private List<ClusterOrbits> clusters = new();

    // Systems whose cluster has left, kept alive while they fade out so a flickering cluster coming
    // back nearby can take one over instead of spawning a second system on top of it
    private List<ClusterOrbits> disappearingClusters = new();

    private void OnEnable()
    {
        Assert.IsNotNull(augmentaClient);

        // Created before hooking to the events, the first clusters are parented to it
        clusterRoot = new GameObject("Clusters");
        clusterRoot.transform.parent = transform;

        // Hook to the events fired by the clients
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
        // Destroy the systems that finished disappearing. Until then they stay available for a
        // revival, so a cluster flickering back does not spawn a second system on the same spot.
        for (int i = disappearingClusters.Count - 1; i >= 0; --i)
        {
            if (!disappearingClusters[i].hasDisappeared)
            {
                continue;
            }

            Destroy(disappearingClusters[i].gameObject);
            disappearingClusters.RemoveAt(i);
        }
    }

    private void OnDisable()
    {
        if (augmentaScene)
        {
            ShutdownScene();
        }

        // Nothing left to run the fade out, so drop everything right away
        foreach (var cluster in disappearingClusters)
        {
            Destroy(cluster.gameObject);
        }
        disappearingClusters.Clear();

        if (clusterRoot)
        {
            Destroy(clusterRoot);
        }

        augmentaClient.onWorldRegistered.RemoveListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.RemoveListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.RemoveListener(OnWorldUnregistered);
    }

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
        CreateCustomCluster(cluster);
    }

    private void OnClusterLeftScene(AugmentaScene augmentaScene, AugmentaCluster cluster)
    {
        RemoveCustomCluster(cluster);
    }

    private void InitializeScene()
    {
        // In this example we assume we're interested in the first scene
        augmentaScene = augmentaClient.GetWorld().GetScene(0);
        augmentaScene.onClusterEntered.AddListener(OnClusterEnteredScene);
        augmentaScene.onClusterLeft.AddListener(OnClusterLeftScene);

        // Create existing objects
        foreach (var augmentaObject in augmentaScene.clusters)
        {
            OnClusterEnteredScene(augmentaScene, augmentaObject);
        }
    }

    private void ShutdownScene()
    {
        augmentaScene.onClusterEntered.RemoveListener(OnClusterEnteredScene);
        augmentaScene.onClusterLeft.RemoveListener(OnClusterLeftScene);
        augmentaScene = null;

        // Fade the clusters out rather than destroying them, so a reconnection does not blink the
        // whole sky out. BeginDisappear unbinds them from their Augmenta cluster, which the client
        // destroys right after this.
        foreach (var cluster in clusters)
        {
            cluster.BeginDisappear();
            disappearingClusters.Add(cluster);
        }
        clusters.Clear();
    }

    private void CreateCustomCluster(AugmentaCluster augmentaCluster)
    {
        ClusterOrbits revived = FindClusterToRevive(augmentaCluster);
        if (revived)
        {
            // Keeps its own color and planets, so a flicker only shows as a dip in size
            revived.Revive(augmentaCluster);
            disappearingClusters.Remove(revived);
            clusters.Add(revived);
            return;
        }

        GameObject newObject = Instantiate(orbitingClusterPrefab, clusterRoot.transform);

        ClusterOrbits clusterComponent = newObject.GetComponent<ClusterOrbits>();
        clusterComponent.Initialize(augmentaCluster, GetClusterColor(augmentaCluster.objectID));
        clusters.Add(clusterComponent);
    }

    /// <summary>
    /// Returns the disappearing system closest to the entering cluster, if one is close enough to
    /// be the same tracking coming back. Ids are not compared: a flickering tracking usually comes
    /// back under a new id.
    /// </summary>
    private ClusterOrbits FindClusterToRevive(AugmentaCluster augmentaCluster)
    {
        Vector3 position = ClusterOrbits.GetFollowPosition(augmentaCluster);

        ClusterOrbits closest = null;
        float closestDistance = reviveDistance;

        foreach (var cluster in disappearingClusters)
        {
            float distance = Vector3.Distance(cluster.transform.position, position);
            if (distance <= closestDistance)
            {
                closest = cluster;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private Color GetClusterColor(int objectID)
    {
        if (clusterColors == null || clusterColors.Length == 0)
        {
            return Color.white;
        }

        int index = objectID * paletteStride % clusterColors.Length;
        return clusterColors[Mathf.Abs(index)];
    }

    private void RemoveCustomCluster(AugmentaCluster augmentaCluster)
    {
        int idx = clusters.FindIndex((ClusterOrbits comp) => { return comp.augmentaCluster == augmentaCluster; });
        Assert.AreNotEqual(idx, -1);
        
        ClusterOrbits comp = clusters[idx];
        clusters.RemoveAt(idx);

        // Kept around until it has fully faded out, and destroyed by Update then
        comp.BeginDisappear();
        disappearingClusters.Add(comp);
    }
}
