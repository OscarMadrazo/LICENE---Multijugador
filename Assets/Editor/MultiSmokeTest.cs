using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prueba automática del Laboratorio Cooperativo en modo sin internet: menú -> sala -> jugador,
// piezas, tomar/soltar, entrega correcta, entrega equivocada, fin de ronda y capturas.
// Batch: -executeMethod MultiSmokeTest.Run (sale con código 0 si todo pasa).
[InitializeOnLoad]
public static class MultiSmokeTest
{
    const string Flag = "MultiSmokeTest.Activo";
    static readonly List<string> resultados = new List<string>();
    static readonly List<string> errores = new List<string>();
    static int paso;
    static double esperarHasta;
    static int fallos;
    static string respaldoCola;

    static MultiSmokeTest()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if ((type == LogType.Error || type == LogType.Exception) && EditorApplication.isPlaying) errores.Add(msg);
        };
    }

    public static void Run()
    {
        SessionState.SetBool(Flag, true);
        SessionState.SetInt("MultiSmokeTest.Paso", 0);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu_Multiplayer.unity");
        EditorApplication.isPlaying = true;
    }

    static void Check(string nombre, bool ok, string detalle = "")
    {
        resultados.Add($"{(ok ? "OK " : "FALLA")} {nombre} {detalle}");
        if (!ok) fallos++;
    }

    static bool Esperar(double s)
    {
        if (esperarHasta == 0) { esperarHasta = EditorApplication.timeSinceStartup + s; return true; }
        if (EditorApplication.timeSinceStartup < esperarHasta) return true;
        esperarHasta = 0;
        return false;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try { Paso(); }
        catch (System.Exception e) { Check("Excepción en la prueba", false, e.ToString()); Terminar(); }
    }

    static void Paso()
    {
        switch (paso)
        {
            case 0:
                if (Esperar(1.5)) return;
                respaldoCola = LiceneCloud.ExportPending();
                var menu = Object.FindFirstObjectByType<MainMenuLauncher>();
                Check("Menú creado", menu != null && GameObject.Find("Menu LICENE") != null);
                Captura("multi_menu.png", Camera.main, null, null, true);
                menu.JugarSinInternet();
                paso++;
                break;
            case 1:
                if (Esperar(4)) return;
                Check("Entró a la sala", PhotonNetwork.InRoom, PhotonNetwork.CurrentRoom?.Name);
                Check("Un solo jugador local", Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length == 1);
                Check("Piezas creadas", Object.FindObjectsByType<PiezaLICENE>(FindObjectsSortMode.None).Length == RetoCooperativo.PiezasEnJuego);
                Check("4 plataformas", RetoCooperativo.Instance != null && RetoCooperativo.Instance.zonas.Length == 4);
                Check("Ronda iniciada", RetoCooperativo.Instance.Ronda == 1 && RetoCooperativo.Instance.Restante > 100f);
                paso++;
                break;
            case 2:
            {
                // Tomar una pieza con la mano del jugador.
                var mano = PlayerController.Local.GetComponentInChildren<CogerObjeto>();
                var pieza = Object.FindObjectsByType<PiezaLICENE>(FindObjectsSortMode.None).First();
                mano.IntentarTomar(pieza);
                Check("Tomar pieza", mano.Cargando == pieza && pieza.cargada);
                paso = 21;
                break;
            }
            case 21:
            {
                if (Esperar(0.6)) return;
                var mano = PlayerController.Local.GetComponentInChildren<CogerObjeto>();
                var pieza = mano.Cargando;
                Check("La pieza sigue a la mano", pieza != null && Vector3.Distance(pieza.transform.position, PlayerController.Local.camara.position) < 2.5f);
                Captura("multi_juego.png", PlayerController.Local.camara.GetComponent<Camera>(), null, null, true);
                mano.Soltar(Vector3.zero);
                Check("Soltar pieza", mano.Cargando == null && pieza != null && !pieza.cargada);
                paso = 3;
                break;
            }
            case 3:
            {
                // Entrega correcta: se pone la pieza sobre la plataforma de su color.
                var pieza = Object.FindObjectsByType<PiezaLICENE>(FindObjectsSortMode.None).First(p => !p.cargada);
                var zona = RetoCooperativo.Instance.zonas.First(z => z.color == pieza.color);
                pieza.ultimoJugador = PhotonNetwork.LocalPlayer.ActorNumber;
                pieza.Reaparecer(zona.transform.position + Vector3.up * 0.6f);
                paso++;
                break;
            }
            case 4:
                if (Esperar(1.5)) return;
                Check("Entrega suma punto", RetoCooperativo.Instance.Puntos == 1, "puntos=" + RetoCooperativo.Instance.Puntos);
                Check("Se repone la pieza", Object.FindObjectsByType<PiezaLICENE>(FindObjectsSortMode.None).Length == RetoCooperativo.PiezasEnJuego);
                {
                    // Entrega equivocada.
                    var pieza = Object.FindObjectsByType<PiezaLICENE>(FindObjectsSortMode.None).First(p => !p.cargada);
                    var zona = RetoCooperativo.Instance.zonas.First(z => z.color != pieza.color);
                    pieza.ultimoJugador = PhotonNetwork.LocalPlayer.ActorNumber;
                    pieza.Reaparecer(zona.transform.position + Vector3.up * 0.6f);
                }
                paso++;
                break;
            case 5:
                if (Esperar(1.5)) return;
                Check("Error no suma punto", RetoCooperativo.Instance.Puntos == 1);
                Check("Error registrado", PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("p_err", out object e) && (int)e >= 1);
                Captura("multi_vista.png", PlayerController.Local.camara.GetComponent<Camera>(), new Vector3(35f, 14f, 8f), new Vector3(35f, 0f, 31f));
                // Fin de ronda: se adelanta el reloj.
                PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["r_ini"] = PhotonNetwork.Time - RetoCooperativo.DuracionRonda - 1 });
                paso++;
                break;
            case 6:
                if (Esperar(1.5)) return;
                Check("Fin de ronda", RetoCooperativo.Instance.RondaTerminada && GameObject.Find("Resultado") != null && GameObject.Find("Resultado").activeInHierarchy);
                Captura("multi_resultado.png", PlayerController.Local.camara.GetComponent<Camera>(), null, null, true);
                PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["r_ini"] = PhotonNetwork.Time - RetoCooperativo.DuracionRonda - RetoCooperativo.PausaEntreRondas - 1 });
                paso++;
                break;
            case 7:
                if (Esperar(1.5)) return;
                Check("Nueva ronda", RetoCooperativo.Instance.Ronda == 2 && RetoCooperativo.Instance.Puntos == 0 && !RetoCooperativo.Instance.RondaTerminada);
                var errs = errores.Distinct().ToList();
                Check("Sin errores en consola", errs.Count == 0, string.Join(" | ", errs.Take(5)));
                Terminar();
                break;
        }
    }

    static void Captura(string archivo, Camera cam, Vector3? pos = null, Vector3? mirar = null, bool conUI = false)
    {
        if (cam == null) return;
        Vector3 p0 = cam.transform.position;
        Quaternion r0 = cam.transform.rotation;
        if (pos.HasValue) { cam.transform.position = pos.Value; cam.transform.LookAt(mirar.Value); }
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        // Las interfaces "Overlay" no salen en un render de cámara: se pasan a modo cámara un momento.
        var lienzos = conUI ? Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList() : new List<Canvas>();
        foreach (Canvas c in lienzos) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.1f; }
        Canvas.ForceUpdateCanvases();
        cam.Render();
        foreach (Canvas c in lienzos) c.renderMode = RenderMode.ScreenSpaceOverlay;
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        File.WriteAllBytes(archivo, tex.EncodeToPNG());
        cam.transform.SetPositionAndRotation(p0, r0);
    }

    static void Terminar()
    {
        if (respaldoCola != null) LiceneCloud.ImportPending(respaldoCola);
        File.WriteAllText("multi_smoke.txt", string.Join("\n", resultados) + $"\n{resultados.Count - fallos}/{resultados.Count} OK");
        Debug.Log("MultiSmokeTest:\n" + string.Join("\n", resultados));
        SessionState.SetBool(Flag, false);
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(fallos == 0 ? 0 : 1);
    }
}
