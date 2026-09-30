using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace MediaPipeTest.HenshinFilter
{
    public sealed class FilterPanelDrag : MonoBehaviour
    {
        public Camera viewCamera;
        public Collider panelCollider;
        public bool acceptInput = true;
        public bool IsDragging { get; private set; }
        public Vector3 InitialPosition { get; private set; }
        Plane dragPlane;
        Vector3 grabOffset;

        void Awake() { InitialPosition = transform.position; }

        void Update()
        {
            if (!acceptInput) { EndDrag(); return; }
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetPanel();
            if (Mouse.current == null) return;
            Vector2 point = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                BeginDrag(point);
            if (Mouse.current.leftButton.isPressed) DragTo(point);
            if (Mouse.current.leftButton.wasReleasedThisFrame) EndDrag();
        }

        public bool BeginDrag(Vector2 screenPoint)
        {
            if (!viewCamera || !panelCollider) return false;
            Ray ray = viewCamera.ScreenPointToRay(screenPoint);
            if (!panelCollider.Raycast(ray, out RaycastHit hit, viewCamera.farClipPlane)) return false;
            dragPlane = new Plane(viewCamera.transform.forward, transform.position);
            if (!dragPlane.Raycast(ray, out float distance)) return false;
            grabOffset = transform.position - ray.GetPoint(distance);
            IsDragging = true;
            return true;
        }

        public void DragTo(Vector2 screenPoint)
        {
            if (!IsDragging || !viewCamera) return;
            Ray ray = viewCamera.ScreenPointToRay(screenPoint);
            if (dragPlane.Raycast(ray, out float distance)) transform.position = ray.GetPoint(distance) + grabOffset;
        }

        public void EndDrag() { IsDragging = false; }
        public void ResetPanel() { EndDrag(); transform.position = InitialPosition; }
        void OnDisable() { EndDrag(); }
        void OnApplicationFocus(bool focused) { if (!focused) EndDrag(); }
    }
}
