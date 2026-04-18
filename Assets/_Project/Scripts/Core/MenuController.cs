using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    private void Awake()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.GetComponent<MenuUIEnhancer>() == null)
        {
            canvas.gameObject.AddComponent<MenuUIEnhancer>();
        }
    }

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
