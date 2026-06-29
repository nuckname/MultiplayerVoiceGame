using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyPeasyFirstPersonController
{
    public class NetworkPhysicsGrabber : NetworkBehaviour
    {
        [Header("Setup")]
        public Camera playerCamera;
        public float maxGrabDistance = 15f;
        [SerializeField] private GameObject holdTarget;

        [Header("Joint Settings")]
        public float springForce = 150f;
        public float damper = 15f;

        [Header("Stability")]
        public float holdDrag = 10f;
        public float holdAngularDrag = 10f;

        private Rigidbody heldObject;
        private SpringJoint grabJoint;
        private float currentHoldDistance;

        private float originalDrag;
        private float originalAngularDrag;

        private void Awake()
        {
            // Fail-safe 1: Auto-fetch the camera from the sibling FirstPersonController
            if (playerCamera == null)
            {
                FirstPersonController fpc = GetComponent<FirstPersonController>();
                if (fpc != null && fpc.cam != null) playerCamera = fpc.cam;
            }

            // Fail-safe 2: Auto-generate the holdTarget anchor if left blank in Inspector
            if (holdTarget == null)
            {
                holdTarget = new GameObject("PhysicsGrabber_HoldTarget");
                holdTarget.transform.SetParent(this.transform);
            }

            // Joints demand a Rigidbody on their host; set it Kinematic so gravity doesn't pull your target down
            Rigidbody targetRb = holdTarget.GetComponent<Rigidbody>();
            if (targetRb == null) targetRb = holdTarget.AddComponent<Rigidbody>();
            targetRb.isKinematic = true;
            targetRb.useGravity = false;
        }

        void Update()
        {
            if (!IsOwner) return;

            // Grab reference to the active mouse
            var mouse = Mouse.current;
            if (mouse == null) return; 

            // New Input System syntax for Left Click
            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryGrab();
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                Release();
            }

            if (heldObject != null)
            {
                Vector3 targetPosition = playerCamera.transform.position + playerCamera.transform.forward * currentHoldDistance;
                holdTarget.transform.position = targetPosition;
            }
        }

        void TryGrab()
        {
            RaycastHit hit;

            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, maxGrabDistance))
            {
                Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
                NetworkObject netObj = hit.collider.GetComponent<NetworkObject>();

                if (rb != null && netObj != null)
                {
                    RequestOwnershipServerRpc(netObj.NetworkObjectId);

                    heldObject = rb;
                    currentHoldDistance = hit.distance;

                    //important to set the position before parenting, otherwise the object will snap to the holdTarget's position
                    holdTarget.transform.position = hit.point;

                    // adds sluggish so it doesn't freak out
                    originalDrag = rb.linearDamping;
                    originalAngularDrag = rb.angularDamping;

                    rb.linearDamping = holdDrag;
                    rb.angularDamping = holdAngularDrag;

                    //Spring Joint Getter
                    grabJoint = holdTarget.GetComponent<SpringJoint>();
                    if (grabJoint == null)
                    {
                        grabJoint = holdTarget.AddComponent<SpringJoint>();
                        Debug.LogWarning("Component doesnt have spring joint");
                    }

                    grabJoint.connectedBody = heldObject;

                    // off set
                    grabJoint.autoConfigureConnectedAnchor = false;
                    grabJoint.anchor = Vector3.zero; 
                    grabJoint.connectedAnchor = heldObject.transform.InverseTransformPoint(hit.point);

                    // applies forces based on weight
                    grabJoint.spring = springForce * heldObject.mass;
                    grabJoint.damper = damper * heldObject.mass;
                    grabJoint.maxDistance = 0f;
                    grabJoint.minDistance = 0f;

                    Debug.Log("grabbed: " + heldObject.name);
                }
                else if (rb != null && netObj == null)
                {
                    Debug.LogError("Grabbed object has a Rigidbody but no NetworkObject! Cannot sync across network.");
                }
            }
        }

        void Release()
        {
            if (heldObject != null)
            {
                heldObject.linearDamping = originalDrag;
                heldObject.angularDamping = originalAngularDrag;

                if (grabJoint != null)
                {
                    Destroy(grabJoint);
                }

                NetworkObject netObj = heldObject.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    RemoveOwnershipServerRpc(netObj.NetworkObjectId);
                }

                heldObject = null;
            }
        }

        [ServerRpc]
        private void RequestOwnershipServerRpc(ulong targetNetworkObjectId, ServerRpcParams rpcParams = default)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject netObj))
            {
                netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
            }
        }

        [ServerRpc]
        private void RemoveOwnershipServerRpc(ulong targetNetworkObjectId)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject netObj))
            {
                netObj.RemoveOwnership();
            }
        }
    }
}