using UnityEngine;

namespace CyberCity
{
    public sealed class InteriorRoom : MonoBehaviour
    {
        public string citizenName, title;
        public Vector2 spawn = new Vector2(16, -13.5f);
        public Vector2 exit = new Vector2(16, -15.2f);
        public Vector2 mentor = new Vector2(16, -8);
        public static string ScenePath(string citizen) => "Assets/CyberCity/Scenes/Room_" + citizen.Replace(" ", "_") + ".unity";
    }
}
