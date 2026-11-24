using UnityEngine;

public class AvatarFace : MonoBehaviour
{
    public float kehalusan = 18f;

    Transform headRoot, browL, browR, lidL, lidR, mouthT, pupilL, pupilR;
    float mouth, blinkL, blinkR, brow;
    Quaternion rot = Quaternion.identity;

    const float EyeX = 0.19f, EyeY = 0.12f, EyeTop = 0.23f;

    static Material Mat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else m.color = c;
        return m;
    }

    static Transform Part(PrimitiveType type, string nama, Transform parent,
                          Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = nama;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g.transform;
    }

    void Awake()
    {
        var skin  = Mat(new Color(0.95f, 0.76f, 0.62f));
        var white = Mat(Color.white);
        var dark  = Mat(new Color(0.08f, 0.06f, 0.06f));
        var red   = Mat(new Color(0.5f, 0.08f, 0.1f));

        headRoot = new GameObject("Kepala").transform;
        headRoot.SetParent(transform, false);

        Part(PrimitiveType.Sphere, "Wajah", headRoot, Vector3.zero, Vector3.one, skin);
        Part(PrimitiveType.Sphere, "Hidung", headRoot, new Vector3(0, -0.03f, 0.5f),
             new Vector3(0.1f, 0.1f, 0.12f), skin);

        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0) ? EyeX : -EyeX;
            var bola = Part(PrimitiveType.Sphere, "Mata", headRoot,
                            new Vector3(x, EyeY, 0.43f), Vector3.one * 0.2f, white);
            var pupil = Part(PrimitiveType.Sphere, "Pupil", bola,
                             new Vector3(0, 0, 0.4f), Vector3.one * 0.55f, dark);
            var lid = Part(PrimitiveType.Cube, "Kelopak", headRoot,
                           new Vector3(x, EyeTop, 0.54f), new Vector3(0.24f, 0.001f, 0.05f), skin);
            var alis = Part(PrimitiveType.Cube, "Alis", headRoot,
                            new Vector3(x, 0.32f, 0.34f), new Vector3(0.22f, 0.035f, 0.04f), dark);
            if (i == 0) { pupilL = pupil; lidL = lid; browL = alis; }
            else        { pupilR = pupil; lidR = lid; browR = alis; }
        }

        mouthT = Part(PrimitiveType.Cube, "Mulut", headRoot,
                      new Vector3(0, -0.18f, 0.47f), new Vector3(0.26f, 0.025f, 0.04f), red);
    }

    void Update()
    {
        var d = ExpressionHub.Latest;
        bool basi = Time.time - ExpressionHub.LastUpdate > 1f; // data berhenti -> pose netral
        float k = 1f - Mathf.Exp(-kehalusan * Time.deltaTime);

        mouth  = Mathf.Lerp(mouth,  basi ? 0f : d.mouth,  k);
        blinkL = Mathf.Lerp(blinkL, basi ? 0f : d.blinkL, k);
        blinkR = Mathf.Lerp(blinkR, basi ? 0f : d.blinkR, k);
        brow   = Mathf.Lerp(brow,   basi ? 0f : d.brow,   k);
        rot    = Quaternion.Slerp(rot, basi ? Quaternion.identity : d.GetHead(), k);
        headRoot.localRotation = rot;

        SetLid(lidL, EyeX, blinkL);
        SetLid(lidR, -EyeX, blinkR);

        browL.localPosition = new Vector3(EyeX, 0.32f + brow * 0.07f, 0.34f);
        browR.localPosition = new Vector3(-EyeX, 0.32f + brow * 0.07f, 0.34f);
        browL.localRotation = Quaternion.Euler(0, 0, -brow * 10f);
        browR.localRotation = Quaternion.Euler(0, 0, brow * 10f);

        float h = Mathf.Lerp(0.025f, 0.17f, mouth);
        mouthT.localScale = new Vector3(0.26f, h, 0.04f);
        mouthT.localPosition = new Vector3(0, -0.17f - h / 2f, 0.47f);

        Vector3 g = new Vector3(ExpressionHub.Gaze.x * 0.3f, ExpressionHub.Gaze.y * 0.3f, 0.4f);
        pupilL.localPosition = g;
        pupilR.localPosition = g;
    }

    void SetLid(Transform lid, float x, float blink)
    {
        float h = Mathf.Lerp(0.001f, 0.22f, blink);
        lid.localScale = new Vector3(0.24f, h, 0.05f);
        lid.localPosition = new Vector3(x, EyeTop - h / 2f, 0.54f);
    }
}