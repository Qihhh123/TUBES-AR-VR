using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class VRExpressionReceiver : MonoBehaviour
{
    [Header("Pengaturan UDP (FR-LNK-01)")]
    public int listenPort = 5055;
    public TMP_Text connectionStatusText;

    [Header("Target Bagian Avatar Rakitan (C7 PRD)")]
    public Transform headTransform;
    public Transform jawTransform;
    public Transform eyelidLeftTransform;
    public Transform eyelidRightTransform;
    public Transform browTransform;
    public bool mirrorHorizontal = false;
    public bool invertPitch = true;

    [Header("Sensitivitas Gerak Avatar")]
    public float jawOpenDistance = 0.14f;
    public float jawOpenAngle = 22f;
    public float mouthMultiplier = 1.5f;
    [Tooltip("Pengali nilai kedip (0.30 x 3.5 = 1.00 penuh)")]
    public float blinkMultiplier = 3.5f;
    public float eyelidCloseDistance = 0.065f;
    public float browMoveDistance = 0.045f;

    [Header("Penghalusan & Timeout (FR-LNK-05)")]
    [Range(0.05f, 1f)] public float smoothSpeed = 0.4f;
    public float disconnectTimeout = 1.5f;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning = false;

    private readonly object dataLock = new object();
    private FaceExpressionPacket latestPacket;
    private bool hasNewPacket = false;
    private float lastPacketTime = -999f;

    private float currentMouth, currentBlinkL, currentBlinkR, currentBrow;
    private Vector3 currentHeadEuler;

    private bool hasCalibratedHead = false;
    private Vector3 headCalibrationOffset = Vector3.zero;

    private Quaternion headInitialLocalRot;
    private Vector3 jawInitialLocalPos;
    private Vector3 jawInitialLocalEuler;
    private Vector3 eyelidLInitialLocalPos;
    private Vector3 eyelidRInitialLocalPos;
    private Vector3 browInitialLocalPos;
    private bool initializedTransforms = false;

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
        if (headTransform == null) headTransform = transform;
        CacheInitialTransforms();
        StartUDPReceiver();
    }

    private void CacheInitialTransforms()
    {
        if (headTransform != null) headInitialLocalRot = headTransform.localRotation;
        if (jawTransform != null)
        {
            jawInitialLocalPos = jawTransform.localPosition;
            jawInitialLocalEuler = jawTransform.localEulerAngles;
        }
        if (eyelidLeftTransform != null) eyelidLInitialLocalPos = eyelidLeftTransform.localPosition;
        if (eyelidRightTransform != null) eyelidRInitialLocalPos = eyelidRightTransform.localPosition;
        if (browTransform != null) browInitialLocalPos = browTransform.localPosition;
        initializedTransforms = true;
    }

    private void StartUDPReceiver()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[VRExpressionReceiver] Gagal membuka port UDP {listenPort}: {e.Message}");
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string json = Encoding.UTF8.GetString(data);
                FaceExpressionPacket parsed = JsonUtility.FromJson<FaceExpressionPacket>(json);
                if (parsed != null)
                {
                    lock (dataLock)
                    {
                        latestPacket = parsed;
                        hasNewPacket = true;
                    }
                }
            }
            catch (SocketException) { }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRExpressionReceiver] Error UDP: {e.Message}");
            }
        }
    }

    void Update()
    {
        if (!initializedTransforms) CacheInitialTransforms();

        // Tekan tombol R di keyboard untuk reset titik nol posisi kepala
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            hasCalibratedHead = false;
        }

        FaceExpressionPacket packetToApply = null;
        lock (dataLock)
        {
            if (hasNewPacket && latestPacket != null)
            {
                packetToApply = latestPacket;
                hasNewPacket = false;
                lastPacketTime = Time.time;
            }
        }

        bool isConnected = (Time.time - lastPacketTime) <= disconnectTimeout;

        if (packetToApply != null)
        {
            float targetMouth = Mathf.Clamp01(packetToApply.mouth * mouthMultiplier);
            float targetBlinkL = Mathf.Clamp01(packetToApply.blinkL * blinkMultiplier);
            float targetBlinkR = Mathf.Clamp01(packetToApply.blinkR * blinkMultiplier);
            float targetBrow = Mathf.Clamp01(packetToApply.brow);

            currentMouth = Mathf.Lerp(currentMouth, targetMouth, smoothSpeed);
            currentBlinkL = Mathf.Lerp(currentBlinkL, targetBlinkL, smoothSpeed);
            currentBlinkR = Mathf.Lerp(currentBlinkR, targetBlinkR, smoothSpeed);
            currentBrow = Mathf.Lerp(currentBrow, targetBrow, smoothSpeed);

            if (packetToApply.head != null && packetToApply.head.Length >= 3)
            {
                Vector3 rawHead = new Vector3(packetToApply.head[0], packetToApply.head[1], packetToApply.head[2]);

                if (!hasCalibratedHead)
                {
                    headCalibrationOffset = rawHead;
                    hasCalibratedHead = true;
                }

                float pitch = Mathf.Clamp(Mathf.DeltaAngle(headCalibrationOffset.x, rawHead.x), -45f, 45f);
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(headCalibrationOffset.y, rawHead.y), -60f, 60f);
                float roll = Mathf.Clamp(Mathf.DeltaAngle(headCalibrationOffset.z, rawHead.z), -45f, 45f);

                if (invertPitch) pitch = -pitch;

                if (mirrorHorizontal)
                {
                    yaw = -yaw;
                    roll = -roll;
                }

                Vector3 targetEuler = new Vector3(pitch, yaw, roll);
                currentHeadEuler = Vector3.Lerp(currentHeadEuler, targetEuler, smoothSpeed);
            }
        }
        else if (!isConnected)
        {
            hasCalibratedHead = false;
            currentMouth = Mathf.Lerp(currentMouth, 0f, Time.deltaTime * 4f);
            currentBlinkL = Mathf.Lerp(currentBlinkL, 0f, Time.deltaTime * 4f);
            currentBlinkR = Mathf.Lerp(currentBlinkR, 0f, Time.deltaTime * 4f);
            currentBrow = Mathf.Lerp(currentBrow, 0f, Time.deltaTime * 4f);
            currentHeadEuler = Vector3.Lerp(currentHeadEuler, Vector3.zero, Time.deltaTime * 4f);
        }

        UpdateConnectionUI(isConnected);
        ApplyExpressionToAvatar();
    }

    private void ApplyExpressionToAvatar()
    {
        if (headTransform != null)
        {
            headTransform.localRotation = headInitialLocalRot * Quaternion.Euler(currentHeadEuler);
        }

        if (jawTransform != null)
        {
            Vector3 jawPos = jawInitialLocalPos;
            jawPos.y -= currentMouth * jawOpenDistance;
            jawTransform.localPosition = jawPos;

            Vector3 jawRot = jawInitialLocalEuler;
            jawRot.x += currentMouth * jawOpenAngle;
            jawTransform.localEulerAngles = jawRot;
        }

        float blinkLeftVal = mirrorHorizontal ? currentBlinkR : currentBlinkL;
        float blinkRightVal = mirrorHorizontal ? currentBlinkL : currentBlinkR;

        if (eyelidLeftTransform != null)
        {
            Vector3 leftPos = eyelidLInitialLocalPos;
            leftPos.y -= blinkLeftVal * eyelidCloseDistance;
            eyelidLeftTransform.localPosition = leftPos;
        }

        if (eyelidRightTransform != null)
        {
            Vector3 rightPos = eyelidRInitialLocalPos;
            rightPos.y -= blinkRightVal * eyelidCloseDistance;
            eyelidRightTransform.localPosition = rightPos;
        }

        if (browTransform != null)
        {
            Vector3 browPos = browInitialLocalPos;
            browPos.y += (currentBrow - 0.5f) * 2f * browMoveDistance;
            browTransform.localPosition = browPos;
        }
    }

    private void UpdateConnectionUI(bool isConnected)
    {
        if (connectionStatusText == null) return;

        if (isConnected)
        {
            connectionStatusText.text = $"Status: Tersambung (Live UDP)\nMulut: {currentMouth:F2} | Kedip: {currentBlinkL:F2} | Kepala: {currentHeadEuler.y:F0}°";
            connectionStatusText.color = Color.green;
        }
        else
        {
            connectionStatusText.text = "Status: Menunggu Data AR...";
            connectionStatusText.color = Color.red;
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        try { udpClient?.Close(); } catch { }
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
    }
}