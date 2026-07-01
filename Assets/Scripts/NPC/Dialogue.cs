using System.Collections;
using UnityEngine;
using TMPro;

public class Dialogue : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject dialogueMark;

    [Header("Sonido de Interacción")]
    [SerializeField] private AudioSource audioSource; 
    [SerializeField] private AudioClip soundOnAppear;

    [Header("Configuración de Diálogo")]
    [SerializeField, TextArea(4, 6)] private string[] dialogueLines;
    [SerializeField] private float typingTime = 0.05f;

    private bool isPlayerInRange;
    private bool didDialogueStart;
    private int lineIndex;
    private PlayerController playerScript;

    void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            if (!didDialogueStart)
            {
                StartDialogue();
            }
            else if (dialogueText.text == dialogueLines[lineIndex])
            {
                NextDialogueLine();
            }
            else
            {
                StopAllCoroutines();
                dialogueText.text = dialogueLines[lineIndex];
            }
        }
    }

    private void StartDialogue()
    {
        didDialogueStart = true;
        dialoguePanel.SetActive(true);
        dialogueMark.SetActive(false);
        lineIndex = 0;
        
        //avisa al jugador que se quede bloqueado
        if (playerScript != null) playerScript.IsTalking = true;

        StartCoroutine(ShowLine());
    }

    private void NextDialogueLine()
    {
        lineIndex++;
        if (lineIndex < dialogueLines.Length)
        {
            StartCoroutine(ShowLine());
        }
        else
        {
            didDialogueStart = false;
            dialoguePanel.SetActive(false);
            if (isPlayerInRange) dialogueMark.SetActive(true);

            if (playerScript != null) playerScript.IsTalking = false;
        }
    }

    private IEnumerator ShowLine()
    {
        dialogueText.text = "";
        foreach (char ch in dialogueLines[lineIndex])
        {
            dialogueText.text += ch;
            //usa WaitForSeconds normal porque ya no pausamos el tiempo global
            yield return new WaitForSeconds(typingTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            dialogueMark.SetActive(true);
            
            //obtiene el script del jugador al entrar en rango
            playerScript = collision.GetComponent<PlayerController>();

            if (audioSource != null && soundOnAppear != null)
                audioSource.PlayOneShot(soundOnAppear);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;
            dialogueMark.SetActive(false);
            
            //si el jugador se sale (por si acaso), desbloqueamos
            if (playerScript != null) playerScript.IsTalking = false;
            
            didDialogueStart = false;
            dialoguePanel.SetActive(false);
        }
    }
}