using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class VRGazeAndFoveationManager : MonoBehaviour
{
    [Header("1. Pengaturan Gaze / Eye Tracking")]
    public Camera vrCamera;
    public RectTransform gazeReticle;
    public Image gazeProgressRing;
    public TMP_Text gazeSourceLabel;
    public bool useMouseSimulation = true;
    public float dwellTimeRequired = 1.2f;

    [Header("2. Mata Avatar Mengikuti Gaze")]
    public Transform eyeLeft;
    public Transform eyeRight;

    [Header("3. Simulasi Foveated Rendering")]
    public bool foveationActive = true;
    [Range(0.15f, 0.65f)] public float fovealRadius = 0.3f;
    public RectTransform fovealMaskUI;
    public Image peripheralOverlay;
    public TMP_Text foveationLabel;

    [Header("4. Panel Performa")]
    public TMP_Text performanceText;

    private Button currentHoveredButton;
    private float currentDwellTimer = 0f;
    private float fpsTimer = 0f;
    private int frameCount = 0;

    void Start()
    {
        if (vrCamera == null) vrCamera = Camera.main;
        UpdateFoveationVisual();
    }

    void Update()
    {
        HandleGazeAndDwellUI();
        HandleFoveatedSimulation();
        UpdatePerformanceStats();
    }

    private void HandleGazeAndDwellUI()
    {
        Vector2 screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (useMouseSimulation && Mouse.current != null)
        {
            screenPoint = Mouse.current.position.ReadValue();
        }

        if (gazeReticle != null)
            gazeReticle.position = screenPoint;

        if (gazeSourceLabel != null)
        {
            string modeStr = useMouseSimulation ? "Simulasi Mouse" : "Simulasi Arah Kepala (XR)";
            gazeSourceLabel.text = $"Sumber Gaze: {modeStr} [Simulasi - Tanpa Sensor Mata]";
        }

        if (vrCamera == null) return;

        Ray ray = vrCamera.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out RaycastHit hit, 20f))
        {
            if (eyeLeft != null) eyeLeft.LookAt(hit.point);
            if (eyeRight != null) eyeRight.LookAt(hit.point);

            Button btn = hit.collider.GetComponent<Button>();
            if (btn != null && btn.interactable)
            {
                if (currentHoveredButton != btn)
                {
                    currentHoveredButton = btn;
                    currentDwellTimer = 0f;
                }

                currentDwellTimer += Time.deltaTime;
                if (gazeProgressRing != null)
                    gazeProgressRing.fillAmount = Mathf.Clamp01(currentDwellTimer / dwellTimeRequired);

                if (currentDwellTimer >= dwellTimeRequired)
                {
                    btn.onClick.Invoke();
                    currentDwellTimer = 0f;
                    if (gazeProgressRing != null) gazeProgressRing.fillAmount = 0f;
                }
                return;
            }
        }

        currentHoveredButton = null;
        currentDwellTimer = 0f;
        if (gazeProgressRing != null) gazeProgressRing.fillAmount = 0f;
    }

    private void HandleFoveatedSimulation()
    {
        if (fovealMaskUI != null && gazeReticle != null)
        {
            fovealMaskUI.position = gazeReticle.position;
            float size = Screen.height * fovealRadius * 1.5f;
            fovealMaskUI.sizeDelta = new Vector2(size, size);
            fovealMaskUI.gameObject.SetActive(foveationActive);
        }

        if (peripheralOverlay != null)
            peripheralOverlay.gameObject.SetActive(foveationActive);

        if (foveationLabel != null)
        {
            string status = foveationActive ? "<color=green>ON</color>" : "<color=yellow>OFF</color>";
            foveationLabel.text = $"Foveated Rendering: {status} (Simulasi Visual & Rasio Piksel)";
        }
    }

    private void UpdatePerformanceStats()
    {
        frameCount++;
        fpsTimer += Time.unscaledDeltaTime;

        if (fpsTimer >= 1f)
        {
            float fps = frameCount / fpsTimer;
            float frameMs = 1000f / Mathf.Max(fps, 1f);
            float pixelRatio = foveationActive ? Mathf.Clamp(fovealRadius * 2.2f, 0.35f, 0.85f) * 100f : 100f;

            if (performanceText != null)
            {
                performanceText.text =
                    $"<b>Panel Performa VR</b>\n" +
                    $"FPS: {fps:F1} FPS\n" +
                    $"Frame Time: {frameMs:F2} ms\n" +
                    $"Rasio Piksel Penuh: {pixelRatio:F0}%";
            }

            frameCount = 0;
            fpsTimer = 0f;
        }
    }

    public void ToggleFoveation()
    {
        foveationActive = !foveationActive;
        UpdateFoveationVisual();
    }

    public void ToggleGazeSource()
    {
        useMouseSimulation = !useMouseSimulation;
    }

    public void SetFovealRadius(float value)
    {
        fovealRadius = value;
    }

    private void UpdateFoveationVisual()
    {
        if (peripheralOverlay != null)
            peripheralOverlay.gameObject.SetActive(foveationActive);
    }
}