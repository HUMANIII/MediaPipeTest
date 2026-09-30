using UnityEngine;
using UnityEngine.InputSystem;

namespace MediaPipeTest.HenshinFilter
{
    /// <summary>Keyboard-only comparison controls; no dependency on the pose server.</summary>
    public sealed class FilterDemoControls : MonoBehaviour
    {
        public HenshinFilterController controller;
        void Update()
        {
            if (!controller || Keyboard.current == null) return;
            if (Keyboard.current.eKey.wasPressedThisFrame) controller.effectEnabled = !controller.effectEnabled;
        }
    }
}
