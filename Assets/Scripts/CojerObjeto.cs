using UnityEngine;
using Photon.Pun;

public class CogerObjeto : MonoBehaviourPun
{
    [Header("Referencia de la mano")]
    public Transform handPoint;

    private GameObject pickedObject = null;
    private PhotonView pickedPhotonView = null;
    private PhotonTransformView ptv = null;
    private Collider pickedCollider = null;
    private Collider playerCollider = null;

    void Start()
    {
        // Obtiene el collider del jugador (para ignorar colisión)
        playerCollider = GetComponent<Collider>();
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (pickedObject != null && pickedPhotonView != null && pickedPhotonView.IsMine)
        {
            // Mover el objeto manualmente sin físicas
            pickedObject.transform.position = handPoint.position;
            pickedObject.transform.rotation = handPoint.rotation;

            if (Input.GetKeyDown(KeyCode.R))
            {
                SoltarObjeto();
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!photonView.IsMine) return;
        if (pickedObject != null) return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Objeto") && Input.GetKeyDown(KeyCode.E))
        {
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv == null) return;

            if (!pv.IsMine)
                pv.RequestOwnership();

            pickedObject = other.gameObject;
            pickedPhotonView = pv;

            // Guardar su collider
            pickedCollider = pickedObject.GetComponent<Collider>();

            // Desactivar física
            Rigidbody rb = pickedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Ignorar colisión entre jugador y objeto
            if (playerCollider != null && pickedCollider != null)
                Physics.IgnoreCollision(playerCollider, pickedCollider, true);

            // Desactivar PhotonTransformView temporalmente
            ptv = pickedObject.GetComponent<PhotonTransformView>();
            if (ptv != null)
                ptv.enabled = false;
        }
    }

    private void SoltarObjeto()
    {
        if (pickedObject == null || pickedPhotonView == null) return;

        Rigidbody rb = pickedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Reactivar PhotonTransformView
        if (ptv != null)
            ptv.enabled = true;

        // Volver a activar la colisión
        if (playerCollider != null && pickedCollider != null)
            Physics.IgnoreCollision(playerCollider, pickedCollider, false);

        // Transferir propiedad al MasterClient
        if (PhotonNetwork.IsMasterClient && pickedPhotonView != null)
        {
            pickedPhotonView.TransferOwnership(PhotonNetwork.MasterClient);
        }

        pickedObject = null;
        pickedPhotonView = null;
        ptv = null;
        pickedCollider = null;
    }
}
