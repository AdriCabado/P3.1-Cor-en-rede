using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
    public class WorldPlayerNoIA : NetworkBehaviour
    {
        private Renderer clientColor;
        public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                MoveAndTakeColors();
            }
        }

        [Rpc(SendTo.Server)]

         public void MoveAndTakeColors()
        {
            SubmitPositionRequestRpc();
            
            
            OnColorChanged(Color.white, clientColor.material.color);
        }

        private void SubmitPositionRequestRpc(RpcParams rpcParams = default)
        {
            var randomPosition = GetRandomPositionOnPlane();
            transform.position = randomPosition;
            Position.Value = randomPosition;
        }

        static Vector3 GetRandomPositionOnPlane()
        {
            return new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
        }

        private void Update()
        {
            transform.position = Position.Value;
        }

        public void SubmitColorChangeRequestRpc(Color oldColor, Color newColor)
        {
            
            

        }
        private void OnColorChanged(Color oldColor, Color newColor)
        {
            clientColor = GetComponent<Renderer>();


            clientColor.material.color = newColor;
        }
    }
}