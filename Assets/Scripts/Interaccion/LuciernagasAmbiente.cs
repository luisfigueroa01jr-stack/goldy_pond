using UnityEngine;

public class LuciernagasAmbiente : MonoBehaviour
{
    [Header("Cantidad y Area del Mapa")]
    [Tooltip("Cantidad maxima de luciernagas visibles al mismo tiempo.")]
    public int cantidadLuciernagas = 120;
    [Tooltip("Radio en metros alrededor del jugador donde flotan las luciernagas.")]
    public Vector3 tamanoArea = new Vector3(45f, 8f, 45f);

    [Header("Apariencia y Brillo")]
    public Color colorLuciernaga = new Color(1f, 0.85f, 0.22f, 1f); // Amarillo calido
    [Range(1f, 5f)]
    public float intensidadBrillo = 2.6f; // Brillo HDR para destacar en la noche
    public float tamanoMinimo = 0.04f;
    public float tamanoMaximo = 0.11f;

    [Header("Viento y Parpadeo")]
    [Tooltip("Direccion y fuerza suave del viento que las arrastra.")]
    public Vector3 direccionViento = new Vector3(0.4f, 0.12f, 0.25f);
    [Tooltip("Cuanto flotan erraticamente por si solas.")]
    public float turbulencia = 0.45f;
    [Tooltip("Duracion en segundos de cada ciclo de aparecer, brillar y desvanecerse.")]
    public float duracionCiclo = 4.5f;

    private ParticleSystem sistemaParticulas;
    private Transform objetivoSeguir;

    private void Start()
    {
        GameObject jugador = GameObject.Find("Jugador");
        if (jugador != null)
        {
            objetivoSeguir = jugador.transform;
        }
        else if (Camera.main != null)
        {
            objetivoSeguir = Camera.main.transform;
        }

        configurarParticulas();
    }

    private void LateUpdate()
    {
        // Mueve la caja generadora con el jugador, pero las particulas flotan libres en el mundo
        if (objetivoSeguir != null)
        {
            transform.position = objetivoSeguir.position + Vector3.up * 2.5f;
        }
    }

    private void configurarParticulas()
    {
        sistemaParticulas = GetComponent<ParticleSystem>();
        if (sistemaParticulas == null)
        {
            sistemaParticulas = gameObject.AddComponent<ParticleSystem>();
        }

        // 1. Configuracion principal
        var main = sistemaParticulas.main;
        main.loop = true;
        main.prewarm = true; // Ya aparecen flotando desde el primer segundo del juego
        main.startLifetime = new ParticleSystem.MinMaxCurve(duracionCiclo * 0.7f, duracionCiclo * 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanoMinimo, tamanoMaximo);
        main.startColor = colorLuciernaga;
        main.maxParticles = cantidadLuciernagas;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Flotan libres en el mapa

        // 2. Emision continua para que siempre vuelvan a aparecer
        var emission = sistemaParticulas.emission;
        emission.enabled = true;
        emission.rateOverTime = cantidadLuciernagas / duracionCiclo;

        // 3. Forma de caja amplia que cubre el mapa alrededor
        var shape = sistemaParticulas.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = tamanoArea;

        // 4. Viento suave que las lleva por el aire
        var velocity = sistemaParticulas.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(direccionViento.x * 0.5f, direccionViento.x * 1.5f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.1f, direccionViento.y);
        velocity.z = new ParticleSystem.MinMaxCurve(direccionViento.z * 0.5f, direccionViento.z * 1.5f);

        // 5. Movimiento organico de vuelo (Noise)
        var noise = sistemaParticulas.noise;
        noise.enabled = true;
        noise.strength = turbulencia;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.35f;

        // 6. Desvanecimiento y aumento de brillo cada pocos segundos
        var colorOverLifetime = sistemaParticulas.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradiente = new Gradient();
        gradiente.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(colorLuciernaga * 0.6f, 0f),
                new GradientColorKey(colorLuciernaga * 1.4f, 0.5f), // Pico maximo de brillo a la mitad
                new GradientColorKey(colorLuciernaga * 0.5f, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),     // Nace invisible
                new GradientAlphaKey(0.95f, 0.35f), // Aparece y sube brillo
                new GradientAlphaKey(1f, 0.6f),     // Brilla intenso
                new GradientAlphaKey(0f, 1f)      // Se desvanece suavemente
            }
        );
        colorOverLifetime.color = gradiente;

        var sizeOverLifetime = sistemaParticulas.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curvaTamano = new AnimationCurve();
        curvaTamano.AddKey(0f, 0.2f);
        curvaTamano.AddKey(0.45f, 1.15f);
        curvaTamano.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curvaTamano);

        // 7. Material luminoso suave (sin bordes cuadrados) para URP
        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Shader shaderParticulas = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shaderParticulas != null)
        {
            Material mat = new Material(shaderParticulas);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 2f);   // Additive (hace que la luz sume brillo)
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);

            // Creamos una textura circular difuminada por codigo para que sean puntos suaves de luz
            mat.SetTexture("_BaseMap", crearTexturaPuntoSuave());
            mat.SetColor("_BaseColor", colorLuciernaga * intensidadBrillo);
            renderer.material = mat;
        }
    }

    private Texture2D crearTexturaPuntoSuave()
    {
        int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float centro = (res - 1) * 0.5f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro)) / centro;
                float alfa = Mathf.Clamp01(1f - dist);
                alfa = Mathf.Pow(alfa, 2.2f); // Difuminado suave hacia los bordes
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alfa));
            }
        }
        tex.Apply();
        return tex;
    }
}
