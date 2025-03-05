using System.Collections.Generic;
using UnityEngine;

public class TestoutScript : MonoBehaviour
{
    [SerializeField]
       Virtue SpannableVirtue;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.V))
        {
            spawnVirtue();
            
        }
    }
    public void spawnVirtue()
    {
        foreach(Player player in PlayerManager.Instance.Players)
        {
            player.Virtues.Add(SpannableVirtue);
        }
        Debug.Log("virtue add to all for test out!!!");
    }
}
