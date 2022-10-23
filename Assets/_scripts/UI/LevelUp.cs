using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class LevelUp : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public bool IsReady;
    public float yAxis;
    public Text LevelText;
    public Button[] PowerUpButtons = new Button[3];
    public Text[] PowerUpText = new Text[3];
    public GameObject[] IconParents = new GameObject[9];
    public Sprite[] Icons = new Sprite[9];

    // Indexes for each sprite
    // 0 - HP
    // 1 - DEF
    // 2 - RunSpeed
    // 3 - AttackDMG
    // 4 - Cooldown
    // 5 - AttackSpeed
    // 6 - Range
    // 7 - Size
    // 8 - Quantity

    public void _OnOwnerSet()
    {
        Debug.Log("In _OnOwnerSet in LevelUp.cs");
        Owner = Networking.LocalPlayer;
        IsReady = true;
        int ind = 0;
        foreach(GameObject parent in IconParents)
        {
            Icons[ind] = parent.GetComponent<SpriteRenderer>().sprite;
            parent.SetActive(false);
            ind++;
        }
        yAxis = 1f;
        PowerUpChoices();
    }

    private void Update()
    {
        if (IsReady)
        {
            Vector3 curPos = Owner.GetPosition();
            Vector3 headPos = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

            transform.position = new Vector3(curPos.x, curPos.y += yAxis, curPos.z);
            transform.LookAt(headPos);
        }
    }

    public void PowerUpChoices()
    {
        int[] indexes = new int[3];

        for (int i = 0; i < 3; i++)
        {
            Debug.Log("Current value of i: " + i);
            int randInd = Random.Range(0, 9);
            if (i == 0)
            {
                Debug.Log("i == 0, first condition met");
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
        Button[] buttons = new Button[3];

        foreach (int num in indexes)
        {
            Image curSpri = PowerUpButtons[ind].GetComponent<Image>();
            Text curText = PowerUpText[ind];
            Debug.Log(num);
            switch (num)
            {
                // HP
                case 0:
                    curSpri.sprite = Icons[0];
                    curSpri.color = Color.gray;
                    curText.text = "HP Up!";
                    ind++;
                    break;
                // DEF
                case 1:
                    curSpri.sprite = Icons[1];
                    curSpri.color = Color.gray;
                    curText.text = "DEF Up!";
                    ind++;
                    break;
                // RunSpeed
                case 2:
                    curSpri.sprite = Icons[2];
                    curSpri.color = Color.gray;
                    curText.text = "Run Speed Up!";
                    ind++;
                    break;
                // AttackDMG
                case 3:
                    curSpri.sprite = Icons[3];
                    curSpri.color = Color.gray;
                    curText.text = "DMG Up!";
                    ind++;
                    break;
                // Cooldown
                case 4:
                    curSpri.sprite = Icons[4];
                    curSpri.color = Color.gray;
                    curText.text = "Cooldown Down!";
                    ind++;
                    break;
                // AttackSpeed
                case 5:
                    curSpri.sprite = Icons[5];
                    curSpri.color = Color.gray;
                    curText.text = "Attack SPD Up!";
                    ind++;
                    break;
                // Range
                case 6:
                    curSpri.sprite = Icons[6];
                    curSpri.color = Color.gray;
                    curText.text = "Range Up!";
                    ind++;
                    break;
                // Size
                case 7:
                    curSpri.sprite = Icons[7];
                    curSpri.color = Color.gray;
                    curText.text = "Size Up!";
                    ind++;
                    break;
                // Quantity
                case 8:
                    curSpri.sprite = Icons[8];
                    curSpri.color = Color.gray;
                    curText.text = "AMT Up!";
                    ind++;
                    break;
            }
        }
    }
}
