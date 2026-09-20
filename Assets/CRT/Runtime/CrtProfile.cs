using UnityEngine;

namespace MediaPipeTest.CRT
{
    [CreateAssetMenu(menuName = "CRT/Profile", fileName = "CRT Profile")]
    public sealed class CrtProfile : ScriptableObject
    {
        public CrtSettings settings = new CrtSettings();
    }
}
