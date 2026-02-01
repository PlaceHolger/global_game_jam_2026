using UnityEngine;

public class MainMenuSettings : MonoBehaviour
{
    public void SetEasyMode(bool isEasy)
    {
        Globals.Instance.EasyMode = isEasy;
    }
}
