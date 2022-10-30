using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class PlayerUI : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner Players;
    public Button Mute;
    public Button Unmute;
    public int SongIndex;
    public bool isMuted;
    public Button Prev;
    public Button Next;
    public Slider Volume;
    public Slider HealthBar;
    public Text WeaponAndStats;
    public AudioSource[] SongsArray = new AudioSource[6];
    public AudioSource CurrentSong;
    public Button AttackToggleOn;
    public Button AttackToggleOff;
    public Button SettingsToggle;
    public GameObject SettingsUI;
    public Slider PUISize;
    public Slider PUIHeight;
    public Slider PUIDistance;
    public Transform LevelUpUI;
    public Slider LUUISize;
    public Slider LUUIHeight;
    public Slider LUUIDistance;
    public PlayerController PlayerController;
    public KnifePool KnifePool;

    public void _OnOwnerSet()
    {
        Owner = Networking.LocalPlayer;
        PlayerUIContainer ThisContainer = transform.parent.GetComponent<PlayerUIContainer>();
        ThisContainer.Owner = Owner;
        ThisContainer.IsReady = true;
        PlayerController = transform.parent.parent.GetComponent<PlayerController>();
        KnifePool = PlayerController.KnifePool;
        KnifePool.PlayerUI = GetComponent<PlayerUI>();
        KnifePool.HP = 10f;
        SetMaxHealth(KnifePool.HP);
        WeaponAndStats = transform.GetChild(0).GetChild(1).GetComponent<Text>();
        transform.parent.GetComponent<PlayerUIContainer>().IsReady = true;
        PlayerController.SetUIWAS("Knife", KnifePool.Level.ToString());
        Transform SongsInHierarchy = GameObject.Find("Music").GetComponent<Transform>();
        for (int i = 0; i < 6; i++)
        {
            SongsArray[i] = SongsInHierarchy.GetChild(i).GetComponent<AudioSource>();
        }
        SongIndex = Random.Range(0, 6);
        CurrentSong = SongsArray[SongIndex];
        CurrentSong.Play();
        CurrentSong.volume = Volume.value;
    }

    private void OnEnable()
    {
        
    }

    public void PrevSong()
    {
        CurrentSong.Stop();
        if (SongIndex == 0)
        {
            SongIndex = 5;
            CurrentSong = SongsArray[SongIndex];
            CurrentSong.Play();
            CurrentSong.volume = Volume.value;
        }
        else
        {
            SongIndex--;
            CurrentSong = SongsArray[SongIndex];
            CurrentSong.Play();
            CurrentSong.volume = Volume.value;
        }
    }

    public void NextSong()
    {
        CurrentSong.Stop();
        if (SongIndex == 5)
        {
            SongIndex = 0;
            CurrentSong = SongsArray[SongIndex];
            if (!isMuted)
            {
                CurrentSong.Play();
            }
            CurrentSong.volume = Volume.value;
        }
        else
        {
            SongIndex++;
            CurrentSong = SongsArray[SongIndex];
            if (!isMuted)
            {
                CurrentSong.Play();
            }
            CurrentSong.volume = Volume.value;
        }
    }

    public void SetMaxHealth(float health)
    {
        HealthBar.maxValue = health;
    }

    public void SetHealth(float health)
    {
        HealthBar.value = health;
    }

    public void MuteSong()
    {
        CurrentSong.Pause();
        isMuted = true;
        Unmute.transform.parent.gameObject.SetActive(true);
        Mute.transform.parent.gameObject.SetActive(false);
    }

    public void UnmuteSong()
    {
        CurrentSong.Play();
        isMuted = false;
        Mute.transform.parent.gameObject.SetActive(true);
        Unmute.transform.parent.gameObject.SetActive(false);
    }

    public void ChangeVolume()
    {
        CurrentSong.volume = Volume.value;
    }

    public void ToggleAttackOn()
    {
        AttackToggleOn.gameObject.SetActive(true);
        AttackToggleOff.gameObject.SetActive(false);
        KnifePool.isKnifeOn = !KnifePool.isKnifeOn;
    }

    public void ToggleAttackOff()
    {
        AttackToggleOff.gameObject.SetActive(true);
        AttackToggleOn.gameObject.SetActive(false);
        KnifePool.isKnifeOn = !KnifePool.isKnifeOn;
    }

    public void ToggleSettings()
    {
        SettingsUI.SetActive(!SettingsUI.activeSelf);
    }

    public void PUIChangeSize()
    {
        float scale = PUISize.value;
        Vector3 curPos = transform.localScale;
        transform.localScale = new Vector3(scale, scale, scale);
    }

    public void PUIChangeHeight()
    {
        float height = PUIHeight.value;
        Vector3 curPos = transform.localPosition;
        transform.localPosition = new Vector3(curPos.x, height, curPos.z);
    }

    public void PUIChangeDistance()
    {
        float dist = PUIDistance.value;
        Vector3 curPos = transform.localPosition;
        transform.localPosition = new Vector3(curPos.x, curPos.y, dist);
    }

    public void LUUIChangeSize()
    {
        float scale = LUUISize.value;
        LevelUpUI.localScale = new Vector3(scale, scale, scale);
    }

    public void LUUIChangeHeight()
    {
        LevelUpUI.localPosition = new Vector3(0, LUUIHeight.value, LevelUpUI.localPosition.z);
    }

    public void LUUIChangeDistance()
    {
        LevelUpUI.localPosition = new Vector3(0, LevelUpUI.localPosition.y, LUUIDistance.value);
    }
}