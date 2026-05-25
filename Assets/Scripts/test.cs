using UnityEngine;
using TMPro;

public class ResolutionDisplay : MonoBehaviour
{
    public TextMeshProUGUI resText;

    void Update()
    {
        if (resText != null)
            resText.text = $"{Screen.width} × {Screen.height}";
    }
}