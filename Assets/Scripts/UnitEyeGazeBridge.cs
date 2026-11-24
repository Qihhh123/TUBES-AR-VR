using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnitEye;

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
    public float eyeMaxAngle = 15f;

    [Header("3. Simulasi Foveated Rendering")]
    public Material foveationMaterial;
    public bool foveationActive = true;
    [Range(0.05f, 0.8f)]
    public float fovealRadius = 0.3f;

    [Header("4. Mode Input")]
    public bool useUnitEyeWebcam = true;

    private Vector2 _currentScreenPos;
    private float _dwellTimer;
    private Button _activeButton;

    private Quaternion _eyeLeftRest = Quaternion.identity;
    private Quaternion _eyeRightRest = Quaternion.identity;
    private string _unitEyeStatus = "";
    private float _noFaceSeconds;
    private bool _noFaceWarned;
    private const float NoFaceWarnSeconds = 5f;

    void Start()
    {
        if (eyeLeft != null) _eyeLeftRest = eyeLeft.localRotation;
        if (eyeRight != null) _eyeRightRest = eyeRight.localRotation;
        _currentScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    void Update()
    {
        Vector2 targetScreenPos = ReadGazeScreenPosition();
        _currentScreenPos = Vector2.Lerp(_currentScreenPos, targetScreenPos, Time.deltaTime * smoothSpeed);

        UpdateReticleAndLabel();
        UpdateAvatarEyes(_currentScreenPos);
        UpdateDwellButtons(_currentScreenPos);
        UpdateFoveationShader(_currentScreenPos);
    }

    private Vector2 ReadGazeScreenPosition()
    {
        if (useUnitEyeWebcam)
        {
            try
            {
                if (UnitEyeAPI.IsUserPresent())
                {
                    _unitEyeStatus = "wajah terdeteksi";
                    _noFaceSeconds = 0f;
                    return UnitEyeAPI.GetGazeLocationInScreen();
                }
                _unitEyeStatus = WebCamTexture.devices.Length == 0
                    ? "tidak ada webcam"
                    : "wajah belum terdeteksi";
                WarnIfNoFace();
            }
            catch (System.InvalidOperationException)
            {
                _unitEyeStatus = "prefab UnitEye tidak ada di scene";
            }

            // Wajah belum terdeteksi: arahkan ke tengah layar.
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        return GetPointerPositionFallback();
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

    private void UpdateAvatarEyes(Vector2 screenPos)
    {
        if (eyeLeft == null && eyeRight == null)
            return;

        Vector2 viewport = Camera.main != null
            ? (Vector2)Camera.main.ScreenToViewportPoint(screenPos)
            : new Vector2(
                Screen.width > 0 ? screenPos.x / Screen.width : 0.5f,
                Screen.height > 0 ? screenPos.y / Screen.height : 0.5f
            );

        float yaw = (viewport.x - 0.5f) * 2f * eyeMaxAngle;
        float pitch = -(viewport.y - 0.5f) * 2f * eyeMaxAngle;

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