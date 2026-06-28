namespace EasyPeasyFirstPersonController
{
    using UnityEngine;
    using UnityEngine.InputSystem; // Required for the new Input System

    public class InputManager : MonoBehaviour, IInputManager
    {
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;

        private void Awake()
        {
            // Initialize move action with standard WASD bindings
            moveAction = new InputAction("Move");
            moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Initialize mouse look action
            lookAction = new InputAction("Look", binding: "<Mouse>/delta");
            
            // Initialize button actions
            jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
            sprintAction = new InputAction("Sprint", binding: "<Keyboard>/leftShift");
            crouchAction = new InputAction("Crouch", binding: "<Keyboard>/leftCtrl");
        }

        private void OnEnable()
        {
            moveAction.Enable();
            lookAction.Enable();
            jumpAction.Enable();
            sprintAction.Enable();
            crouchAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            lookAction.Disable();
            jumpAction.Disable();
            sprintAction.Disable();
            crouchAction.Disable();
        }

        public Vector2 moveInput => moveAction.ReadValue<Vector2>();
        
        // Note: The new Input System's mouse delta returns raw pixel movement, 
        // which is much faster than the old GetAxis. We scale it down here by 0.1f 
        // to keep your existing mouseSensitivity variable feeling roughly the same.
        public Vector2 lookInput => lookAction.ReadValue<Vector2>() * 0.1f; 

        public bool jump => jumpAction.IsPressed();
        public bool sprint => sprintAction.IsPressed();
        public bool crouch => crouchAction.IsPressed();
        public bool slide => crouchAction.IsPressed();
    }
}