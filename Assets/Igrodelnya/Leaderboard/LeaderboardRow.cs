using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class LeaderboardRow : MonoBehaviour
{
    [SerializeField] private Text _rank;
    [SerializeField] private Text _name;
    [SerializeField] private Text _score;
    [SerializeField] private Image _img;


    public void Init(LBRecord playerScore)
    {
        _rank.text = playerScore.Rank.ToString();
        _name.text = playerScore.Name;
        _score.text = playerScore.Score.ToString();
        StartCoroutine(LoadTexture(playerScore.URL));
    }


    IEnumerator LoadTexture(string url)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                webRequest.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError("ImageLoadYG Error: " + webRequest.error);
            }
            else
            {
                DownloadHandlerTexture handlerTexture = webRequest.downloadHandler as DownloadHandlerTexture;

                if (handlerTexture.isDone)
                {
                    Rect rect = new Rect(0, 0, handlerTexture.texture.width, handlerTexture.texture.height);
                    _img.sprite = Sprite.Create(handlerTexture.texture, rect, Vector2.zero);
                    _img.enabled = true;
                    
                }
            }
        }
    }
}
