using UnityEngine;
using UnityEngine.SceneManagement;

// Menu utama: pilihan Mode AR, Mode VR, dan Tentang (FR-NAV-01, FR-NAV-03).
public class MainMenu : MonoBehaviour
{
    bool tentang;
    string pesan = "";

    void Start()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.14f);
        }
    }

    void Buka(string nama)
    {
        if (Application.CanStreamedLevelBeLoaded(nama)) SceneManager.LoadScene(nama);
        else pesan = "Scene '" + nama + "' belum ada di Build Profiles (File > Build Profiles > Scene List).";
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;
        int fs = Mathf.RoundToInt(Mathf.Min(w, h) / 20f);

        var judul = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(fs * 1.3f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        judul.normal.textColor = Color.white;
        var sub = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(fs * 0.7f), alignment = TextAnchor.MiddleCenter, wordWrap = true };
        sub.normal.textColor = new Color(0.8f, 0.85f, 1f);
        var btn = new GUIStyle(GUI.skin.button) { fontSize = fs };
        var isi = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(fs * 0.65f), wordWrap = true };
        isi.normal.textColor = Color.white;

        GUI.Label(new Rect(0, h * 0.06f, w, h * 0.12f), "AR Face Tracking + Avatar VR", judul);
        GUI.Label(new Rect(w * 0.05f, h * 0.18f, w * 0.9f, h * 0.1f),
            "Eye Tracking & Foveated Rendering (SIMULASI tanpa headset)", sub);

        if (tentang)
        {
            GUI.Label(new Rect(w * 0.06f, h * 0.3f, w * 0.88f, h * 0.5f),
                "TENTANG\n" +
                "Mode AR (Android): kamera depan melacak wajah, menampilkan 4 filter 3D, dan menghitung ekspresi (mulut, kedip, alis, kepala).\n" +
                "Mode VR (PC): avatar wajah meniru ekspresi lewat UDP, ada gaze, UI tatapan (dwell), dan demo foveated rendering.\n" +
                "CATATAN: eye tracking dan foveated rendering di sini adalah SIMULASI (mouse sebagai titik pandang). " +
                "Tidak ada gambar atau data wajah yang disimpan atau dikirim ke internet; hanya angka ekspresi lewat jaringan lokal.", isi);
            if (GUI.Button(new Rect(w * 0.2f, h * 0.82f, w * 0.6f, h * 0.09f), "Kembali", btn)) tentang = false;
            return;
        }

        float bw = w * 0.7f, bh = h * 0.1f, x = (w - bw) / 2f, y = h * 0.34f;
        if (GUI.Button(new Rect(x, y, bw, bh), "Mode AR (Android)", btn))
            Buka(Application.CanStreamedLevelBeLoaded("FaceScene") ? "FaceScene" : "SampleScene");
        if (GUI.Button(new Rect(x, y + bh * 1.3f, bw, bh), "Mode VR (PC)", btn)) Buka("VRScene");
        if (GUI.Button(new Rect(x, y + bh * 2.6f, bw, bh), "Tentang", btn)) tentang = true;

        if (pesan != "")
            GUI.Label(new Rect(w * 0.06f, y + bh * 3.9f, w * 0.88f, h * 0.2f), pesan, isi);
    }
}
