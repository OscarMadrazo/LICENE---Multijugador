using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Conexión de los juegos de Unity con la app LICENE (Firebase) sin instalar el SDK
// de Firebase: usa las APIs REST de Firebase Auth y Firestore.
//
// - Inicia sesión con el mismo correo y contraseña de la app y recuerda la sesión.
// - Cada misión terminada se guarda en users/{uid}/games/{juego}/missions/{misión} y
//   suma a users/{uid}/games/{juego}/stats/general (xpTotal, aciertosTotales,
//   erroresTotales, tiempoTotal, misionesCompletadas), que es lo que lee la app.
// - Sin internet o sin sesión, los avances se guardan en una cola y se envían después.
//
// Este archivo es el mismo en todos los proyectos de Unity de LICENE.
public class LiceneCloud : MonoBehaviour
{
    // Configuración del proyecto de Firebase de LICENE (claves públicas de cliente;
    // la seguridad la dan las reglas de Firestore).
    const string ApiKey = "AIzaSyC35MsWdnlnXdz_WmJPPrnXyK8Ro5NUrwM";
    const string ProjectId = "licene-cac84";

    const string KeyRefresh = "licene_refresh_token";
    const string KeyUid = "licene_uid";
    const string KeyEmail = "licene_email";
    const string KeyQueue = "licene_pending";

    static LiceneCloud instance;
    string idToken;
    float idTokenExpires;

    public static bool SignedIn => !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyRefresh, ""));
    public static string Email => PlayerPrefs.GetString(KeyEmail, "");
    public static int PendingCount => LoadQueue().Count;

    static LiceneCloud Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("LiceneCloud");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<LiceneCloud>();
            }
            return instance;
        }
    }

    // ---------- Sesión ----------

    public static void SignIn(string email, string password, Action<bool, string> done)
    {
        Instance.StartCoroutine(Instance.SignInRoutine(email.Trim(), password, done));
    }

    public static void SignOut()
    {
        PlayerPrefs.DeleteKey(KeyRefresh);
        PlayerPrefs.DeleteKey(KeyUid);
        PlayerPrefs.DeleteKey(KeyEmail);
        PlayerPrefs.Save();
        if (instance != null) instance.idToken = null;
    }

    IEnumerator SignInRoutine(string email, string password, Action<bool, string> done)
    {
        string body = "{\"email\":" + Json(email) + ",\"password\":" + Json(password) + ",\"returnSecureToken\":true}";
        using (UnityWebRequest req = Post("https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" + ApiKey, body))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                done?.Invoke(false, FriendlyAuthError(req));
                yield break;
            }

            var res = JsonUtility.FromJson<SignInResponse>(req.downloadHandler.text);
            idToken = res.idToken;
            idTokenExpires = Time.realtimeSinceStartup + ParseSeconds(res.expiresIn) - 60f;
            PlayerPrefs.SetString(KeyRefresh, res.refreshToken);
            PlayerPrefs.SetString(KeyUid, res.localId);
            PlayerPrefs.SetString(KeyEmail, email);
            PlayerPrefs.Save();
        }

        done?.Invoke(true, "");
        Flush();
    }

    IEnumerator EnsureToken(Action<bool> done)
    {
        if (!string.IsNullOrEmpty(idToken) && Time.realtimeSinceStartup < idTokenExpires)
        {
            done(true);
            yield break;
        }

        string refresh = PlayerPrefs.GetString(KeyRefresh, "");
        if (string.IsNullOrEmpty(refresh))
        {
            done(false);
            yield break;
        }

        var form = new WWWForm();
        form.AddField("grant_type", "refresh_token");
        form.AddField("refresh_token", refresh);
        using (UnityWebRequest req = UnityWebRequest.Post("https://securetoken.googleapis.com/v1/token?key=" + ApiKey, form))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                // Un 400 significa sesión inválida (contraseña cambiada, cuenta borrada).
                if (req.responseCode == 400) SignOut();
                done(false);
                yield break;
            }

            var res = JsonUtility.FromJson<RefreshResponse>(req.downloadHandler.text);
            idToken = res.id_token;
            idTokenExpires = Time.realtimeSinceStartup + ParseSeconds(res.expires_in) - 60f;
            PlayerPrefs.SetString(KeyRefresh, res.refresh_token);
            PlayerPrefs.Save();
        }

        done(true);
    }

    // ---------- Progreso ----------

    [Serializable]
    public class Mission
    {
        public string gameId;      // Id del juego en la app (ej. "Bosque_VR").
        public string gameTitle;   // Nombre visible.
        public string missionId;   // Id estable de la misión.
        public string missionName;
        public int xp;
        public int aciertos;
        public int errores;
        public int tiempo;         // Segundos.
        public string mundo;
        public string fechaIso;
    }

    // Registra una misión terminada. Nunca falla para el jugador: si no se puede enviar,
    // queda en la cola y se reintenta en el siguiente envío o al iniciar sesión.
    public static void ReportMission(string gameId, string gameTitle, string missionId, string missionName,
        int xp, int aciertos, int errores, int tiempoSegundos, string mundo = "")
    {
        var mission = new Mission
        {
            gameId = gameId,
            gameTitle = gameTitle,
            missionId = Sanitize(missionId),
            missionName = missionName,
            xp = Mathf.Max(0, xp),
            aciertos = Mathf.Max(0, aciertos),
            errores = Mathf.Max(0, errores),
            tiempo = Mathf.Max(0, tiempoSegundos),
            mundo = mundo,
            fechaIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
        };

        List<Mission> queue = LoadQueue();
        queue.Add(mission);
        SaveQueue(queue);
        Debug.Log(SignedIn
            ? $"LiceneCloud: enviando \"{missionName}\" a la cuenta {Email}"
            : $"LiceneCloud: \"{missionName}\" guardada en este dispositivo (sin cuenta). Se enviará al iniciar sesión.");
        Flush();
    }

    // Texto corto para mostrar al jugador dónde se guarda su progreso.
    public static string StatusText => SignedIn
        ? "Guardando en tu cuenta: " + Email
        : "Sin cuenta: tu progreso se guarda aquí y se enviará al entrar";

    // Solo para pruebas automáticas: respaldar y restaurar la cola sin perder avances reales.
    public static string ExportPending() => PlayerPrefs.GetString(KeyQueue, "");

    public static void ImportPending(string json)
    {
        PlayerPrefs.SetString(KeyQueue, json ?? "");
        PlayerPrefs.Save();
    }

    public static void Flush()
    {
        if (!Instance.flushing) Instance.StartCoroutine(Instance.FlushRoutine());
    }

    bool flushing;

    IEnumerator FlushRoutine()
    {
        flushing = true;
        bool ok = false;
        yield return EnsureToken(r => ok = r);

        while (ok)
        {
            List<Mission> queue = LoadQueue();
            if (queue.Count == 0) break;

            Mission m = queue[0];
            bool sent = false;
            yield return Commit(m, r => sent = r);
            if (!sent) break;

            queue.RemoveAt(0);
            SaveQueue(queue);
            Debug.Log($"LiceneCloud: \"{m.missionName}\" guardada en la app LICENE");
        }

        flushing = false;
    }

    IEnumerator Commit(Mission m, Action<bool> done)
    {
        string uid = PlayerPrefs.GetString(KeyUid, "");
        string root = $"projects/{ProjectId}/databases/(default)/documents/users/{uid}/games/{m.gameId}";

        var sb = new StringBuilder();
        sb.Append("{\"writes\":[");

        // Documento del juego (la app lista los juegos a partir de estos documentos).
        sb.Append("{\"update\":{\"name\":\"").Append(root).Append("\",\"fields\":{")
          .Append("\"nombre\":").Append(Str(m.gameTitle)).Append(",")
          .Append("\"ultimaMision\":").Append(Str(m.missionName))
          .Append("}},\"updateMask\":{\"fieldPaths\":[\"nombre\",\"ultimaMision\"]},")
          .Append("\"updateTransforms\":[{\"fieldPath\":\"ultimaActualizacion\",\"setToServerValue\":\"REQUEST_TIME\"}]},");

        // La misión (misma forma que escribe Misión 1 con el SDK de Firebase).
        sb.Append("{\"update\":{\"name\":\"").Append(root).Append("/missions/").Append(m.missionId).Append("\",\"fields\":{")
          .Append("\"missionName\":").Append(Str(m.missionName)).Append(",")
          .Append("\"xp\":").Append(Int(m.xp)).Append(",")
          .Append("\"aciertos\":").Append(Int(m.aciertos)).Append(",")
          .Append("\"errores\":").Append(Int(m.errores)).Append(",")
          .Append("\"tiempo\":").Append(Int(m.tiempo)).Append(",")
          .Append("\"misionCompletada\":{\"booleanValue\":true},")
          .Append("\"mundo\":").Append(Str(m.mundo)).Append(",")
          .Append("\"correo\":").Append(Str(Email)).Append(",")
          .Append("\"fecha\":{\"timestampValue\":\"").Append(m.fechaIso).Append("\"}")
          .Append("}}},");

        // Totales: se suman con incrementos atómicos.
        sb.Append("{\"update\":{\"name\":\"").Append(root).Append("/stats/general\",\"fields\":{")
          .Append("\"ultimoJuego\":").Append(Str(m.mundo.Length > 0 ? m.mundo : m.gameTitle))
          .Append("}},\"updateMask\":{\"fieldPaths\":[\"ultimoJuego\"]},\"updateTransforms\":[")
          .Append(Inc("xpTotal", m.xp)).Append(",")
          .Append(Inc("aciertosTotales", m.aciertos)).Append(",")
          .Append(Inc("erroresTotales", m.errores)).Append(",")
          .Append(Inc("tiempoTotal", m.tiempo)).Append(",")
          .Append(Inc("misionesCompletadas", 1)).Append(",")
          .Append("{\"fieldPath\":\"ultimaActualizacion\",\"setToServerValue\":\"REQUEST_TIME\"}")
          .Append("]}");

        sb.Append("]}");

        using (UnityWebRequest req = Post($"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents:commit", sb.ToString()))
        {
            req.SetRequestHeader("Authorization", "Bearer " + idToken);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"LiceneCloud: no se pudo guardar la misión ({req.responseCode}): {req.downloadHandler.text}");
                // Un 401/403 indica token vencido o sin permiso: se fuerza a renovar.
                if (req.responseCode == 401 || req.responseCode == 403) idToken = null;
                done(false);
                yield break;
            }
        }

        done(true);
    }

    // ---------- Cola local ----------

    [Serializable]
    class Queue { public List<Mission> items = new List<Mission>(); }

    static List<Mission> LoadQueue()
    {
        string json = PlayerPrefs.GetString(KeyQueue, "");
        if (string.IsNullOrEmpty(json)) return new List<Mission>();
        try { return JsonUtility.FromJson<Queue>(json)?.items ?? new List<Mission>(); }
        catch { return new List<Mission>(); }
    }

    static void SaveQueue(List<Mission> items)
    {
        // Límite para no crecer sin fin si el alumno nunca inicia sesión.
        if (items.Count > 200) items.RemoveRange(0, items.Count - 200);
        PlayerPrefs.SetString(KeyQueue, JsonUtility.ToJson(new Queue { items = items }));
        PlayerPrefs.Save();
    }

    // ---------- Utilidades ----------

    [Serializable] class SignInResponse { public string idToken, refreshToken, localId, expiresIn; }
    [Serializable] class RefreshResponse { public string id_token, refresh_token, expires_in; }
    [Serializable] class ErrorWrapper { public ErrorBody error; }
    [Serializable] class ErrorBody { public string message; }

    static string FriendlyAuthError(UnityWebRequest req)
    {
        if (req.result == UnityWebRequest.Result.ConnectionError) return "Sin conexión a internet";

        string code = "";
        try { code = JsonUtility.FromJson<ErrorWrapper>(req.downloadHandler.text)?.error?.message ?? ""; }
        catch { }

        if (code.Contains("INVALID") || code.Contains("EMAIL_NOT_FOUND") || code.Contains("PASSWORD")) return "Correo o contraseña incorrectos";
        if (code.Contains("TOO_MANY_ATTEMPTS")) return "Demasiados intentos. Espera un momento";
        if (code.Contains("USER_DISABLED")) return "Esta cuenta está desactivada";
        return "No se pudo iniciar sesión";
    }

    static UnityWebRequest Post(string url, string json)
    {
        var req = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 15,
        };
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }

    static float ParseSeconds(string value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : 3600f;

    static string Sanitize(string id)
    {
        var sb = new StringBuilder();
        foreach (char c in id ?? "mision")
            sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
        return sb.Length > 0 ? sb.ToString() : "mision";
    }

    static string Json(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    static string Str(string s) => "{\"stringValue\":" + Json(s) + "}";
    static string Int(int v) => "{\"integerValue\":\"" + v + "\"}";
    static string Inc(string field, int v) => "{\"fieldPath\":\"" + field + "\",\"increment\":{\"integerValue\":\"" + v + "\"}}";
}
