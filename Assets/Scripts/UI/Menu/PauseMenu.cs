using System.Collections;
using UnityEngine;

// Oyun içi Esc menüsü: Devam / Ayarlar / Çık. Oyunu durdurur (timeScale 0, oyun sesleri susar),
// ekranı "radyo" görüntüsüne çevirir. Görünüm RadioMenuView'da, ekran efekti PauseScreenFx'te.
[RequireComponent(typeof(RadioMenuView))]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    public Camera playerCamera;             // oyuncu kamerası kapalıyken (sinematik) menü açılmaz
    public PauseScreenFx screenFx;
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("Yazılar")]
    public string pausedCaption = "- PAUSED -";
    public string resumeLabel = "Resume";
    public string settingsLabel = "Settings";
    public string quitLabel = "Quit";

    RadioMenuView view;
    bool blockedLastFrame;     // not defteri/okuma ekranı Esc ile kapanırken menü aynı karede açılmasın
    float prevTimeScale = 1f;
    Coroutine anim;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => IsPaused = false;

    void Start()
    {
        view = GetComponent<RadioMenuView>();
        view.Build("PauseMenuUI", 250f);
        view.AddItem(resumeLabel, 120f, Resume);
        view.AddItem(settingsLabel, 50f, () => view.ShowSettings(true));
        view.AddItem(quitLabel, -20f, MainMenu.QuitGame);
        view.AddCaption(pausedCaption, 200f);
        view.SetVisible(false);
    }

    bool CanPause()
    {
        if (MainMenu.IsOpen || blockedLastFrame || Time.timeScale <= 0f) return false;
        if (playerCamera && !playerCamera.isActiveAndEnabled) return false;
        return true;
    }

    void Update()
    {
        if (view == null) return;
        if (!IsPaused)
        {
            if (Input.GetKeyDown(pauseKey) && CanPause()) Open();
            return;
        }
        if (view.Busy) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        bool back = Input.GetKeyDown(pauseKey) || (view.InSettings && Input.GetMouseButtonDown(1));
        if (!back) return;
        if (view.InSettings) { view.radio?.Click(); view.ShowSettings(false); }
        else Resume();
    }

    void LateUpdate() => blockedLastFrame = !IsPaused && PlayerControlLock.IsLocked;

    public void Open()
    {
        if (IsPaused) return;
        IsPaused = true;
        PlayerControlLock.Push();
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        view.SetVisible(true);
        view.radio?.Click();
        view.radio?.SetStaticLevel(1f);
        if (screenFx) screenFx.SetActive(true);
        Play(Burst(true));
    }

    public void Resume()
    {
        if (!IsPaused || view.Busy) return;
        GameSettings.Save();
        Play(Burst(false));
    }

    void Play(IEnumerator r)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(r);
    }

    // Açılış/kapanışta kısa bir parazit patlaması
    IEnumerator Burst(bool opening)
    {
        view.Busy = true;
        const float dur = 0.22f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            view.Static.SetBoost(Mathf.Sin(k * Mathf.PI) * 0.08f);
            view.Root.alpha = opening ? k : 1f - k;
            yield return null;
        }
        view.Static.SetBoost(0f);
        view.Busy = false;
        anim = null;

        if (!opening)
        {
            view.SetVisible(false);
            view.radio?.SetStaticLevel(0f);
            if (screenFx) screenFx.SetActive(false);
            AudioListener.pause = false;
            Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
            IsPaused = false;
            PlayerControlLock.Pop();   // imleci tekrar kilitler
        }
    }

    void OnDisable()
    {
        if (!IsPaused) return;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        IsPaused = false;
        PlayerControlLock.Pop();
    }
}
