using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnitEye;
using Mediapipe.Unity.FaceMesh;

public class UnitEyeGazeBridge : MonoBehaviour
{
    [Header("1. Gaze & UI Dwell")]
    public RectTransform gazeReticle;
    public Image progressRing;
    public TMP_Text gazeSourceLabel;
    public Button[] dwellButtons;
    public float dwellTimeRequired = 1.2f;
    public float smoothSpeed = 15f;

    [Header("2. Mata Avatar")]
    public Transform eyeLeft;
    public Transform eyeRight;
    public float eyeMaxAngle = 35f;
    [Tooltip("Saat wajah terdeteksi UnitEye, pupil avatar mengikuti posisi iris user dari landmark FaceMesh (tanpa kalibrasi). Bila mati, pupil mengikuti titik gaze di layar.")]
    public bool eyesFollowIris = true;
    [Tooltip("Penguat geser iris (relatif lebar mata) ke sudut pupil; naikkan bila gerak pupil terlalu kecil.")]
    public float irisGainX = 8f;
    public float irisGainY = 12f;
    [Tooltip("Centang bila pupil avatar bergerak berlawanan arah dengan mata user.")]
    public bool invertIrisX = false;
    public bool invertIrisY = false;

    [Header("3. Simulasi Foveated Rendering")]
    public Material foveationMaterial;
    public bool foveationActive = true;
    [Range(0.05f, 0.8f)]
    public float fovealRadius = 0.3f;

    [Header("4. Mode Input")]
    public bool useUnitEyeWebcam = true;
    [Tooltip("Sembunyikan preview webcam layar penuh UnitEye supaya avatar terlihat; pelacakan wajah tetap berjalan. Tombol V menampilkan/menyembunyikan.")]
    public bool hideWebcamPreview = true;

    private Vector2 _currentScreenPos;
    private float _dwellTimer;
    private Button _activeButton;

    private Quaternion _eyeLeftRest = Quaternion.identity;
    private Quaternion _eyeRightRest = Quaternion.identity;
    private string _unitEyeStatus = "";
    private float _noFaceSeconds;
    private bool _noFaceWarned;
    private const float NoFaceWarnSeconds = 5f;

    private Vector2 _eyeDirection;
    private bool _hasIrisDirection;
    private Vector2 _irisDirection;
    private FaceMeshSolution _faceMesh;
    private float _nextFaceMeshSearch;
    private readonly float[] _irisOffsets = new float[4];
    private Vector2 _irisNeutralSum;
    private int _irisNeutralCount;
    private const int IrisNeutralSamples = 30;

    void Start()
    {
        if (eyeLeft != null) _eyeLeftRest = eyeLeft.localRotation;
        if (eyeRight != null) _eyeRightRest = eyeRight.localRotation;
        _currentScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            RecenterIris();
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            hideWebcamPreview = !hideWebcamPreview;
        ApplyWebcamPreview();

        Vector2 targetScreenPos = ReadGazeScreenPosition();
        float t = Time.deltaTime * smoothSpeed;
        _currentScreenPos = Vector2.Lerp(_currentScreenPos, targetScreenPos, t);
        Vector2 targetEyeDirection = _hasIrisDirection ? _irisDirection : ScreenToEyeDirection(targetScreenPos);
        _eyeDirection = Vector2.Lerp(_eyeDirection, targetEyeDirection, t);

        UpdateReticleAndLabel();
        UpdateAvatarEyes(_eyeDirection);
        UpdateDwellButtons(_currentScreenPos);
        UpdateFoveationShader(_currentScreenPos);
    }

    private Vector2 ReadGazeScreenPosition()
    {
        _hasIrisDirection = false;
        if (useUnitEyeWebcam)
        {
            try
            {
                // Objek UnitEye yang dimatikan menyimpan landmark terakhir, jadi IsUserPresent tetap true
                // dengan data beku; anggap tidak ada pelacakan.
                if (!UnitEyeAPI.GetGazeReference().isActiveAndEnabled || !FaceMeshRunning())
                {
                    _unitEyeStatus = "UnitEye nonaktif: centang lagi UnitEyeUsingHomulerMediapipe, preview webcam sudah disembunyikan otomatis";
                }
                else if (UnitEyeAPI.IsUserPresent())
                {
                    _noFaceSeconds = 0f;
                    _hasIrisDirection = eyesFollowIris && TryReadIrisDirection(out _irisDirection);
                    _unitEyeStatus = _hasIrisDirection
                        ? "wajah terdeteksi, mata avatar mengikuti iris (C: atur ulang titik tengah)"
                        : "wajah terdeteksi";
                    return UnitEyeAPI.GetGazeLocationInScreen();
                }
                else
                {
                    _unitEyeStatus = WebCamTexture.devices.Length == 0
                        ? "tidak ada webcam"
                        : "wajah belum terdeteksi";
                    WarnIfNoFace();
                    if (_noFaceSeconds > 1f)
                        RecenterIris();
                }
            }
            catch (System.InvalidOperationException)
            {
                _unitEyeStatus = "UnitEye tidak ada atau nonaktif sejak awal (centang UnitEyeUsingHomulerMediapipe)";
            }

            // Wajah belum terdeteksi: sementara pakai mouse supaya reticle, foveation, dan mata avatar tetap jalan.
            _unitEyeStatus += " (sementara pakai mouse)";
            return GetPointerPositionFallback();
        }

        return GetPointerPositionFallback();
    }
    // Geser iris rata-rata kedua mata terhadap titik tengah sudut mata (satuan lebar mata),
    // dikurangi posisi netral yang direkam dari sampel pertama sesudah wajah terdeteksi.
    private bool TryReadIrisDirection(out Vector2 direction)
    {
        direction = Vector2.zero;
        if (!FaceMeshRunning() || _faceMesh.FaceLandmarks == null)
            return false;

        HomulerFunctions.FillIrisFeatures(_faceMesh.FaceLandmarks, _irisOffsets, 0);
        Vector2 offset = new Vector2(
            (_irisOffsets[0] + _irisOffsets[2]) * 0.5f,
            (_irisOffsets[1] + _irisOffsets[3]) * 0.5f);

        if (_irisNeutralCount < IrisNeutralSamples)
        {
            _irisNeutralSum += offset;
            _irisNeutralCount++;
            return true;
        }

        // Gambar webcam tidak dicerminkan dan sumbu Y landmark mengarah ke bawah: balik kedua sumbu
        // supaya avatar bergerak seperti bayangan cermin user (lihat kanan -> pupil ke kanan layar).
        Vector2 delta = offset - _irisNeutralSum / _irisNeutralCount;
        float x = -delta.x * irisGainX * (invertIrisX ? -1f : 1f);
        float y = -delta.y * irisGainY * (invertIrisY ? -1f : 1f);
        direction = new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
        return true;
    }

    private bool FaceMeshRunning()
    {
        if (_faceMesh == null && Time.unscaledTime >= _nextFaceMeshSearch)
        {
            _faceMesh = FindAnyObjectByType<FaceMeshSolution>();
            _nextFaceMeshSearch = Time.unscaledTime + 1f;
        }
        return _faceMesh != null && _faceMesh.isActiveAndEnabled;
    }

    private void ApplyWebcamPreview()
    {
        if (useUnitEyeWebcam && FaceMeshRunning())
            _faceMesh.IsRendering = !hideWebcamPreview;
    }

    public void RecenterIris()
    {
        _irisNeutralSum = Vector2.zero;
        _irisNeutralCount = 0;
    }

    private void WarnIfNoFace()
    {
        _noFaceSeconds += Time.unscaledDeltaTime;
        if (_noFaceWarned || _noFaceSeconds < NoFaceWarnSeconds)
            return;

        _noFaceWarned = true;
        var names = new System.Text.StringBuilder();
        foreach (var device in WebCamTexture.devices)
            names.Append(names.Length > 0 ? ", " : "").Append(device.name);
        Debug.LogWarning($"[UnitEyeGazeBridge] Wajah belum terdeteksi selama {NoFaceWarnSeconds:F0} detik. " +
                         $"Webcam terdeteksi ({WebCamTexture.devices.Length}): {names}. " +
                         "Pilih webcam lain lewat Gaze UI UnitEye (Next Cam), pastikan wajah terang dan berjarak sekitar 50 cm.");
    }

    private Vector2 GetPointerPositionFallback()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private void UpdateReticleAndLabel()
    {
        if (gazeReticle != null)
            gazeReticle.position = _currentScreenPos;

        if (gazeSourceLabel != null)
        {
            gazeSourceLabel.text = useUnitEyeWebcam
                ? $"Sumber Gaze: Webcam (UnitEye) - {_unitEyeStatus}"
                : "Sumber Gaze: Simulasi Mouse / Simulator";
        }
    }

    // Titik layar -> arah pupil dalam rentang -1..1 per sumbu (0 = tengah layar).
    private Vector2 ScreenToEyeDirection(Vector2 screenPos)
    {
        Vector2 viewport = Camera.main != null
            ? (Vector2)Camera.main.ScreenToViewportPoint(screenPos)
            : new Vector2(
                Screen.width > 0 ? screenPos.x / Screen.width : 0.5f,
                Screen.height > 0 ? screenPos.y / Screen.height : 0.5f
            );
        return new Vector2(
            Mathf.Clamp((viewport.x - 0.5f) * 2f, -1f, 1f),
            Mathf.Clamp((viewport.y - 0.5f) * 2f, -1f, 1f));
    }

    private void UpdateAvatarEyes(Vector2 direction)
    {
        if (eyeLeft == null && eyeRight == null)
            return;

        float yaw = direction.x * eyeMaxAngle;
        float pitch = -direction.y * eyeMaxAngle;

        // Avatar yang menghadap kamera (kepala diputar 180 derajat) membalik sumbu kiri-kanan;
        // balik yaw supaya pupil bergerak ke arah layar yang sama dengan titik gaze.
        Transform head = eyeLeft != null ? eyeLeft.parent : eyeRight.parent;
        if (Camera.main != null && head != null && Vector3.Dot(head.forward, Camera.main.transform.forward) < 0f)
            yaw = -yaw;

        Quaternion eyeRot = Quaternion.Euler(pitch, yaw, 0f);
        if (eyeLeft != null) eyeLeft.localRotation = _eyeLeftRest * eyeRot;
        if (eyeRight != null) eyeRight.localRotation = _eyeRightRest * eyeRot;
    }

    private void UpdateDwellButtons(Vector2 screenPos)
    {
        if (dwellButtons == null || dwellButtons.Length == 0)
            return;

        Button hovered = null;
        foreach (Button btn in dwellButtons)
        {
            if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable)
                continue;

            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
            {
                hovered = btn;
                break;
            }
        }

        if (hovered == null)
        {
            ResetDwell();
            return;
        }

        if (_activeButton != hovered)
        {
            _activeButton = hovered;
            _dwellTimer = 0f;
        }

        _dwellTimer += Time.deltaTime;
        if (progressRing != null)
            progressRing.fillAmount = Mathf.Clamp01(_dwellTimer / dwellTimeRequired);

        if (_dwellTimer >= dwellTimeRequired)
        {
            Button buttonToTrigger = _activeButton;
            ResetDwell();
            buttonToTrigger.onClick.Invoke();
        }
    }

    private void ResetDwell()
    {
        _dwellTimer = 0f;
        _activeButton = null;
        if (progressRing != null)
            progressRing.fillAmount = 0f;
    }

    private void UpdateFoveationShader(Vector2 screenPos)
    {
        if (foveationMaterial == null)
            return;

        float normX = Screen.width > 0 ? Mathf.Clamp01(screenPos.x / Screen.width) : 0.5f;
        float normY = Screen.height > 0 ? Mathf.Clamp01(screenPos.y / Screen.height) : 0.5f;

        foveationMaterial.SetVector("_GazeCenter", new Vector4(normX, normY, 0f, 0f));
        foveationMaterial.SetFloat("_FovealRadius", fovealRadius);
        foveationMaterial.SetFloat("_FoveationEnabled", foveationActive ? 1f : 0f);
    }

    public void SetFoveationActive(bool active)
    {
        foveationActive = active;
    }

    public void SetFovealRadius(float radius)
    {
        fovealRadius = radius;
    }

    public void ToggleInputMode()
    {
        useUnitEyeWebcam = !useUnitEyeWebcam;
    }
}