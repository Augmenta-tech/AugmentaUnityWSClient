using System.Collections.Generic;
using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Places the Augmenta client in the Unity scene and draws always-on-top debug geometry to calibrate it on site.
/// </summary>
public class AugmentaCalibrationManager : MonoBehaviour
{
    public AugmentaClient augmentaClient;

    [Header("Transform")]
    [Tooltip("Position of the Augmenta client in its parent space")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Euler rotation of the Augmenta client in its parent space")]
    public Vector3 rotation = Vector3.zero;

    [Tooltip("Scale of the Augmenta client")]
    public Vector3 scale = Vector3.one;

    [Header("Debug")]
    [Tooltip("Draw every Augmenta scene's floor grid and bounding box")]
    public bool showSceneFloor = true;

    [Tooltip("Draw every cluster's bounding box, centroid and point cloud")]
    public bool showObjects = true;

    public Color floorColor = new(1, 1, 1, .1f);
    public Color gridColor = new(1, 1, 1, .5f);
    public Color boundsColor = Color.white;

    [Header("Materials")]
    public Material overlayMaterial;
    public Material gridMaterial;

    [HideInInspector] public Mesh wireCubeMesh;
    [HideInInspector] public Mesh quadMesh;

    private readonly List<AugmentaScene> scenes = new();
    private readonly List<CalibrationSceneFloor> floors = new();
    private readonly List<CalibrationCluster> clusters = new();

    #region MonoBehaviour

    private void OnEnable()
    {
        Assert.IsNotNull(augmentaClient);

        wireCubeMesh = CalibrationMeshes.WireCube();
        quadMesh = CalibrationMeshes.Quad();

        augmentaClient.onWorldRegistered.AddListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.AddListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.AddListener(OnWorldUnregistered);

        if (augmentaClient.IsWorldRegistered())
        {
            InitializeWorld(augmentaClient.GetWorld());
        }
    }

    private void Update()
    {
        ApplyTransform();

        foreach (var floor in floors)
        {
            if (floor.gameObject.activeSelf != showSceneFloor)
            {
                floor.gameObject.SetActive(showSceneFloor);
            }
        }

        foreach (var cluster in clusters)
        {
            if (cluster.gameObject.activeSelf != showObjects)
            {
                cluster.gameObject.SetActive(showObjects);
            }
        }
    }

    private void OnDisable()
    {
        ShutdownWorld();

        augmentaClient.onWorldRegistered.RemoveListener(OnWorldRegistered);
        augmentaClient.onWorldUpdated.RemoveListener(OnWorldUpdated);
        augmentaClient.onWorldUnregistered.RemoveListener(OnWorldUnregistered);

        Destroy(wireCubeMesh);
        Destroy(quadMesh);
    }

    private void OnValidate()
    {
        if (augmentaClient)
        {
            ApplyTransform();
        }
    }

    #endregion

    private void ApplyTransform()
    {
        augmentaClient.transform.localPosition = offset;
        augmentaClient.transform.localEulerAngles = rotation;
        augmentaClient.transform.localScale = scale;
    }

    private void OnWorldRegistered(AugmentaWorld world)
    {
        InitializeWorld(world);
    }

    private void OnWorldUpdated(AugmentaWorld world)
    {
        ShutdownWorld();
        InitializeWorld(world);
    }

    private void OnWorldUnregistered(AugmentaWorld world)
    {
        ShutdownWorld();
    }

    private void OnClusterEntered(AugmentaScene scene, AugmentaCluster cluster)
    {
        var clusterComponent = new GameObject("Calibration " + cluster.name).AddComponent<CalibrationCluster>();
        clusterComponent.Initialize(cluster, this);
        clusterComponent.gameObject.SetActive(showObjects);
        clusters.Add(clusterComponent);
    }

    private void OnClusterLeft(AugmentaScene scene, AugmentaCluster cluster)
    {
        int idx = clusters.FindIndex(comp => comp.augmentaCluster == cluster);
        Assert.AreNotEqual(idx, -1);

        var clusterComponent = clusters[idx];
        clusterComponent.Shutdown();
        clusters.RemoveAt(idx);
        Destroy(clusterComponent.gameObject);
    }

    private void InitializeWorld(AugmentaWorld world)
    {
        foreach (var scene in world.scenes)
        {
            scene.onClusterEntered.AddListener(OnClusterEntered);
            scene.onClusterLeft.AddListener(OnClusterLeft);
            scenes.Add(scene);

            var floor = new GameObject("Calibration Floor").AddComponent<CalibrationSceneFloor>();
            floor.Initialize(scene, this);
            floor.gameObject.SetActive(showSceneFloor);
            floors.Add(floor);

            foreach (var cluster in scene.clusters)
            {
                OnClusterEntered(scene, cluster);
            }
        }
    }

    private void ShutdownWorld()
    {
        foreach (var cluster in clusters)
        {
            cluster.Shutdown();
            if (cluster)
            {
                Destroy(cluster.gameObject);
            }
        }
        clusters.Clear();

        foreach (var floor in floors)
        {
            floor.Shutdown();
            if (floor)
            {
                Destroy(floor.gameObject);
            }
        }
        floors.Clear();

        foreach (var scene in scenes)
        {
            if (scene)
            {
                scene.onClusterEntered.RemoveListener(OnClusterEntered);
                scene.onClusterLeft.RemoveListener(OnClusterLeft);
            }
        }
        scenes.Clear();
    }
}
