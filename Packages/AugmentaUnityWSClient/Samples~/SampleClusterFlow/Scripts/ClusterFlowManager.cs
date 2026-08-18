using System.Collections.Generic;
using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Listens to events fired by an Augmenta client, and spawns one particle cloud per incoming
/// cluster, fed by the points of that cluster.
///
/// The clouds are owned here rather than by the clusters themselves: the client destroys a cluster
/// right after announcing that it left, and a cloud has to outlive its cluster to fade out.
/// </summary>
public class ClusterFlowManager : MonoBehaviour
{
    public AugmentaClient augmentaClient;
    public GameObject clusterFlowPrefab;

    [Tooltip("Color given to each cluster, picked from its id. Additive, so values above 1 glow.")]
    public Color[] clusterColors =
    {
        new(.35f, 1.6f, 1.9f),    // Cyan
        new(1.6f, .55f, 1.5f),    // Magenta
        new(1.9f, 1.2f, .35f),    // Amber
        new(.45f, 1.1f, 1.9f),    // Azure
        new(1.4f, 1.8f, .55f),    // Lime
        new(1.9f, .6f, .5f),      // Coral
        new(1.1f, .7f, 1.9f),     // Violet
    };

    [Tooltip("Maximum distance, in meters, at which an entering cluster revives a cloud that is " +
             "disappearing instead of spawning a new one on top of it. Absorbs tracking flicker.")]
    public float reviveDistance = 1f;

    // Walking the palette by a stride coprime with its length keeps consecutive cluster ids on
    // clearly different colors, instead of on the two neighbouring shades of the same one
    private const int paletteStride = 3;

    private GameObject emitterRoot;

    private AugmentaScene augmentaScene;

    private List<ClusterFlowEmitter> emitters = new();

    // Clouds whose cluster has left, kept alive while they fade out so a flickering cluster coming
    // back nearby can take one over instead of spawning a second cloud on top of it
    private List<ClusterFlowEmitter> disappearingEmitters = new();

    #region MonoBehaviour

    private void OnEnable()
    {
        Assert.IsNotNull(augmentaClient);
        Assert.IsNotNull(clusterFlowPrefab);

        // Created before hooking to the events, the first clusters are parented to it
        emitterRoot = new GameObject("Clusters");
        emitterRoot.transform.parent = transform;

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
        // Destroy the clouds that finished disappearing. Until then they stay available for a
        // revival, so a cluster flickering back does not spawn a second cloud on the same spot.
        for (int i = disappearingEmitters.Count - 1; i >= 0; --i)
        {
            if (!disappearingEmitters[i].hasDisappeared)
            {
                continue;
            }

            Destroy(disappearingEmitters[i].gameObject);
            disappearingEmitters.RemoveAt(i);
        }
    }

    private void OnDisable()
    {
        if (augmentaScene)
        {
            ShutdownScene();
        }

        // Nothing left to run the fade out, so drop everything right away
        foreach (var emitter in disappearingEmitters)
        {
            Destroy(emitter.gameObject);
        }
        disappearingEmitters.Clear();

        if (emitterRoot)
        {
            Destroy(emitterRoot);
        }

        augmentaClient.onWorldRegistered.RemoveListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.RemoveListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.RemoveListener(OnWorldUnregistered);
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
        CreateEmitter(cluster);
    }

    private void OnClusterLeftScene(AugmentaScene augmentaScene, AugmentaCluster cluster)
    {
        RemoveEmitter(cluster);
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

        // Fade the clouds out rather than destroying them, so a reconnection does not blink
        // everything out. BeginDisappear unbinds them from their Augmenta cluster, which the client
        // destroys right after this.
        foreach (var emitter in emitters)
        {
            emitter.BeginDisappear();
            disappearingEmitters.Add(emitter);
        }
        emitters.Clear();
    }

    private void CreateEmitter(AugmentaCluster augmentaCluster)
    {
        ClusterFlowEmitter revived = FindEmitterToRevive(augmentaCluster);
        if (revived)
        {
            // Keeps its own color and its live particles, so a flicker only shows as a dip in density
            revived.Revive(augmentaCluster);
            disappearingEmitters.Remove(revived);
            emitters.Add(revived);
            return;
        }

        GameObject newObject = Instantiate(clusterFlowPrefab, emitterRoot.transform);

        ClusterFlowEmitter emitter = newObject.GetComponent<ClusterFlowEmitter>();
        emitter.Initialize(augmentaCluster, GetClusterColor(augmentaCluster.objectID));
        emitters.Add(emitter);
    }

    private void RemoveEmitter(AugmentaCluster augmentaCluster)
    {
        int idx = emitters.FindIndex((ClusterFlowEmitter comp) => { return comp.augmentaCluster == augmentaCluster; });
        Assert.AreNotEqual(idx, -1);

        ClusterFlowEmitter comp = emitters[idx];
        emitters.RemoveAt(idx);

        // Kept around until it has fully faded out, and destroyed by Update then
        comp.BeginDisappear();
        disappearingEmitters.Add(comp);
    }

    /// <summary>
    /// Returns the disappearing cloud closest to the entering cluster, if one is close enough to
    /// be the same tracking coming back. Ids are not compared: a flickering tracking usually comes
    /// back under a new id.
    /// </summary>
    private ClusterFlowEmitter FindEmitterToRevive(AugmentaCluster augmentaCluster)
    {
        Vector3 position = ClusterFlowEmitter.GetFollowPosition(augmentaCluster);

        ClusterFlowEmitter closest = null;
        float closestDistance = reviveDistance;

        foreach (var emitter in disappearingEmitters)
        {
            float distance = Vector3.Distance(emitter.transform.position, position);
            if (distance <= closestDistance)
            {
                closest = emitter;
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
}
