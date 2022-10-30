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
    public float Size = 1;
    public float yAxis = 0;
    public float zAxis = 1;
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
        // Setting the "Owner" to each local player, so that way we can minimize file space, since these
        // are meant to be local.
        Owner = Networking.LocalPlayer;
        PlayerController = transform.parent.parent.GetComponent<PlayerController>();
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
        PowerUpChoices();
    }

    public void OnEnable()
    {
        Size = 1;
        yAxis = 0;
        zAxis = 1;
        if (IsReady && PlayerController.KnifePool.Level > 1)
        {
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
        if (IsReady)
        {
            if (PlayerController.KnifePool.Upgrades[ChoiceIndex] == 4)
            {
                CleanUpArrays(ChoiceIndex);
            }
        }
    }

    public int[] GetRandom()
    {
        // Get the array for the player that tracks the amount of times each PowerUp has been chosen
        int[] Upgrades = PlayerController.KnifePool.Upgrades;
        // Variables representing each option
        int firstNum;
        int secondNum;
        int thirdNum;
        // Finding the first random number
        firstNum = UnityEngine.Random.Range(0, 9);
        Debug.Log("firstNum = " + firstNum);
        // if the Power Up at the index of the first random number is chosen is maxxed out, continue finding random integers.
        while (Upgrades[firstNum] == 4)
        {
            Debug.Log("The Upgrade at Upgrades[" + firstNum + "] is maxed out.");
            firstNum = UnityEngine.Random.Range(0, 9);
            Debug.Log("firstNum = " + firstNum);
        }
        // Finding second number
        secondNum = UnityEngine.Random.Range(0, 9);
        Debug.Log("secondNum = " + secondNum);
        // if the second int is the same as the first one, or if the Power Up at the index of the second int is maxxed, continue finding random integers.
        while (secondNum == firstNum || Upgrades[secondNum] == 4)
        {
            Debug.Log(
                "The Upgrade at Upgrades["
                    + secondNum
                    + "] is maxed out or "
                    + secondNum
                    + " is the same as "
                    + firstNum
                    + "."
            );
            secondNum = UnityEngine.Random.Range(0, 9);
            Debug.Log("secondNum = " + secondNum);
        }
        // Finding third number
        thirdNum = UnityEngine.Random.Range(0, 9);
        Debug.Log("thirdNum = " + thirdNum);
        // if the third int is the same as either the first or second int, or if the upgrade at the index of the number is maxxed, keep finding random int.
        while (thirdNum == secondNum || thirdNum == firstNum || Upgrades[thirdNum] == 4)
        {
            Debug.Log(
                "The Upgrade at Upgrades["
                    + thirdNum
                    + "] is maxed, "
                    + thirdNum
                    + " is the same as "
                    + secondNum
                    + ", or "
                    + thirdNum
                    + " is the same as "
                    + firstNum
                    + "."
            );
            thirdNum = UnityEngine.Random.Range(0, 9);
        }
        // Create the array that will carry the choices
        int[] numbers = new int[3];
        // Set the choices
        numbers[0] = firstNum;
        numbers[1] = secondNum;
        numbers[2] = thirdNum;
        Debug.Log(numbers);
        // Return the array of choices
        return numbers;
    }

    public void PowerUpChoices()
    {
        int[] indexes = GetRandom();
        if (PlayerController.KnifePool.Level == 1)
        {
            LevelText.text =
                "Pick your first Power Up!" + "\nLevel: " + PlayerController.KnifePool.Level;
        }
        else
        {
            LevelText.text = "Level Up!!" + "\nLevel: " + PlayerController.KnifePool.Level;
        }

        // for (int i = 0; i < 3; i++)
        // {
        //     int randInd = UnityEngine.Random.Range(0, Icons.Length);
        //     if (i == 0 && Icons[i] != null)
        //     {
        //         indexes[i] = randInd;
        //     }
        //     else
        //     {
        //         if (indexes[i - 1] != randInd)
        //         {
        //             indexes[i] = randInd;
        //         }
        //         else
        //         {
        //             i--;
        //         }
        //     }
        // }

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
        switch (Stat)
        {
            case "HP":
                PlayerController.KnifePool.MaxHP += 10;
                PlayerController.KnifePool.Upgrades[0]++;
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
                PlayerController.KnifePool.DEF += .3f;
                PlayerController.KnifePool.Upgrades[1]++;
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
                PlayerController.KnifePool.RunSpeed += .3f;
                PlayerController.KnifePool.Upgrades[2]++;
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
                PlayerController.KnifePool.Damage += 1;
                PlayerController.KnifePool.Upgrades[3]++;
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
                PlayerController.KnifePool.Cooldown -= .33f;
                PlayerController.KnifePool.Upgrades[4]++;
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
                PlayerController.KnifePool.Force += 1;
                PlayerController.KnifePool.Upgrades[5]++;
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
                PlayerController.KnifePool.Range += 225;
                PlayerController.KnifePool.Upgrades[6]++;
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
                PlayerController.KnifePool.Size += 1;
                PlayerController.KnifePool.Upgrades[7]++;
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
                PlayerController.KnifePool.Quantity += 1;
                PlayerController.KnifePool.Upgrades[8]++;
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

    public void LUUIChangeSize(float scale)
    {
        transform.localScale = new Vector3(scale, scale, scale);
    }

    public void LUUIChangeHeight(float yValue)
    {
        Vector3 CurrentPosition = transform.position;
        transform.localPosition = new Vector3(0, yValue, CurrentPosition.z);
    }

    public void LUUIChangeDistance(float zValue)
    {
        Vector3 CurrentPosition = transform.position;
        transform.localPosition = new Vector3(0, CurrentPosition.y, zValue);
    }
}
