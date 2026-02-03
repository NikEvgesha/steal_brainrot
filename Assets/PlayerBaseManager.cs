using System.Collections.Generic;
using UnityEngine;

public class PlayerBaseManager : MonoBehaviour
{
    private List<PlayerBase> _bases;
    private List<BaseOwner> _players;


    private void Start()
    {
        _bases = new List<PlayerBase>(GetComponentsInChildren<PlayerBase>());
        _players = new List<BaseOwner>(FindObjectsByType<BaseOwner>(FindObjectsSortMode.None));

        if (_bases.Count != _players.Count)
            Debug.Log("Количество баз и игроков не одинаковое!");

        for (int i = 0; i < _bases.Count; i++)
        {
            _players[i].SetBase(_bases[i]);
        }
    }
}
