// Линии навигационного прибора: орбиты, траектории, сферы влияния, метки, выноски.
//
// Главное здесь — ZTest Always: показания рисуются поверх тел, а не отсекаются ими.
// Перекрытие несёт информацию «эта дуга за телом», но на схематическом приборе такой
// глубиной игрок не пользуется, а теряет при этом самый нужный кусок траектории —
// у самого тела, там, где идёт подход и стыковка. Диск Титана честно показывает свой
// размер и продолжает это делать; он просто перестаёт съедать показания.
//
// ZWrite Off по той же причине: линии не должны загораживать друг друга по глубине,
// порядок между ними задаётся очередью, а не тем, какая дуга ближе к камере.
Shader "Sim/InstrumentLine"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                // Вершинный цвет — то, чем LineRenderer красит дуги: цвет дуги задаёт
                // отрисовщик, а не материал, поэтому материал один на все линии.
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
