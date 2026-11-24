using System.Net;
using UnityEngine;
using TMPro;

public class IPConfigUI : MonoBehaviour
{
    public TMP_InputField ipInputField;
    public TMP_Text statusText;

    void Start()
    {
        // Tampilkan IP tersimpan di kotak ketik saat aplikasi dibuka
        string savedIP = PlayerPrefs.GetString(ARExpressionSender.TargetIPKey, ARExpressionSender.DefaultTargetIP);
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
            if (!IPAddress.TryParse(ipBaru, out _))
            {
                if (statusText != null) statusText.text = "IP tidak valid: " + ipBaru;
                return;
            }
            PlayerPrefs.SetString(ARExpressionSender.TargetIPKey, ipBaru);
            PlayerPrefs.Save();

            if (statusText != null)
            {
                statusText.text = "Target IP: " + ipBaru;
            }
        }
    }
}