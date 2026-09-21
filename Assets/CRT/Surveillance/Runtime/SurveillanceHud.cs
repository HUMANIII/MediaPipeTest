using UnityEngine;

namespace MediaPipeTest.CRT.Surveillance
{
    public sealed class SurveillanceHud : MonoBehaviour
    {
        public SurveillancePlayer player;
        public SurveillanceFeed feed;
        GUIStyle label, title, button;
        public bool Visible { get; set; } = true;
        void OnGUI()
        {
            if (!Visible) return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            title ??= new GUIStyle(label) { fontSize = 21, fontStyle = FontStyle.Bold };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 17 };
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.Box(new Rect(20, 20, 440, 76), "");
            GUI.Label(new Rect(30, 24, 420, 32), "HOSPITAL / SURVEILLANCE", title);
            GUI.Label(new Rect(30, 57, 420, 28), feed.ChannelLabel + (feed.CrtEnabled ? "  |  CRT" : "  |  ORIGINAL"), label);
            if (player.Seated)
            {
                float x = (width - 840) / 2;
                GUI.Box(new Rect(x - 15, height - 108, 870, 88), "");
                GUI.Label(new Rect(x, height - 105, 840, 27), "ARROWS: CHANNEL    C: CRT / ORIGINAL    E / ESC: STAND", label);
                GUI.enabled = !feed.IsTransitioning && !player.MovingToSeat;
                if (GUI.Button(new Rect(x, height - 70, 180, 36), "< PREVIOUS", button)) feed.PreviousChannel();
                if (GUI.Button(new Rect(x + 190, height - 70, 180, 36), "NEXT >", button)) feed.NextChannel();
                GUI.enabled = !player.MovingToSeat;
                if (GUI.Button(new Rect(x + 380, height - 70, 240, 36), feed.CrtEnabled ? "CRT  /  ORIGINAL" : "ORIGINAL  /  CRT", button)) feed.SetCrtEnabled(!feed.CrtEnabled);
                GUI.enabled = true;
                if (GUI.Button(new Rect(x + 630, height - 70, 210, 36), "STAND UP", button)) player.Stand();
            }
            else
            {
                GUI.Box(new Rect((width - 670) / 2, height - 75, 670, 45), "");
                GUI.Label(new Rect((width - 660) / 2, height - 72, 660, 38), player.CanSit ? "[ E ]  SIT AT MONITOR" : "WASD: MOVE    MOUSE: LOOK    APPROACH THE MONITOR", label);
                GUI.Label(new Rect(width / 2 - 10, height / 2 - 10, 20, 20), "+", label);
            }
        }
    }
}
