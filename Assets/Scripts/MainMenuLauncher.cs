using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú principal: nombre, código de sala (para que un grupo juegue junto), jugar en línea o sin
// internet, y cuenta LICENE opcional para guardar el progreso en la app. La interfaz se arma con código.
public class MainMenuLauncher : MonoBehaviourPunCallbacks
{
    public const string VersionJuego = "licene-2";

    TMP_InputField nombre, sala, correo, clave;
    TextMeshProUGUI estado, estadoCuenta;
    Button botonEnLinea, botonSinInternet, botonCuenta;
    Transform figuras;
    bool conectando;

    void Start()
    {
        PlayerController.BloquearCursor(false);
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        PhotonNetwork.OfflineMode = false;
        PhotonNetwork.GameVersion = VersionJuego;

        // La interfaz vieja de la escena se reemplaza por la nueva.
        foreach (Canvas viejo in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) viejo.gameObject.SetActive(false);
        ConstruirFondo();
        ConstruirInterfaz();

        nombre.text = PlayerPrefs.GetString("PlayerName", "");
        sala.text = PlayerPrefs.GetString(Launcher.ClaveSala, "LICENE");
        correo.text = LiceneCloud.Email;
        if (!string.IsNullOrEmpty(Launcher.MensajeSalida)) { Estado(Launcher.MensajeSalida, true); Launcher.MensajeSalida = null; }
        ActualizarCuenta();
        LiceneCloud.Flush();
    }

    // ---------- Fondo animado: figuras de colores flotando ----------

    void ConstruirFondo()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.08f, 0.16f);
        figuras = new GameObject("Figuras").transform;
        string[] mallas = { "Cube.fbx", "Sphere.fbx", "Capsule.fbx", "Cylinder.fbx" };
        var rnd = new System.Random(7);
        for (int i = 0; i < 18; i++)
        {
            var go = new GameObject("Figura");
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(mallas[i % 4]);
            var mat = Resources.Load<Material>("PiezasMat/" + PiezaLICENE.Colores[i % 4]);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.SetParent(figuras, false);
            go.transform.position = cam.transform.position + cam.transform.forward * (8f + (float)rnd.NextDouble() * 10f)
                + cam.transform.right * ((float)rnd.NextDouble() * 22f - 11f) + cam.transform.up * ((float)rnd.NextDouble() * 12f - 6f);
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * (0.6f + (float)rnd.NextDouble() * 0.9f);
        }
    }

    void Update()
    {
        if (figuras == null) return;
        int i = 0;
        foreach (Transform f in figuras)
        {
            f.Rotate(new Vector3(20f + i * 3f, 30f - i * 2f, 10f) * Time.deltaTime, Space.Self);
            f.position += Vector3.up * Mathf.Sin(Time.time * 0.6f + i) * 0.004f;
            i++;
        }
        if (Input.GetKeyDown(KeyCode.Return) && !conectando && !correo.isFocused && !clave.isFocused) JugarEnLinea();
    }

    // ---------- Interfaz ----------

    void ConstruirInterfaz()
    {
        Canvas canvas = UIKit.Canvas("Menu LICENE");
        Transform c = canvas.transform;
        var izquierda = new Vector2(0f, 0.5f);

        UIKit.Texto(c, "<color=#33BFFF>LICENE</color>", 150f, izquierda, new Vector2(140f, 250f), new Vector2(900f, 180f), TextAlignmentOptions.Left, null, FontStyles.Bold).rectTransform.pivot = new Vector2(0f, 0.5f);
        UIKit.Texto(c, "Laboratorio Cooperativo", 64f, izquierda, new Vector2(140f, 140f), new Vector2(900f, 90f), TextAlignmentOptions.Left, UIKit.Acento2, FontStyles.Bold).rectTransform.pivot = new Vector2(0f, 0.5f);
        UIKit.Texto(c, "Juega con tus compañeros en el mismo mundo.\nTomen las figuras y llévenlas a la plataforma de su color\nantes de que se acabe el tiempo. ¡Solo en equipo se logra el récord!",
            32f, izquierda, new Vector2(140f, 10f), new Vector2(900f, 160f), TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.85f)).rectTransform.pivot = new Vector2(0f, 0.5f);
        UIKit.Texto(c, "<b>Controles</b>   WASD mover · Mouse mirar · E tomar/soltar · Clic lanzar · Espacio saltar · Shift correr",
            26f, izquierda, new Vector2(140f, -120f), new Vector2(1000f, 60f), TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.6f)).rectTransform.pivot = new Vector2(0f, 0.5f);

        // Tarjeta para jugar.
        var derecha = new Vector2(1f, 0.5f);
        Image tarjeta = UIKit.Panel("Tarjeta", c, derecha, new Vector2(-120f, 120f), new Vector2(640f, 600f), UIKit.Fondo);
        tarjeta.rectTransform.pivot = new Vector2(1f, 0.5f);
        Transform t = tarjeta.transform;
        var arriba = new Vector2(0.5f, 1f);
        UIKit.Texto(t, "¡A jugar!", 48f, arriba, new Vector2(0f, -60f), new Vector2(560f, 70f), TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        UIKit.Texto(t, "Tu nombre", 26f, arriba, new Vector2(0f, -120f), new Vector2(540f, 34f), TextAlignmentOptions.Left, UIKit.Acento);
        nombre = UIKit.Campo(t, "Escribe tu nombre", arriba, new Vector2(0f, -165f), new Vector2(540f, 64f));
        nombre.characterLimit = 16;
        UIKit.Texto(t, "Código de sala (el mismo para todo el grupo)", 26f, arriba, new Vector2(0f, -225f), new Vector2(540f, 34f), TextAlignmentOptions.Left, UIKit.Acento);
        sala = UIKit.Campo(t, "LICENE", arriba, new Vector2(0f, -270f), new Vector2(540f, 64f));
        sala.characterLimit = 12;
        sala.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        botonEnLinea = UIKit.Boton(t, "Jugar en línea", arriba, new Vector2(0f, -360f), new Vector2(540f, 80f), new Color(0.15f, 0.6f, 0.95f), JugarEnLinea);
        botonSinInternet = UIKit.Boton(t, "Practicar sin internet", arriba, new Vector2(0f, -455f), new Vector2(540f, 70f), new Color(1f, 1f, 1f, 0.15f), JugarSinInternet);
        estado = UIKit.Texto(t, "", 26f, arriba, new Vector2(0f, -540f), new Vector2(560f, 70f));

        // Cuenta LICENE (opcional).
        Image cuenta = UIKit.Panel("Cuenta", c, derecha, new Vector2(-120f, -350f), new Vector2(640f, 300f), UIKit.Fondo);
        cuenta.rectTransform.pivot = new Vector2(1f, 0.5f);
        Transform k = cuenta.transform;
        UIKit.Texto(k, "Cuenta LICENE <size=24><color=#FFFFFFAA>(opcional, guarda tu progreso en la app)</color></size>", 30f, arriba, new Vector2(0f, -38f), new Vector2(600f, 50f), TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        correo = UIKit.Campo(k, "Correo", arriba, new Vector2(-135f, -105f), new Vector2(270f, 58f));
        correo.contentType = TMP_InputField.ContentType.EmailAddress;
        clave = UIKit.Campo(k, "Contraseña", arriba, new Vector2(150f, -105f), new Vector2(270f, 58f), true);
        botonCuenta = UIKit.Boton(k, "Iniciar sesión", arriba, new Vector2(0f, -180f), new Vector2(540f, 62f), new Color(1f, 0.55f, 0.15f), IniciarSesion);
        estadoCuenta = UIKit.Texto(k, "", 24f, arriba, new Vector2(0f, -250f), new Vector2(600f, 50f));
    }

    void Estado(string texto, bool error = false)
    {
        estado.text = texto;
        estado.color = error ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 1f, 1f, 0.85f);
    }

    void ActualizarCuenta()
    {
        estadoCuenta.text = LiceneCloud.SignedIn ? "<color=#7CFF9A>Sesión iniciada: " + LiceneCloud.Email + "</color>" : LiceneCloud.StatusText;
        botonCuenta.GetComponentInChildren<TextMeshProUGUI>().text = LiceneCloud.SignedIn ? "Cerrar sesión" : "Iniciar sesión";
    }

    void IniciarSesion()
    {
        if (LiceneCloud.SignedIn) { LiceneCloud.SignOut(); ActualizarCuenta(); return; }
        if (string.IsNullOrWhiteSpace(correo.text) || string.IsNullOrEmpty(clave.text)) { estadoCuenta.text = "Escribe tu correo y contraseña de LICENE"; return; }
        if (LiceneCloud.IniciandoSesion) return;
        botonCuenta.interactable = false;
        estadoCuenta.text = "Conectando con LICENE...";
        LiceneCloud.SignIn(correo.text.Trim(), clave.text, (ok, mensaje) =>
        {
            if (botonCuenta == null) return; // la escena ya cambió
            botonCuenta.interactable = true;
            clave.text = "";
            ActualizarCuenta();
            if (!ok) estadoCuenta.text = "<color=#FF8A80>" + mensaje + "</color>";
        });
    }

    bool GuardarDatos()
    {
        string n = nombre.text.Trim();
        if (n.Length == 0) { Estado("Escribe tu nombre para que tus compañeros te reconozcan", true); return false; }
        PhotonNetwork.NickName = n;
        PlayerPrefs.SetString("PlayerName", n);
        string s = string.IsNullOrWhiteSpace(sala.text) ? "LICENE" : sala.text.Trim().ToUpperInvariant();
        PlayerPrefs.SetString(Launcher.ClaveSala, s);
        return true;
    }

    public void JugarEnLinea()
    {
        if (conectando || !GuardarDatos()) return;
        conectando = true;
        botonEnLinea.interactable = botonSinInternet.interactable = false;
        Estado("Conectando al servidor...");
        PhotonNetwork.OfflineMode = false;
        if (!PhotonNetwork.ConnectUsingSettings()) FalloConexion("No se pudo iniciar la conexión");
    }

    public void JugarSinInternet()
    {
        if (conectando) return;
        if (nombre.text.Trim().Length == 0) nombre.text = "Explorador";
        if (!GuardarDatos()) return;
        conectando = true;
        PhotonNetwork.OfflineMode = true; // Dispara OnConnectedToMaster al instante.
    }

    // Compatibilidad con el botón de la interfaz anterior.
    public void OnClickConnect() => JugarEnLinea();

    public override void OnConnectedToMaster()
    {
        Estado(PhotonNetwork.OfflineMode ? "Modo práctica..." : "¡Conectado! Entrando a la sala " + PlayerPrefs.GetString(Launcher.ClaveSala) + "...");
        SceneManager.LoadScene("SceneMultiplayer");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (conectando && cause != DisconnectCause.DisconnectByClientLogic) FalloConexion("No hay conexión con el servidor (" + cause + "). Revisa tu internet o practica sin internet.");
    }

    void FalloConexion(string mensaje)
    {
        conectando = false;
        botonEnLinea.interactable = botonSinInternet.interactable = true;
        Estado(mensaje, true);
    }
}
