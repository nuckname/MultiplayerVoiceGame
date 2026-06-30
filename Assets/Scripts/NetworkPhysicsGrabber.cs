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

        [Header("Push & Pull Settings")]
        public float scrollSensitivity = 1.5f;
        public float minHoldDistance = 1.5f;

        [Header("Joint Settings")]
        public float springForce = 150f;
        public float damper = 15f;

        [Header("Stability")]
        [Tooltip("Lowered from 10. Too much drag makes objects feel like they are floating in water.")]
        public float holdDrag = 3f; 
        public float holdAngularDrag = 2f;

        [Header("Leverage Settings")]
        [Tooltip("Higher = objects feel drastically heavier when grabbed off-center")]
        public float leverageSensitivity = 3.0f;

        private Rigidbody heldObject;
        private SpringJoint grabJoint;
        private float currentHoldDistance;

        private float originalDrag;
        private float originalAngularDrag;
        private bool originalUseGravity;

        private void Awake()
        {
            if (playerCamera == null)
            {
                FirstPersonController fpc = GetComponent<FirstPersonController>();
                if (fpc != null && fpc.cam != null) playerCamera = fpc.cam;
            }

            if (holdTarget == null)
            {
                holdTarget = new GameObject("PhysicsGrabber_HoldTarget");
                holdTarget.transform.SetParent(this.transform);
            }

            Rigidbody targetRb = holdTarget.GetComponent<Rigidbody>();
            if (targetRb == null) targetRb = holdTarget.AddComponent<Rigidbody>();
            targetRb.isKinematic = true;
            targetRb.useGravity = false;
        }

        void Update()
        {
            if (!IsOwner) return;

            var mouse = Mouse.current;
            if (mouse == null) return; 

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
                float scrollDelta = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    float scrollStep = (scrollDelta / 120f) * scrollSensitivity;
                    currentHoldDistance = Mathf.Clamp(currentHoldDistance + scrollStep, minHoldDistance, maxGrabDistance);
                }

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

                    holdTarget.transform.position = hit.point;

                    // Cache original values
                    originalDrag = rb.linearDamping;
                    originalAngularDrag = rb.angularDamping;
                    originalUseGravity = rb.useGravity;

                    // Apply stability drag, but leave gravity ON so it naturally drops based on mass
                    rb.linearDamping = holdDrag;
                    rb.angularDamping = holdAngularDrag;
                    rb.useGravity = originalUseGravity; 

                    grabJoint = holdTarget.GetComponent<SpringJoint>();
                    if (grabJoint == null)
                    {
                        grabJoint = holdTarget.AddComponent<SpringJoint>();
                    }

                    grabJoint.connectedBody = heldObject;

                    grabJoint.autoConfigureConnectedAnchor = false;
                    grabJoint.anchor = Vector3.zero; 
                    grabJoint.connectedAnchor = heldObject.transform.InverseTransformPoint(hit.point);

                    float distToCenterOfMass = Vector3.Distance(hit.point, heldObject.worldCenterOfMass);
                    float rawLeverage = 1f / (1f + (distToCenterOfMass * leverageSensitivity));
                    float leverageFactor = Mathf.Clamp(rawLeverage, 0.25f, 1.0f);

                    // CORE FIX: Use the square root of the mass.
                    // If you multiply linearly (springForce * mass), mass cancels out against gravity 
                    // and every object sags the exact same amount. Squaring the root means heavier objects 
                    // get a stronger spring so they don't break the joint, but they will still sag noticeably 
                    // lower and swing heavier than light objects.
                    float weightFeelMultiplier = Mathf.Pow(heldObject.mass, 0.6f); 

                    grabJoint.spring = (springForce * weightFeelMultiplier) * leverageFactor;
                    grabJoint.damper = (damper * weightFeelMultiplier) * leverageFactor;
                    grabJoint.maxDistance = 0f;
                    grabJoint.minDistance = 0f;

                    Debug.Log("grabbed: " + heldObject.name + " | Mass: " + heldObject.mass);
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
                heldObject.useGravity = originalUseGravity;

                if (grabJoint != null)
                {
                    Destroy(grabJoint);
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