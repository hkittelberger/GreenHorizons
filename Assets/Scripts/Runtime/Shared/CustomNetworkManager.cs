using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
#if UNITY_SERVER || ENABLE_UCS_SERVER
using Unity.Services.Authentication.Server;
#endif
using Unity.Services.Core;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Unity.Template.Multiplayer.NGO.Runtime
{
    /// <summary>
    /// A custom network manager that implements additional setup logic and rules
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class CustomNetworkManager : MonoBehaviour
    {
        internal static event Action OnConfigurationLoaded;
        const string k_DefaultServerListenAddress = "0.0.0.0";
        public static CustomNetworkManager Singleton { get; private set; }
        public static ConfigurationManager Configuration { get; private set; }
        internal static MultiplayAssignment s_AssignmentForCurrentGame;
        public bool UsingBots => Configuration.GetBool(ConfigurationManager.k_EnableBots);
#if UNITY_EDITOR
        public static bool s_AreTestsRunning = false;
#endif
        internal bool AutoConnectOnStartup
        {
            get
            {
                bool startAutomatically = Configuration.GetBool(ConfigurationManager.k_Autoconnect);
#if UNITY_EDITOR
                startAutomatically |= s_AreTestsRunning;
#endif
                return startAutomatically;
            }
        }

        internal bool IsClient => m_NetworkManager.IsClient;
        internal bool IsServer => m_NetworkManager.IsServer;
        internal bool IsHost => m_NetworkManager.IsHost;

        internal Action ReturnToMetagame;
        internal int ExpectedPlayers { get; private set; } = 2;
        internal byte BotsSpawned { get; private set; } = 0;
        bool m_PreparedGame = true;

        [SerializeField]
        GameApplication m_GameAppPrefab;

        [SerializeField]
        GameApplication m_GameAppPrefabBad;
        GameApplication m_GameApp;
        [SerializeField]
        Player m_BotPrefab;

        internal HashSet<Player> ReadyPlayers { get; private set; }
        NetworkManager m_NetworkManager;

        void Awake()
        {
            if (Singleton == null)
            {
                Singleton = this;
            }
            m_NetworkManager = GetComponent<NetworkManager>();
            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            m_NetworkManager.OnServerStarted += OnServerStarted;
        }

        [RuntimeInitializeOnLoadMethod]
        static void OnApplicationStarted()
        {
            if (!Singleton) //this happens during PlayMode tests
            {
                return;
            }
            Configuration = new ConfigurationManager(Singleton, ConfigurationManager.k_DevConfigFile, OnConfigurationLoadedCallback);
        }

        static void OnConfigurationLoadedCallback(ConfigurationManager configurationManager)
        {
            Configuration = configurationManager;
            OnConfigurationLoaded?.Invoke();
            if (Configuration.GetMultiplayerRole() != MultiplayerRoleFlags.Server)
            {
                //note: this is a good place where to load player-specific configuration (I.E: Audio/video settings)
            }
            /* note: this is the entry point for all autoconnected instances (including standalone servers) 
            note 2: waiting a frame seems to be necessary to avoid race conditions related to serialization and network setup when using bots in Host autoconnect mode*/
            Singleton.StartCoroutine(CoroutinesHelper.WaitAndDo(CoroutinesHelper.WaitAFrame(), () => Singleton.InitializeNetworkLogic(false, false)));
        }

        public void SetConfiguration(ConfigurationManager configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Initializes the application's network-related behaviour according to the circumstances
        /// </summary>
        /// <param name="gameMode">The game mode to initialize</param>
        /// <param name="startedByUser">Was the setup manually started by the user, I.E: when starting a game manually in single player mode?</param>
        /// <param name="startedByMatchmaker">Was the setup automatically started by the matchmaker?</param>
        public void InitializeNetworkLogic(bool startedByUser, bool startedByMatchmaker)
        {
            if (IsClient || IsServer)
            {
                m_NetworkManager.Shutdown();
            }

            ExpectedPlayers = Configuration.GetInt(ConfigurationManager.k_MaxPlayers);
            if (ExpectedPlayers < 1)
            {
                Debug.LogError("Can't start a match with less than 1 player, please set MaxPlayers in the configuration or the Bootstrapper to at least 1.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }

            if (startedByMatchmaker) //then you can only run in client mode
            {
                if (IsClient)
                {
                    Debug.Log("Already connected!");
                    return;
                }
                StartClientWithMatchmakerData();
                return;
            }

            var commandLineArgumentsParser = new CommandLineArgumentsParser();
            ushort listeningPort = commandLineArgumentsParser.ServerPort != -1 ? (ushort)commandLineArgumentsParser.ServerPort
                                                                               : (ushort)Configuration.GetInt(ConfigurationManager.k_Port);
            if (startedByUser) //single player mode!
            {
                StartClientAsSinglePlayer(listeningPort);
                return;
            }

            if (AutoConnectOnStartup)
            {
                AutoConnect(listeningPort);
            }
        }

        void StartClientAsSinglePlayer(ushort listeningPort)
        {
            Debug.Log($"Starting Host (single player mode) on port {listeningPort}, expecting {ExpectedPlayers}");
            if (ExpectedPlayers > 1)
            {
                Configuration.Set(ConfigurationManager.k_EnableBots, true);
            }
            SetNetworkPortAndAddress(listeningPort, k_DefaultServerListenAddress, k_DefaultServerListenAddress);
            m_NetworkManager.StartHost();
        }

        void StartClientWithMatchmakerData()
        {
            Debug.Log($"Attempting to connect to: {s_AssignmentForCurrentGame.Ip}:{s_AssignmentForCurrentGame.Port}");
            SetNetworkPortAndAddress((ushort)s_AssignmentForCurrentGame.Port, s_AssignmentForCurrentGame.Ip, k_DefaultServerListenAddress);
            m_NetworkManager.StartClient();
        }

        void AutoConnect(ushort listeningPort)
        {
            MultiplayerRoleFlags multiplayerRole = Configuration.GetMultiplayerRole();
            switch (multiplayerRole)
            {
                case MultiplayerRoleFlags.Client:
                    if (IsClient)
                    {
                        Debug.Log("Already connected!");
                        return;
                    }
                    SetNetworkPortAndAddress(listeningPort, Configuration.GetString(ConfigurationManager.k_ServerIP), k_DefaultServerListenAddress);
                    m_NetworkManager.StartClient();
                    break;
                case MultiplayerRoleFlags.Server:
                    Debug.Log($"Starting server on port {listeningPort}, expecting {ExpectedPlayers} players");
                    Application.targetFrameRate = 60; //lock framerate on dedicated servers
                    OnServerMarkServerAsReadyToAcceptPlayers(listeningPort);
                    break;
                case MultiplayerRoleFlags.ClientAndServer:
                    Debug.Log($"Starting Host on port {listeningPort}, expecting {ExpectedPlayers} players");
                    SetNetworkPortAndAddress(listeningPort, k_DefaultServerListenAddress, k_DefaultServerListenAddress);
                    m_NetworkManager.StartHost();
                    break;
                default:
                    break;
            }
        }

        void OnServerMarkServerAsReadyToAcceptPlayers(ushort listeningPort)
        {
#if UNITY_SERVER || ENABLE_UCS_SERVER
            Task.Run(() => OnServerMarkServerAsReadyToAcceptPlayersAsync(listeningPort));
            return;
#else
            SetNetworkPortAndAddress(listeningPort, k_DefaultServerListenAddress, k_DefaultServerListenAddress);
            m_NetworkManager.StartServer();
            Debug.Log("[Server] Server is ready to accept players");
#endif
        }

#if UNITY_SERVER || ENABLE_UCS_SERVER
        IMultiplaySessionManager m_SessionManager;
        async Task OnServerMarkServerAsReadyToAcceptPlayersAsync(ushort listeningPort)
        {
            if (UnityServices.Instance.GetMultiplayerService() != null)
            {
                await ServerAuthenticationService.Instance.SignInFromServerAsync();
                var token = ServerAuthenticationService.Instance.AccessToken;

                var callbacks = new MultiplaySessionManagerEventCallbacks();
                callbacks.Allocated += CallbacksOnAllocated;

                var sessionManagerOptions = new MultiplaySessionManagerOptions()
                {
                    SessionOptions = new SessionOptions()
                    {
                        MaxPlayers = 2
                    }.WithDirectNetwork(k_DefaultServerListenAddress, k_DefaultServerListenAddress, listeningPort),

                    MultiplayServerOptions = new MultiplayServerOptions(
                        serverName: "Dummy",
                        gameType: "TemplateGame",
                        buildId: "0",
                        map: "TemplateMap"
                    ),
                    Callbacks = callbacks
                };
                m_SessionManager = await MultiplayerServerService.Instance.StartMultiplaySessionManagerAsync(sessionManagerOptions);

                //continue this after the allocation happened
                async void CallbacksOnAllocated(IMultiplayAllocation obj)
                {
                    var session = m_SessionManager.Session;
                    await m_SessionManager.SetPlayerReadinessAsync(true);
                    Debug.Log("[Multiplay] Server is ready to accept players");
                }
            }
        }
#endif

        void SetNetworkPortAndAddress(ushort port, string address, string serverListenAddress)
        {
            var transport = GetComponent<UnityTransport>();
            if (transport == null) //happens during Play Mode Tests
            {
                return;
            }
            transport.SetConnectionData(address, port, serverListenAddress);
        }

        void OnServerStarted()
        {
            ReadyPlayers = new HashSet<Player>();
            m_PreparedGame = false;
            if (UsingBots)
            {
                OnServerInstantiateBots();
            }
        }

        void OnServerInstantiateBots()
        {
            BotsSpawned = 0;
            bool isDedicatedServer = m_NetworkManager.IsServer && !m_NetworkManager.IsClient;
            int totalPlayersCountToReach = ExpectedPlayers;
            if (isDedicatedServer)
            {
                if (m_NetworkManager.ConnectedClients.Count == 0)
                {
                    totalPlayersCountToReach--; //leave room to at least one human
                }
            }

            while ((m_NetworkManager.ConnectedClients.Count + BotsSpawned) < totalPlayersCountToReach)
            {
                InstantiateBotGamePlayer();
            }
        }

        Player InstantiateBotGamePlayer()
        {
            Player bot = Instantiate(m_BotPrefab, Vector3.zero, Quaternion.identity);
            bot.GetComponent<NetworkObject>().Spawn();
            BotsSpawned++;
            return bot;
        }

        internal void OnServerQuitAfter(float seconds)
        {
            Debug.Log($"[Server] quitting game in {seconds} seconds!");
            StartCoroutine(CoroutinesHelper.WaitAndDo(new WaitForSeconds(seconds), OnServerQuit));
        }

        void OnServerQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        void OnClientDisconnected(ulong ClientId)
        {
            Debug.Log($"Client {ClientId} disconnected");
            if (IsServer)
            {
                ReadyPlayers.RemoveWhere(p => p.NetworkObject == m_NetworkManager.ConnectedClients[ClientId].PlayerObject);
                if (GameApplication.Instance) //the game already started
                {
                    GameApplication.Instance.Broadcast(new PlayerDisconnected(ClientId));
                }
            }
        }

        void OnClientConnected(ulong ClientId)
        {
            if (IsClient)
            {
                Debug.Log($"Local client {ClientId} connected, waiting for other players...");
                if (MetagameApplication.Instance)
                {
                    MetagameApplication.Instance.Broadcast(new MatchLoadingEvent());
                }
            }
            else
            {
                Debug.Log($"Remote client {ClientId} connected");
            }

            if (m_PreparedGame || !IsServer) //game should be prepared only once per server session
            {
                return;
            }
            if ((m_NetworkManager.ConnectedClients.Count + BotsSpawned) == ExpectedPlayers)
            {
                OnServerPrepareGame();
            }
        }

        internal void OnServerPlayerIsReady(Player player)
        {
            ReadyPlayers.Add(player);
            if (ReadyPlayers.Count + BotsSpawned == ExpectedPlayers)
            {
                OnServerGameReadyToStart();
            }
        }

        void OnServerPrepareGame()
        {
            Debug.Log("[Server] Preparing game");
            
            // Only continue if all clients have valid PlayerObjects
            foreach (var client in m_NetworkManager.ConnectedClients.Values)
            {
                if (client.PlayerObject == null)
                {
                    Debug.LogWarning("Delaying game start: PlayerObject not yet spawned.");
                    return;
                }
            }

            m_PreparedGame = true;
            InstantiateGameApplication();

            foreach (var connectionToClient in m_NetworkManager.ConnectedClients.Values)
            {
                var playerObj = connectionToClient.PlayerObject;

                var player = connectionToClient.PlayerObject.GetComponent<Player>();

                if (player != null)
                {
                    player.OnClientPrepareGameClientRpc();
                }
                else
                {
                    Debug.LogError("Player component missing from PlayerObject");
                }
            }
        }


        internal void InstantiateGameApplication()
        {
            m_GameApp = Instantiate(m_GameAppPrefab);
            // m_GameApp = Instantiate(m_GameAppPrefabBad);
        }

        internal void OnServerGameReadyToStart()
        {
            m_GameApp.Broadcast(new StartMatchEvent(true, false));
            foreach (var player in ReadyPlayers)
            {
                player.OnClientStartGameClientRpc();
            }
            ReadyPlayers.Clear();
        }

        /// <summary>
        /// Performs cleanup operation after a game
        /// </summary>
        internal void OnClientDoPostMatchCleanupAndReturnToMetagame()
        {
            if (IsClient)
            {
                m_NetworkManager.Shutdown();
            }
            Destroy(GameApplication.Instance.gameObject);
            ReturnToMetagame?.Invoke();
        }

        internal void OnEnteredMatchmaker()
        {
            s_AssignmentForCurrentGame = null;
        }
    }
}
