using Augmenta;
using AugmentaWebsocketClient;
using UnityEngine;

/// <summary>
/// Draws a cluster's bounding box, centroid and point cloud, on top of everything else.
/// </summary>
public class CalibrationCluster : MonoBehaviour
{
    private static readonly int colorId = Shader.PropertyToID("_Color");
    private const float centroidSize = .1f;

    [HideInInspector] public AugmentaCluster augmentaCluster;

    private Transform box;
    private Transform centroid;
    private Mesh pointsMesh;
    private MeshRenderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Color currentColor;

    public void Initialize(AugmentaCluster cluster, AugmentaCalibrationManager manager)
    {
        propertyBlock = new MaterialPropertyBlock();
        augmentaCluster = cluster;
        augmentaCluster.onUpdate.AddListener(OnClusterUpdated);

        // Points are relative to the scene pivot, so the root shares the cluster's parent frame
        transform.SetParent(cluster.transform.parent, false);

        pointsMesh = new Mesh { name = "CalibrationPoints" };
        box = CreateChild("Box", manager.wireCubeMesh, manager.overlayMaterial);
        centroid = CreateChild("Centroid", manager.wireCubeMesh, manager.overlayMaterial);
        CreateChild("Points", pointsMesh, manager.overlayMaterial);
        renderers = GetComponentsInChildren<MeshRenderer>();

        centroid.localScale = Vector3.one * centroidSize;
        Refresh();
    }

    public void Shutdown()
    {
        augmentaCluster.onUpdate.RemoveListener(OnClusterUpdated);
        augmentaCluster = null;
        Destroy(pointsMesh);
    }

    private Transform CreateChild(string childName, Mesh mesh, Material material)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return child.transform;
    }

    private void OnClusterUpdated(AugmentaObject obj)
    {
        Refresh();
    }

    private void Refresh()
    {
        box.localPosition = augmentaCluster.transform.localPosition;
        box.localRotation = augmentaCluster.transform.localRotation;
        box.localScale = augmentaCluster.boxSize;
        centroid.localPosition = augmentaCluster.localCentroid;

        CalibrationMeshes.UpdatePoints(pointsMesh, augmentaCluster.points);

        bool aboutToLeave = augmentaCluster.state == GenericObject<Vector3>.State.Ghost
                            || augmentaCluster.state == GenericObject<Vector3>.State.Leave;
        Color color = aboutToLeave ? Color.gray : Color.HSVToRGB(augmentaCluster.objectID * .1f % 1, 1, 1);
        if (color == currentColor)
        {
            return;
        }

        currentColor = color;
        propertyBlock.SetColor(colorId, color);
        foreach (var renderer in renderers)
        {
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
