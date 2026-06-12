using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class GameManagerPUN : MonoBehaviourPunCallbacks
{
    [Header("Prefab del jugador")]
    public GameObject playerPrefab;

    [Header("Spawn")]
    public Vector3 spawnAreaMin = new Vector3(-5, 1, -5);
    public Vector3 spawnAreaMax = new Vector3(5, 1, 5);

    void Start()
    {
        // Conecta a Photon usando la configuración de PhotonServerSettings
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Conectado a Photon Master");
        // Intenta unirse o crear una sala llamada "Sala1"
        PhotonNetwork.JoinOrCreateRoom("Sala1", new RoomOptions { MaxPlayers = 10 }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Jugador unido a la sala: " + PhotonNetwork.CurrentRoom.Name);
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("No se ha asignado el prefab de jugador en el GameManager");
            return;
        }

        // Spawn aleatorio dentro del área definida
        Vector3 spawnPos = new Vector3(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            spawnAreaMin.y,
            Random.Range(spawnAreaMin.z, spawnAreaMax.z)
        );

        // Instancia del jugador en la red
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPos, Quaternion.identity);
    }
}
