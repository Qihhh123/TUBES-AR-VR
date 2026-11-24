using UnityEngine;

public class VRBootstrap : MonoBehaviour
{
    void Start()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.14f);
        cam.transform.position = new Vector3(0, 0.05f, 2.3f);
        cam.transform.LookAt(Vector3.zero);

        if (FindFirstObjectByType<Light>() == null)
        {
            var l = new GameObject("Cahaya").AddComponent<Light>();
            l.type = LightType.Directional;
            l.transform.rotation = Quaternion.Euler(30, 160, 0);
        }

        new GameObject("Avatar").AddComponent<AvatarFace>();
        gameObject.AddComponent<SimulatedExpressionInput>();
    }

    void OnGUI()
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = Mathf.RoundToInt(Screen.height / 38f);
        s.normal.textColor = Color.white;
        GUI.Label(new Rect(20, 20, Screen.width - 40, Screen.height / 3f),
            "MODE VR - SIMULASI\n" +
            "Sumber ekspresi: " + ExpressionHub.Source + "\n" +
            "Spasi = mulut | Q / E = kedip | R = alis | Panah = kepala", s);
    }
}