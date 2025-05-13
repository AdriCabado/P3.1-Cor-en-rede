// HelloWorldPlayer.cs
using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
    /// <summary>
    /// Attached to your player prefab (with a NetworkObject & Renderer).
    /// Handles position RPCs and listens for color changes.
    /// </summary>
    public class HelloWorldPlayer : NetworkBehaviour
    {
        // =====================================================
        // 1) Position synchronization via a NetworkVariable
        // =====================================================
        public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();

        public override void OnNetworkSpawn()
        {
            // If this client owns the object, request an initial Move
            if (IsOwner)
            {
                Move();
            }

            // Hook up the color-change callback (see below)
            PlayerColor.OnValueChanged += OnColorChanged;
            // Apply any color already set before we spawned
            OnColorChanged(Color.white, PlayerColor.Value);
        }

        // Called by GUI: either directly on the server or via Request RPC
        public void Move()
        {
            SubmitPositionRequestRpc();
        }

        // Client→Server RPC: server picks a random spot & writes to Position
        [Rpc(SendTo.Server)]
        private void SubmitPositionRequestRpc(RpcParams rpcParams = default)
        {
            Vector3 rnd = GetRandomPositionOnPlane();
            transform.position = rnd;
            Position.Value    = rnd;
        }

        private static Vector3 GetRandomPositionOnPlane()
        {
            return new Vector3(
                Random.Range(-3f, 3f),
                1f,
                Random.Range(-3f, 3f)
            );
        }

        private void Update()
        {
            // All instances (client & server) lerp to the networked Position
            transform.position = Position.Value;
        }


        // =====================================================
        // 2) Color assignment via a server-writable NetworkVariable
        // =====================================================
        public NetworkVariable<Color> PlayerColor =
            new NetworkVariable<Color>(
                writePerm: NetworkVariableWritePermission.Server
            );

        // ServerRpc for clients to ask the server for a new color.
        [ServerRpc(RequireOwnership = false)]
        public void RequestColorChangeServerRpc(ServerRpcParams rpcParams = default)
        {
            // Ask the manager to reassign for this client
            ulong clientId = rpcParams.Receive.SenderClientId;
            var mgr       = FindFirstObjectByType<HelloWorldManager>();
            mgr.AssignNewColor(clientId);
        }

        // Whenever PlayerColor.Value changes, this runs on all machines.
        private void OnColorChanged(Color oldColor, Color newColor)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null) return;

            // Clone material so tinting doesn’t bleed across instances
            renderer.material        = new Material(renderer.sharedMaterial);
            renderer.material.color  = newColor;
        }
    }
}
