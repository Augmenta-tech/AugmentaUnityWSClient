using System.Collections.Generic;
using AugmentaWebsocketClient;
using UnityEngine;

[RequireComponent(typeof(AugmentaClient))]
public class MyAugmentaManager : MonoBehaviour
{
    public GameObject customObjectPrefab;

    private GameObject zonesRoot;
    private GameObject objectsRoot;

    private AugmentaClient augmentaClient;
    private AugmentaScene augmentaScene;

    private Dictionary<int, GameObject> customObjectInstances = new();
    private List<AugmentaZone> zones = new();

    private void OnEnable()
    {
        augmentaClient = GetComponent<AugmentaClient>();
        augmentaClient.onWorldRegistered.AddListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.AddListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.AddListener(OnWorldUnregistered);

        if (augmentaClient.IsWorldRegistered())
        {
            // In this example we assume we're interested in the first scene
            augmentaScene = augmentaClient.GetWorld().GetScene(0);
            augmentaScene.onObjectEnter.AddListener(OnSceneObjectEntered);
            augmentaScene.onObjectLeave.AddListener(OnSceneObjectLeave);

            // Create existing objects
            foreach (var augmentaObject in augmentaScene.clusters)
            {
                OnSceneObjectEntered(augmentaScene, augmentaObject);
            }
        }

        zonesRoot = new GameObject("Zones");
        zonesRoot.transform.parent = transform;

        objectsRoot = new GameObject("Objects");
        objectsRoot.transform.parent = transform;
    }

    private void OnDisable()
    {
        if (augmentaScene)
        {
            augmentaScene.onObjectEnter.RemoveListener(OnSceneObjectEntered);
            augmentaScene.onObjectLeave.RemoveListener(OnSceneObjectLeave);
            augmentaScene = null;
        }

        augmentaClient.onWorldRegistered.RemoveListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.RemoveListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.RemoveListener(OnWorldUnregistered);
    }

    void OnWorldRegistered(AugmentaWorld world)
    {
        augmentaScene = augmentaClient.GetWorld().GetScene(0);
        augmentaScene.onObjectEnter.AddListener(OnSceneObjectEntered);
        augmentaScene.onObjectLeave.AddListener(OnSceneObjectLeave);

        GetAllZonesInSceneRecursive(augmentaScene, ref zones);
        foreach (var zone in zones)
        {
            var zoneGameObject = new GameObject(zone.name);
            var zoneComponent = zoneGameObject.AddComponent<MyZoneComponent>();
            zoneComponent.Initialize(zone);

            zoneGameObject.transform.parent = zonesRoot.transform;
        }
    }

    void OnWorldUpdated(AugmentaWorld world)
    {
        var newScene = world.GetScene(0);
        if (newScene != augmentaScene)
        {
            augmentaScene.onObjectEnter.RemoveListener(OnSceneObjectEntered);
            augmentaScene.onObjectLeave.RemoveListener(OnSceneObjectLeave);

            // Clean up existing objects
            foreach (var entry in customObjectInstances)
            {
                Destroy(entry.Value);
            }
            customObjectInstances.Clear();

            augmentaScene = newScene;

            augmentaScene.onObjectEnter.AddListener(OnSceneObjectEntered);
            augmentaScene.onObjectLeave.AddListener(OnSceneObjectLeave);

            // Create existing objects
            foreach (var augmentaObject in augmentaScene.clusters)
            {
                OnSceneObjectEntered(augmentaScene, augmentaObject);
            }
        }
    }

    void OnWorldUnregistered(AugmentaWorld world)
    {
        if (augmentaScene)
        {
            augmentaScene.onObjectEnter.RemoveListener(OnSceneObjectEntered);
            augmentaScene.onObjectLeave.RemoveListener(OnSceneObjectLeave);
            augmentaScene = null;
        }

        // Clear objects
        foreach (var entry in customObjectInstances)
        {
            Destroy(entry.Value);
        }
        customObjectInstances.Clear();

        // Clear zones
        foreach (var zone in zones)
        {
            Destroy(zone.gameObject);
        }
        zones.Clear();
    }

    void OnSceneObjectEntered(AugmentaScene augmentaScene, AugmentaObject augmentaObject)
    {
        if (customObjectInstances.ContainsKey(augmentaObject.objectID))
        {
            Debug.LogWarning("Received entered event for object " + augmentaObject.objectID + " which was already here. Replacing it.");
            RemoveCustomObject(augmentaObject);
        }

        CreateCustomObject(augmentaObject);
    }

    void OnSceneObjectLeave(AugmentaScene augmentaScene, AugmentaObject augmentaObject)
    {
        if (customObjectInstances.ContainsKey(augmentaObject.objectID))
        {
            RemoveCustomObject(augmentaObject);
        }
    }

    void OnContainerUpdated(AugmentaContainer container)
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

    private void CreateCustomObject(AugmentaObject augmentaObject)
    {
        GameObject newObject = Instantiate(customObjectPrefab, objectsRoot.transform);

        MyClusterComponent clusterComponent = newObject.GetComponent<MyClusterComponent>();
        clusterComponent.Initialize(augmentaObject);

        MyPointCloudComponent pointCloudComponent = newObject.GetComponent<MyPointCloudComponent>();
        pointCloudComponent.Initialize(augmentaObject);

        customObjectInstances.Add(augmentaObject.objectID, newObject);
    }

    private void RemoveCustomObject(AugmentaObject augmentaObject)
    {
        customObjectInstances.Remove(augmentaObject.objectID);
        Destroy(augmentaObject);
    }
}
