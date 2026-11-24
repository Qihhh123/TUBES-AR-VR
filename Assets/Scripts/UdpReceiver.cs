using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UdpReceiver : MonoBehaviour
{
    public int port = 9050;
    public static float LastPacket = -100f;

    UdpClient client;
    Thread thread;
    volatile bool running;
    readonly object kunci = new object();
    string pesan;
    string ipLokal = "-";

    void Start()
    {
        ipLokal = CariIp();
        try
        {
            client = new UdpClient(port);
            running = true;
            thread = new Thread(Loop) { IsBackground = true };
            thread.Start();
        }
        catch (Exception e) { Debug.LogWarning("UDP gagal dibuka: " + e.Message); }
    }

    static string CariIp()
    {
        try
        {
            foreach (var a in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (a.AddressFamily == AddressFamily.InterNetwork && !a.ToString().StartsWith("127."))
                    return a.ToString();
        }
        catch { }
        return "tidak ketemu";
    }

    void Loop()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                var data = client.Receive(ref ep);
                lock (kunci) pesan = Encoding.UTF8.GetString(data);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
        }
    }

    void Update()
    {
        string p;
        lock (kunci) { p = pesan; pesan = null; }
        if (p == null) return;
        try
        {
            var d = JsonUtility.FromJson<ExpressionData>(p);
            LastPacket = Time.time;
            ExpressionHub.Push(d, "UDP (jaringan lokal) port " + port);
        }
        catch { }
    }

    void OnGUI()
    {
        bool ok = Time.time - LastPacket < 3f;
        var s = new GUIStyle(GUI.skin.label);
        s.fontSize = Mathf.RoundToInt(Screen.height / 42f);
        s.normal.textColor = ok ? Color.green : new Color(1f, 0.55f, 0.4f);
        GUI.Label(new Rect(20, Screen.height * 0.88f, Screen.width * 0.8f, Screen.height * 0.1f),
            "UDP: " + (ok ? "TERSAMBUNG" : "TERPUTUS") + " | port " + port + " | IP PC: " + ipLokal, s);
    }

    void OnDestroy()
    {
        running = false;
        if (client != null) client.Close();
    }
}
