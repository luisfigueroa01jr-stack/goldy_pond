// Agua de Goldy Pond (URP). Dos modos:
//  _Cascada = 0: superficie (fuente, laguna, lago): olitas que se mueven, brillo de las luces, transparente.
//  _Cascada = 1: agua que cae (cascadas, chorros de la fuente): hilos que bajan y espuma blanca.
// Usa la posicion del mundo (no necesita UVs), asi sirve para cualquier modelo.
Shader "GoldyPond/Agua"
{
    Properties
    {
        _ColorProfundo ("Color profundo", Color) = (0.05, 0.22, 0.32, 1)
        _ColorClaro ("Color claro", Color) = (0.25, 0.62, 0.72, 1)
        _ColorEspuma ("Espuma", Color) = (0.85, 0.95, 1, 1)
        _Transparencia ("Opacidad", Range(0, 1)) = 0.78
        _Escala ("Tamano de las olas", Float) = 1.6
        _Velocidad ("Velocidad", Float) = 0.35
        _FuerzaOlas ("Fuerza de las olas", Range(0, 2)) = 0.6
        _Brillo ("Brillo de las luces", Range(0, 4)) = 1.6
        _Suavidad ("Suavidad del brillo", Range(8, 512)) = 140
        _Cascada ("Modo cascada (0/1)", Range(0, 1)) = 0
        _VelocidadCaida ("Velocidad de caida", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Agua"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorProfundo, _ColorClaro, _ColorEspuma;
                half _Transparencia, _FuerzaOlas, _Brillo, _Suavidad, _Cascada;
                float _Escala, _Velocidad, _VelocidadCaida;
            CBUFFER_END

            struct Entrada { float4 posicion : POSITION; float3 normal : NORMAL; };
            struct Salida
            {
                float4 posicionCS : SV_POSITION;
                float3 posicionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half niebla : TEXCOORD2;
            };

            // Ruido suave (valor) para las olas.
            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float olas(float2 p, float t)
            {
                float h = ruido(p + float2(t, t * 0.6)) * 0.6;
                h += ruido(p * 2.3 - float2(t * 0.8, -t * 0.5)) * 0.3;
                h += ruido(p * 5.1 + float2(-t * 1.3, t)) * 0.1;
                return h;
            }

            Salida vert(Entrada e)
            {
                Salida s;
                float3 ws = TransformObjectToWorld(e.posicion.xyz);
                // Superficie: sube y baja un poquito (olitas).
                if (_Cascada < 0.5)
                    ws.y += (olas(ws.xz / _Escala, _Time.y * _Velocidad) - 0.5) * 0.03 * _FuerzaOlas;
                s.posicionWS = ws;
                s.normalWS = TransformObjectToWorldNormal(e.normal);
                s.posicionCS = TransformWorldToHClip(ws);
                s.niebla = ComputeFogFactor(s.posicionCS.z);
                return s;
            }

            half3 Luz(Light l, float3 n, float3 v, half3 color)
            {
                float3 h = normalize(l.direction + v);
                half atenuacion = l.distanceAttenuation * l.shadowAttenuation;
                half difusa = saturate(dot(n, l.direction)) * 0.5 + 0.15;
                half especular = pow(saturate(dot(n, h)), _Suavidad) * _Brillo;
                return l.color * atenuacion * (color * difusa + especular);
            }

            half4 frag(Salida s, bool frente : SV_IsFrontFace) : SV_Target
            {
                float t = _Time.y;
                float3 v = normalize(GetWorldSpaceViewDir(s.posicionWS));
                float3 n = normalize(s.normalWS);
                if (!frente) n = -n;
                half3 color;
                half alfa;

                if (_Cascada < 0.5)
                {
                    // Normal de las olas (derivada del ruido) sobre la superficie.
                    float2 p = s.posicionWS.xz / _Escala;
                    float tt = t * _Velocidad;
                    float e = 0.05;
                    float h0 = olas(p, tt);
                    float hx = olas(p + float2(e, 0), tt), hz = olas(p + float2(0, e), tt);
                    float3 onda = normalize(float3((h0 - hx) * _FuerzaOlas * 6.0, 1.0, (h0 - hz) * _FuerzaOlas * 6.0));
                    n = normalize(lerp(n, onda, 0.85));
                    half fresnel = pow(1.0 - saturate(dot(n, v)), 3.0);
                    color = lerp(_ColorProfundo.rgb, _ColorClaro.rgb, saturate(h0 * 0.9 + fresnel * 0.5));
                    alfa = saturate(_Transparencia + fresnel * 0.25);
                }
                else
                {
                    // Cascada: hilos que bajan (en y) con espuma y huecos transparentes.
                    float3 w = s.posicionWS;
                    float caida = w.y * 0.9 + t * _VelocidadCaida;
                    float hx = ruido(float2(w.x * 4.0, caida * 0.45)) * 0.6 + ruido(float2(w.x * 9.0, caida * 1.1)) * 0.4;
                    float hz = ruido(float2(w.z * 4.0 + 17.0, caida * 0.45)) * 0.6 + ruido(float2(w.z * 9.0 + 31.0, caida * 1.1)) * 0.4;
                    float hilos = saturate((hx + hz) * 0.5);
                    hilos = saturate((hilos - 0.3) * 1.9);                 // mas contraste
                    float espuma = smoothstep(0.55, 0.9, hilos);
                    color = lerp(_ColorClaro.rgb * 0.9, _ColorEspuma.rgb, espuma);
                    alfa = saturate(_Transparencia * lerp(0.25, 1.25, hilos));
                    n = normalize(n + float3(0, (hilos - 0.5) * 0.8, 0));
                }

                // Luces: ambiente + principal (luna/sol) + lamparas y antorchas.
                half3 luz = color * SampleSH(n) * 1.2;
                luz += Luz(GetMainLight(), n, v, color);
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = s.posicionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(s.posicionCS);
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                        luz += Luz(GetAdditionalLight(li, s.posicionWS), n, v, color);
                    #endif
                    uint cuantas = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(cuantas)
                        luz += Luz(GetAdditionalLight(lightIndex, s.posicionWS), n, v, color);
                    LIGHT_LOOP_END
                #endif

                luz = MixFog(luz, s.niebla);
                return half4(luz, alfa);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
