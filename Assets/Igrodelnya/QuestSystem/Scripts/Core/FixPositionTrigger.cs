using UnityEngine;

public class FixPositionTrigger : MonoBehaviour
{
    bool _use;
    private void OnTriggerEnter(Collider other)
    {
/*        if (_use)
            return;
        BoardController boardController = other.GetComponentInParent<BoardController>();
        if (boardController)
        {
            boardController.WaitFixUpdate = true;
             _use = true;
        }*/
    }

}
