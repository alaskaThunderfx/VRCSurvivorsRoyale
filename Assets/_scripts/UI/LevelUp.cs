using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class LevelUp : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner Players;
    public PlayerController PlayerController;
    public LevelUpUIContainer ThisContainer;
    public bool IsReady;
    public int ChoiceIndex;
    public float yAxis;
    public float zAxis;
    public Text LevelText;
    public Button[] PowerUpButtons = new Button[3];
    public Text[] PowerUpText = new Text[3];
    public GameObject[] IconParents = new GameObject[9];
    public Sprite[] Icons = new Sprite[9];

    // Indexes for each sprite
    // 0 - HP
    // 1 - DEF
    // 2 - RunSpeed
    // 3 - Damage
    // 4 - Cooldown
    // 5 - Force
    // 6 - Range
    // 7 - Size
    // 8 - Quantity

    public void _OnOwnerSet()
    {
        Debug.Log("In _OnOwnerSet in LevelUp.cs");
        // Setting the "Owner" to each local player, so that way we can minimize file space, since these
        // are meant to be local.
        Owner = Networking.LocalPlayer;
        PlayerController = Players._GetPlayerPooledObject(Owner).GetComponent<PlayerController>();
        IsReady = true;
        int ind = 0;
        foreach (GameObject parent in IconParents)
        {
            Icons[ind] = parent.GetComponent<SpriteRenderer>().sprite;
            parent.SetActive(false);
            ind++;
        }
        ThisContainer = transform.parent.GetComponent<LevelUpUIContainer>();
        ThisContainer.Owner = Owner;
        ThisContainer.LevelUp = GetComponent<LevelUp>();
        ThisContainer.IsReady = true;
        yAxis = 0;
        zAxis = 1;
        PowerUpChoices();
    }

    public void OnEnable()
    {
        if (IsReady)
        {
            Debug.Log("In _OnOwnerSet in LevelUp.cs");
            Owner = Networking.LocalPlayer;
            PlayerController = Players
                ._GetPlayerPooledObject(Owner)
                .GetComponent<PlayerController>();
            int ind = 0;
            foreach (GameObject parent in IconParents)
            {
                Icons[ind] = parent.GetComponent<SpriteRenderer>().sprite;
                parent.SetActive(false);
                ind++;
            }
            PowerUpChoices();
        }
    }

    private void OnDisable()
    {
        if (PlayerController.KnifePool.Upgrades[ChoiceIndex] == 4)
        {
            CleanUpArrays(ChoiceIndex);
        }
    }

    private void Update()
    {
        if (IsReady && ThisContainer.IsReady)
        {
            transform.localPosition = new Vector3(0, yAxis, zAxis);
        }
    }

    public void PowerUpChoices()
    {
        int[] indexes = new int[3];
        if (PlayerController.KnifePool.Level == 1)
        {
            LevelText.text =
                "Pick your first Power Up!" + "\nLevel: " + PlayerController.KnifePool.Level;
        }
        else
        {
            LevelText.text = "Level Up!!" + "\nLevel: " + PlayerController.KnifePool.Level;
        }

        for (int i = 0; i < 3; i++)
        {
            Debug.Log("Current value of i: " + i);
            int randInd = UnityEngine.Random.Range(0, Icons.Length);
            if (i == 0 && Icons[i] != null)
            {
                Debug.Log("i == 0, first condition met");
                Debug.Log("Icons[" + i + "].name: " + Icons[i].name);
                indexes[i] = randInd;
            }
            else
            {
                Debug.Log("Beginning a loop that isn't the first");
                Debug.Log("i == " + i);
                Debug.Log("indexes[i - 1]: " + indexes[i - 1]);
                Debug.Log("randInd: " + randInd);
                if (indexes[i - 1] != randInd)
                {
                    Debug.Log("indexes[i - 1]: " + indexes[i - 1]);
                    Debug.Log("randInd: " + randInd);
                    indexes[i] = randInd;
                    Debug.Log("indexes[i]: " + indexes[i]);
                }
                else
                {
                    Debug.Log("The values were the same");
                    Debug.Log("i before: " + i);
                    i--;
                    Debug.Log("i after: " + i);
                }
            }
        }

        int ind = 0;

        foreach (int num in indexes)
        {
            Image curSpri = PowerUpButtons[ind].GetComponent<Image>();
            Text curText = PowerUpText[ind];
            switch (Icons[num].name)
            {
                // HP
                case "HP":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "HP Up!";
                    ind++;
                    break;
                // DEF
                case "DEF":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "DEF Up!";
                    ind++;
                    break;
                // RunSpeed
                case "RunSpeed":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "Run Speed Up!";
                    ind++;
                    break;
                // Damage
                case "Damage":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "DMG Up!";
                    ind++;
                    break;
                // Cooldown
                case "Cooldown":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "Reduce\nCooldown!";
                    ind++;
                    break;
                // Force
                case "Force":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "Attack SPD Up!";
                    ind++;
                    break;
                // Range
                case "Range":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "Range Up!";
                    ind++;
                    break;
                // Size
                case "Size":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "Size Up!";
                    ind++;
                    break;
                // Quantity
                case "Quantity":
                    curSpri.sprite = Icons[num];
                    curSpri.color = Color.gray;
                    curText.text = "AMT Up!";
                    ind++;
                    break;
            }
        }
    }

    public void FirstButton()
    {
        PickedOne(PowerUpButtons[0].GetComponent<Image>().sprite.name);
    }

    public void SecondButton()
    {
        PickedOne(PowerUpButtons[1].GetComponent<Image>().sprite.name);
    }

    public void ThirdButton()
    {
        PickedOne(PowerUpButtons[2].GetComponent<Image>().sprite.name);
    }

    // Indexes for each sprite
    // 0 - HP
    // 1 - DEF
    // 2 - RunSpeed
    // 3 - Damage
    // 4 - Cooldown
    // 5 - Force
    // 6 - Range
    // 7 - Size
    // 8 - Quantity
    public void PickedOne(string Stat)
    {
        Debug.Log(Stat);
        switch (Stat)
        {
            case "HP":
                Debug.Log(PlayerController.KnifePool.HP);
                PlayerController.KnifePool.HP += 10;
                PlayerController.KnifePool.Upgrades[0]++;
                Debug.Log(PlayerController.KnifePool.HP);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "HP")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "DEF":
                Debug.Log(PlayerController.KnifePool.DEF);
                PlayerController.KnifePool.DEF += .3f;
                PlayerController.KnifePool.Upgrades[1]++;
                Debug.Log(PlayerController.KnifePool.DEF);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "DEF")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "RunSpeed":
                Debug.Log(PlayerController.KnifePool.RunSpeed);
                PlayerController.KnifePool.RunSpeed += .3f;
                PlayerController.KnifePool.Upgrades[2]++;
                Debug.Log(PlayerController.KnifePool.RunSpeed);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "RunSpeed")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "Damage":
                Debug.Log(PlayerController.KnifePool.Damage);
                PlayerController.KnifePool.Damage += 1;
                PlayerController.KnifePool.Upgrades[3]++;
                Debug.Log(PlayerController.KnifePool.Damage);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "Damage")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "Cooldown":
                Debug.Log(PlayerController.KnifePool.Cooldown);
                PlayerController.KnifePool.Cooldown -= .33f;
                PlayerController.KnifePool.Upgrades[4]++;
                Debug.Log(PlayerController.KnifePool.Cooldown);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "Cooldown")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "Force":
                Debug.Log(PlayerController.KnifePool.Force);
                PlayerController.KnifePool.Force += 1;
                PlayerController.KnifePool.Upgrades[5]++;
                Debug.Log(PlayerController.KnifePool.Force);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "Force")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "Range":
                Debug.Log(PlayerController.KnifePool.Range);
                PlayerController.KnifePool.Range += 225;
                PlayerController.KnifePool.Upgrades[6]++;
                Debug.Log(PlayerController.KnifePool.Range);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "Range")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "Size":
                Debug.Log(PlayerController.KnifePool.Size);
                PlayerController.KnifePool.Size += 1;
                PlayerController.KnifePool.Upgrades[7]++;
                Debug.Log(PlayerController.KnifePool.Size);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "Size")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
            case "Quantity":
                Debug.Log(PlayerController.KnifePool.Quantity);
                PlayerController.KnifePool.Quantity += 1;
                PlayerController.KnifePool.Upgrades[8]++;
                Debug.Log(PlayerController.KnifePool.Quantity);
                for (int i = 0; i < Icons.Length; i++)
                {
                    if (Icons[i].name == "Quantity")
                    {
                        ChoiceIndex = i;
                        break;
                    }
                }
                gameObject.SetActive(false);
                break;
        }
    }

    public void CleanUpArrays(int chose)
    {
        GameObject[] TempParentsArray = new GameObject[IconParents.Length - 1];
        Sprite[] TempIconsArray = new Sprite[Icons.Length - 1];

        bool passedVal = false;
        string skipThis = Icons[chose].name;

        for (int i = 0; i < Icons.Length; i++)
        {
            if (Icons[i].name == skipThis)
            {
                passedVal = true;
                i++;
            }

            if (!passedVal && TempIconsArray[i] != null)
            {
                TempParentsArray[i] = IconParents[i];
                TempIconsArray[i] = Icons[i];
            }
            else if (passedVal && TempIconsArray[i] != null)
            {
                TempParentsArray[i - 1] = IconParents[i];
                TempIconsArray[i - 1] = Icons[i];
            }
        }

        IconParents = new GameObject[TempParentsArray.Length];
        Icons = new Sprite[TempIconsArray.Length];
        for (int i = 0; i < TempParentsArray.Length; i++)
        {
            IconParents[i] = TempParentsArray[i];
            Icons[i] = TempIconsArray[i];
        }
    }
}
