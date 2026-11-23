using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Elevador del subterraneo (la jaula con la reja doble).
// - Mientras no esten activadas las 3 palancas de la Aldea: la reja esta cerrada y no se puede abrir.
// - Con las 3 palancas activadas, al bajar al subterraneo la reja ya esta abierta.
// - Al meterte a la jaula: te acomodas al centro mirando hacia la reja, la reja se cierra,
// arranca el motor (giran los engranes y la polea), la jaula sube, los cables se recogen
// y a los pocos segundos la pantalla se va a negro.
// - En la caceria infinita (JefeSubterraneo): el jefe llega hasta enfrente de la reja y se queda viendo hacia arriba;
// la camara lo mira mientras subes, grita (ondas que se expanden, la imagen se tuerce y tiembla) y luego negro.
// - Despues del negro aparece la imagen de victoria (Assets/Imagenes/interfaz de victoria),
// se queda unos segundos (o hasta que presiones una tecla) y regresa al menu principal.
// Lo configura Goldy Pond > Interaccion > Elevador.
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

    private Bounds piso;            // la base de la jaula, al inicio
    private Vector3 haciaReja;      // del centro de la jaula hacia la reja (horizontal)
    private float largoCables, topeCables;
    private Vector3 escalaCables;
    private int ejeCables = -1;
    private Vector3[] ejesEngranes;
    private Vector3[] centrosEngranes;
    private float subidaMaxima = float.MaxValue;

    private void Start()
    {
        if (jaula == null)
        {
            Debug.LogWarning("[Goldy Pond] El elevador no tiene jaula asignada.");
            enabled = false;
            return;
        }

        Renderer r = jaula.GetComponent<Renderer>();
        piso = r != null ? r.bounds : new Bounds(jaula.position, new Vector3(1.8f, 0.1f, 1.8f));

        Vector3 centroReja = Vector3.zero;
        int n = 0;
        foreach (Puerta p in reja)
        {
            if (p != null)
            {
                centroReja += p.GetComponent<Renderer>() != null ? p.GetComponent<Renderer>().bounds.center : p.transform.position;
                n++;
            }
        }

        haciaReja = n > 0 ? Vector3.ProjectOnPlane(centroReja / n - piso.center, Vector3.up).normalized : Vector3.forward;

        // Hasta donde puede subir sin meterse en la viga de arriba.
        if (techo != null)
        {
            float tope = float.MinValue;
            foreach (Renderer rr in jaula.GetComponentsInChildren<Renderer>())
            {
                tope = Mathf.Max(tope, rr.bounds.max.y);
            }

            subidaMaxima = Mathf.Max(0f, techo.bounds.min.y - tope - 0.1f);
        }

        prepararCables();
        prepararEngranes();

        Abierto = EstadoJuego.PalancasActivadas >= palancasNecesarias;
        foreach (Puerta p in reja)
        {
            if (p == null)
            {
                continue;
            }

            p.bloqueada = true; // el elevador decide cuando se abre y cierra, no la E
            if (Abierto)
            {
                p.fijarAlInstante(true, piso.center); // abre hacia afuera de la jaula
            }
        }

        // Caceria infinita: el jefe cae en el puente y te persigue hasta aqui.
        if (Abierto && Caceria.Infinita && prefabJefe != null)
        {
            JefeSubterraneo.crear(this, prefabJefe);
        }
    }

    // Donde se para el jefe: enfrente de la reja, afuera de la jaula.
    public Vector3 PuntoParaElJefe =>
        new Vector3(piso.center.x, piso.min.y, piso.center.z) + haciaReja * (Mathf.Max(piso.extents.x, piso.extents.z) + distanciaJefe);

    private void Update()
    {
        if (!Abierto || Subiendo)
        {
            return;
        }

        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        if (j == null || j.Escalando)
        {
            return;
        }

        if (estaAdentro(j.transform.position))
        {
            StartCoroutine(subirElevador(j));
        }
    }

    // Ya paso la reja: dentro de la base de la jaula, sin contar una orilla de 35 cm.
    public bool jugadorAdentro(Vector3 pies)
    {
        return estaAdentro(pies);
    }

    private bool estaAdentro(Vector3 pies)
    {
        Bounds interior = piso;
        interior.Expand(new Vector3(-0.7f, 0f, -0.7f));
        bool dentro = pies.x > interior.min.x && pies.x < interior.max.x && pies.z > interior.min.z && pies.z < interior.max.z;
        return dentro && pies.y > piso.min.y - 0.3f && pies.y < piso.max.y + 0.8f;
    }

    private IEnumerator subirElevador(JugadorPrimeraPersona j)
    {
        Subiendo = true;
        j.Inmovil = true;
        JefeSubterraneo js = JefeSubterraneo.Instancia;
        bool conJefe = js != null && js.Jefe != null;
        if (conJefe)
        {
            j.BloquearMirar = true; // la camara la mueve la escena
            js.irAlElevador(PuntoParaElJefe, piso.center);
            Debug.Log("[Goldy Pond] Elevador: subes con el jefe abajo.");
        }

        // 1. Al centro de la jaula, mirando hacia la reja (se ve como se cierra).
        Vector3 desde = j.transform.position;
        Vector3 hasta = new Vector3(piso.center.x, piso.max.y, piso.center.z) - haciaReja * 0.15f;
        Quaternion rotDesde = j.transform.rotation;
        Quaternion rotHasta = Quaternion.LookRotation(haciaReja, Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            j.transform.SetPositionAndRotation(Vector3.Lerp(desde, hasta, k), Quaternion.Slerp(rotDesde, rotHasta, k));
            yield return null;
        }

        j.transform.SetPositionAndRotation(hasta, rotHasta);

        // 2. Se cierra la reja.
        foreach (Puerta p in reja)
        {
            if (p != null)
            {
                p.fijar(false, hasta);
            }
        }

        float espera = 0.3f;
        foreach (Puerta p in reja)
        {
            if (p != null)
            {
                espera = Mathf.Max(espera, p.duracion);
            }
        }

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
                JefeSubterraneo.mirarA(j, j.camara.transform, js.Cabeza, 2.5f);
                if (!grito && tiempo >= inicioGrito)
                {
                    grito = true;
                    StartCoroutine(gritoDelJefe(js, duracionGrito));
                }
            }

            float v = vel * Mathf.SmoothStep(0f, 1f, tiempo / 1.2f);
            v *= Mathf.Clamp01((subidaMaxima - subido) / 1.2f); // frena suave antes de la viga
            Vector3 paso = Vector3.up * (v * Time.deltaTime);
            subido += paso.y;
            jaula.position += paso;
            j.transform.position += paso;
            ajustarCables(subido);
            girarEngranes(Mathf.Clamp01(tiempo / 1.2f));

            if (tiempo >= antesDeNegro)
            {
                if (negro == null)
                {
                    negro = crearNegro();
                }

                float a = Mathf.Clamp01((tiempo - antesDeNegro) / Mathf.Max(0.05f, duracionNegro));
                negro.color = new Color(0f, 0f, 0f, a);
                if (a >= 1f)
                {
                    break;
                }
            }

            yield return null;
        }

        // 4. Pantalla de victoria y de regreso al menu principal.
        yield return mostrarVictoria(negro);
    }

    private IEnumerator mostrarVictoria(Image negro)
    {
        yield return new WaitForSeconds(esperaEnNegro);

        if (imagenVictoria != null)
        {
            RawImage imagen = crearImagen(negro.canvas.transform, imagenVictoria);
            yield return fundido(imagen, 0f, 1f, duracionAparecer);

            float t = 0f;
            while (t < segundosVictoria)
            {
                t += Time.deltaTime;
                if (t > 2f && algunaTecla())
                {
                    break;
                }

                yield return null;
            }

            yield return fundido(imagen, 1f, 0f, duracionAparecer);
            yield return new WaitForSeconds(0.3f);
        }
        else
        {
            Debug.LogWarning("[Goldy Pond] El elevador no tiene imagen de victoria asignada.");
        }

        Debug.Log("[Goldy Pond] Victoria: regresando al menu principal.");
        EstadoJuego.nuevaPartida();
        SceneManager.LoadScene(escenaMenu);
    }

    private static bool algunaTecla()
    {
        Keyboard k = Keyboard.current; Mouse m = Mouse.current;
        return (k != null && k.anyKey.wasPressedThisFrame) || (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame));
    }

    // ------------------- EL GRITO DEL JEFE: ONDAS QUE SE EXPANDEN DESDE SU CABEZA ------------------------------

    private IEnumerator gritoDelJefe(JefeSubterraneo js, float duracion)
    {
        Debug.Log("[Goldy Pond] Elevador: el jefe grita.");
        Camera cam = JugadorPrimeraPersona.Instancia != null ? JugadorPrimeraPersona.Instancia.camara : Camera.main;

        GameObject lienzo = new GameObject("Grito del jefe", typeof(Canvas));
        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950; // debajo del negro
        Sprite anillo = spriteAnillo();

        // La imagen se tuerce y se separan los colores con cada onda.
        GameObject goVol = new GameObject("Grito (distorsion)");
        Volume volumen = goVol.AddComponent<Volume>();
        volumen.isGlobal = true;
        volumen.priority = 100f;
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        LensDistortion lente = perfil.Add<LensDistortion>(true);
        ChromaticAberration colores = perfil.Add<ChromaticAberration>(true);
        volumen.profile = perfil;
        volumen.weight = 0f;
        if (cam != null)
        {
            UniversalAdditionalCameraData datos = cam.GetUniversalAdditionalCameraData();
            if (datos != null)
            {
                datos.renderPostProcessing = true;
            }
        }

        List<(Image img, float nacio)> ondas = new List<(Image img, float nacio)>();
        float t = 0f, siguiente = 0f;
        const float VIDA_ONDA = 1.3f;
        while (t < duracion || ondas.Count > 0)
        {
            t += Time.deltaTime;
            float fuerza = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((duracion - t) / 0.8f);

            // Una onda nueva cada ~0.28 s desde la cabeza del jefe.
            if (t < duracion - 0.5f && t >= siguiente)
            {
                siguiente = t + 0.28f;
                GameObject go = new GameObject("Onda", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(lienzo.transform, false);
                Image img = go.GetComponent<Image>();
                img.sprite = anillo;
                img.raycastTarget = false;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                ondas.Add((img, t));
            }

            // Desde donde sale: la cabeza del jefe en la pantalla (si no se ve, desde abajo).
            Vector2 origen = new Vector2(Screen.width * 0.5f, Screen.height * 0.2f);
            if (cam != null && js != null && js.Jefe != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(js.Cabeza);
                if (sp.z > 0f)
                {
                    origen = new Vector2(Mathf.Clamp(sp.x, 0f, Screen.width), Mathf.Clamp(sp.y, -Screen.height * 0.2f, Screen.height));
                }
            }

            for (int i = ondas.Count - 1; i >= 0; i--)
            {
                var (img, nacio) = ondas[i];
                float k = (t - nacio) / VIDA_ONDA;
                if (k >= 1f)
                {
                    Destroy(img.gameObject);
                    ondas.RemoveAt(i);
                    continue;
                }

                float crecer = 1f - Mathf.Pow(1f - k, 2.2f);
                float tam = Mathf.Lerp(0.12f, 3.4f, crecer) * Screen.height;
                img.rectTransform.anchoredPosition = origen;
                img.rectTransform.sizeDelta = new Vector2(tam, tam);
                img.color = new Color(1f, 0.82f, 0.8f, 0.55f * Mathf.Pow(1f - k, 1.4f));
            }

            volumen.weight = fuerza;
            lente.intensity.Override(-0.45f * fuerza * (0.6f + 0.4f * Mathf.Sin(t * 22f)));
            colores.intensity.Override(fuerza);
            Retumbe.fuerzaContinua(0.55f * fuerza);
            yield return null;
        }

        Retumbe.fuerzaContinua(0f);
        Destroy(goVol);
        Destroy(lienzo);
    }

    // Anillo suave (la onda del grito).
    private static Sprite spriteAnillo()
    {
        const int N = 256;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float borde = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.06f, 2f));          // el aro
                float estela = Mathf.Clamp01(1f - (0.86f - d) / 0.35f) * (d < 0.86f ? 0.25f : 0f); // brillo por dentro
                float a = Mathf.Clamp01(borde + estela);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }

    private static IEnumerator fundido(
        RawImage img,
        float desde,
        float hasta,
        float duracion
    )
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, duracion))
        {
            img.color = new Color(1f, 1f, 1f, Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }

        img.color = new Color(1f, 1f, 1f, hasta);
    }

    // La imagen encima del negro, llenando la pantalla sin deformarse (se recorta la orilla si la pantalla no es 16:9).
    private static RawImage crearImagen(Transform lienzo, Texture2D textura)
    {
        RawImage img = new GameObject("Victoria", typeof(RectTransform)).AddComponent<RawImage>();
        img.transform.SetParent(lienzo, false);
        img.texture = textura;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        AspectRatioFitter ajuste = img.gameObject.AddComponent<AspectRatioFitter>();
        ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajuste.aspectRatio = (float)textura.width / textura.height;
        return img;
    }

    // ------------------- CABLES: LA PUNTA DE ARRIBA SE QUEDA FIJA Y SE ACORTAN ------------------------------

    private void prepararCables()
    {
        if (cables == null)
        {
            return;
        }

        Transform t = cables.transform;
        float mejor = 0f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 e = Vector3.zero; e[i] = 1f;
            float vertical = Mathf.Abs(t.TransformVector(e).normalized.y);
            if (vertical > mejor)
            {
                mejor = vertical;
                ejeCables = i;
            }
        }

        escalaCables = t.localScale;
        largoCables = cables.bounds.size.y;
        topeCables = cables.bounds.max.y;
    }

    private void ajustarCables(float subido)
    {
        if (cables == null || ejeCables < 0 || largoCables <= 0.01f)
        {
            return;
        }

        float factor = Mathf.Max(0.02f, (largoCables - subido) / largoCables);
        Transform t = cables.transform;
        Vector3 s = escalaCables; s[ejeCables] *= factor;
        t.localScale = s;
        t.position += Vector3.up * (topeCables - cables.bounds.max.y);
    }

    // ------------------- POLEA Y ENGRANES DEL MOTOR ------------------------------

    private void prepararEngranes()
    {
        ejesEngranes = new Vector3[engranes.Length];
        centrosEngranes = new Vector3[engranes.Length];
        for (int i = 0; i < engranes.Length; i++)
        {
            Transform g = engranes[i];
            if (g == null)
            {
                continue;
            }

            MeshFilter mf = g.GetComponent<MeshFilter>();
            Bounds b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            int k = b.size.x <= b.size.y && b.size.x <= b.size.z ? 0 : b.size.y <= b.size.z ? 1 : 2; // lo mas delgado = el eje
            Vector3 e = Vector3.zero; e[k] = 1f;
            ejesEngranes[i] = e;
            centrosEngranes[i] = b.center;
        }
    }

    private void girarEngranes(float fuerza)
    {
        for (int i = 0; i < engranes.Length; i++)
        {
            Transform g = engranes[i];
            if (g == null)
            {
                continue;
            }

            float sentido = i % 2 == 0 ? 1f : -1f; // los que estan juntos giran al reves
            g.RotateAround(g.TransformPoint(centrosEngranes[i]), g.TransformDirection(ejesEngranes[i]), giroEngranes * sentido * fuerza * Time.deltaTime);
        }
    }

    private static Image crearNegro()
    {
        GameObject go = new GameObject("Pantalla Negra", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // encima de todo, tambien de la barra
        Image img = new GameObject("Negro", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return img;
    }
}
