using UnityEngine;
using TMPro;

public class IPConfigUI : MonoBehaviour
{
    public TMP_InputField ipInputField;
    public TMP_Text statusText;

    void Start()
    {
        // Tampilkan IP tersimpan di kotak ketik saat aplikasi dibuka
        string savedIP = PlayerPrefs.GetString("TargetIP", "192.168.1.10");
        if (ipInputField != null)
        {
            ipInputField.text = savedIP;
        }
    }

    // Fungsi ini akan dipanggil saat tombol "Sambungkan" ditekan
    public void SimpanIP()
    {
        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
        {
            string ipBaru = ipInputField.text.Trim();
            PlayerPrefs.SetString("TargetIP", ipBaru);
            PlayerPrefs.Save();

            if (statusText != null)
            {
                statusText.text = "Target IP: " + ipBaru;
            }
        }
    }
}