using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class SceneLoad : MonoBehaviour
{

    public void OnClick(string SceneName)
    {
        SceneManager.LoadScene(SceneName);
    }
}
