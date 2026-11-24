using UnityEngine;

[System.Serializable]
public class ExpressionData
{
    public float t;
    public float mouth;
    public float blinkL;
    public float blinkR;
    public float brow;

    public float[] head = new float[] { 0f, 0f, 0f, 1f };

    public Quaternion GetHead()
    {
        if (head == null || head.Length < 4)
            return Quaternion.identity;

        Quaternion q = new Quaternion(
            head[0],
            head[1],
            head[2],
            head[3]
        );

        float n = q.x * q.x + q.y * q.y +
                  q.z * q.z + q.w * q.w;

        return n < 0.5f ? Quaternion.identity : q;
    }

    public void SetHead(Quaternion q)
    {
        head = new float[]
        {
            q.x,
            q.y,
            q.z,
            q.w
        };
    }
}

public static class ExpressionHub
{
    public static ExpressionData Latest = new ExpressionData();

    public static ExpressionData Local = new ExpressionData();

    public static string Source = "SIMULASI";

    public static float LastUpdate;

    public static Vector2 Gaze;

    public static void Push(ExpressionData d, string source)
    {
        if (d == null)
            return;

        Latest = d;
        Local = d;

        Source = source;
        LastUpdate = Time.time;
    }
}