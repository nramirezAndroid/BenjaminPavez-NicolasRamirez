using UnityEngine;

public class PlayerSoundController : MonoBehaviour
{
    [Header("Configuración")]
    public AudioSource audioSource;

    [Header("Clips de Sonido")]
    public AudioClip ataque;
    public AudioClip recibirDano;
    public AudioClip salto;
    public AudioClip dash;
    public AudioClip poder;
    public AudioClip muerte;

    //Funciones que se llamara desde el PlayerController
    public void PlayAtaque() { if(ataque) audioSource.PlayOneShot(ataque); }
    public void PlayRecibirDano() { if(recibirDano) audioSource.PlayOneShot(recibirDano); }
    public void PlaySalto() { if(salto) audioSource.PlayOneShot(salto); }
    public void PlayDash() { if(dash) audioSource.PlayOneShot(dash); }
    public void PlayPoder() { if(poder) audioSource.PlayOneShot(poder); }
    public void PlayMuerte() { if(muerte) audioSource.PlayOneShot(muerte); }
}
