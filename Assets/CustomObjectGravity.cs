using Unity.Netcode;
using UnityEngine;

namespace EasyPeasyFirstPersonController
{
    public class CustomObjectGravity : NetworkBehaviour
    {
        public float gravityMultiplier = 1.0f; 

        private Rigidbody rb;

        private void Awake() => rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            // 2. THE MULTIPLAYER GATEKEEPER
            if (!IsSpawned || !IsOwner) return;

            // 3. THE MASS FORMULA (Force = Mass * Acceleration)
            Vector3 customGravityForce = Physics.gravity * gravityMultiplier * rb.mass;

            rb.AddForce(customGravityForce, ForceMode.Force);
        }
    }
}