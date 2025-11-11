using UnityEngine;
using UnityEngine.UI;

public class BackButton : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(GoMain);
    }

    public void GoMain()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
    }
}
