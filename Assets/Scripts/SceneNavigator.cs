using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    public void BukaModeAR()
    {
        SceneManager.LoadScene("FaceScene");
    }

    public void BukaModeVR()
    {
        SceneManager.LoadScene("VR_Scene");
    }

    public void KembaliKeMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}