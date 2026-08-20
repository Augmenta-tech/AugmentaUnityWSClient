using AugmentaWebsocketClient;
using UnityEngine;

public class ClusterBoxZone : MonoBehaviour
{
    private AugmentaZone augmentaZone;

    public int presence;
    public float sliderValue;
    public Vector2 xyPadValue;

    public void Initialize(AugmentaZone zone)
    {
        augmentaZone = zone;
        augmentaZone.onObjectsEntered.AddListener(OnZoneObjectsEntered);
        augmentaZone.onObjectsExited.AddListener(OnZoneObjectsExited);
        augmentaZone.onSliderUpdated.AddListener(OnZoneSliderUpdated);
        augmentaZone.onXYPadUpdated.AddListener(OnZoneXYPadUpdated);
        augmentaZone.onPresenceUpdated.AddListener(OnZonePresenceUpdated);
        augmentaZone.onPointCloudUpdated.AddListener(OnZonePointCloudUpdated);
    }

    public void Shutdown()
    {
        augmentaZone.onObjectsEntered.RemoveListener(OnZoneObjectsEntered);
        augmentaZone.onObjectsExited.RemoveListener(OnZoneObjectsExited);
        augmentaZone.onSliderUpdated.RemoveListener(OnZoneSliderUpdated);
        augmentaZone.onXYPadUpdated.RemoveListener(OnZoneXYPadUpdated);
        augmentaZone.onPresenceUpdated.RemoveListener(OnZonePresenceUpdated);
        augmentaZone.onPointCloudUpdated.RemoveListener(OnZonePointCloudUpdated);
        augmentaZone = null;
    }

    private void OnZoneObjectsEntered(AugmentaZone zone, int count)
    {
        Debug.Log("Objects entered zone: " + count);
    }

    private void OnZoneObjectsExited(AugmentaZone zone, int count)
    {
        Debug.Log("Objects exited zone: " + count);
    }

    private void OnZonePresenceUpdated(AugmentaZone zone, int presence)
    {
        this.presence = presence;
    }

    private void OnZoneSliderUpdated(AugmentaZone zone, float value)
    {
        this.sliderValue = value;
    }

    private void OnZoneXYPadUpdated(AugmentaZone zone, float xValue, float yValue)
    {
        this.xyPadValue.x = xValue;
        this.xyPadValue.y = yValue;
    }

    private void OnZonePointCloudUpdated(AugmentaZone zone)
    {
        // TODO
    }
}
