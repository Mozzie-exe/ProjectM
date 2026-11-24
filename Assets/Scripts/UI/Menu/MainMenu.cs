using System.Collections;
using UnityEngine;

// Ana menü: Başla / Ayarlar / Çık. Oyunla aynı sahnede durur; Başla'ya basınca parazitle kararır
// ve giriş sinematiğini (araba) başlatır. Görünüm RadioMenuView'da.
[RequireComponent(typeof(RadioMenuView))]
public class MainMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Bağlantılar")]
    public IntroCinematic intro;
    public MenuBackdrop backdrop;

    [Header("Yazılar")]
    public string gameTitle = "";
    public string startLabel = "Start";
    public string settingsLabel = "Settings";
    public string quitLabel = "Quit";

    [Header("Geçiş")]
    public float transitionTime = 1.9f;
    [Range(0f, 1f)] public float ambienceHushInMenu = 0.55f;

    RadioMenuView view;
    bool starting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => IsOpen = false;

    void Awake() => IsOpen = true;
    void OnDestroy() => IsOpen = false;

    void Start()
    {
        view = GetComponent<RadioMenuView>();
        GameSettings.ApplyAll();
        view.Build("MainMenuUI", gameTitle.Length > 0 ? 225f : 250f);
        if (!string.IsNullOrEmpty(gameTitle))
        {
            var title = view.NewText("Title", view.Canvas.transform, gameTitle, 84, new Vector2(160f, 300f));
            title.color = new Color(0.92f, 0.92f, 0.9f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 120f);
        }
        view.AddItem(startLabel, 120f, OnStart);
        view.AddItem(settingsLabel, 50f, () => view.ShowSettings(true));
        view.AddItem(quitLabel, -20f, OnQuit);
        view.ShowSettings(false);

        if (intro != null && intro.ambience != null)
            foreach (var a in intro.ambience) if (a) a.hush = ambienceHushInMenu;
        StartCoroutine(FadeFromBlack());
    }

    void Update()
    {
        if (starting || view == null) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (view.InSettings && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            view.ShowSettings(false);
    }

    IEnumerator FadeFromBlack()
    {
        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.unscaledDeltaTime;
            view.Black.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, t / 1.4f));
            yield return null;
        }
        view.Black.color = Color.clear;
    }

    void OnStart()
    {
        if (starting) return;
        StartCoroutine(StartGame());
    }

    IEnumerator StartGame()
    {
        starting = true;
        view.Busy = true;
        GameSettings.Save();
        view.Root.interactable = false;
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        view.radio?.Transition();
        view.radio?.SetStaticLevel(1.6f);

        float t = 0f;
        while (t < transitionTime)
        {
            t += Time.unscaledDeltaTime;
            float k = t / transitionTime;
            view.MainList.alpha = 1f - Mathf.Clamp01(k * 1.6f);
            view.Static.SetBoost(Mathf.Sin(k * Mathf.PI) * 0.12f + k * 0.04f);
            view.Black.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, k)));
            yield return null;
        }
        view.Black.color = Color.black;

        if (backdrop) backdrop.Shutdown();
        view.radio?.SetStaticLevel(0f);
        IsOpen = false;
        if (intro) intro.Begin();          // sinematik kendi karartmasından açılır

        yield return new WaitForSecondsRealtime(0.25f);
        Destroy(view.Canvas.gameObject);
        yield return new WaitForSecondsRealtime(2f);
        Destroy(gameObject);
    }

    void OnQuit() => QuitGame();

    public static void QuitGame()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
