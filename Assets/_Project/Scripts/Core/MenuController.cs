using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public void StartLab()
    {
        SceneManager.LoadScene("Lab Scene");
    }

    public void QuitGame()
    {
        Debug.Log("Quit pressed");
        Application.Quit();
    }
}