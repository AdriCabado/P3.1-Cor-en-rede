// HelloWorldManager.cs
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
    /// <summary>
    /// Attach to the same GameObject as your NetworkManager.
    /// Handles lobby GUI, connection approval, and per-player color assignment.
    /// </summary>
    public class HelloWorldManager : MonoBehaviour
    {
        private NetworkManager m_NetworkManager;

        // The “master” list of 6 distinct colors we hand out.
        private static readonly List<Color> MasterColors = new List<Color>
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };

        // Working pool we draw from and reclaim into.
        private readonly List<Color> _colorPool = new List<Color>();

        // Tracks which client ID currently holds which color.
        private readonly Dictionary<ulong, Color> _assignedColors = new Dictionary<ulong, Color>();

        private void Awake()
        {
            m_NetworkManager = GetComponent<NetworkManager>();

            // Populate our working pool from the master list.
            _colorPool.AddRange(MasterColors);

            // Turn on Connection Approval before wiring callbacks.
            m_NetworkManager.NetworkConfig.ConnectionApproval = true;
            m_NetworkManager.ConnectionApprovalCallback    += ApproveOrReject;
            m_NetworkManager.OnClientConnectedCallback    += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback   += OnClientDisconnected;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));

            // If we're neither client nor server yet, show Host/Client/Server buttons.
            if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
            {
                if (GUILayout.Button("Host"))   m_NetworkManager.StartHost();
                if (GUILayout.Button("Client")) m_NetworkManager.StartClient();
                if (GUILayout.Button("Server")) m_NetworkManager.StartServer();
            }
            else
            {
                // Otherwise show current transport & mode.
                var mode = m_NetworkManager.IsHost   ? "Host"
                         : m_NetworkManager.IsServer ? "Server"
                                                     : "Client";
                GUILayout.Label("Transport: " +
                    m_NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
                GUILayout.Label("Mode: " + mode);

                // “Move” button: server moves everyone; client requests their own.
                if (GUILayout.Button(
                    m_NetworkManager.IsServer && !m_NetworkManager.IsClient
                        ? "Move"
                        : "Request Position Change"))
                {
                    if (m_NetworkManager.IsServer && !m_NetworkManager.IsClient)
                    {
                        // Server: iterate all connected clients
                        foreach (ulong uid in m_NetworkManager.ConnectedClientsIds)
                        {
                            var netObj = m_NetworkManager.SpawnManager
                                .GetPlayerNetworkObject(uid);
                            netObj.GetComponent<HelloWorldPlayer>().Move();
                        }
                    }
                    else
                    {
                        // Client: just request on the local player
                        var local = m_NetworkManager.SpawnManager
                            .GetLocalPlayerObject()
                            .GetComponent<HelloWorldPlayer>();
                        local.Move();
                    }
                }

                // “Change Color” button: only shown/runnable on any client (including host-as-client).
                if (m_NetworkManager.IsClient)
                {
                    if (GUILayout.Button("Change Color"))
                    {
                        var local = m_NetworkManager.SpawnManager
                            .GetLocalPlayerObject()
                            .GetComponent<HelloWorldPlayer>();
                        local.RequestColorChangeServerRpc();
                    }
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Connection approval callback: reject if >=6 players, otherwise accept.
        /// </summary>
        private void ApproveOrReject(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            if (_assignedColors.Count >= MasterColors.Count)
            {
                res.Approved = false;
                res.Reason   = "Lobby is full (6 players max).";
                return;
            }

            res.Approved          = true;
            res.CreatePlayerObject = true;
            res.PlayerPrefabHash   = null;            // use default spawn prefab
            res.Position           = Vector3.zero;    // spawn position
            res.Rotation           = Quaternion.identity;
        }

        /// <summary>
        /// Called on the server when a new client is approved & connected.
        /// Assigns that client a free color.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            AssignNewColor(clientId);
        }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// Reclaims their color back into the pool.
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            if (_assignedColors.TryGetValue(clientId, out var color))
            {
                _assignedColors.Remove(clientId);
                _colorPool.Add(color);
            }
        }

        /// <summary>
        /// Server-side helper: reclaims any old color,
        /// picks the next free one, stores the assignment,
        /// and directly writes into the player’s NetworkVariable.
        /// </summary>
        public void AssignNewColor(ulong clientId)
        {
            // 1) If they had an old color, give it back.
            if (_assignedColors.TryGetValue(clientId, out var old))
            {
                _colorPool.Add(old);
            }

            // 2) Safeguard.
            if (_colorPool.Count == 0)
            {
                Debug.LogWarning("No free colors to assign!");
                return;
            }

            // 3) Pop a new color from the pool.
            Color newColor = _colorPool[0];
            _colorPool.RemoveAt(0);

            // 4) Record the assignment.
            _assignedColors[clientId] = newColor;

            // 5) Fetch that player’s object and write directly to its NetworkVariable.
            var player = m_NetworkManager.SpawnManager
                .GetPlayerNetworkObject(clientId)
                .GetComponent<HelloWorldPlayer>();
            player.PlayerColor.Value = newColor;
        }
    }
}
