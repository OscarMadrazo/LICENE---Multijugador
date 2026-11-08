using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// Entra a la sala elegida en el menú y crea al jugador en su punto de salida.
// Si se abre esta escena directamente desde el editor, juega en modo sin internet.
public class Launcher : MonoBehaviourPunCallbacks
{
    [Header("Prefab de jugador y spawn points")]
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    public const string ClaveSala = "SalaLICENE";
    public static string MensajeSalida; // Lo muestra el menú si se perdió la conexión.

    void Start()
    {
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 20;

        if (PhotonNetwork.InRoom) { OnJoinedRoom(); return; }
        if (!PhotonNetwork.IsConnected) PhotonNetwork.OfflineMode = true; // Dispara OnConnectedToMaster.
        else if (PhotonNetwork.IsConnectedAndReady) Entrar();
    }

    public override void OnConnectedToMaster() => Entrar();

    void Entrar()
    {
        if (PhotonNetwork.InRoom) return;
        if (PhotonNetwork.OfflineMode) { PhotonNetwork.CreateRoom("SinInternet"); return; }
        string sala = PlayerPrefs.GetString(ClaveSala, "LICENE").Trim().ToUpperInvariant();
        PhotonNetwork.JoinOrCreateRoom("LICENE-" + sala, new RoomOptions { MaxPlayers = 12, CleanupCacheOnLeave = true }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        int n = Mathf.Max(0, PhotonNetwork.LocalPlayer.ActorNumber - 1);
        Vector3 pos = transform.position;
        Quaternion rot = Quaternion.identity;
        if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[n % spawnPoints.Length] != null)
        {
            Transform p = spawnPoints[n % spawnPoints.Length];
            pos = p.position;
            rot = p.rotation;
        }
        PhotonNetwork.Instantiate(playerPrefab != null ? playerPrefab.name : "Player 1", pos, rot);
    }

    public override void OnJoinRoomFailed(short returnCode, string message) => Salir("No se pudo entrar a la sala: " + message);
    public override void OnCreateRoomFailed(short returnCode, string message) => Salir("No se pudo crear la sala: " + message);

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (cause == DisconnectCause.DisconnectByClientLogic || cause == DisconnectCause.None) return;
        Salir("Se perdió la conexión (" + cause + ")");
    }

    public static void Salir(string mensaje)
    {
        MensajeSalida = mensaje;
        PlayerController.BloquearCursor(false);
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        SceneManager.LoadScene("MainMenu_Multiplayer");
    }
}
