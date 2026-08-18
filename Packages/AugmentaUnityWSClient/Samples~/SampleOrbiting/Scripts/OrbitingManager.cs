using System.Collections.Generic;
using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Listens to events fired by an Augmenta client, and create new custom GameObjects for each
/// incoming cluster.
/// </summary>
public class OrbitingManager : MonoBehaviour
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

    // Walking the palette by a stride coprime with its length keeps consecutive cluster ids on
    // clearly different colors, instead of on the two neighbouring shades of the same one
    private const int paletteStride = 3;

    private GameObject clusterRoot;

    private AugmentaScene augmentaScene;

    private List<OrbitingCluster> clusters = new();

    private void OnEnable()
    {
        Assert.IsNotNull(augmentaClient);

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

        clusterRoot = new GameObject("Clusters");
        clusterRoot.transform.parent = transform;
    }

    private void OnDisable()
    {
        if (augmentaScene)
        {
            ShutdownScene();
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

        // Clear clusters
        foreach (var cluster in clusters)
        {
            cluster.Shutdown();
            Destroy(cluster.gameObject);
        }
        clusters.Clear();
    }

    private void CreateCustomCluster(AugmentaCluster augmentaCluster)
    {
        GameObject newObject = Instantiate(orbitingClusterPrefab, clusterRoot.transform);

        OrbitingCluster clusterComponent = newObject.GetComponent<OrbitingCluster>();
        clusterComponent.Initialize(augmentaCluster, GetClusterColor(augmentaCluster.objectID));
        clusters.Add(clusterComponent);
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
        int idx = clusters.FindIndex((OrbitingCluster comp) => { return comp.augmentaCluster == augmentaCluster; });
        Assert.AreNotEqual(idx, -1);
        
        OrbitingCluster comp = clusters[idx];
        comp.Shutdown();
        clusters.RemoveAt(idx);
        
        Destroy(comp.gameObject);
    }
}
