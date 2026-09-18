using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;
using WebSocketSharp;
using static Augmenta.ProtocolOptions;

namespace AugmentaWebsocketClient
{
    [Serializable]
    public class UserEditableProtocolOptions
    {
        [Tooltip("Send the full scene point cloud")]
        public bool streamScenePointCloud = true;

        [Tooltip("Send individual clusters (tracked objects)")]
        public bool streamClusters = true;

        [Tooltip("Send the points that make up each cluster")]
        public bool streamClusterPoints = true;

        [Tooltip("Send the points contained in each zone")]
        public bool streamZonePoints = false;

        [Tooltip("Tags the server will use to filter what to send")]
        public List<string> tags = new();

        [Tooltip("Downsample the number of points sent by the server. 1 = no downsampling, 2 = send half the points, etc.")]
        public int downSample = 1;
    }

    /// <summary>
    /// This client is used to connect to an Augmenta server's WebSocket output and receive data from it.
    /// </summary>
    public class AugmentaClient : MonoBehaviour
    {
        //[Header("Spawn")]
        //Should be linked in script default but not needed in inspector since they should not be changed
        [HideInInspector] public GameObject scenePrefab;
        [HideInInspector] public GameObject zonePrefab;
        [HideInInspector] public GameObject objectPrefab;

        [Header("Connection")]
        [Tooltip("A human-readable name that will appear on the server to identify this client")]
        public string clientName = "Unity";

        [Delayed, Tooltip("Server IP address")]
        public string ipAddress = "127.0.0.1";
        private string activeIPAddress;

        [Delayed, Tooltip("Server port")]
        public int port = 6060;
        private int activePort;

        [Space]

        [Tooltip("If on, this client will keep attempting to reconnect to the server when the connection is lost")]
        public bool autoReconnect = true;

        [Tooltip("The time to wait before attempting to reconnect after the connection was lost (in seconds)")]
        public float autoReconnectPeriod = 1;

        [Space]
        // TODO: All that should probably be readonly. Remove from the inspector ?
        public bool isConnected = false;
        private bool isConnecting = false;
        public bool receivingData = false;
        bool hasReceivedSincePolling = false;

        [Header("WebSocket options")]
        public UserEditableProtocolOptions options;

        [Header("Events")]
        /// <summary> 
        /// Fired after the connection with the server has been established
        /// </summary>
        public UnityEvent<AugmentaWorld> onWorldRegistered = new();

        /// <summary>
        /// Fired when the connected server's world changes (i.e. hierarchy change)
        /// </summary>
        public UnityEvent<AugmentaWorld> onWorldUpdated = new();

        /// <summary>
        /// Fired when the connection with the server is closed or lost
        /// </summary>
        public UnityEvent<AugmentaWorld> onWorldUnregistered = new();

        [Header("Augmenta World Origin")]
        public string mainSceneName = "Scene";
        public bool centerX = false;
        public bool centerY = false;
        public bool centerZ = false;

        [Header("Editor")]
        [Tooltip("Show received data's gizmos in Scene Mode")]
        public bool showGizmos = true;
        private bool areGizmosShown = true;

        private WebSocket websocketClient;
        private AugmentaUnityClient augmentaClient;

        private float lastUpdateTime;
        private float lastConnectTime;
        private float lastMessageTime;

        private bool isProcessing;
        private List<MessageEventArgs> wsMessages;

        private AugmentaWorld world;

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
            if (activeIPAddress != ipAddress || activePort != port)
            {
                websocketClient.Close();
                websocketClient = null;
            }

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
                var requestedOptions = GetProtocolOptions();
                var clientOptions = augmentaClient.GetOptions();
                if (!requestedOptions.Equals(clientOptions))
                {
                    ShutdownAugmentaClient();
                    InitializeAugmentaClient(requestedOptions);
                }

                if (clientOptions.usePolling && hasReceivedSincePolling)
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

            if (showGizmos != areGizmosShown)
            {
                world.SetShowGizmos(showGizmos);
                areGizmosShown = showGizmos;
            }
        }

        private void InitWebSocketClient()
        {
            string serverURL = "ws://" + ipAddress + ":" + port;
            websocketClient = new WebSocket(serverURL);

            websocketClient.OnOpen += (sender, e) =>
            {
                isConnecting = false;
                isConnected = true;

                Debug.Log("Connection to " + serverURL + " open");

                var options = GetProtocolOptions();
                InitializeAugmentaClient(options);
            };

            websocketClient.OnError += (sender, e) =>
            {
                Debug.Log("Connection error: " + e.Message);

                wsMessages.Clear();
                isConnecting = false;
                isConnected = false;
            };

            websocketClient.OnClose += (sender, e) =>
            {
                wsMessages.Clear();
                isConnecting = false;
                isConnected = false;

                Debug.Log("Connection to" + serverURL + " closed: " + e.Reason);
            };

            websocketClient.OnMessage += (sender, e) =>
            {
                isConnected = true;
                lastMessageTime = lastUpdateTime;

                while (isProcessing) { }

                wsMessages.Add(e);
            };

            activeIPAddress = ipAddress;
            activePort = port;
        }

        private void InitializeAugmentaClient(Augmenta.ProtocolOptions options)
        {
            var registerMessage = augmentaClient.Initialize(clientName, ref options);
            websocketClient.Send(registerMessage);
        }

        void ShutdownAugmentaClient()
        {
            augmentaClient.Shutdown();
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
            worldComponent.SetShowGizmos(showGizmos);
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
            options.version = Augmenta.ProtocolVersion.Latest;
            options.tags = new List<string>(this.options.tags);
            options.downSample = this.options.downSample;
            options.streamClouds = this.options.streamScenePointCloud;
            options.streamClusters = this.options.streamClusters;
            options.streamClusterPoints = this.options.streamClusterPoints;
            options.streamZonePoints = this.options.streamZonePoints;
            options.boxRotationMode = RotationMode.Degrees;
            options.useCompression = true;
            options.usePolling = false;
            options.axisTransform.axis = Augmenta.AxisTransform.AxisMode.YUpLeftHanded;
            options.axisTransform.origin = Augmenta.AxisTransform.OriginMode.BottomLeft;
            options.axisTransform.flipX = false;
            options.axisTransform.flipY = false;
            options.axisTransform.flipZ = false;
            options.axisTransform.coordinateSpace = Augmenta.AxisTransform.CoordinateSpace.Relative;

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