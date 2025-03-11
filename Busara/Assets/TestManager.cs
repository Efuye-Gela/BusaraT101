using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;


public class TestManager : MonoBehaviour
{
    [SerializeField] GameObject BoardsHolder;
    [SerializeField] List<GameObject> boards;
    [SerializeField] float rotateBoardAngle = -90;
    [SerializeField] float roateHolderAngle = 90;
    public void TestRotateLeft()
    {
        if (BoardsHolder.transform.rotation != Quaternion.Euler(0, 0, roateHolderAngle))
        {
            BoardsHolder.transform.DORotate(new Vector3(0, 0, roateHolderAngle), 1f);
            foreach (var board in BoardManager.Instance.gameBoards)
            {
                foreach (var slot in board.Slots)
                {
                    slot.gameObject.transform.rotation = Quaternion.Euler(0, 0, rotateBoardAngle);
                }
            }
        }
    }

    public void TestRotateRight()
    {
        if (BoardsHolder.transform.rotation != Quaternion.Euler(0, 0, -roateHolderAngle))
        {
            BoardsHolder.transform.DORotate(new Vector3(0, 0, -roateHolderAngle), 1f);
            foreach (var board in BoardManager.Instance.gameBoards)
            {
                foreach (var slot in board.Slots)
                {
                    slot.gameObject.transform.rotation = Quaternion.Euler(0, 0, -rotateBoardAngle);
                }
            }
        }
    }
}
