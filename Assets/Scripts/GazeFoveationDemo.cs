using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class GazeFoveationDemo : MonoBehaviour
{
    public float dwellTime = 1.2f;
    public int radius = 220;
    public int lowResFactor = 4;
    public bool foveasi = true;

    public static RenderTexture Full, Low;
    public static bool Aktif;
    public static Vector2 GazeGui;
    public static int Radius;

    class DwellBtn
    {
        public System.Func<string> text;
        public System.Action aksi;
        public Rect r;
        public float t;
    }

    Camera cam;
    Texture2D ring;
    DwellBtn[] btns;
    int w, h;
    float fps, sumOn, sumOff;
    int nOn, nOff;
    string pesan = "";

    void Start()
    {
        cam = Camera.main;
        ring = MakeRing(64);
        gameObject.AddComponent<FoveationView>();
        btns = new DwellBtn[]
        {
            new DwellBtn { text = () => "Foveasi: " + (foveasi ? "ON" : "OFF"), aksi = () => foveasi = !foveasi },
            new DwellBtn { text = () => "Radius +", aksi = () => radius = Mathf.Min(600, radius + 40) },
            new DwellBtn { text = () => "Radius -", aksi = () => radius = Mathf.Max(80, radius - 40) },
            new DwellBtn { text = () => "Simpan hasil", aksi = Simpan },
        };
        MakeTargets();
    }

    void MakeTargets()
    {
        w = Screen.width; h = Screen.height;
        if (Full != null) Full.Release();
        if (Low != null) Low.Release();
        Full = new RenderTexture(w, h, 24);
        Full.filterMode = FilterMode.Bilinear;
        Low = new RenderTexture(Mathf.Max(1, w / lowResFactor), Mathf.Max(1, h / lowResFactor), 0);
        Low.filterMode = FilterMode.Point;
        cam.targetTexture = Full;
    }

    static Texture2D MakeRing(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                bool on = (d < c && d > c - 4f) || d < 3f;
                t.SetPixel(x, y, on ? new Color(1f, 0.9f, 0.2f, 1f) : Color.clear);
            }
        t.Apply();
        return t;
    }

    void Update()
    {
        if (cam == null || btns == null) return;
        if (w != Screen.width || h != Screen.height) MakeTargets();

        // GAZE (SIMULASI): posisi mouse dianggap titik pandang
        var m = Mouse.current;
        Vector2 mp = m != null ? m.position.ReadValue() : new Vector2(w / 2f, h / 2f);
        GazeGui = new Vector2(mp.x, h - mp.y);
        ExpressionHub.Gaze = new Vector2(
            Mathf.Clamp(-(mp.x / w * 2f - 1f), -1f, 1f),
            Mathf.Clamp(mp.y / h * 2f - 1f, -1f, 1f));

        var k = Keyboard.current;
        if (k != null && k.fKey.wasPressedThisFrame) foveasi = !foveasi;
        Aktif = foveasi;
        Radius = radius;

        float inst = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        fps = Mathf.Lerp(fps, inst, 0.05f);
        if (foveasi) { sumOn += inst; nOn++; } else { sumOff += inst; nOff++; }

        Graphics.Blit(Full, Low);

        float bw = w * 0.22f, bh = h * 0.09f, x = w - bw - 20f, y = h * 0.25f;
        for (int i = 0; i < btns.Length; i++)
            btns[i].r = new Rect(x, y + i * (bh + 14f), bw, bh);

        // UI TATAPAN: tombol aktif setelah ditatap dwellTime detik
        foreach (var b in btns)
        {
            if (b.r.Contains(GazeGui))
            {
                if (b.t >= 0f)
                {
                    b.t += Time.unscaledDeltaTime;
                    if (b.t >= dwellTime) { b.aksi(); b.t = -1f; }
                }
            }
            else b.t = 0f;
        }
    }

    float Avg(float sum, int n) { return n > 0 ? sum / n : 0f; }

    void Simpan()
    {
        string isi =
            "HASIL FOVEATED RENDERING (SIMULASI, BUKAN PERANGKAT KERAS ASLI)\n" +
            "Waktu: " + System.DateTime.Now + "\n" +
            "Resolusi layar: " + w + "x" + h + "\n" +
            "Radius area tajam: " + radius + " px | Faktor resolusi rendah: 1/" + lowResFactor + "\n" +
            "Rata-rata FPS foveasi ON : " + Avg(sumOn, nOn).ToString("0.0") + " (" + nOn + " frame)\n" +
            "Rata-rata FPS foveasi OFF: " + Avg(sumOff, nOff).ToString("0.0") + " (" + nOff + " frame)\n" +
            "Catatan: gaze dari mouse, foveasi digambar ulang lewat render texture.\n";
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "hasil_foveasi.txt");
        File.WriteAllText(path, isi);
        pesan = "Tersimpan: " + path;
        Debug.Log(pesan);
    }

    void OnGUI()
    {
        GUI.depth = 0;
        if (btns == null) return;
        int fs = Mathf.RoundToInt(h / 42f);
        var st = new GUIStyle(GUI.skin.box) { fontSize = fs, alignment = TextAnchor.MiddleCenter };
        st.normal.textColor = Color.white;
        var lb = new GUIStyle(GUI.skin.label) { fontSize = fs };
        lb.normal.textColor = Color.white;

        foreach (var b in btns)
        {
            GUI.Box(b.r, b.text(), st);
            float p = Mathf.Clamp01(b.t / dwellTime);
            var old = GUI.color;
            GUI.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            GUI.DrawTexture(new Rect(b.r.x, b.r.yMax - 10f, b.r.width * p, 10f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        GUI.DrawTexture(new Rect(GazeGui.x - 32, GazeGui.y - 32, 64, 64), ring);

        float frac = Mathf.Min(1f, (2f * radius) * (2f * radius) / (w * (float)h));
        float beban = foveasi ? frac + (1f - frac) / (lowResFactor * lowResFactor) : 1f;
        string info =
            "Sumber gaze: SIMULASI (mouse) - eye tracking headset tidak tersedia\n" +
            "Foveated rendering: " + (foveasi ? "ON (SIMULASI)" : "OFF") + "   [tombol F = ganti]\n" +
            "FPS: " + fps.ToString("0") + " | frame: " + (1000f / Mathf.Max(fps, 1f)).ToString("0.0") + " ms\n" +
            "Piksel tajam: " + (frac * 100f).ToString("0") + "% | estimasi beban piksel: " + (beban * 100f).ToString("0") + "%\n" +
            "Rata-rata FPS  ON: " + Avg(sumOn, nOn).ToString("0") + "  OFF: " + Avg(sumOff, nOff).ToString("0") + "\n" +
            "Tatap tombol " + dwellTime.ToString("0.0") + " detik untuk memilih\n" + pesan;
        GUI.Label(new Rect(20, h * 0.18f, w * 0.7f, h * 0.6f), info, lb);
    }
}

public class FoveationView : MonoBehaviour
{
    void OnGUI()
    {
        GUI.depth = 100;
        var full = GazeFoveationDemo.Full;
        if (full == null) return;
        int w = Screen.width, h = Screen.height;
        var layar = new Rect(0, 0, w, h);

        if (!GazeFoveationDemo.Aktif)
        {
            GUI.DrawTexture(layar, full, ScaleMode.StretchToFill, false);
            return;
        }

        GUI.DrawTexture(layar, GazeFoveationDemo.Low, ScaleMode.StretchToFill, false);

        Vector2 g = GazeFoveationDemo.GazeGui;
        float r = GazeFoveationDemo.Radius;
        float x0 = Mathf.Max(0, g.x - r), y0 = Mathf.Max(0, g.y - r);
        float x1 = Mathf.Min(w, g.x + r), y1 = Mathf.Min(h, g.y + r);
        if (x1 <= x0 || y1 <= y0) return;

        var rect = new Rect(x0, y0, x1 - x0, y1 - y0);
        var uv = new Rect(x0 / w, 1f - y1 / h, (x1 - x0) / w, (y1 - y0) / h);
        GUI.DrawTextureWithTexCoords(rect, full, uv, false);

        var tipis = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), tipis);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2, rect.width, 2), tipis);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), tipis);
        GUI.DrawTexture(new Rect(rect.xMax - 2, rect.y, 2, rect.height), tipis);
    }
}