using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class PlayerUI : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner Players;
    public Vector3 HandToElbowDist;
    public Button Mute;
    public Button Unmute;
    public Button PrevSong;
    public Button NextSong;
    public Slider Volume;
    public Slider HealthBar;
    public Text WeaponAndStats;
    public Transform Songs;
    public AudioSource[] SongsArray = new AudioSource[6];
    public AudioSource CurrentSong;
    public PlayerController PlayerController;
    public KnifePool KnifePool;

    public void _OnOwnerSet()
    {
        Owner = Networking.LocalPlayer;
        PlayerUIContainer ThisContainer = transform.parent.GetComponent<PlayerUIContainer>();
        ThisContainer.Owner = Owner;
        ThisContainer.IsReady = true;
        PlayerController = Players._GetPlayerPooledObject(Owner).GetComponent<PlayerController>();
        KnifePool = PlayerController.KnifePool;
        KnifePool.PlayerUI = GetComponent<PlayerUI>();
        KnifePool.HP = 10f;
        SetMaxHealth(KnifePool.HP);
        WeaponAndStats = transform.GetChild(0).GetChild(1).GetComponent<Text>();
        // Vector3 ElbowPos = Owner.GetBonePosition(HumanBodyBones.LeftLowerArm);
        // Vector3 HandPos = Owner.GetBonePosition(HumanBodyBones.LeftHand);
        // HandToElbowDist = ElbowPos - HandPos;
        // transform.parent.GetComponent<PlayerUIContainer>().Scale = HandToElbowDist.y; 
        transform.parent.GetComponent<PlayerUIContainer>().IsReady = true;
    }

    private void OnEnable()
    {
        Songs = GameObject.Find("Music").transform;
        for (int i = 0; i < SongsArray.Length; i++)
        {
            SongsArray[i] = Songs.GetChild(i).GetComponent<AudioSource>();
        }
        int SongIndex = Random.Range(0, 6);
        CurrentSong = SongsArray[SongIndex];
        CurrentSong.Play();
        CurrentSong.volume = Volume.value;
    }

    public void SkipSong()
    {

    }

    // private void Update()
    // {
    //     if (IsReady)
    //     {
    //         Vector3 PlayerPosition = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

    //         transform.position = new Vector3(PlayerPosition.x, PlayerPosition.y, PlayerPosition.z);
    //         transform.rotation = Owner.GetRotation();
    //     }
    // }

    public void SetMaxHealth(float health)
    {
        HealthBar.maxValue = KnifePool.HP;
        HealthBar.value = KnifePool.HP;
    }

    public void SetHealth(float health)
    {
        HealthBar.value = KnifePool.HP;
    }

    public void ToggleMute()
    {
        if (Unmute.gameObject.activeSelf)
        {
            CurrentSong.Pause();
            Unmute.transform.parent.gameObject.SetActive(false);
            Mute.transform.parent.gameObject.SetActive(true);
        }
        else
        {
            CurrentSong.Play();
            Unmute.transform.parent.gameObject.SetActive(true);
            Mute.transform.parent.gameObject.SetActive(false);
        }
    }

    public void ChangeVolume()
    {
        CurrentSong.volume = Volume.value;
    }
}
