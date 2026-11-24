using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using TMPro;

public class ARExpressionSender : MonoBehaviour
{
    [Header("Pengaturan Jaringan UDP")]
    public string targetIP = "192.168.1.30";
    public int targetPort = 5055;
    public float sendRateHz = 30f;

    [Header("Penghalusan & Kalibrasi")]
    [Range(0.05f, 1f)] public float smoothing = 0.4f;
    public TMP_Text debugText;

    private ARFaceManager faceManager;
    private ARFace localArFace;
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private float sendInterval;
    private float timer;

    private float smoothMouth, smoothBlinkL, smoothBlinkR, smoothBrow;
    private string currentSavedIP = "";

    [Serializable]
    public class FaceExpressionPacket
    {
        public long t;
        public float mouth;
        public float blinkL;
        public float blinkR;
        public float brow;
        public float[] head;
    }

    void Start()
    {
        localArFace = GetComponentInParent<ARFace>();
        if (localArFace == null)
        {
            faceManager = FindFirstObjectByType<ARFaceManager>();
        }

        udpClient = new UdpClient();
        sendInterval = 1f / Mathf.Max(1f, sendRateHz);
        PerbaruiEndpointDariPrefs();
    }

    private void PerbaruiEndpointDariPrefs()
    {
        string savedIP = PlayerPrefs.GetString("TargetIP", targetIP).Trim();
        if (!string.IsNullOrEmpty(savedIP) && savedIP != currentSavedIP)
        {
            if (IPAddress.TryParse(savedIP, out IPAddress parsedIP))
            {
                currentSavedIP = savedIP;
                targetIP = savedIP;
                remoteEndPoint = new IPEndPoint(parsedIP, targetPort);
            }
        }
        else if (remoteEndPoint == null && IPAddress.TryParse(targetIP, out IPAddress defaultIP))
        {
            remoteEndPoint = new IPEndPoint(defaultIP, targetPort);
        }
    }

    private ARFace GetActiveARFace()
    {
        if (localArFace != null) return localArFace;

        if (faceManager == null)
            faceManager = FindFirstObjectByType<ARFaceManager>();

        if (faceManager != null)
        {
            foreach (var face in faceManager.trackables)
            {
                if (face != null && face.gameObject.activeInHierarchy)
                    return face;
            }
        }
        return null;
    }

    void Update()
    {
        PerbaruiEndpointDariPrefs();
        if (remoteEndPoint == null) return;

        timer += Time.deltaTime;
        if (timer < sendInterval) return;
        timer = 0f;

        ARFace activeFace = GetActiveARFace();
        if (activeFace == null) return;

        // Ambil rotasi langsung dari transform ARFace (bukan child filter yang offset-nya beda-beda)
        Vector3 euler = activeFace.transform.rotation.eulerAngles;
        float pitch = NormalizeAngle(euler.x);
        float yaw = NormalizeAngle(euler.y);
        float roll = NormalizeAngle(euler.z);

        var vertices = activeFace.vertices;
        int vertCount = vertices.IsCreated ? vertices.Length : 0;

        if (vertCount > 454)
        {
            float faceWidth = Mathf.Max(0.01f, Vector3.Distance(vertices[234], vertices[454]));

            // Mulut: bibir atas (13) & bibir bawah (14)
            float mouthRatio = Vector3.Distance(vertices[13], vertices[14]) / faceWidth;
            float rawMouth = Mathf.Clamp01(Mathf.InverseLerp(0.01f, 0.14f, mouthRatio));

            // Mata kiri (159 & 145) dan kanan (386 & 374)
            float leftEyeRatio = Vector3.Distance(vertices[159], vertices[145]) / faceWidth;
            float rawBlinkL = Mathf.Clamp01(1f - Mathf.InverseLerp(0.025f, 0.07f, leftEyeRatio));

            float rightEyeRatio = Vector3.Distance(vertices[386], vertices[374]) / faceWidth;
            float rawBlinkR = Mathf.Clamp01(1f - Mathf.InverseLerp(0.025f, 0.07f, rightEyeRatio));

            // Alis kiri (105 ke 159)
            float browRatio = Vector3.Distance(vertices[105], vertices[159]) / faceWidth;
            float rawBrow = Mathf.Clamp01(Mathf.InverseLerp(0.08f, 0.15f, browRatio));

            smoothMouth = Mathf.Lerp(smoothMouth, rawMouth, smoothing);
            smoothBlinkL = Mathf.Lerp(smoothBlinkL, rawBlinkL, smoothing);
            smoothBlinkR = Mathf.Lerp(smoothBlinkR, rawBlinkR, smoothing);
            smoothBrow = Mathf.Lerp(smoothBrow, rawBrow, smoothing);
        }

        if (debugText != null)
        {
            debugText.text = $"V:{vertCount} M:{smoothMouth:F2} B:{smoothBlinkL:F2} Y:{yaw:F0}";
        }

        FaceExpressionPacket packet = new FaceExpressionPacket
        {
            t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            mouth = (float)Math.Round(smoothMouth, 3),
            blinkL = (float)Math.Round(smoothBlinkL, 3),
            blinkR = (float)Math.Round(smoothBlinkR, 3),
            brow = (float)Math.Round(smoothBrow, 3),
            head = new float[]
            {
                (float)Math.Round(pitch, 2),
                (float)Math.Round(yaw, 2),
                (float)Math.Round(roll, 2)
            }
        };

        try
        {
            string json = JsonUtility.ToJson(packet);
            byte[] data = Encoding.UTF8.GetBytes(json);
            udpClient.Send(data, data.Length, remoteEndPoint);
        }
        catch { }
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    void OnDestroy()
    {
        udpClient?.Close();
    }
}