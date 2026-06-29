namespace EasyPeasyFirstPersonController
{
    using UnityEngine;
    using Unity.Netcode; // Added Netcode namespace

    // Changed from MonoBehaviour to NetworkBehaviour
    public partial class FirstPersonController : NetworkBehaviour
    {
        [Header("Settings")]
        public float walkSpeed = 3f;
        public float sprintSpeed = 5f;
        public float crouchSpeed = 1.5f;
        public float jumpSpeed = 4f;
        public float gravity = 9.81f;
        public float slideDuration = 0.7f;
        public float slideSpeed = 6f;
        public float mouseSensitivity = 2f;
        public float strafeTiltAmount = 2f;

        [Header("Jump King Settings")]
        public bool useJumpKingMechanic = true;
        public float maxLeapForce = 18f;
        public float maxChargeTime = 1.25f;

        [Header("References")]
        public Transform playerCamera;
        public Transform cameraParent;
        public Transform groundCheck;
        public LayerMask groundMask;

        [HideInInspector] public CharacterController characterController;
        [HideInInspector] public IInputManager input;
        [HideInInspector] public Vector3 moveDirection;
        [HideInInspector] public bool isGrounded;

        private PlayerBaseState currentState;
        private PlayerStateFactory states;
        private float xRotation = 0f;
        private float currentTilt;
        private float tiltVelocity;

        // Jump King Internal Tracking
        private bool isChargingLeap = false;
        private bool isAirborneFromLeap = false;
        private float currentChargeTimer = 0f;
        private float groundCheckLockTimer = 0f;
        private Vector3 leapVelocity;

        public PlayerBaseState CurrentState { get => currentState; set => currentState = value; }

        [Header("Visual Settings")]
        public float normalFov = 60f;
        public float sprintFov = 75f;
        public float slideFovBoost = 5f;
        public float fovChangeSpeed = 8f;
        public float bobAmount = 0.001f;
        public float bobSpeed = 10f;
        public float recoilReturnSpeed = 5f;

        [HideInInspector] public Camera cam;
        [HideInInspector] public float targetFov;
        [HideInInspector] public float currentBobIntensity;
        [HideInInspector] public float currentBobSpeed;
        [HideInInspector] public float targetTilt;

        private float bobTimer;
        private float fovVelocity;
        private float originalCamY;

        [Header("Height Settings")]
        public float standingCameraHeight = 1.75f;
        public float crouchingCameraHeight = 1f;
        public float crouchingCharacterControllerHeight = 1f;
        [HideInInspector] public float standingCharacterControllerHeight = 1.8f;
        [HideInInspector] public Vector3 standingCharacterControllerCenter = new Vector3(0, 0.9f, 0);
        [HideInInspector] public float targetCameraY;

        [Header("Ledge Settings")]
        public LayerMask ledgeLayer;
        public float ledgeDetectionDistance = 1f;
        private float landingMomentum;

        [Header("Swimming Settings")]
        public float swimSpeed = 4f;
        public float swimSprintSpeed = 6f;
        public float waterDrag = 2f;
        public LayerMask waterMask;
        [HideInInspector] public bool isInWater;

        [Header("Visual Preferences")]
        public bool useFovKick = true;
        public bool useHeadBob = true;
        public bool useCameraTilt = true;
        public bool useClimbTilt = true;

        [Header("Debug")]
        public bool currentStateDebug = true;

        void OnGUI()
        {
            if (!IsOwner) return; 

            if (currentState != null && Application.isEditor && currentStateDebug)
                GUILayout.Label("Current State: " + (isAirborneFromLeap ? "BALLISTIC_LEAP" : currentState.GetType().Name));
        }

        private void Awake()
        {
            cam = playerCamera.GetComponent<Camera>();
            targetFov = normalFov;
            targetCameraY = standingCameraHeight;
            originalCamY = standingCameraHeight;

            characterController = GetComponent<CharacterController>();
            standingCharacterControllerHeight = characterController.height;
            standingCharacterControllerCenter = characterController.center;
            input = GetComponent<IInputManager>();
            states = new PlayerStateFactory(this);

            currentState = states.Grounded();
            currentState.EnterState();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerCamera.gameObject.SetActive(true);
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                playerCamera.gameObject.SetActive(false);
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }

        private void Update()
        {
            if (!IsOwner) return; 

            // Handle momentary launch ground-lock
            if (groundCheckLockTimer > 0)
            {
                groundCheckLockTimer -= Time.deltaTime;
                isGrounded = false;
            }
            else
            {
                isGrounded = Physics.CheckSphere(groundCheck.position, 0.2f, groundMask, QueryTriggerInteraction.Ignore);
            }

            if (useJumpKingMechanic)
            {
                HandleJumpKingLogic();
            }

            // --- BALLISTIC FLIGHT TAKEOVER ---
            // While leaping, we bypass the normal State Machine so it can't delete our X/Z velocity
            if (isAirborneFromLeap)
            {
                if (HasCeiling()) leapVelocity.y = -2f; // Head bonk!

                leapVelocity.y -= gravity * Time.deltaTime;

                // Apply gentle air-drag to horizontal movement so jumps feel weighty
                leapVelocity.x = Mathf.Lerp(leapVelocity.x, 0, Time.deltaTime * 0.4f);
                leapVelocity.z = Mathf.Lerp(leapVelocity.z, 0, Time.deltaTime * 0.4f);

                characterController.Move(leapVelocity * Time.deltaTime);

                // Touchdown check
                if (isGrounded && leapVelocity.y <= 0)
                {
                    isAirborneFromLeap = false;
                    moveDirection = Vector3.zero;
                }

                HandleRotation();
                UpdateVisuals();
                return; // <--- This 'return' stops the standard State Machine from running this frame
            }

            // Normal grounded/walking states
            if (!isChargingLeap)
            {
                currentState.UpdateState();
            }

            HandleRotation();
            UpdateVisuals();
        }

        private void HandleJumpKingLogic()
        {
            // 1. Start Charge
            if (isGrounded && input.jumpPressed && !isChargingLeap && !isAirborneFromLeap)
            {
                isChargingLeap = true;
                currentChargeTimer = 0f;
                moveDirection = Vector3.zero; 
            }

            // 2. Cancel if pushed off a ledge while charging
            if (isChargingLeap && !isGrounded)
            {
                isChargingLeap = false;
            }

            // 3. Charging Loop
            if (isChargingLeap)
            {
                currentChargeTimer += Time.deltaTime;
                float chargePercent = Mathf.Clamp01(currentChargeTimer / maxChargeTime);

                if (input.jumpReleased || chargePercent >= 1.0f)
                {
                    isChargingLeap = false;
                    isAirborneFromLeap = true; // Hijack Update()

                    Vector3 barrelOfCamera = playerCamera.forward;

                    // THE HORIZON FIX: 
                    // Guarantee the Y vector is at least 0.35f (roughly a 20-degree upward arc). 
                    // This forces looking straight ahead to result in a massive forward Long-Jump rather than a face-plant.
                    if (barrelOfCamera.y < 0.35f)
                    {
                        barrelOfCamera.y = 0.35f;
                        barrelOfCamera.Normalize(); 
                    }

                    leapVelocity = barrelOfCamera * (chargePercent * maxLeapForce);
                    groundCheckLockTimer = 0.2f; 
                }
            }
        }

        private void HandleRotation()
        {
            float mouseX = input.lookInput.x * mouseSensitivity;
            float mouseY = input.lookInput.y * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            float strafeTilt = useCameraTilt ? (-input.moveInput.x * strafeTiltAmount) : 0;
            float combinedTargetTilt = (useCameraTilt ? targetTilt : 0) + strafeTilt;

            currentTilt = Mathf.SmoothDamp(currentTilt, combinedTargetTilt, ref tiltVelocity, 0.1f);
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0, currentTilt);
        }

        public void UpdateVisuals()
        {
            if (!useFovKick) targetFov = normalFov;
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, 1f / fovChangeSpeed);

            landingMomentum = Mathf.Lerp(landingMomentum, 0, Time.deltaTime * 10f);
            float newY = Mathf.Lerp(cameraParent.localPosition.y, targetCameraY, Time.deltaTime * 8f);

            if (useHeadBob && characterController.velocity.magnitude > 0.1f && isGrounded && !isChargingLeap)
            {
                bobTimer += Time.deltaTime * currentBobSpeed;
                float bobOffset = Mathf.Sin(bobTimer) * currentBobIntensity;
                cameraParent.localPosition = new Vector3(cameraParent.localPosition.x, newY + bobOffset, cameraParent.localPosition.z);
            }
            else
            {
                bobTimer = 0;
                cameraParent.localPosition = new Vector3(cameraParent.localPosition.x, newY, cameraParent.localPosition.z);
            }
        }
        
        public bool HasCeiling()
        {
            float radius = characterController.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (characterController.height - radius);
            float checkDistance = standingCharacterControllerHeight - characterController.height + 0.1f;

            return Physics.SphereCast(origin, radius, Vector3.up, out _, checkDistance, groundMask, QueryTriggerInteraction.Ignore);
        }
        
        public bool CheckLedge(out Vector3 climbPosition)
        {
            climbPosition = Vector3.zero;
            RaycastHit wallHit;
            Vector3 wallOrigin = transform.position + Vector3.up * 1.5f;

            if (Physics.Raycast(wallOrigin, transform.forward, out wallHit, ledgeDetectionDistance, ledgeLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 ledgeOrigin = wallOrigin + Vector3.up * 0.6f + transform.forward * 0.2f;
                if (!Physics.Raycast(ledgeOrigin, transform.forward, 0.5f, groundMask))
                {
                    if (Physics.Raycast(ledgeOrigin + transform.forward * 0.4f, Vector3.down, out RaycastHit ledgeHit, 1f, groundMask))
                    {
                        climbPosition = ledgeHit.point + Vector3.up * 1f;
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned || !IsOwner) return; 
            if (((1 << other.gameObject.layer) & waterMask) != 0) isInWater = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsSpawned || !IsOwner) return;
            if (((1 << other.gameObject.layer) & waterMask) != 0) isInWater = false;
        }
    }
}