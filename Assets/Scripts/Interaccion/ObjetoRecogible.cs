using System.Collections;
using UnityEngine;

// Marca un objeto como recogible. El jugador (JugadorPrimeraPersona) muestra "(E) Recoger"
// al estar cerca y, al presionar E, lo guarda en la BarraHerramientas y lo oculta de la escena.
// Necesita un Collider en el mismo objeto o en sus hijos.
public class ObjetoRecogible : Interactuable
{
    [Tooltip("Nombre que se muestra y con el que se guarda en la barra (ej. Llave).")]
    public string nombre = "Objeto";
    [Tooltip("Imagen que aparece en la casilla de la barra.")]
    public Sprite icono;
    [Min(1)] public int cantidad = 1;
    [Tooltip("Si ya traes uno igual en la barra, no deja recoger otro hasta que lo uses (ej. el engranaje).")]
    public bool soloUnoALaVez;
    [Tooltip("Lo que sale si intentas recoger otro teniendo ya uno. Vacio = mensaje automatico.")]
    public string mensajeYaTienes = "";

    [Header("Sonido")]
    public AudioClip sonidoAlRecoger;
    [Range(0f, 1f)]
    public float volumenSonido = 0.8f;

    [Header("Sonido Ambiental 3D")]
    public AudioClip sonidoCercania;
    [Range(0f, 1f)]
    public float volumenCercania = 0.6f;
    public float distanciaMinima = 1.5f;
    public float distanciaMaxima = 12f;
    private AudioSource fuenteCercania;

    [Header("Luz Guia Envolvente")]
    public bool activarLuzGuia = true;
    public Color colorLuzGuia = new Color(1f, 0.82f, 0.25f, 1f); // Amarillo calido
    public float radioLuz = 3.5f;
    public float velocidadPulso = 2.2f;

    private Light luzGuia;
    private GameObject objLuzGuia;
    private bool recogido = false;

    private string fallo = "La barra esta llena";

    public override string TextoAccion => $"Recoger <color=#FFDFA0>{nombre}</color>";
    public override string MensajeFallo => fallo;

    private void Start()
    {
        if (activarLuzGuia)
        {
            crearLuzGuia();
        }

        if (sonidoCercania != null)
        {
            fuenteCercania = GetComponent<AudioSource>();
            if (fuenteCercania == null)
            {
                fuenteCercania = gameObject.AddComponent<AudioSource>();
            }

            fuenteCercania.clip = sonidoCercania;
            fuenteCercania.loop = true;
            fuenteCercania.playOnAwake = true;
            fuenteCercania.spatialBlend = 1f;
            fuenteCercania.rolloffMode = AudioRolloffMode.Linear;
            fuenteCercania.minDistance = distanciaMinima;
            fuenteCercania.maxDistance = distanciaMaxima;
            fuenteCercania.volume = volumenCercania * ConfiguracionSonido.VolumenJuego;
            fuenteCercania.Play();
        }
    }

    private void Update()
    {
        if (recogido) return;

        if (luzGuia != null)
        {
            // Oscila suavemente entre 0 y 1
            float pulso = (Mathf.Sin(Time.time * velocidadPulso) + 1f) * 0.5f;
            // La luz puntual ilumina el objeto y el suelo alrededor subiendo y bajando su intensidad
            luzGuia.intensity = Mathf.Lerp(1.5f, 7f, pulso);
        }

        if (fuenteCercania != null && fuenteCercania.isPlaying)
        {
            fuenteCercania.volume = volumenCercania * ConfiguracionSonido.VolumenJuego;
        }
    }

    public override bool interactuar(Transform jugador)
    {
        return recogerObjeto();
    }

    // Lo guarda en la barra. Devuelve false si la barra esta llena o ya traes uno (soloUnoALaVez).
    public bool recogerObjeto()
    {
        if (recogido) return false;

        BarraHerramientas barra = BarraHerramientas.Instancia;
        if (barra == null)
        {
            return false;
        }

        if (soloUnoALaVez && barra.tieneObjeto(nombre))
        {
            fallo = string.IsNullOrEmpty(mensajeYaTienes)
                ? $"Ya traes un {nombre.ToLower()}. Úsalo primero"
                : mensajeYaTienes;
            return false;
        }

        if (!barra.agregar(nombre, icono, cantidad))
        {
            fallo = "La barra esta llena";
            return false;
        }

        recogido = true;

        // 1. Detenemos el sonido 3D de cercania de inmediato
        if (fuenteCercania != null)
        {
            fuenteCercania.Stop();
            Destroy(fuenteCercania);
        }

        if (sonidoAlRecoger != null)
        {
            AudioSource.PlayClipAtPoint(sonidoAlRecoger, transform.position, volumenSonido * ConfiguracionSonido.VolumenJuego);
        }

        // 2. Sacamos luzGuia del objeto ANTES de registrarlo en la mano para que el objeto conserve su forma y material original
        if (objLuzGuia != null)
        {
            objLuzGuia.transform.SetParent(null);
        }

        ObjetoEnMano.registrar(nombre, gameObject);
        EstadoJuego.marcarRecogido(gameObject);

        // 3. Ocultamos el objeto limpiamente y desvanecemos la luz guia en su lugar
        gameObject.SetActive(false);

        if (objLuzGuia != null)
        {
            StartCoroutine(desvanecerLuzSola());
        }

        return true;
    }

    private IEnumerator desvanecerLuzSola()
    {
        float duracion = 0.45f;
        float tiempo = 0f;
        float intensidadInicial = luzGuia != null ? luzGuia.intensity : 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            if (luzGuia != null)
            {
                luzGuia.intensity = Mathf.Lerp(intensidadInicial, 0f, tiempo / duracion);
            }
            yield return null;
        }

        if (objLuzGuia != null)
        {
            Destroy(objLuzGuia);
        }
    }

    private void crearLuzGuia()
    {
        Vector3 centroMundo = transform.position;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds limites = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                limites.Encapsulate(renderers[i].bounds);
            }
            centroMundo = limites.center;
        }

        // Creamos un objeto limpio solo con la luz puntual (sin malla 3D opaca que tape el objeto)
        objLuzGuia = new GameObject("luzGuia");
        objLuzGuia.transform.SetParent(transform);
        // La elevamos apenas 15 cm sobre el centro para que bañe todo el objeto y el suelo sin meterse dentro del suelo
        objLuzGuia.transform.position = centroMundo + Vector3.up * 0.15f;

        luzGuia = objLuzGuia.AddComponent<Light>();
        luzGuia.type = LightType.Point;
        luzGuia.color = colorLuzGuia;
        luzGuia.range = radioLuz;
        luzGuia.intensity = 4f;
        luzGuia.shadows = LightShadows.None;
    }
}
