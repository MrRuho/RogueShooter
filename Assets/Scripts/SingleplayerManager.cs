using UnityEngine;

public class SingleplayerManager : MonoBehaviour
{
    public GameObject playerPrefab;


    void Start()
    {
        // Create singleplayer
        Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
    }

}
