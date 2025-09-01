using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class FPS_UI : MonoBehaviour
{
    private Text FPS;
    private int _framesCount = 0;
    private int _sumFPS = 0;
    private float _timeFPSCollectStart = 0.5f;
    private float _timeFPSCollect = 0;
    private void Awake()
    {
        FPS = GetComponent<Text>();
    }
    private void Update()
    {

        if (_timeFPSCollect < _timeFPSCollectStart)
        {
            _timeFPSCollect += Time.deltaTime;
            _sumFPS += (int)(1.0f / Time.deltaTime);
            _framesCount++;
        } else
        {
            _sumFPS /= _framesCount;
            FPS.text = "FPS : " + _sumFPS.ToString();
            _sumFPS = 0;
            _timeFPSCollect = 0;
            _framesCount = 0;
        }
        
    }
}
