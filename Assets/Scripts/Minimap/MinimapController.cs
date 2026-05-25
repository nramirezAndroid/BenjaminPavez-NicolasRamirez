using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("Referencias")]
    public Camera minimapCamera;
    public RectTransform minimapPanel;
    public Transform playerTarget;

    [Header("Config")]
    public KeyCode toggleKey = KeyCode.Tab;
    public Vector2 sizeSmall  = new Vector2(200, 120);
    public Vector2 sizeLarge  = new Vector2(500, 300);
    public float followSpeed  = 5f;

    private bool isExpanded = false;
    private bool isVisible  = true;

    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.isPaused) return;

        if (Input.GetKeyDown(toggleKey))
        {
            isExpanded = !isExpanded;
            minimapPanel.sizeDelta = isExpanded ? sizeLarge : sizeSmall;
        }

        //La cámara del minimap sigue al jugador en X
        if (playerTarget != null && minimapCamera != null)
        {
            Vector3 target = new Vector3(playerTarget.position.x,
                                         playerTarget.position.y,
                                         minimapCamera.transform.position.z);
            minimapCamera.transform.position = Vector3.Lerp(
                minimapCamera.transform.position, target, followSpeed * Time.deltaTime);
        }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        minimapPanel.gameObject.SetActive(visible);
    }
}