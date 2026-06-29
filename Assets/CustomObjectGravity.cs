using Unity.Netcode;
using UnityEngine;

namespace EasyPeasyFirstPersonController
{
    public class CustomObjectGravity : NetworkBehaviour
    {
        [Tooltip("1.0 = Normal Earth | 0.2 = Moon | 2.5 = Heavy Metal")]
        public float gravityMultiplier = 1.0f; 

        private Rigidbody rb;

        private void Awake() => rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner) return;

            // Applies Unity's global gravity, but scaled up or down for just this object
            Vector3 customGravityForce = Physics.gravity * gravityMultiplier * rb.mass;
            rb.AddForce(customGravityForce, ForceMode.Force);
        }
    }
}