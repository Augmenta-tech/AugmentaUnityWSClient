using AugmentaWebsocketClient;
using UnityEngine;

/// <summary>
/// Draws an Augmenta scene's floor grid and bounding box, on top of everything else.
/// </summary>
public class CalibrationSceneFloor : MonoBehaviour
{
    private static readonly int colorId = Shader.PropertyToID("_Color");
    private static readonly int lineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int sizeId = Shader.PropertyToID("_Size");

    private AugmentaScene augmentaScene;
    private Transform floor;
    private Transform bounds;
    private MeshRenderer floorRenderer;
    private MeshRenderer boundsRenderer;
    private MaterialPropertyBlock propertyBlock;

    public void Initialize(AugmentaScene scene, AugmentaCalibrationManager manager)
    {
        propertyBlock = new MaterialPropertyBlock();
        augmentaScene = scene;
        augmentaScene.onUpdate += OnSceneUpdated;

        transform.SetParent(scene.transform, false);

        floor = CreateChild("Floor", manager.quadMesh, manager.gridMaterial, out floorRenderer);
        bounds = CreateChild("Bounds", manager.wireCubeMesh, manager.overlayMaterial, out boundsRenderer);

        ApplyColors(manager);
        ApplySize();
    }

    public void ApplyColors(AugmentaCalibrationManager manager)
    {
        floorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorId, manager.floorColor);
        propertyBlock.SetColor(lineColorId, manager.gridColor);
        propertyBlock.SetVector(sizeId, new Vector2(augmentaScene.size.x, augmentaScene.size.z));
        floorRenderer.SetPropertyBlock(propertyBlock);

        boundsRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorId, manager.boundsColor);
        boundsRenderer.SetPropertyBlock(propertyBlock);
    }

    public void Shutdown()
    {
        augmentaScene.onUpdate -= OnSceneUpdated;
        augmentaScene = null;
    }

    private Transform CreateChild(string childName, Mesh mesh, Material material, out MeshRenderer renderer)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return child.transform;
    }

    private void OnSceneUpdated(AugmentaContainer container)
    {
        floorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(sizeId, new Vector2(augmentaScene.size.x, augmentaScene.size.z));
        floorRenderer.SetPropertyBlock(propertyBlock);
        ApplySize();
    }

    private void ApplySize()
    {
        Vector3 size = augmentaScene.size;
        floor.localPosition = new Vector3(0, -size.y / 2, 0);
        floor.localScale = new Vector3(size.x, 1, size.z);
        bounds.localScale = size;
    }
}
