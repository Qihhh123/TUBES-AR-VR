using UnityEngine;
using UnityEngine.SceneManagement;

// Tombol "Menu" kecil di pojok kanan bawah pada setiap mode (FR-NAV-02). Dibuat otomatis.
public class BackToMenu : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Daftar()
    {
        SceneManager.sceneLoaded += (s, m) => Buat();
        Buat();
    }

    static void Buat()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu") return;
        if (FindAnyObjectByType<BackToMenu>() != null) return;
        new GameObject("BackToMenu").AddComponent<BackToMenu>();
    }

    void OnGUI()
    {
        GUI.depth = -10;
        int w = Screen.width, h = Screen.height;
        var st = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Mathf.Min(w, h) / 28f) };
        if (GUI.Button(new Rect(w - w * 0.22f - 10, h - h * 0.07f - 10, w * 0.22f, h * 0.07f), "< Menu", st))
        {
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
            else Debug.LogWarning("Scene MainMenu belum ada di Build Profiles (File > Build Profiles > Scene List).");
        }
    }
}
