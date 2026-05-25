using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsManager : MonoBehaviour
{
    private const string KEY_VOLUME     = "opt_volume";
    private const string KEY_RES_WIDTH  = "opt_res_width";
    private const string KEY_RES_HEIGHT = "opt_res_height";

    [Header("Volumen")]
    public Slider volumeSlider;
    public TextMeshProUGUI volumeLabel;

    [Header("Resolucion")]
    public Button btn1080p;
    public Button btn720p;
    public Image img1080p;
    public Image img720p;

    [Header("Colores de seleccion")]
    public Color colorSeleccionado   = new Color(1f, 0.85f, 0f);
    public Color colorDeseleccionado = new Color(1f, 1f, 1f);

    private int resWidth;
    private int resHeight;

    void Awake()
    {
        float savedVolume = PlayerPrefs.GetFloat(KEY_VOLUME, 1f);
        resWidth          = PlayerPrefs.GetInt(KEY_RES_WIDTH,  1920);
        resHeight         = PlayerPrefs.GetInt(KEY_RES_HEIGHT, 1080);

        AudioListener.volume = savedVolume;
        AplicarResolucion(resWidth, resHeight, logear: false);
    }

    void OnEnable()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        ActualizarBotonesResolucion();
    }

    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(KEY_VOLUME, value);
        PlayerPrefs.Save();

        if (volumeLabel != null)
            volumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    public void SetResolution1080p() => AplicarResolucion(1920, 1080);
    public void SetResolution720p()  => AplicarResolucion(1280, 720);

    private void AplicarResolucion(int width, int height, bool logear = true)
    {
        Resolution? resMatch     = null;
        double      bestRefresh  = 0;

        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width == width && r.height == height)
            {
                //refreshRateRatio es la API moderna (Unity 2022.2+)
                double hz = (double)r.refreshRateRatio.numerator / r.refreshRateRatio.denominator;
                if (hz > bestRefresh)
                {
                    bestRefresh = hz;
                    resMatch    = r;
                }
            }
        }

        if (resMatch.HasValue)
        {
            //SetResolution con RefreshRate struct (API moderna, sin warnings)
            Screen.SetResolution(resMatch.Value.width, resMatch.Value.height,
                                 FullScreenMode.ExclusiveFullScreen,
                                 resMatch.Value.refreshRateRatio);

            if (logear) Debug.Log($"Monitor soporta {width}x{height} a {bestRefresh:F1}Hz -> aplicado");
        }
        else
        {
            Debug.LogWarning($"El monitor no soporta {width}x{height}. Resoluciones disponibles:");
            foreach (Resolution r in Screen.resolutions)
            {
                double hz = (double)r.refreshRateRatio.numerator / r.refreshRateRatio.denominator;
                Debug.LogWarning($"   {r.width}x{r.height} @{hz:F1}Hz");
            }

            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }

        resWidth  = width;
        resHeight = height;

        PlayerPrefs.SetInt(KEY_RES_WIDTH,  width);
        PlayerPrefs.SetInt(KEY_RES_HEIGHT, height);
        PlayerPrefs.Save();

        ActualizarBotonesResolucion();

        if (logear) StartCoroutine(VerificarResolucion(width, height));
    }

    private System.Collections.IEnumerator VerificarResolucion(int targetW, int targetH)
    {
        yield return null;
        Debug.Log($"Pedida: {targetW}x{targetH} | Real: {Screen.width}x{Screen.height}");
    }

    private void ActualizarBotonesResolucion()
    {
        bool es1080 = (resWidth == 1920 && resHeight == 1080);

        if (img1080p != null) img1080p.color = es1080 ? colorSeleccionado : colorDeseleccionado;
        if (img720p  != null) img720p.color  = es1080 ? colorDeseleccionado : colorSeleccionado;

        if (img1080p == null && btn1080p != null)
        {
            ColorBlock cb   = btn1080p.colors;
            cb.normalColor  = es1080 ? colorSeleccionado : colorDeseleccionado;
            btn1080p.colors = cb;
        }
        if (img720p == null && btn720p != null)
        {
            ColorBlock cb  = btn720p.colors;
            cb.normalColor = es1080 ? colorDeseleccionado : colorSeleccionado;
            btn720p.colors = cb;
        }
    }

    // ─────────────────────────────────────────────────────────────
    public void OpenOptions()  => gameObject.SetActive(true);
    public void CloseOptions() => gameObject.SetActive(false);
}