using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;
using WebSocketSharp;
using static Augmenta.ProtocolOptions;

namespace AugmentaWebsocketClient
{
    // Duplicate protocol options classes to make them serializable
    // If you know of a better way to do this, please ping us :-)
    [Serializable]
    public class SerializableAxisTransform
    {
        public Augmenta.AxisTransform.AxisMode axis = Augmenta.AxisTransform.AxisMode.YUpLeftHanded;
        public Augmenta.AxisTransform.OriginMode origin = Augmenta.AxisTransform.OriginMode.BottomLeft;
        public bool flipX = false;
        public bool flipY = false;
        public bool flipZ = false;
        public Augmenta.AxisTransform.CoordinateSpace coordinateSpace = Augmenta.AxisTransform.CoordinateSpace.Relative;
    }

    [Serializable]
    public class SerializableProtocolOptions
    {
        [HideInInspector] public Augmenta.ProtocolVersion version = Augmenta.ProtocolVersion.Latest;
        public List<string> tags = new();
        public int downSample = 1;
        public bool streamScenePointCloud = true;
        public bool streamClusters = true;
        public bool streamClusterPoints = true;
        public bool streamZonePoints = false;
        [HideInInspector] public RotationMode boxRotationMode = RotationMode.Degrees;
        [HideInInspector] public SerializableAxisTransform axisTransform;
        [HideInInspector] public bool useCompression = true;
        public bool usePolling = false;
    }

    public class AugmentaClient : MonoBehaviour
    {
        public string ipAddress
        {
            get => _ipAddress;
            set
            {
                _ipAddress = value;
                if (websocketClient != null)
                {
                    websocketClient.Close();
                    websocketClient = null;
                }
            }
        }

        public int port
        {
            get => _port;
            set
            {
                _port = value;
                if (websocketClient != null)
                {
                    websocketClient.Close();
                    websocketClient = null;
                }
            }
        }

        WebSocket websocketClient;
        AugmentaUnityClient augmentaClient;

        float lastUpdateTime;
        float lastConnectTime;
        float lastMessageTime;

        public SerializableProtocolOptions protocolOptions;

        //[Header("Spawn")]
        //Should be linked in script default but not needed in inspector since they should not be changed
        [HideInInspector] public GameObject scenePrefab;
        [HideInInspector] public GameObject zonePrefab;
        [HideInInspector] public GameObject objectPrefab;

        [Header("Connection")]
        public string clientName = "Unity";
        [SerializeField] string _ipAddress = "127.0.0.1";
        [SerializeField] int _port = 6060;

        [Space]
        public bool autoReconnect = true;
        public float autoReconnectPeriod = 1;

        [Space]
        public bool isConnected = false;
        private bool isConnecting = false;
        public bool receivingData = false;
        bool hasReceivedSincePolling = false;

        bool isProcessing;
        List<MessageEventArgs> wsMessages;

        private AugmentaWorld world;

        [Header("Events")]
        public UnityEvent<AugmentaWorld> onWorldRegistered = new();
        public UnityEvent<AugmentaWorld> onWorldUpdated = new();
        public UnityEvent<AugmentaWorld> onWorldUnregistered = new();

        [Header("Augmenta World Origin")]
        public string mainSceneName = "Scene";
        public bool centerX = false;
        public bool centerY = false;
        public bool centerZ = false;

        public AugmentaWorld GetWorld() { return world; }

        public bool IsWorldRegistered() { return world != null; }

        private void OnEnable()
        {
            wsMessages = new List<MessageEventArgs>();

            augmentaClient = new();
            augmentaClient.onSetupCompleted += OnSetupCompleted;
            InitWebSocketClient();
            ConnectAsync();
        }

        void OnDisable()
        {
            augmentaClient.onSetupCompleted -= OnSetupCompleted;

            websocketClient.Close();
            augmentaClient.Clear();
            augmentaClient = null;
            wsMessages.Clear();
        }

        void Update()
        {
            if (!isConnected && world != null)
            {
                onWorldUnregistered.Invoke(world);
                Destroy(world.gameObject);
                world = null;
            }

            if (websocketClient == null)
            {
                InitWebSocketClient();
            }

            if (autoReconnect && (!isConnected || !websocketClient.IsAlive) && Time.time - lastConnectTime > autoReconnectPeriod)
            {
                if (!isConnecting)
                {
                    ConnectAsync();
                }
            }

            lastUpdateTime = Time.time;

            if (isConnected)
            {
                var options = GetProtocolOptions();
                if (!options.Equals(augmentaClient.options))
                {
                    augmentaClient.options = options;
                    SendRegisterMessage();
                }

                if (augmentaClient.options.usePolling && hasReceivedSincePolling)
                {
                    SendPollMessage(); //we can do it here since update will be shared with the engine runtime, so it will be called once per frame
                }
            }

            isProcessing = true;
            foreach (var e in wsMessages)
            {
                ProcessMessage(e);
            }

            wsMessages.Clear();
            isProcessing = false;

            receivingData = (Time.time - lastMessageTime) < 1;

            if (IsWorldRegistered())
                UpdateSceneCenter();
        }

        private void InitWebSocketClient()
        {
            websocketClient = new WebSocket("ws://" + ipAddress + ":" + port);
            websocketClient.OnOpen += (sender, e) =>
            {
                isConnecting = false;
                isConnected = true;
                Debug.Log("Connection " + "ws://" + ipAddress + ":" + port + " opened !");
                augmentaClient.options = GetProtocolOptions();
                SendRegisterMessage();
            };

            websocketClient.OnError += (sender, e) =>
            {
                Debug.Log("Error! " + e.Message);
            
                isConnecting = false;
                isConnected = false;
            };

            websocketClient.OnClose += (sender, e) =>
            {
                isConnecting = false;
                isConnected = false;
                
                Debug.Log("Connection " + "ws://" + ipAddress + ":" + port + " closed. Reason: " + e.Reason);
            };

            websocketClient.OnMessage += (sender, e) =>
            {
                isConnected = true;
                lastMessageTime = lastUpdateTime;

                while (isProcessing) { }

                wsMessages.Add(e);
            };
        }

        private void SendRegisterMessage()
        {
            var message = augmentaClient.GetRegisterMessage(clientName);
            websocketClient.Send(message);
        }

        private void SendPollMessage()
        {
            var message = augmentaClient.GetPollMessage();
            websocketClient.Send(message);
        }

        private void ProcessMessage(MessageEventArgs e)
        {
            if (e.IsText)
            {
                augmentaClient.ProcessMessage(e.Data);
            }
            else if (e.IsBinary)
            {
                augmentaClient.ProcessData(e.RawData);
                hasReceivedSincePolling = true;
            }
        }

        private void ConnectAsync()
        {
            Debug.Log("Connecting websocket...");

            lastConnectTime = Time.time;

            isConnecting = true;
            websocketClient.ConnectAsync();
        }

        private void OnSetupCompleted(Augmenta.Container<Vector3> worldContainer)
        {
            AugmentaWorld worldComponent = new GameObject().AddComponent<AugmentaWorld>();
            worldComponent.Setup(worldContainer, this);
            worldComponent.transform.SetParent(this.transform, false);
            worldContainer.onUpdate += OnWorldContainerUpdated;

            world = worldComponent;
            Debug.Log("World Registered");

            UpdateSceneCenter();

            onWorldRegistered.Invoke(world);
        }

        private void OnWorldContainerUpdated(Augmenta.Container<Vector3> worldContainer)
        {
            // Forward update event to the client
            Debug.Log("World updated");
            onWorldUpdated.Invoke(world);
        }

        private void UpdateSceneCenter()
        {
            if (!mainSceneName.IsNullOrEmpty())
            {
                var mainScene = world.GetSceneByName(mainSceneName);
                if (mainScene == null)
                {
                    Debug.Log("Main scene not found");
                }
                else
                {
                    // Offset the client's origin so that the selected scene is centered
                    Vector3 newLocalPos = Vector3.zero;
                    if (centerX)
                    {
                        newLocalPos.x -= mainScene.size.x * .5f;
                    }
                    if (centerY)
                    {
                        newLocalPos.y -= mainScene.size.y * .5f;
                    }
                    if (centerZ)
                    {
                        newLocalPos.z -= mainScene.size.z * .5f;
                    }
                    this.world.transform.localPosition = newLocalPos;
                }
            }
        }

        private Augmenta.ProtocolOptions GetProtocolOptions()
        {
            Augmenta.ProtocolOptions options = new();
            options.version = protocolOptions.version;
            options.tags = new List<string>(protocolOptions.tags);
            options.downSample = protocolOptions.downSample;
            options.streamClouds = protocolOptions.streamScenePointCloud;
            options.streamClusters = protocolOptions.streamClusters;
            options.streamClusterPoints = protocolOptions.streamClusterPoints;
            options.streamZonePoints = protocolOptions.streamZonePoints;
            options.boxRotationMode = protocolOptions.boxRotationMode;
            options.useCompression = protocolOptions.useCompression;
            options.usePolling = protocolOptions.usePolling;
            options.axisTransform.axis = protocolOptions.axisTransform.axis;
            options.axisTransform.origin = protocolOptions.axisTransform.origin;
            options.axisTransform.flipX = protocolOptions.axisTransform.flipX;
            options.axisTransform.flipY = protocolOptions.axisTransform.flipY;
            options.axisTransform.flipZ = protocolOptions.axisTransform.flipZ;
            options.axisTransform.coordinateSpace = protocolOptions.axisTransform.coordinateSpace;
            return options;
        }
    }

    /// <summary>
    /// Overrides Augmenta SDK's clients to provide unity-specific behavior
    /// </summary>
    internal class AugmentaUnityClient : Augmenta.Client<Vector3>
    {
        public AugmentaUnityClient() : base("Unity", Application.unityVersion, AugmentaUnityPlugin.version)
        {
        }
    }
}