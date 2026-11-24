using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARExpressionBridge : MonoBehaviour
{
    ARFaceManager faceManager;
    UdpSender sender;

    string ipText;

    float nMouth = 0.02f;
    float nApL = 0.27f;
    float nApR = 0.27f;
    float nBrow = 0.14f;

    Quaternion nHead = Quaternion.identity;
    bool headSet;

    float rawMouth, rawApL, rawApR, rawBrow;

    Quaternion relHead = Quaternion.identity;

    bool kalibrasi;
    float kalibSelesai;

    float sM, sAL, sAR, sB;
    int sN;

    float oMouth, oBL, oBR, oBrow;

    int sYaw = 1, sPitch = 1, sRoll = 1;

    bool tukarMata;
    bool wajah;

    readonly ExpressionData data = new ExpressionData();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Daftar()
    {
        SceneManager.sceneLoaded += (s, m) => Buat();
        Buat();
    }

    static void Buat()
    {
        if (FindAnyObjectByType<ARFaceManager>() != null &&
            FindAnyObjectByType<ARExpressionBridge>() == null)
        {
            new GameObject("ARExpressionBridge")
                .AddComponent<ARExpressionBridge>();
        }
    }

    void Start()
    {
        faceManager = FindAnyObjectByType<ARFaceManager>();

        ipText = PlayerPrefs.GetString(
            "ip_pc",
            "192.168.1.2"
        );

        sender = gameObject.AddComponent<UdpSender>();
        sender.ip = ipText;
    }

    static float Wrap(float a)
    {
        a %= 360f;

        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;

        return a;
    }

    static float D(
        Unity.Collections.NativeArray<Vector3> v,
        int a,
        int b)
    {
        return Vector3.Distance(v[a], v[b]);
    }

    void Hitung(
        ARFace f,
        Unity.Collections.NativeArray<Vector3> v)
    {
        float lebar = D(v, 234, 454);

        if (lebar < 0.0001f)
            return;

        rawMouth = D(v, 13, 14) / lebar;

        float lebarR = Mathf.Max(D(v, 33, 133), 1e-5f);
        float lebarL = Mathf.Max(D(v, 362, 263), 1e-5f);

        rawApR = D(v, 159, 145) / lebarR;
        rawApL = D(v, 386, 374) / lebarL;

        rawBrow =
            (D(v, 105, 159) + D(v, 334, 386))
            / 2f / lebar;

        var cam = Camera.main;

        relHead = cam != null
            ? Quaternion.Inverse(cam.transform.rotation)
              * f.transform.rotation
            : f.transform.rotation;
    }

    void Update()
    {
        wajah = false;

        if (faceManager != null)
        {
            foreach (var f in faceManager.trackables)
            {
                if (f.trackingState != TrackingState.Tracking)
                    continue;

                var v = f.vertices;

                if (!v.IsCreated || v.Length < 468)
                    continue;

                Hitung(f, v);

                wajah = true;
                break;
            }
        }

        float k = 1f - Mathf.Exp(-25f * Time.deltaTime);

        if (wajah)
        {
            if (kalibrasi)
            {
                sM += rawMouth;
                sAL += rawApL;
                sAR += rawApR;
                sB += rawBrow;
                sN++;

                if (Time.time >= kalibSelesai && sN > 0)
                {
                    nMouth = sM / sN;
                    nApL = sAL / sN;
                    nApR = sAR / sN;
                    nBrow = sB / sN;

                    nHead = relHead;
                    headSet = true;
                    kalibrasi = false;
                }
            }

            if (!headSet)
            {
                nHead = relHead;
                headSet = true;
            }

            float tm = Mathf.Clamp01(
                (rawMouth - nMouth) / 0.2f
            );

            float bR = Mathf.Clamp01(
                (nApR - rawApR) /
                Mathf.Max(nApR * 0.7f, 0.01f)
            );

            float bL = Mathf.Clamp01(
                (nApL - rawApL) /
                Mathf.Max(nApL * 0.7f, 0.01f)
            );

            float tb = Mathf.Clamp01(
                (rawBrow - nBrow) / 0.03f
            );

            float tl = tukarMata ? bL : bR;
            float tr = tukarMata ? bR : bL;

            oMouth = Mathf.Lerp(oMouth, tm, k);
            oBL = Mathf.Lerp(oBL, tl, k);
            oBR = Mathf.Lerp(oBR, tr, k);
            oBrow = Mathf.Lerp(oBrow, tb, k);

            Vector3 e =
                (Quaternion.Inverse(nHead) * relHead)
                .eulerAngles;

            data.SetHead(
                Quaternion.Euler(
                    sPitch * Wrap(e.x),
                    sYaw * Wrap(e.y),
                    sRoll * Wrap(e.z)
                )
            );
        }
        else
        {
            oMouth = Mathf.Lerp(oMouth, 0f, k);
            oBL = Mathf.Lerp(oBL, 0f, k);
            oBR = Mathf.Lerp(oBR, 0f, k);
            oBrow = Mathf.Lerp(oBrow, 0f, k);

            data.SetHead(Quaternion.identity);
        }

        data.mouth = oMouth;
        data.blinkL = oBL;
        data.blinkR = oBR;
        data.brow = oBrow;
        data.t = Time.time;

        // Mengirim data ke ExpressionHub
        ExpressionHub.Push(data, "AR Face Tracking");
    }

    void OnGUI()
    {
        GUI.depth = -5;

        int w = Screen.width;
        int h = Screen.height;

        int fs = Mathf.RoundToInt(Mathf.Min(w, h) / 24f);

        var tb = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(fs * 0.8f)
        };

        var tf = new GUIStyle(GUI.skin.textField)
        {
            fontSize = fs,
            alignment = TextAnchor.MiddleLeft
        };

        var lb = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(fs * 0.8f)
        };

        lb.normal.textColor = Color.white;
        lb.wordWrap = true;

        float m = w * 0.02f;
        float rowH = fs * 2f;
        float y = m;

        GUI.Label(
            new Rect(m, y, w * 0.2f, rowH),
            "IP PC:",
            lb
        );

        ipText = GUI.TextField(
            new Rect(m + w * 0.17f, y, w * 0.46f, rowH),
            ipText,
            40,
            tf
        );

        if (GUI.Button(
            new Rect(w * 0.67f, y, w * 0.31f, rowH),
            "Sambungkan",
            tb))
        {
            sender.ip = ipText.Trim();

            PlayerPrefs.SetString("ip_pc", sender.ip);

            sender.Buka();
        }

        y += rowH + m * 0.5f;

        float bw = (w - 2f * m) / 5f;

        if (GUI.Button(
            new Rect(m, y, bw - 4, rowH),
            kalibrasi ? "Tahan..." : "Kalibrasi",
            tb))
        {
            kalibrasi = true;
            kalibSelesai = Time.time + 2f;

            sM = sAL = sAR = sB = 0f;
            sN = 0;
        }

        if (GUI.Button(
            new Rect(m + bw, y, bw - 4, rowH),
            "Yaw " + (sYaw > 0 ? "+" : "-"),
            tb))
            sYaw = -sYaw;

        if (GUI.Button(
            new Rect(m + 2 * bw, y, bw - 4, rowH),
            "Pitch " + (sPitch > 0 ? "+" : "-"),
            tb))
            sPitch = -sPitch;

        if (GUI.Button(
            new Rect(m + 3 * bw, y, bw - 4, rowH),
            "Roll " + (sRoll > 0 ? "+" : "-"),
            tb))
            sRoll = -sRoll;

        if (GUI.Button(
            new Rect(m + 4 * bw, y, bw - 4, rowH),
            tukarMata ? "Mata: tukar" : "Mata: biasa",
            tb))
            tukarMata = !tukarMata;

        y += rowH + m * 0.5f;

        string info =
            "MODE AR - FACE TRACKING (ARCore)\n" +
            (wajah
                ? "Wajah: TERDETEKSI"
                : "Wajah: TIDAK terdeteksi") + "\n" +
            "Mulut " + oMouth.ToString("0.00") +
            " | Kedip kiri " + oBL.ToString("0.00") +
            " kanan " + oBR.ToString("0.00") +
            " | Alis " + oBrow.ToString("0.00") + "\n" +
            (kalibrasi
                ? "KALIBRASI: tahan wajah netral 2 detik\n"
                : "") +
            (sender != null ? sender.status : "");

        GUI.Label(
            new Rect(m, y, w - 2f * m, h * 0.4f),
            info,
            lb
        );
    }
}