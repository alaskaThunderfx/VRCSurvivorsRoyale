
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
public class MusicControls : UdonSharpBehaviour
{
    public Button MusicOn;
    public Button MusicOff;
    public Button Shuffle;
    public Transform SongsContainer;
    public AudioSource[] SongsArray = new AudioSource[6];
    public AudioSource CurrentSong;

    private void OnEnable() {
        int SongIndex = 0;
        foreach(Transform child in SongsContainer)
        {
            SongsArray[SongIndex] = child.GetComponent<AudioSource>();
            SongIndex++;
        }
        SongIndex = Random.Range(0, 6);
        CurrentSong = SongsArray[SongIndex];
        CurrentSong.Play();
    }
    
    public void ToggleMute()
    {
        if (MusicOn.gameObject.activeSelf)
        {
            CurrentSong.Pause();
            MusicOn.gameObject.SetActive(false);
            MusicOff.gameObject.SetActive(true);
        }
        else if (MusicOff.gameObject.activeSelf)
        {
            CurrentSong.Play();
            MusicOn.gameObject.SetActive(true);
            MusicOff.gameObject.SetActive(false);
        }
    }

    public void ShuffleSong()
    {
        CurrentSong.Stop();
        int RandomIndex = Random.Range(0, 6);
        CurrentSong = SongsArray[RandomIndex];
        if (MusicOn.gameObject.activeSelf)
        {
            CurrentSong.Play();
        }
        else if (MusicOff.gameObject.activeSelf)
        {
            CurrentSong.Pause();
        }
        
    }
}
