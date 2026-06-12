using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class Launcher : MonoBehaviourPunCallbacks
{
    [Header("Prefab de jugador y spawn points")]
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    // Nombres de los prefabs individuales en Resources
    private string[] prefabsObjetos = { "Table", "Capsule", "Sphere", "Cube", "Cube1", "Cube2", "Cube3" };

    // Posiciones exactas de cada prefab en el mapa
    private Vector3[] posicionesObjetos =
    {
        new Vector3(33.87f, 0.37f, 42.60f),           // Table (base de Capsule, Sphere, Cube)
        new Vector3(31.37f, 1.5f, 42.14f),         // Capsule
        new Vector3(36.11f, 1.384f, 42.14f),       // Sphere
        new Vector3(33.68f, 1.422f, 42.14f),       // Cube
        new Vector3(25.69f, 0.09f, 20.39f),        // Cube1
        new Vector3(38.26998f, 0.09999585f, 18.27998f), // Cube2
        new Vector3(45.79f, 0.1299946f, 29.15999f)      // Cube3
    };

    void Start()
    {
        PhotonNetwork.JoinRandomOrCreateRoom();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Unido a la sala: " + PhotonNetwork.CurrentRoom.Name);

        // Spawn aleatorio para el jugador
        Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Instanciar jugador
        GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, spawn.position, spawn.rotation);
        player.GetComponent<PhotonView>().RPC("SetNameText", RpcTarget.AllBuffered, PlayerPrefs.GetString("PlayerName"));

        if (PhotonNetwork.IsMasterClient)
        {
            for (int i = 0; i < prefabsObjetos.Length; i++)
            {
                string nombrePrefab = prefabsObjetos[i];
                Vector3 spawnPos = posicionesObjetos[i];

                // Verificar que el prefab exista en Resources
                GameObject prefab = Resources.Load<GameObject>(nombrePrefab);
                if (prefab != null)
                {
                    // Instancia sin buffered para evitar duplicados
                    PhotonNetwork.Instantiate(nombrePrefab, spawnPos, Quaternion.identity, 0);
                }
                else
                {
                    Debug.LogError("Prefab no encontrado en Resources: " + nombrePrefab);
                }
            }
        }
    }
}
