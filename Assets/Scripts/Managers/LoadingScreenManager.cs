using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance;

    [Header("Referencias UI")]
    [SerializeField] private GameObject loadingCanvas; 
    [SerializeField] private Slider progressBar;       
    [SerializeField] private TMP_Text progressText;    

    [Header("Ajustes de Velocidad")]
    [SerializeField] private float barFillSpeed = 1.2f; 

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    public void LoadScene(int sceneBuildIndex)
    {
        StartCoroutine(LoadSceneAsync(sceneBuildIndex));
    }

    //Corutina para Carga por Nombre
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        yield return StartCoroutine(LoadingLoop(operation));
    }

    //Corutina para Carga por Índice
    private IEnumerator LoadSceneAsync(int sceneBuildIndex)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneBuildIndex);
        yield return StartCoroutine(LoadingLoop(operation));
    }

    //Bucle unificado que procesa la barra de carga de forma suave
    private IEnumerator LoadingLoop(AsyncOperation operation)
    {
        loadingCanvas.SetActive(true);
        progressBar.value = 0f;
        progressText.text = "0%";

        operation.allowSceneActivation = false;
        float fakeProgress = 0f;

        while (!operation.isDone)
        {
            float realProgress = Mathf.Clamp01(operation.progress / 0.9f);
            fakeProgress = Mathf.MoveTowards(fakeProgress, realProgress, Time.unscaledDeltaTime * barFillSpeed);
            
            progressBar.value = fakeProgress;
            progressText.text = (fakeProgress * 100f).ToString("0") + "%";

            if (Mathf.Approximately(fakeProgress, 1f) && operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        loadingCanvas.SetActive(false);
    }
}