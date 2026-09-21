using UnityEngine;
using UnityEngine.InputSystem;

namespace MediaPipeTest.CRT.Surveillance
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SurveillancePlayer : MonoBehaviour
    {
        public Camera view;
        public Transform interactionPoint, seatedView;
        public SurveillanceFeed feed;
        public float interactionDistance = 1.5f, walkSpeed = 2.3f, mouseSensitivity = .10f;
        public float seatDuration = .25f;
        public bool Seated { get; private set; }
        public bool MovingToSeat { get; private set; }
        public bool CanSit => !Seated && !MovingToSeat && Vector3.Distance(transform.position, interactionPoint.position) <= interactionDistance;
        public bool AcceptInput { get; set; } = true;
        public CharacterController Controller { get; private set; }
        float pitch, verticalSpeed, seatElapsed;
        Vector3 savedPosition, cameraLocalPosition, transitionPosition;
        Quaternion savedRotation, cameraLocalRotation, transitionRotation;
        CursorLockMode initialCursor, returnCursor;
        bool initialVisible, returnVisible;
        int suppressLookFrames;

        void Awake() => Controller = GetComponent<CharacterController>();
        void OnEnable()
        {
            initialCursor = Cursor.lockState; initialVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            suppressLookFrames = 2;
        }
        void OnApplicationFocus(bool focused) { if (focused) suppressLookFrames = 2; }
        void Update()
        {
            if (MovingToSeat)
            {
                seatElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, seatElapsed / Mathf.Max(.001f, seatDuration));
                view.transform.SetPositionAndRotation(Vector3.Lerp(transitionPosition, seatedView.position, t),
                    Quaternion.Slerp(transitionRotation, seatedView.rotation, t));
                if (seatElapsed >= seatDuration) MovingToSeat = false;
            }
            if (!AcceptInput) return;
            var key = Keyboard.current; var mouse = Mouse.current;
            if (Seated)
            {
                if (key != null && (key.eKey.wasPressedThisFrame || key.escapeKey.wasPressedThisFrame)) Stand();
                else if (!MovingToSeat && key != null)
                {
                    if (key.rightArrowKey.wasPressedThisFrame) feed.NextChannel();
                    if (key.leftArrowKey.wasPressedThisFrame) feed.PreviousChannel();
                    if (key.cKey.wasPressedThisFrame) feed.SetCrtEnabled(!feed.CrtEnabled);
                }
                return;
            }
            if (key != null && key.eKey.wasPressedThisFrame && CanSit) { Sit(); return; }
            if (key != null && key.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                suppressLookFrames = 2;
            }
            if (Cursor.lockState != CursorLockMode.Locked) return;
            // Locking/focusing can warp the pointer; discard that delta without interrupting movement.
            if (suppressLookFrames > 0) suppressLookFrames--;
            else if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -75, 75);
                view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector2 move = key == null ? Vector2.zero : new Vector2(
                (key.dKey.isPressed ? 1 : 0) - (key.aKey.isPressed ? 1 : 0),
                (key.wKey.isPressed ? 1 : 0) - (key.sKey.isPressed ? 1 : 0));
            Move(move, Time.deltaTime);
        }
        public void Move(Vector2 direction, float deltaTime)
        {
            if (Seated || !Controller.enabled) return;
            verticalSpeed = Controller.isGrounded ? -2 : Mathf.Max(-25, verticalSpeed - 20 * deltaTime);
            Vector2 move = Vector2.ClampMagnitude(direction, 1);
            Controller.Move((transform.right * move.x * walkSpeed + transform.forward * move.y * walkSpeed + Vector3.up * verticalSpeed) * deltaTime);
        }
        public void Sit()
        {
            if (!CanSit) return;
            savedPosition = transform.position; savedRotation = transform.rotation;
            cameraLocalPosition = view.transform.localPosition; cameraLocalRotation = view.transform.localRotation;
            returnCursor = Cursor.lockState; returnVisible = Cursor.visible;
            transitionPosition = view.transform.position; transitionRotation = view.transform.rotation;
            Controller.enabled = false; verticalSpeed = 0;
            Seated = true; MovingToSeat = true; seatElapsed = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        public void Stand()
        {
            if (!Seated) return;
            transform.SetPositionAndRotation(savedPosition, savedRotation);
            view.transform.SetLocalPositionAndRotation(cameraLocalPosition, cameraLocalRotation);
            Seated = false; MovingToSeat = false; Controller.enabled = true;
            Cursor.lockState = returnCursor; Cursor.visible = returnVisible;
            suppressLookFrames = 2;
        }
        void OnDisable()
        {
            if (Seated) Stand();
            Cursor.lockState = initialCursor; Cursor.visible = initialVisible;
        }
    }
}
