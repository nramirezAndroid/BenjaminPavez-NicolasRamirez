using UnityEngine;

public class PlayerSoundController : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private AudioSource audioSource;

    [Header("Clips de Sonido")]
    [SerializeField] private AudioClip ataque;
    [SerializeField] private AudioClip recibirDano;
    [SerializeField] private AudioClip salto;
    [SerializeField] private AudioClip dash;
    [SerializeField] private AudioClip poder;
    [SerializeField] private AudioClip muerte;

    //funciones que se llamara desde el PlayerController
    public void PlayAtaque() { if(ataque) audioSource.PlayOneShot(ataque); }
    public void PlayRecibirDano() { if(recibirDano) audioSource.PlayOneShot(recibirDano); }
    public void PlaySalto() { if(salto) audioSource.PlayOneShot(salto); }
    public void PlayDash() { if(dash) audioSource.PlayOneShot(dash); }
    public void PlayPoder() { if(poder) audioSource.PlayOneShot(poder); }
    public void PlayMuerte() { if(muerte) audioSource.PlayOneShot(muerte); }
}
