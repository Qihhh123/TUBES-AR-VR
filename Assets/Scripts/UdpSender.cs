using System.Net.Sockets;
using System.Text;
using UnityEngine;
public class UdpSender : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 9050;
    public float hz = 30f;
    public string status = "";
    UdpClient client;
    float berikut;
    void Start()
    {
        Buka();
    }
    public void Buka()
    {
        Tutup();
        try
        {
            client = new UdpClient();
            client.Connect(ip, port);
            status = "Mengirim ke " + ip + ":" + port;
            Debug.Log(status);
        }
        catch (System.Exception e)
        {
            status = "Gagal: " + e.Message;
            client = null;
            Debug.LogError(status);
        }
    }
    void Update()
    {
        if (client == null || Time.time < berikut)
            return;
        berikut = Time.time + 1f / Mathf.Max(hz, 1f);
        try
        {
            ExpressionData d = ExpressionHub.Latest;
            d.t = Time.time;
            byte[] b = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(d)
            );
            client.Send(b, b.Length);
        }
        catch (System.Exception e)
        {
            status = "Kirim gagal: " + e.Message;
        }
    }
    void Tutup()
    {
        if (client != null)
        {
            client.Close();
            client = null;
        }
    }
    void OnDestroy()
    {
        Tutup();
    }
}