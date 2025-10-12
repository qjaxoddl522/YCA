using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    Animator animator;
    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            OnLoadEnd();
        }
    }

    public void OnRevealEnd()
    {
        var op = SceneManager.LoadSceneAsync("Result");
    }

    public void OnLoadEnd()
    {
        animator.SetTrigger("Hide");
    }

    public void OnHideEnd()
    {
        Destroy(GetComponentInParent<Canvas>(true).gameObject);
    }
}
