using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Elevador del subterraneo (la jaula con la reja doble).
/// - Mientras no esten activadas las 3 palancas de la Aldea: la reja esta cerrada y no se puede abrir.
/// - Con las 3 palancas activadas, al bajar al subterraneo la reja ya esta abierta.
/// - Al meterte a la jaula: te acomodas al centro mirando hacia la reja, la reja se cierra,
///   arranca el motor (giran los engranes y la polea), la jaula sube, los cables se recogen
///   y a los pocos segundos la pantalla se va a negro.
/// - En la caceria infinita (JefeSubterraneo): el jefe llega hasta enfrente de la reja y se queda viendo hacia arriba;
///   la camara lo mira mientras subes, grita (ondas que se expanden, la imagen se tuerce y tiembla) y luego negro.
/// - Despues del negro aparece la imagen de victoria (Assets/Imagenes/interfaz de victoria),
///   se queda unos segundos (o hasta que presiones una tecla) y regresa al menu principal.
/// Lo configura Goldy Pond > Interaccion > Elevador.
/// </summary>
public class Elevador : MonoBehaviour
{
    [Header("Partes")]
    [Tooltip("La jaula (Jaula_Base): sube con todo lo que tiene adentro (barrotes, cupula, reja...).")]
    public Transform jaula;
    [Tooltip("Las dos hojas de la reja.")]
    public Puerta[] reja = new Puerta[0];
    [Tooltip("Los cables (se van recogiendo al subir).")]
    public Renderer cables;
    [Tooltip("Lo que gira mientras sube (polea, engranes del motor).")]
    public Transform[] engranes = new Transform[0];
    [Tooltip("La viga de arriba (Soporte_Viga): la jaula frena antes de tocarla.")]
    public Renderer techo;

    [Header("Cuando se abre")]
    public int palancasNecesarias = 3;

    [Header("Subida")]
    [Tooltip("Metros por segundo ya a toda velocidad.")]
    public float velocidad = 1f;
    [Tooltip("Segundos subiendo antes de que empiece la pantalla negra.")]
    public float segundosAntesDeNegro = 5f;
    [Tooltip("Segundos que tarda la pantalla en ponerse negra.")]
    public float duracionNegro = 1.5f;
    [Tooltip("Grados por segundo de la polea y los engranes.")]
    public float giroEngranes = 120f;

    [Header("Jefe (caceria infinita)")]
    [Tooltip("El prefab del jefe (Assets/Prefabs/Villanos/Boss). Solo aparece en la caceria infinita.")]
    public GameObject prefabJefe;
    [Tooltip("Metros afuera de la reja donde se para el jefe a ver como subes.")]
    public float distanciaJefe = 2.3f;
    [Tooltip("Con el jefe: velocidad de subida (mas lenta, para que se vea la escena).")]
    public float velocidadConJefe = 0.7f;
    [Tooltip("Con el jefe: segundos subiendo antes de que grite.")]
    public float inicioGrito = 3.5f;
    [Tooltip("Cuanto dura el grito (las ondas).")]
    public float duracionGrito = 4.5f;

    [Header("Victoria")]
    [Tooltip("La imagen que sale despues de la pantalla negra (Assets/Imagenes/interfaz de victoria).")]
    public Texture2D imagenVictoria;
    [Tooltip("Segundos en negro antes de que aparezca la imagen.")]
    public float esperaEnNegro = 0.8f;
    [Tooltip("Segundos que tarda la imagen en aparecer (y en irse).")]
    public float duracionAparecer = 1.5f;
    [Tooltip("Segundos que se queda la imagen. Despues de los primeros 2 s, cualquier tecla o clic la quita antes.")]
    public float segundosVictoria = 7f;
    [Tooltip("Escena a la que regresa al final.")]
    public string escenaMenu = "MenuPrincipal";

    public bool Abierto { get; private set; }
    public bool Subiendo { get; private set; }

    Bounds piso;            // la base de la jaula, al inicio
    Vector3 haciaReja;      // del centro de la jaula hacia la reja (horizontal)
    float largoCables, topeCables;
    Vector3 escalaCables;
    int ejeCables = -1;
    Vector3[] ejesEngranes;
    Vector3[] centrosEngranes;
    float subidaMaxima = float.MaxValue;

    void Start()
    {
        if (jaula == null) { Debug.LogWarning("[Goldy Pond] El elevador no tiene jaula asignada."); enabled = false; return; }
        var r = jaula.GetComponent<Renderer>();
        piso = r != null ? r.bounds : new Bounds(jaula.position, new Vector3(1.8f, 0.1f, 1.8f));

        var centroReja = Vector3.zero;
        int n = 0;
        foreach (var p in reja)
            if (p != null) { centroReja += p.GetComponent<Renderer>() != null ? p.GetComponent<Renderer>().bounds.center : p.transform.position; n++; }
        haciaReja = n > 0 ? Vector3.ProjectOnPlane(centroReja / n - piso.center, Vector3.up).normalized : Vector3.forward;

        // Hasta donde puede subir sin meterse en la viga de arriba.
        if (techo != null)
        {
            float tope = float.MinValue;
            foreach (var rr in jaula.GetComponentsInChildren<Renderer>()) tope = Mathf.Max(tope, rr.bounds.max.y);
            subidaMaxima = Mathf.Max(0f, techo.bounds.min.y - tope - 0.1f);
        }

        PrepararCables();
        PrepararEngranes();

        Abierto = EstadoJuego.PalancasActivadas >= palancasNecesarias;
        foreach (var p in reja)
        {
            if (p == null) continue;
            p.bloqueada = true; // el elevador decide cuando se abre y cierra, no la E
            if (Abierto) p.FijarAlInstante(true, piso.center); // abre hacia afuera de la jaula
        }

        // Cacería infinita: el jefe cae en el puente y te persigue hasta aqui.
        if (Abierto && Caceria.Infinita && prefabJefe != null) JefeSubterraneo.Crear(this, prefabJefe);
    }

    /// <summary>Donde se para el jefe: enfrente de la reja, afuera de la jaula.</summary>
    public Vector3 PuntoParaElJefe =>
        new Vector3(piso.center.x, piso.min.y, piso.center.z) + haciaReja * (Mathf.Max(piso.extents.x, piso.extents.z) + distanciaJefe);

    void Update()
    {
        if (!Abierto || Subiendo) return;
        var j = JugadorPrimeraPersona.Instancia;
        if (j == null || j.Escalando) return;
        if (Adentro(j.transform.position)) StartCoroutine(Subir(j));
    }

    /// <summary>Ya paso la reja: dentro de la base de la jaula, sin contar una orilla de 35 cm.</summary>
    public bool JugadorAdentro(Vector3 pies) => Adentro(pies);

    bool Adentro(Vector3 pies)
    {
        var interior = piso;
        interior.Expand(new Vector3(-0.7f, 0f, -0.7f));
        bool dentro = pies.x > interior.min.x && pies.x < interior.max.x && pies.z > interior.min.z && pies.z < interior.max.z;
        return dentro && pies.y > piso.min.y - 0.3f && pies.y < piso.max.y + 0.8f;
    }

    IEnumerator Subir(JugadorPrimeraPersona j)
    {
        Subiendo = true;
        j.Inmovil = true;
        var js = JefeSubterraneo.Instancia;
        bool conJefe = js != null && js.Jefe != null;
        if (conJefe)
        {
            j.BloquearMirar = true; // la camara la mueve la escena
            js.IrAlElevador(PuntoParaElJefe, piso.center);
            Debug.Log("[Goldy Pond] Elevador: subes con el jefe abajo.");
        }

        // 1. Al centro de la jaula, mirando hacia la reja (se ve como se cierra).
        var desde = j.transform.position;
        var hasta = new Vector3(piso.center.x, piso.max.y, piso.center.z) - haciaReja * 0.15f;
        var rotDesde = j.transform.rotation;
        var rotHasta = Quaternion.LookRotation(haciaReja, Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            j.transform.SetPositionAndRotation(Vector3.Lerp(desde, hasta, k), Quaternion.Slerp(rotDesde, rotHasta, k));
            yield return null;
        }
        j.transform.SetPositionAndRotation(hasta, rotHasta);

        // 2. Se cierra la reja.
        foreach (var p in reja) if (p != null) p.Fijar(false, hasta);
        float espera = 0.3f;
        foreach (var p in reja) if (p != null) espera = Mathf.Max(espera, p.duracion);
        yield return new WaitForSeconds(espera + 0.4f);

        // 3. Sube: arranca despacio, el motor gira, los cables se recogen. A los pocos segundos, negro.
        //    Con el jefe: lo ves abajo viendo hacia arriba, grita y despues negro.
        float tiempo = 0f, subido = 0f;
        Image negro = null;
        bool grito = false;
        float antesDeNegro = conJefe ? inicioGrito + duracionGrito + 0.2f : segundosAntesDeNegro;
        float vel = conJefe ? velocidadConJefe : velocidad;
        while (true)
        {
            tiempo += Time.deltaTime;
            if (conJefe && js != null && js.Jefe != null)
            {
                JefeSubterraneo.MirarA(j, j.camara.transform, js.Cabeza, 2.5f);
                if (!grito && tiempo >= inicioGrito) { grito = true; StartCoroutine(Grito(js, duracionGrito)); }
            }
            float v = vel * Mathf.SmoothStep(0f, 1f, tiempo / 1.2f);
            v *= Mathf.Clamp01((subidaMaxima - subido) / 1.2f); // frena suave antes de la viga
            var paso = Vector3.up * (v * Time.deltaTime);
            subido += paso.y;
            jaula.position += paso;
            j.transform.position += paso;
            AjustarCables(subido);
            GirarEngranes(Mathf.Clamp01(tiempo / 1.2f));

            if (tiempo >= antesDeNegro)
            {
                if (negro == null) negro = CrearNegro();
                float a = Mathf.Clamp01((tiempo - antesDeNegro) / Mathf.Max(0.05f, duracionNegro));
                negro.color = new Color(0f, 0f, 0f, a);
                if (a >= 1f) break;
            }
            yield return null;
        }

        // 4. Pantalla de victoria y de regreso al menu principal.
        yield return Victoria(negro);
    }

    IEnumerator Victoria(Image negro)
    {
        yield return new WaitForSeconds(esperaEnNegro);

        if (imagenVictoria != null)
        {
            var imagen = CrearImagen(negro.canvas.transform, imagenVictoria);
            yield return Fundido(imagen, 0f, 1f, duracionAparecer);

            float t = 0f;
            while (t < segundosVictoria)
            {
                t += Time.deltaTime;
                if (t > 2f && AlgunaTecla()) break;
                yield return null;
            }

            yield return Fundido(imagen, 1f, 0f, duracionAparecer);
            yield return new WaitForSeconds(0.3f);
        }
        else Debug.LogWarning("[Goldy Pond] El elevador no tiene imagen de victoria asignada.");

        Debug.Log("[Goldy Pond] Victoria: regresando al menu principal.");
        EstadoJuego.NuevaPartida();
        SceneManager.LoadScene(escenaMenu);
    }

    static bool AlgunaTecla()
    {
        var k = Keyboard.current; var m = Mouse.current;
        return (k != null && k.anyKey.wasPressedThisFrame) || (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame));
    }

    // ---------- El grito del jefe: ondas que se expanden desde su cabeza ----------

    IEnumerator Grito(JefeSubterraneo js, float duracion)
    {
        Debug.Log("[Goldy Pond] Elevador: el jefe grita.");
        var cam = JugadorPrimeraPersona.Instancia != null ? JugadorPrimeraPersona.Instancia.camara : Camera.main;

        var lienzo = new GameObject("Grito del jefe", typeof(Canvas));
        var canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950; // debajo del negro
        var anillo = SpriteAnillo();

        // La imagen se tuerce y se separan los colores con cada onda.
        var goVol = new GameObject("Grito (distorsion)");
        var volumen = goVol.AddComponent<Volume>();
        volumen.isGlobal = true;
        volumen.priority = 100f;
        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        var lente = perfil.Add<LensDistortion>(true);
        var colores = perfil.Add<ChromaticAberration>(true);
        volumen.profile = perfil;
        volumen.weight = 0f;
        if (cam != null)
        {
            var datos = cam.GetUniversalAdditionalCameraData();
            if (datos != null) datos.renderPostProcessing = true;
        }

        var ondas = new List<(Image img, float nacio)>();
        float t = 0f, siguiente = 0f;
        const float vidaOnda = 1.3f;
        while (t < duracion || ondas.Count > 0)
        {
            t += Time.deltaTime;
            float fuerza = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((duracion - t) / 0.8f);

            // Una onda nueva cada ~0.28 s desde la cabeza del jefe.
            if (t < duracion - 0.5f && t >= siguiente)
            {
                siguiente = t + 0.28f;
                var go = new GameObject("Onda", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(lienzo.transform, false);
                var img = go.GetComponent<Image>();
                img.sprite = anillo;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                ondas.Add((img, t));
            }

            // Desde donde sale: la cabeza del jefe en la pantalla (si no se ve, desde abajo).
            Vector2 origen = new Vector2(Screen.width * 0.5f, Screen.height * 0.2f);
            if (cam != null && js != null && js.Jefe != null)
            {
                var sp = cam.WorldToScreenPoint(js.Cabeza);
                if (sp.z > 0f) origen = new Vector2(Mathf.Clamp(sp.x, 0f, Screen.width), Mathf.Clamp(sp.y, -Screen.height * 0.2f, Screen.height));
            }
            for (int i = ondas.Count - 1; i >= 0; i--)
            {
                var (img, nacio) = ondas[i];
                float k = (t - nacio) / vidaOnda;
                if (k >= 1f) { Destroy(img.gameObject); ondas.RemoveAt(i); continue; }
                float crecer = 1f - Mathf.Pow(1f - k, 2.2f);
                float tam = Mathf.Lerp(0.12f, 3.4f, crecer) * Screen.height;
                img.rectTransform.anchoredPosition = origen;
                img.rectTransform.sizeDelta = new Vector2(tam, tam);
                img.color = new Color(1f, 0.82f, 0.8f, 0.55f * Mathf.Pow(1f - k, 1.4f));
            }

            volumen.weight = fuerza;
            lente.intensity.Override(-0.45f * fuerza * (0.6f + 0.4f * Mathf.Sin(t * 22f)));
            colores.intensity.Override(fuerza);
            Retumbe.FuerzaContinua(0.55f * fuerza);
            yield return null;
        }
        Retumbe.FuerzaContinua(0f);
        Destroy(goVol);
        Destroy(lienzo);
    }

    /// <summary>Anillo suave (la onda del grito).</summary>
    static Sprite SpriteAnillo()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float borde = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.06f, 2f));          // el aro
                float estela = Mathf.Clamp01(1f - (0.86f - d) / 0.35f) * (d < 0.86f ? 0.25f : 0f); // brillo por dentro
                float a = Mathf.Clamp01(borde + estela);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    static IEnumerator Fundido(RawImage img, float desde, float hasta, float duracion)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, duracion))
        {
            img.color = new Color(1f, 1f, 1f, Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        img.color = new Color(1f, 1f, 1f, hasta);
    }

    /// <summary>La imagen encima del negro, llenando la pantalla sin deformarse (se recorta la orilla si la pantalla no es 16:9).</summary>
    static RawImage CrearImagen(Transform lienzo, Texture2D textura)
    {
        var img = new GameObject("Victoria", typeof(RectTransform)).AddComponent<RawImage>();
        img.transform.SetParent(lienzo, false);
        img.texture = textura;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        var ajuste = img.gameObject.AddComponent<AspectRatioFitter>();
        ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajuste.aspectRatio = (float)textura.width / textura.height;
        return img;
    }

    // ---------- Cables: la punta de arriba se queda fija y se acortan ----------

    void PrepararCables()
    {
        if (cables == null) return;
        var t = cables.transform;
        float mejor = 0f;
        for (int i = 0; i < 3; i++)
        {
            var e = Vector3.zero; e[i] = 1f;
            float vertical = Mathf.Abs(t.TransformVector(e).normalized.y);
            if (vertical > mejor) { mejor = vertical; ejeCables = i; }
        }
        escalaCables = t.localScale;
        largoCables = cables.bounds.size.y;
        topeCables = cables.bounds.max.y;
    }

    void AjustarCables(float subido)
    {
        if (cables == null || ejeCables < 0 || largoCables <= 0.01f) return;
        float factor = Mathf.Max(0.02f, (largoCables - subido) / largoCables);
        var t = cables.transform;
        var s = escalaCables; s[ejeCables] *= factor;
        t.localScale = s;
        t.position += Vector3.up * (topeCables - cables.bounds.max.y);
    }

    // ---------- Polea y engranes del motor ----------

    void PrepararEngranes()
    {
        ejesEngranes = new Vector3[engranes.Length];
        centrosEngranes = new Vector3[engranes.Length];
        for (int i = 0; i < engranes.Length; i++)
        {
            var g = engranes[i];
            if (g == null) continue;
            var mf = g.GetComponent<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            int k = b.size.x <= b.size.y && b.size.x <= b.size.z ? 0 : b.size.y <= b.size.z ? 1 : 2; // lo mas delgado = el eje
            var e = Vector3.zero; e[k] = 1f;
            ejesEngranes[i] = e;
            centrosEngranes[i] = b.center;
        }
    }

    void GirarEngranes(float fuerza)
    {
        for (int i = 0; i < engranes.Length; i++)
        {
            var g = engranes[i];
            if (g == null) continue;
            float sentido = i % 2 == 0 ? 1f : -1f; // los que estan juntos giran al reves
            g.RotateAround(g.TransformPoint(centrosEngranes[i]), g.TransformDirection(ejesEngranes[i]), giroEngranes * sentido * fuerza * Time.deltaTime);
        }
    }

    static Image CrearNegro()
    {
        var go = new GameObject("Pantalla Negra", typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // encima de todo, tambien de la barra
        var img = new GameObject("Negro", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return img;
    }
}
