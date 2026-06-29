namespace EasyPeasyFirstPersonController
{
    using UnityEngine;
    using UnityEngine.InputSystem; 

    public class InputManager : MonoBehaviour, IInputManager
    {
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;

        // Fixed: Unity Input System uses PascalCase Methods ()
        public bool jumpPressed => jumpAction.WasPressedThisFrame();
        public bool jumpReleased => jumpAction.WasReleasedThisFrame();
        
        private void Awake()
        {
            moveAction = new InputAction("Move");
            moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            lookAction = new InputAction("Look", binding: "<Mouse>/delta");
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
        public Vector2 lookInput => lookAction.ReadValue<Vector2>() * 0.1f; 

        public bool jump => jumpAction.IsPressed();
        public bool sprint => sprintAction.IsPressed();
        public bool crouch => crouchAction.IsPressed();
        public bool slide => crouchAction.IsPressed();
    }
}