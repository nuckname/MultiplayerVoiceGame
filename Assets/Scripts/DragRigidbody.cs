using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

// Drag a rigidbody with the mouse using a spring joint like Dani did in KARLSON. By Boxply
public class DragRigidbody : NetworkBehaviour
{
    [Header("Settings")]
    public float force = 600;
    public float damping = 6;
    public float distance = 15;

    [Header("References")]
    [Tooltip("Assign the specific camera attached to this local player.")]
    public Camera playerCamera;

    // Converted to private so they can be fetched automatically at runtime
    private LineRenderer lr;
    private Transform lineRenderLocation;

    // Server-side reference to the joint
    private Transform jointTrans;
    
    // Client-side local calculation variables
    private float dragDepth;
    private bool isLocalDragging = false;

    // Network Variables to sync the visual rope to all clients
    private NetworkVariable<bool> isDraggingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> dragAnchorPositionNet = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        // 1. Automatically find the LineRenderer on this object or any of its children
        lr = GetComponentInChildren<LineRenderer>();
        if (lr == null)
        {
            Debug.LogError("[CLIENT] DragRigidbody: No LineRenderer found on the player or its children!");
        }

        // 2. Automatically find the starting point for the rope
        Transform customPoint = transform.Find("RopeStartPoint");
        lineRenderLocation = customPoint != null ? customPoint : this.transform;
    }

    void Update()
    {
        // 1. Only the Local Player (Owner) processes inputs
        if (IsOwner)
        {
            if (playerCamera == null)
            {
                Debug.LogWarning("[CLIENT] DragRigidbody: playerCamera is missing! Assign it in the Inspector.");
                return;
            }

            if (Mouse.current != null) 
            {
                Vector3 currentMousePosition = Mouse.current.position.ReadValue();

                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    HandleInputBegin(currentMousePosition);
                }
                else if (Mouse.current.leftButton.isPressed && isLocalDragging)
                {
                    HandleInput(currentMousePosition);
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && isLocalDragging)
                {
                    HandleInputEnd(currentMousePosition);
                }
            }
        }

        // 2. All clients (including owner) update visuals based on Server State
        if (isDraggingNet.Value)
        {
            DrawRope();
        }
        else if (lr != null && lr.positionCount > 0)
        {
            DestroyRope();
        }
    }
    
    public void HandleInputBegin (Vector3 screenPosition)
    {
       var ray = playerCamera.ScreenPointToRay (screenPosition);
       
       // VISUALIZE THE RAYCAST
       Debug.DrawRay(ray.origin, ray.direction * distance, Color.red, 3f);
       
       RaycastHit hit;
       
       if (Physics.Raycast (ray, out hit, distance)) 
       {
          Debug.Log($"[CLIENT] Raycast hit object: {hit.transform.name}. Layer: {LayerMask.LayerToName(hit.transform.gameObject.layer)}");
          
          if (hit.transform.gameObject.layer == LayerMask.NameToLayer ("Interactive")) 
          {
             dragDepth = CameraPlane.CameraToPointDepth (playerCamera, hit.point);
             
             NetworkObject targetNetObj = hit.rigidbody != null ? hit.rigidbody.GetComponent<NetworkObject>() : null;
             
             if (targetNetObj != null)
             {
                 isLocalDragging = true;
                 AttachJointServerRpc(targetNetObj.NetworkObjectId, hit.point);
             }
             else
             {
                 Debug.LogWarning($"[CLIENT] Hit an Interactive object ({hit.transform.name}), but it is missing a Rigidbody or NetworkObject component!");
             }
          }
       }
       else
       {
           Debug.Log("[CLIENT] Raycast missed entirely. Are you too far away? (Max Distance: " + distance + ")");
       }
    }
    
    public void HandleInput (Vector3 screenPosition)
    {
        if (!isLocalDragging)
            return;
            
       Vector3 targetPos = CameraPlane.ScreenToWorldPlanePoint (playerCamera, dragDepth, screenPosition);
       MoveJointServerRpc(targetPos);
    }
    
    public void HandleInputEnd (Vector3 screenPosition)
    {
        if (!isLocalDragging) return;

        isLocalDragging = false;
        DetachJointServerRpc();
    }

    [ServerRpc]
    private void AttachJointServerRpc(ulong targetNetworkId, Vector3 attachmentPosition)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkId, out NetworkObject targetNetObj))
        {
            Rigidbody rb = targetNetObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                jointTrans = AttachJoint(rb, attachmentPosition);
                isDraggingNet.Value = true;
                dragAnchorPositionNet.Value = attachmentPosition;
            }
        }
    }

    [ServerRpc]
    private void MoveJointServerRpc(Vector3 newPosition)
    {
        if (jointTrans != null)
        {
            jointTrans.position = newPosition;
            dragAnchorPositionNet.Value = newPosition; // Sync visual end-point to clients
        }
    }

    [ServerRpc]
    private void DetachJointServerRpc()
    {
        isDraggingNet.Value = false;
        if (jointTrans != null)
        {
            Destroy(jointTrans.gameObject);
            jointTrans = null;
        }
    }
    
    Transform AttachJoint (Rigidbody rb, Vector3 attachmentPosition)
    {
       GameObject go = new GameObject ("Attachment Point");
       go.hideFlags = HideFlags.HideInHierarchy; 
       go.transform.position = attachmentPosition;
       
       var newRb = go.AddComponent<Rigidbody> ();
       newRb.isKinematic = true;
       
       var joint = go.AddComponent<ConfigurableJoint> ();
       joint.connectedBody = rb;
       joint.configuredInWorldSpace = true;
       joint.xDrive = NewJointDrive (force, damping);
       joint.yDrive = NewJointDrive (force, damping);
       joint.zDrive = NewJointDrive (force, damping);
       joint.slerpDrive = NewJointDrive (force, damping);
       joint.rotationDriveMode = RotationDriveMode.Slerp;
       
       return go.transform;
    }
    
    private JointDrive NewJointDrive (float force, float damping)
    {
       JointDrive drive = new JointDrive ();
       drive.positionSpring = force;
       drive.positionDamper = damping;
       drive.maximumForce = Mathf.Infinity;
       return drive;
    }

    private void DrawRope()
    {
       if (lr == null) return;
       
       lr.positionCount = 2;
       lr.SetPosition(0, lineRenderLocation.position);
       lr.SetPosition(1, dragAnchorPositionNet.Value);
    }

    private void DestroyRope()
    {
       if (lr == null) return;
       
       lr.positionCount = 0;
    }
}