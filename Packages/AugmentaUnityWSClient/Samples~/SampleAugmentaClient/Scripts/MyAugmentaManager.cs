using System.Collections.Generic;
using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Listens to events fired by an Augmenta client, and create new custom GameObjects for each
/// incoming cluster / point cloud.
/// </summary>
public class MyAugmentaManager : MonoBehaviour
{
    public AugmentaClient augmentaClient;
    public GameObject customClusterPrefab;
    public GameObject customPointCloudPrefab;

    private GameObject zonesRoot;
    private GameObject clusterRoot;
    private GameObject pointCloudsRoot;

    private AugmentaScene augmentaScene;

    private List<MyZoneComponent> zones = new();
    private List<MyClusterComponent> clusters = new();
    private List<MyPointCloudComponent> pointClouds = new();

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

        zonesRoot = new GameObject("Zones");
        zonesRoot.transform.parent = transform;

        clusterRoot = new GameObject("Clusters");
        clusterRoot.transform.parent = transform;

        pointCloudsRoot = new GameObject("PointClouds");
        pointCloudsRoot.transform.parent = transform;
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

    private void OnPointCloudEnteredScene(AugmentaScene scene, AugmentaPointCloud pc)
    {
        CreateCustomPointCloud(pc);
    }

    private void OnPointCloudLeftScene(AugmentaScene scene, AugmentaPointCloud pc)
    {
        RemoveCustomPointCloud(pc);
    }

    private void OnContainerUpdated(AugmentaContainer container)
    {
        // TODO
    }

    private void GetAllZonesInSceneRecursive(AugmentaContainer container, ref List<AugmentaZone> outZones)
    {
        if (container is AugmentaZone)
        {
            outZones.Add(container as AugmentaZone);
        }

        foreach (var child in container.GetChildrenContainers())
        {
            GetAllZonesInSceneRecursive(child, ref outZones);
        }
    }

    private void InitializeScene()
    {
        // In this example we assume we're interested in the first scene
        augmentaScene = augmentaClient.GetWorld().GetScene(0);
        augmentaScene.onClusterEntered.AddListener(OnClusterEnteredScene);
        augmentaScene.onClusterLeft.AddListener(OnClusterLeftScene);
        augmentaScene.onPointCloudEntered.AddListener(OnPointCloudEnteredScene);
        augmentaScene.onPointCloudLeft.AddListener(OnPointCloudLeftScene);

        // Create existing objects
        foreach (var augmentaObject in augmentaScene.clusters)
        {
            OnClusterEnteredScene(augmentaScene, augmentaObject);
        }

        // Create zones
        List<AugmentaZone> augmentaZones = new List<AugmentaZone>();
        GetAllZonesInSceneRecursive(augmentaScene, ref augmentaZones);
        foreach (var zone in augmentaZones)
        {
            var zoneGameObject = new GameObject(zone.name);
            var zoneComponent = zoneGameObject.AddComponent<MyZoneComponent>();
            zoneComponent.Initialize(zone);

            zoneGameObject.transform.parent = zonesRoot.transform;
        }
    }

    private void ShutdownScene()
    {
        augmentaScene.onClusterEntered.RemoveListener(OnClusterEnteredScene);
        augmentaScene.onClusterLeft.RemoveListener(OnClusterLeftScene);
        augmentaScene.onPointCloudEntered.RemoveListener(OnPointCloudEnteredScene);
        augmentaScene.onPointCloudLeft.RemoveListener(OnPointCloudLeftScene);
        augmentaScene = null;

        // Clear clusters
        foreach (var cluster in clusters)
        {
            cluster.Shutdown();
            Destroy(cluster.gameObject);
        }

        // Clear point clouds
        foreach (var pc in pointClouds)
        {
            pc.Shutdown();
            Destroy(pc.gameObject);
        }

        // Clear zones
        foreach (var zone in zones)
        {
            zone.Shutdown();
            Destroy(zone.gameObject);
        }
        zones.Clear();
    }

    private void CreateCustomCluster(AugmentaCluster augmentaCluster)
    {
        GameObject newObject = Instantiate(customClusterPrefab, clusterRoot.transform);

        MyClusterComponent clusterComponent = newObject.GetComponent<MyClusterComponent>();
        clusterComponent.Initialize(augmentaCluster);
        clusters.Add(clusterComponent);
    }

    private void RemoveCustomCluster(AugmentaCluster augmentaCluster)
    {
        int idx = clusters.FindIndex((MyClusterComponent comp) => { return comp.augmentaCluster == augmentaCluster; });
        Assert.AreNotEqual(idx, -1);
        MyClusterComponent comp = clusters[idx];
        clusters.RemoveAt(idx);
        Destroy(comp.gameObject);
    }

    private void CreateCustomPointCloud(AugmentaPointCloud augmentaPC)
    {
        GameObject newObject = Instantiate(customPointCloudPrefab, pointCloudsRoot.transform);

        MyPointCloudComponent pointCloudComponent = newObject.GetComponent<MyPointCloudComponent>();
        pointCloudComponent.Initialize(augmentaPC);
        pointClouds.Add(pointCloudComponent);
    }

    private void RemoveCustomPointCloud(AugmentaPointCloud augmentaPC)
    {
        int idx = pointClouds.FindIndex((MyPointCloudComponent comp) => { return comp.augmentaPointCloud == augmentaPC; });
        Assert.AreNotEqual(idx, -1);
        MyPointCloudComponent comp = pointClouds[idx];
        pointClouds.RemoveAt(idx);
        Destroy(comp.gameObject);
    }
}
