using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;

public class MyClusterComponent : MonoBehaviour
{
    [HideInInspector] public AugmentaObject augmentaObject;

    public void Initialize(AugmentaObject obj)
    {
        augmentaObject = obj;
        augmentaObject.onEnter.AddListener(OnObjectEnter);
        augmentaObject.onUpdate.AddListener(OnObjectUpdate);
        augmentaObject.onLeave.AddListener(OnObjectLeave);
    }

    private void OnObjectUpdate(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaObject);

        transform.position = augmentaObject.transform.position;
        transform.rotation = augmentaObject.transform.rotation;
        transform.localScale = augmentaObject.boxSize;
    }

    private void OnObjectLeave(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaObject);
        Debug.Log("Cluster object leaving. Bye !");

        augmentaObject.onEnter.RemoveListener(OnObjectEnter);
        augmentaObject.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaObject.onLeave.RemoveListener(OnObjectLeave);

        Destroy(gameObject);
    }

    private void OnObjectEnter(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaObject);
        Debug.Log("Cluster object entered. Hello !");
    }
}
