using UnityEngine;
using UnityEngine.InputSystem;

public class SimulatedExpressionInput : MonoBehaviour
{
    public bool aktif = true;
    float mouth, blinkL, blinkR, brow, yaw, pitch;

    void Update()
    {
        if (!aktif) return;
        var k = Keyboard.current;
        if (k == null) return;
        float dt = Time.deltaTime;

        mouth  = Mathf.MoveTowards(mouth,  k.spaceKey.isPressed ? 1f : 0f, 6f * dt);
        blinkL = Mathf.MoveTowards(blinkL, k.qKey.isPressed ? 1f : 0f, 12f * dt);
        blinkR = Mathf.MoveTowards(blinkR, k.eKey.isPressed ? 1f : 0f, 12f * dt);
        brow   = Mathf.MoveTowards(brow,   k.rKey.isPressed ? 1f : 0f, 6f * dt);

        float ty = (k.leftArrowKey.isPressed ? 1f : 0f) - (k.rightArrowKey.isPressed ? 1f : 0f);
        float tp = (k.downArrowKey.isPressed ? 1f : 0f) - (k.upArrowKey.isPressed ? 1f : 0f);
        yaw   = Mathf.MoveTowards(yaw,   ty * 30f, 90f * dt);
        pitch = Mathf.MoveTowards(pitch, tp * 20f, 90f * dt);

        var d = new ExpressionData
        {
            t = Time.time, mouth = mouth, blinkL = blinkL, blinkR = blinkR, brow = brow
        };
        d.SetHead(Quaternion.Euler(pitch, yaw, 0f));
        ExpressionHub.Push(d, "SIMULASI (keyboard)");
    }
}