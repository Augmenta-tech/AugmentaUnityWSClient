using AugmentaWebsocketClient;
using UnityEngine;

/// <summary>
/// Matches this GameObject to the bounding box of the cluster it represents.
/// </summary>
public class ClusterBox : MonoBehaviour
{
    [HideInInspector] public AugmentaCluster augmentaCluster;

    private bool hasObjectChanged = false;

    private void Update()
    {
        if (!hasObjectChanged)
        {
            return;
        }

        transform.position = augmentaCluster.transform.position;
        transform.rotation = augmentaCluster.transform.rotation;
        transform.localScale = augmentaCluster.boxSize;

        hasObjectChanged = false;
    }

    public void Initialize(AugmentaCluster obj)
    {
        augmentaCluster = obj;
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);

        transform.position = augmentaCluster.transform.position;
        transform.rotation = augmentaCluster.transform.rotation;
        transform.localScale = augmentaCluster.boxSize;
    }

    public void Shutdown()
    {
        augmentaCluster.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaCluster = null;
    }

    private void OnObjectUpdate(AugmentaObject obj)
    {
        hasObjectChanged = true;
    }
}
