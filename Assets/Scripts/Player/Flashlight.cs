using UnityEngine;
using UnityEngine.Rendering;

// Kameraya eklenir. F tuşu ile feneri açıp kapatır.
// Light ve model atanmazsa oyun başında otomatik oluşturulur.
public class Flashlight : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = false;

    [Header("Işık")]
    public Light spotLight;
    public float range = 70f;
    [Range(10f, 150f)] public float spotAngle = 70f;
    public float intensity = 120f; // URP'de ışık mesafenin karesiyle azalır, o yüzden yüksek
    public Color color = new Color(1f, 0.95f, 0.85f);
    public LightShadows shadows = LightShadows.Soft;

    [Header("Etrafı aydınlatan geniş ışık")]
    public bool useSpill = true;
    [Range(60f, 170f)] public float spillAngle = 130f;
    public float spillIntensity = 15f;
    public float spillRange = 20f;

    Light spillLight;

    [Header("Model (eldeki fener)")]
    public bool createModel = true;
    public Vector3 holdPosition = new Vector3(0.22f, -0.2f, 0.55f);

    [Header("Ses (opsiyonel)")]
    public AudioClip clickSound;

    bool isOn;
    public bool IsOn => isOn;
    Renderer lensRenderer;
    Material lensMaterial;
    AudioSource audioSource;

    void Start()
    {
        Transform holder = null;
        if (createModel)
            holder = CreateModel();

        Transform lightParent = holder != null ? holder : transform;

        if (spotLight == null)
            spotLight = CreateLight(lightParent, "FlashlightLight", range, spotAngle, spotAngle * 0.6f, intensity, shadows);

        if (useSpill)
            spillLight = CreateLight(lightParent, "FlashlightSpill", spillRange, spillAngle, spillAngle * 0.3f, spillIntensity, LightShadows.None);

        if (clickSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.outputAudioMixerGroup = GameAudio.Group(AudioChannel.SFX);
        }

        SetOn(startOn, false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetOn(!isOn, true);
    }

    public void SetOn(bool on, bool playSound)
    {
        isOn = on;
        if (spotLight != null) spotLight.enabled = on;
        if (spillLight != null) spillLight.enabled = on;

        if (lensMaterial != null)
        {
            if (on) lensMaterial.EnableKeyword("_EMISSION");
            else lensMaterial.DisableKeyword("_EMISSION");
        }

        if (playSound && audioSource != null)
            audioSource.PlayOneShot(clickSound);
    }

    Light CreateLight(Transform parent, string name, float lRange, float angle, float innerAngle, float lIntensity, LightShadows lShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        // Model varsa ışık ucundan çıksın, yoksa kameradan
        go.transform.localPosition = parent == transform ? Vector3.zero : new Vector3(0f, 0f, 0.13f);
        // Model hafif içe dönük; ışık kameranın baktığı yere gitsin
        go.transform.rotation = transform.rotation;

        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.range = lRange;
        l.spotAngle = angle;
        l.innerSpotAngle = innerAngle;
        l.intensity = lIntensity;
        l.color = color;
        l.shadows = lShadows;
        l.shadowBias = 0.02f;
        l.shadowNormalBias = 0.3f;
        l.renderMode = LightRenderMode.ForcePixel; // her zaman piksel bazlı hesaplansın
        return l;
    }

    // Inspector'da oyun sırasında değer değiştirince anında uygulansın
    void OnValidate()
    {
        if (spotLight != null)
        {
            spotLight.range = range;
            spotLight.spotAngle = spotAngle;
            spotLight.innerSpotAngle = spotAngle * 0.6f;
            spotLight.intensity = intensity;
            spotLight.color = color;
        }
        if (spillLight != null)
        {
            spillLight.range = spillRange;
            spillLight.spotAngle = spillAngle;
            spillLight.innerSpotAngle = spillAngle * 0.3f;
            spillLight.intensity = spillIntensity;
            spillLight.color = color;
        }
    }

    Transform CreateModel()
    {
        var root = new GameObject("FlashlightModel").transform;
        root.SetParent(transform, false);
        root.localPosition = holdPosition;
        root.localRotation = Quaternion.Euler(-2f, -4f, 0f); // hafif içe doğru

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var bodyMat = new Material(shader);
        bodyMat.color = new Color(0.08f, 0.08f, 0.09f);
        if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.6f);
        if (bodyMat.HasProperty("_Metallic")) bodyMat.SetFloat("_Metallic", 0.7f);

        // Gövde
        MakePart(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, -0.03f), new Vector3(0.035f, 0.08f, 0.035f), bodyMat);
        // Baş kısmı (biraz daha kalın)
        MakePart(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0.08f), new Vector3(0.05f, 0.035f, 0.05f), bodyMat);

        // Cam / lens
        lensMaterial = new Material(shader);
        lensMaterial.color = new Color(0.9f, 0.9f, 0.8f);
        if (lensMaterial.HasProperty("_EmissionColor"))
            lensMaterial.SetColor("_EmissionColor", color * 2f);
        lensRenderer = MakePart(PrimitiveType.Cylinder, root, new Vector3(0f, 0f, 0.116f), new Vector3(0.042f, 0.002f, 0.042f), lensMaterial);

        return root;
    }

    Renderer MakePart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var part = GameObject.CreatePrimitive(type);
        DestroyImmediate(part.GetComponent<Collider>()); // karakterle bir kare bile çarpışmasın
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // silindiri ileri doğru çevir
        part.transform.localScale = scale;

        var r = part.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off; // fener kendi ışığını engellemesin
        r.receiveShadows = false;
        return r;
    }
}
