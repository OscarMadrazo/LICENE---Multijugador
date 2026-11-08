using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Reto cooperativo en red: todos los jugadores de la sala llevan figuras a la plataforma de su
// color antes de que se acabe el tiempo. El MasterClient crea las piezas, revisa las entregas y
// lleva el marcador (propiedades de la sala); todos ven el mismo reloj (PhotonNetwork.Time).
// Al terminar cada ronda, cada jugador reporta su resultado a la app LICENE (si inició sesión).
public class RetoCooperativo : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static RetoCooperativo Instance { get; private set; }
    public static Vector3 CentroMesa = new Vector3(35f, 1.2f, 31f);

    public const float DuracionRonda = 150f;
    public const float PausaEntreRondas = 12f;
    public const int PiezasEnJuego = 8;

    const string PInicio = "r_ini", PPuntos = "r_pts", PRonda = "r_num", PMeta = "r_meta";
    const string PEntregas = "p_ok", PErrores = "p_err";
    const byte EvEntrega = 1, EvError = 2;
    const string ClaveRecord = "licene_multijugador_record";

    public Transform mesa;
    public ZonaColor[] zonas;

    public enum Sonido { Tomar, Acierto, Error, Fin }

    readonly Dictionary<Sonido, AudioClip> sonidos = new Dictionary<Sonido, AudioClip>();
    readonly List<string> noticias = new List<string>();
    AudioSource audioFuente;
    Material particula;
    bool rondaReportada;
    int ultimaRonda = -1;
    float revisarEn;

    // HUD
    TextMeshProUGUI reloj, marcador, lista, aviso, ayuda, feed, resultado;
    Image mira, panelResultado, progreso, panelLista;
    float avisoHasta;
    string textoResultado = "";

    public int Puntos => Prop(PPuntos, 0);
    public int Meta => Prop(PMeta, 20);
    public int Ronda => Prop(PRonda, 0);
    public bool HayRonda => PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(PInicio);
    public double Inicio => HayRonda && PhotonNetwork.CurrentRoom.CustomProperties[PInicio] is double d ? d : 0.0;
    public float Restante => !HayRonda ? DuracionRonda : Mathf.Max(0f, DuracionRonda - (float)(PhotonNetwork.Time - Inicio));
    public bool RondaTerminada => HayRonda && Restante <= 0f;

    void Awake()
    {
        Instance = this;
        if (mesa != null) CentroMesa = mesa.position + Vector3.up * 1.2f;
        if (zonas == null || zonas.Length == 0) zonas = FindObjectsByType<ZonaColor>(FindObjectsSortMode.None).OrderBy(z => z.color).ToArray();
        audioFuente = gameObject.AddComponent<AudioSource>();
        audioFuente.spatialBlend = 0f;
        particula = Resources.Load<Material>("PiezasMat/Particula");
        CrearSonidos();
        ConstruirHud();
        CargarHistorial();
        ConstruirMenus();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    T Prop<T>(string clave, T defecto) =>
        PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(clave, out object v) && v is T t ? t : defecto;

    static int PropJugador(Player p, string clave) => p.CustomProperties.TryGetValue(clave, out object v) && v is int i ? i : 0;

    // ---------- Ciclo de juego ----------

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient && !HayRonda) NuevaRonda();
        Aviso(PhotonNetwork.OfflineMode ? "Modo práctica\nLleva cada figura a la plataforma de su color" : "¡Bienvenido a la sala!\nTrabajen en equipo: cada figura va a la plataforma de su color", 5f);
    }

    public override void OnMasterClientSwitched(Player nuevo)
    {
        if (nuevo.IsLocal) Aviso("Ahora tú coordinas la sala", 3f);
    }

    public override void OnPlayerEnteredRoom(Player p) => Noticia($"<color=#{ColorUtility.ToHtmlStringRGB(PlayerName.ColorDe(p))}>{p.NickName}</color> se unió");
    public override void OnPlayerLeftRoom(Player p) => Noticia($"{p.NickName} salió de la sala");

    void NuevaRonda()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        int jugadores = Mathf.Max(1, PhotonNetwork.CurrentRoom.PlayerCount);
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            [PInicio] = PhotonNetwork.Time,
            [PPuntos] = 0,
            [PRonda] = Ronda + 1,
            [PMeta] = 12 + jugadores * 6, // La meta crece con el equipo.
        });
        foreach (Player p in PhotonNetwork.PlayerList) p.SetCustomProperties(new Hashtable { [PEntregas] = 0, [PErrores] = 0 });
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom) { ActualizarHud(); return; }

        if (Ronda != ultimaRonda)
        {
            ultimaRonda = Ronda;
            rondaReportada = false;
            if (Ronda > 1) Aviso($"¡Ronda {Ronda}!\nMeta del equipo: {Meta} figuras", 4f);
        }

        if (RondaTerminada && !rondaReportada) TerminarRonda();

        if (PhotonNetwork.IsMasterClient && Time.time >= revisarEn)
        {
            revisarEn = Time.time + 0.15f;
            if (!RondaTerminada) { MantenerPiezas(); RevisarEntregas(); }
            else if (Restante <= 0f && PhotonNetwork.Time - Inicio > DuracionRonda + PausaEntreRondas) NuevaRonda();
        }
        ActualizarHud();
    }

    void MantenerPiezas()
    {
        int faltan = PiezasEnJuego - FindObjectsByType<PiezaLICENE>(FindObjectsSortMode.None).Length;
        for (int i = 0; i < faltan; i++) CrearPieza();
    }

    public void CrearPieza()
    {
        Vector2 r = Random.insideUnitCircle * 1.9f;
        Vector3 pos = CentroMesa + new Vector3(r.x, 0.6f + Random.value * 1.8f, r.y);
        PhotonNetwork.InstantiateRoomObject("PiezaLICENE", pos, Random.rotation, 0, new object[] { Random.Range(0, 4), Random.Range(0, zonas.Length) });
    }

    // El master revisa qué piezas sueltas están dentro de una plataforma.
    void RevisarEntregas()
    {
        foreach (PiezaLICENE p in FindObjectsByType<PiezaLICENE>(FindObjectsSortMode.None))
        {
            if (p.cargada) continue;
            foreach (ZonaColor z in zonas)
            {
                if (!z.Contiene(p.transform.position)) continue;
                bool ok = z.color == p.color;
                PhotonNetwork.RaiseEvent(ok ? EvEntrega : EvError,
                    new object[] { z.color, p.forma, p.color, p.ultimoJugador, p.photonView.ViewID },
                    new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);

                Player jugador = PhotonNetwork.CurrentRoom.GetPlayer(p.ultimoJugador);
                if (ok)
                {
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [PPuntos] = Puntos + 1 });
                    if (jugador != null) jugador.SetCustomProperties(new Hashtable { [PEntregas] = PropJugador(jugador, PEntregas) + 1 });
                    PhotonNetwork.Destroy(p.gameObject);
                }
                else if (jugador != null)
                {
                    jugador.SetCustomProperties(new Hashtable { [PErrores] = PropJugador(jugador, PErrores) + 1 });
                }
                break;
            }
        }
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code != EvEntrega && ev.Code != EvError) return;
        var d = (object[])ev.CustomData;
        int zona = (int)d[0], forma = (int)d[1], color = (int)d[2], actor = (int)d[3], vista = (int)d[4];
        ZonaColor z = zonas.FirstOrDefault(x => x.color == zona);
        Player quien = PhotonNetwork.CurrentRoom?.GetPlayer(actor);
        string nombre = quien != null ? $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerName.ColorDe(quien))}>{quien.NickName}</color>" : "Alguien";

        if (ev.Code == EvEntrega)
        {
            if (quien != null && quien.IsLocal) RegistrarEntrega(forma, color);
            if (z != null) { z.Celebrar(); Destello(z.transform.position + Vector3.up, PiezaLICENE.Tintes[color], 60); }
            Noticia($"{nombre} entregó {NombrePieza(forma, color)}  <color=#7CFF9A>+1</color>");
            Sonar(Sonido.Acierto);
            if (quien != null && quien.IsLocal) Aviso($"¡Bien! {NombrePieza(forma, color)}", 1.4f);
        }
        else
        {
            Sonar(Sonido.Error);
            if (quien != null && quien.IsLocal) Aviso($"¡Ups! {NombrePieza(forma, color)} va en la plataforma <color=#{ColorUtility.ToHtmlStringRGB(PiezaLICENE.Tintes[color])}>{PiezaLICENE.Colores[color].ToUpperInvariant()}</color>", 2.5f);
            PhotonView pv = PhotonView.Find(vista);
            if (pv != null && z != null) pv.GetComponent<PiezaLICENE>()?.Rebotar(z.transform.position);
        }
    }

    static string NombrePieza(int forma, int color)
    {
        bool femenino = forma == 1 || forma == 2;
        string c = PiezaLICENE.Colores[color].ToLowerInvariant();
        if (femenino && color != 3) c = c.Substring(0, c.Length - 1) + "a";
        return $"{PiezaLICENE.Formas[forma]} {c}";
    }

    void TerminarRonda()
    {
        rondaReportada = true;
        Sonar(Sonido.Fin);
        int puntos = Puntos;
        Player yo = PhotonNetwork.LocalPlayer;
        int mias = PropJugador(yo, PEntregas), errores = PropJugador(yo, PErrores);
        int record = PlayerPrefs.GetInt(ClaveRecord, 0);
        bool nuevoRecord = puntos > record;
        if (nuevoRecord) PlayerPrefs.SetInt(ClaveRecord, puntos);
        Destello(CentroMesa + Vector3.up * 3f, Color.white, 120);

        // Progreso para la app LICENE (se encola si no hay internet; solo si hay sesión iniciada).
        GuardarRonda(puntos, mias);
        if (LiceneCloud.SignedIn && (mias > 0 || errores > 0))
        {
            LiceneCloud.ReportMission("Multijugador_LICENE", "Laboratorio Cooperativo LICENE",
                "ronda_cooperativa", "Ronda cooperativa: clasificar por color",
                xp: mias * 10 + (puntos >= Meta ? 50 : 0), aciertos: mias, errores: errores,
                tiempoSegundos: (int)DuracionRonda, mundo: PhotonNetwork.OfflineMode ? "Práctica" : "Sala " + PlayerPrefs.GetString(Launcher.ClaveSala));
        }

        Player mejor = PhotonNetwork.PlayerList.OrderByDescending(p => PropJugador(p, PEntregas)).FirstOrDefault();
        string titulo = puntos >= Meta ? "<color=#7CFF9A>¡META CUMPLIDA!</color>" : "¡Tiempo!";
        textoResultado = $"{titulo}\n<size=60>El equipo entregó <b>{puntos}</b> de {Meta} figuras</size>\n" +
                         $"<size=44>Tú: {mias} entregas · {errores} errores</size>\n" +
                         (mejor != null && PropJugador(mejor, PEntregas) > 0 ? $"<size=40>Estrella de la ronda: <color=#{ColorUtility.ToHtmlStringRGB(PlayerName.ColorDe(mejor))}>{mejor.NickName}</color></size>\n" : "") +
                         (nuevoRecord ? "<size=44><color=#FFD54F>¡Nuevo récord del equipo!</color></size>\n" : $"<size=36>Récord: {Mathf.Max(record, puntos)}</size>\n") +
                         (LiceneCloud.SignedIn ? "<size=30><color=#9FE8FF>Progreso enviado a tu app LICENE</color></size>" : "<size=30><color=#FFFFFF99>Inicia sesión en el menú para guardar tu progreso</color></size>");
    }

    // ---------- Efectos ----------

    public void Sonar(Sonido s)
    {
        if (sonidos.TryGetValue(s, out AudioClip clip)) audioFuente.PlayOneShot(clip, s == Sonido.Tomar ? 0.4f : 0.7f);
    }

    void CrearSonidos()
    {
        sonidos[Sonido.Tomar] = Tono("tomar", new[] { 660f }, 0.08f);
        sonidos[Sonido.Acierto] = Tono("acierto", new[] { 523f, 659f, 784f, 1046f }, 0.09f);
        sonidos[Sonido.Error] = Tono("error", new[] { 220f, 180f }, 0.16f, cuadrada: true);
        sonidos[Sonido.Fin] = Tono("fin", new[] { 392f, 523f, 659f, 784f, 659f, 784f }, 0.14f);
    }

    static AudioClip Tono(string nombre, float[] notas, float duracionNota, bool cuadrada = false)
    {
        const int frecuencia = 44100;
        int porNota = (int)(frecuencia * duracionNota);
        var datos = new float[porNota * notas.Length];
        for (int n = 0; n < notas.Length; n++)
            for (int i = 0; i < porNota; i++)
            {
                float t = i / (float)frecuencia;
                float onda = Mathf.Sin(2f * Mathf.PI * notas[n] * t);
                if (cuadrada) onda = Mathf.Sign(onda) * 0.5f;
                float envolvente = Mathf.Min(1f, i / 300f) * (1f - i / (float)porNota);
                datos[n * porNota + i] = onda * envolvente * 0.5f;
            }
        AudioClip clip = AudioClip.Create(nombre, datos.Length, 1, frecuencia, false);
        clip.SetData(datos, 0);
        return clip;
    }

    void Destello(Vector3 pos, Color color, int cantidad)
    {
        if (LiceneAjustes.ModoCalma) return; // Sin destellos en modo calma.
        var go = new GameObject("Destello");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.gravityModifier = 0.8f;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emision = ps.emission;
        emision.rateOverTime = 0;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cantidad) });
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.5f;
        if (particula != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particula;
        ps.Play();
    }


    // ---------- Estadísticas y álbum guardados en el equipo ----------

    [System.Serializable]
    public class Historial
    {
        public int rondas, entregas, mejorEquipo;
        public bool[] album = new bool[16]; // forma * 4 + color
    }

    public static string RutaHistorial => System.IO.Path.Combine(Application.persistentDataPath, "multijugador_licene.json");
    public Historial historial = new Historial();
    GameObject album;
    readonly UnityEngine.UI.Image[] celdas = new UnityEngine.UI.Image[16];
    UnityEngine.UI.Text resumenAlbum;

    void CargarHistorial()
    {
        try
        {
            if (System.IO.File.Exists(RutaHistorial)) historial = JsonUtility.FromJson<Historial>(System.IO.File.ReadAllText(RutaHistorial)) ?? new Historial();
        }
        catch (System.Exception e) { Debug.LogWarning("Multijugador: historial dañado: " + e.Message); historial = new Historial(); }
        if (historial.album == null || historial.album.Length != 16) historial.album = new bool[16];
    }

    void GuardarHistorial()
    {
        try { System.IO.File.WriteAllText(RutaHistorial, JsonUtility.ToJson(historial)); }
        catch (System.Exception e) { Debug.LogWarning("Multijugador: no se pudo guardar: " + e.Message); }
    }

    public void RegistrarEntrega(int forma, int color)
    {
        historial.entregas++;
        int i = forma * 4 + color;
        if (i >= 0 && i < 16 && !historial.album[i])
        {
            historial.album[i] = true;
            Aviso($"¡Nueva figura en tu álbum!\n<size=40>{NombrePieza(forma, color)}  ·  Tab</size>", 2.5f);
        }
        GuardarHistorial();
    }

    void GuardarRonda(int puntosEquipo, int mias)
    {
        historial.rondas++;
        historial.mejorEquipo = Mathf.Max(historial.mejorEquipo, puntosEquipo);
        GuardarHistorial();
    }

    void ConstruirMenus()
    {
        Transform l = LiceneUI.Lienzo("Menus LICENE", transform, 20);
        var a = LiceneUI.Tarjeta("Album", l, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 760f), LiceneUI.Acento2);
        LiceneUI.Texto(a.transform, "Mi álbum de figuras", 46, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(900f, 64f));
        resumenAlbum = LiceneUI.Texto(a.transform, "", 24, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(900f, 34f), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 1f, 1f, 0.75f));
        for (int f = 0; f < 4; f++)
        {
            LiceneUI.Texto(a.transform, PiezaLICENE.Formas[f], 26, new Vector2(0.5f, 1f), new Vector2(-330f, -190f - f * 130f), new Vector2(200f, 40f), TextAnchor.MiddleLeft);
            for (int c = 0; c < 4; c++)
            {
                var celda = LiceneUI.Panel("Celda", a.transform, new Vector2(0.5f, 1f), new Vector2(-110f + c * 150f, -190f - f * 130f), new Vector2(110f, 110f), PiezaLICENE.Tintes[c]);
                if (f == 1) celda.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f); // la esfera se distingue girada
                celdas[f * 4 + c] = celda;
            }
        }
        LiceneUI.Texto(a.transform, "Tab para cerrar", 22, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(600f, 30f), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 1f, 1f, 0.6f));
        album = a.gameObject;
        album.SetActive(false);

        LiceneCuentaPanel.Crear(l);
        LiceneMenuPausa.Crear(l, "Laboratorio Cooperativo",
            "<b>Moverte:</b> WASD   ·   <b>Mirar:</b> mouse   ·   <b>Correr:</b> Shift   ·   <b>Saltar:</b> Espacio\n" +
            "<b>Tomar / soltar figura:</b> E   ·   <b>Lanzar:</b> clic\n" +
            "<b>Álbum:</b> Tab   ·   <b>Cuenta LICENE:</b> L   ·   <b>Menú:</b> Esc\n\n" +
            "Toma una figura de la mesa central y llévala a la plataforma de su mismo color.\n" +
            "Cada entrega suma un punto para todo el equipo. ¡Lleguen juntos a la meta!\n" +
            "Las figuras nuevas que entregas se guardan en tu álbum para siempre.",
            null, false, () => Launcher.Salir(null));
    }

    void ActualizarAlbum()
    {
        int n = 0;
        for (int i = 0; i < 16; i++)
        {
            bool tiene = historial.album[i];
            if (tiene) n++;
            Color c = PiezaLICENE.Tintes[i % 4];
            celdas[i].color = tiene ? c : new Color(c.r, c.g, c.b, 0.12f);
        }
        resumenAlbum.text = $"{n} de 16 figuras  ·  Rondas jugadas: {historial.rondas}  ·  Entregas: {historial.entregas}  ·  Mejor equipo: {historial.mejorEquipo}";
    }

    // ---------- HUD ----------

    void ConstruirHud()
    {
        Canvas canvas = UIKit.Canvas("HUD Multijugador", 5);
        Transform c = canvas.transform;
        var arriba = new Vector2(0.5f, 1f);

        // Barra superior: reloj | marcador del equipo, con franja de acento y barra de progreso.
        Image barra = UIKit.Panel("Barra", c, arriba, new Vector2(0f, -60f), new Vector2(860f, 96f), UIKit.Fondo);
        UIKit.Panel("Acento", barra.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(860f, 4f), UIKit.Acento);
        UIKit.Panel("Separador", barra.transform, new Vector2(0f, 0.5f), new Vector2(250f, 0f), new Vector2(2f, 60f), new Color(1f, 1f, 1f, 0.2f));
        reloj = UIKit.Texto(barra.transform, "2:30", 54f, new Vector2(0f, 0.5f), new Vector2(15f, 2f), new Vector2(230f, 80f), TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        marcador = UIKit.Texto(barra.transform, "", 40f, new Vector2(0f, 0.5f), new Vector2(265f, 10f), new Vector2(580f, 60f), TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        UIKit.Panel("Progreso fondo", barra.transform, new Vector2(0f, 0.5f), new Vector2(295f, -28f), new Vector2(520f, 10f), new Color(1f, 1f, 1f, 0.12f));
        progreso = UIKit.Panel("Progreso", barra.transform, new Vector2(0f, 0.5f), new Vector2(295f, -28f), new Vector2(0f, 10f), new Color(0.45f, 1f, 0.6f));
        progreso.rectTransform.pivot = new Vector2(0f, 0.5f);

        panelLista = UIKit.Panel("Jugadores", c, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(380f, 120f), new Color(0.06f, 0.09f, 0.18f, 0.7f));
        panelLista.rectTransform.pivot = new Vector2(0f, 1f);
        UIKit.Panel("Acento", panelLista.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(380f, 4f), UIKit.Acento2);
        lista = UIKit.Texto(panelLista.transform, "", 28f, new Vector2(0.5f, 1f), new Vector2(8f, -14f), new Vector2(340f, 600f), TextAlignmentOptions.TopLeft);
        lista.rectTransform.pivot = new Vector2(0.5f, 1f);

        feed = UIKit.Texto(c, "", 28f, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(620f, 300f), TextAlignmentOptions.TopRight);
        feed.rectTransform.pivot = new Vector2(1f, 1f);

        mira = UIKit.Panel("Mira", c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Color(1f, 1f, 1f, 0.8f));
        aviso = UIKit.Texto(c, "", 56f, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1500f, 220f), TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.45f), FontStyles.Bold);
        aviso.outlineWidth = 0.2f;
        ayuda = UIKit.Texto(c, "", 32f, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(1500f, 60f), TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        ayuda.outlineWidth = 0.25f;
        UIKit.Texto(c, "WASD mover · Mouse mirar · <b>E</b> tomar/soltar · <b>Clic</b> lanzar · Espacio saltar · Shift correr · <b>Tab</b> álbum · <b>Esc</b> menú",
            24f, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 40f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.65f));

        panelResultado = UIKit.Estirar(UIKit.Panel("Resultado", c, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.03f, 0.05f, 0.12f, 0.75f)));
        resultado = UIKit.Texto(panelResultado.transform, "", 90f, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1600f, 800f), TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        panelResultado.gameObject.SetActive(false);
    }

    public void Aviso(string texto, float segundos)
    {
        if (aviso == null) return;
        aviso.text = texto;
        avisoHasta = Time.time + segundos;
    }

    void Noticia(string texto)
    {
        noticias.Insert(0, texto);
        if (noticias.Count > 6) noticias.RemoveAt(noticias.Count - 1);
    }

    void ActualizarHud()
    {
        if (LiceneUI.TeclaPresionada(KeyCode.Tab) && album != null) album.SetActive(!album.activeSelf);
        if (album != null && album.activeSelf) ActualizarAlbum();

        if (!PhotonNetwork.InRoom)
        {
            reloj.text = "--:--";
            marcador.text = "Conectando...";
            return;
        }

        float r = Restante;
        bool fin = RondaTerminada;
        reloj.text = LiceneAjustes.SinTiempo ? "<size=34>Ronda</size>" : $"{(int)(r / 60f)}:{(int)(r % 60f):00}";
        reloj.color = r < 20f && !fin ? Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), Mathf.PingPong(Time.time * 2f, 1f)) : Color.white;
        marcador.text = $"Equipo  <color=#7CFF9A>{Puntos}</color> / {Meta}";

        var sb = new System.Text.StringBuilder($"<b>Sala {(PhotonNetwork.OfflineMode ? "práctica" : PlayerPrefs.GetString(Launcher.ClaveSala, "LICENE"))}</b>  <size=22>({PhotonNetwork.CurrentRoom.PlayerCount} jugadores)</size>\n");
        foreach (Player p in PhotonNetwork.PlayerList.OrderByDescending(x => PropJugador(x, PEntregas)))
            sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(PlayerName.ColorDe(p))}>■</color> {(p.IsLocal ? "<b>" + p.NickName + " (tú)</b>" : p.NickName)}  <color=#7CFF9A>{PropJugador(p, PEntregas)}</color>\n");
        lista.text = sb.ToString();
        panelLista.rectTransform.sizeDelta = new Vector2(380f, 64f + 36f * PhotonNetwork.CurrentRoom.PlayerCount);
        progreso.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(progreso.rectTransform.sizeDelta.x, 520f * Mathf.Clamp01(Puntos / (float)Mathf.Max(1, Meta)), Time.deltaTime * 6f), 10f);
        feed.text = string.Join("\n", noticias);

        aviso.gameObject.SetActive(Time.time < avisoHasta && !fin);
        panelResultado.gameObject.SetActive(fin);
        if (fin)
        {
            float siguiente = Mathf.Max(0f, (float)(Inicio + DuracionRonda + PausaEntreRondas - PhotonNetwork.Time));
            ayuda.text = "";
            resultado.text = textoResultado + $"\n<size=28>Siguiente ronda en {Mathf.CeilToInt(siguiente)} s</size>";
            return;
        }

        // Ayuda contextual según lo que mira o carga el jugador local.
        CogerObjeto mano = PlayerController.Local != null ? PlayerController.Local.GetComponentInChildren<CogerObjeto>() : null;
        string hex(int col) => ColorUtility.ToHtmlStringRGB(PiezaLICENE.Tintes[col]);
        if (mano != null && mano.Cargando != null)
            ayuda.text = $"Llevas: <color=#{hex(mano.Cargando.color)}>{mano.Cargando.Nombre}</color>  ·  llévala a la plataforma <color=#{hex(mano.Cargando.color)}>{PiezaLICENE.Colores[mano.Cargando.color].ToUpperInvariant()}</color>";
        else if (mano != null && mano.Mirando != null)
            ayuda.text = $"<b>E</b>  tomar <color=#{hex(mano.Mirando.color)}>{mano.Mirando.Nombre}</color>";
        else
            ayuda.text = "";
        mira.color = mano != null && mano.Mirando != null ? UIKit.Acento : new Color(1f, 1f, 1f, 0.8f);
    }
}
