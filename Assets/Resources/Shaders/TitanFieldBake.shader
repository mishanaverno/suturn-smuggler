Shader "Hidden/Suturn/Titan Field Bake"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "TitanFields.cginc"

        int _Face;
        // Offset for the finite-difference gradient: about half a texel of the cubemap.
        float _Epsilon;

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 ndc : TEXCOORD0;
        };

        v2f vert(uint id : SV_VertexID)
        {
            // One triangle over the whole face.
            float2 ndc = float2(id == 1 ? 3.0 : -1.0, id == 2 ? 3.0 : -1.0);
            v2f o;
            o.vertex = float4(ndc, 0, 1);
            o.ndc = ndc;
            return o;
        }

        // Direction of a texel in the standard cubemap face layout, faces +X -X +Y -Y +Z -Z.
        float3 Direction(float2 ndc)
        {
            #if UNITY_UV_STARTS_AT_TOP
            float s = ndc.x, t = -ndc.y;
            #else
            float s = ndc.x, t = ndc.y;
            #endif
            float3 d = _Face == 0 ? float3(1, -t, -s) :
                _Face == 1 ? float3(-1, -t, s) :
                _Face == 2 ? float3(s, 1, t) :
                _Face == 3 ? float3(s, -1, -t) :
                _Face == 4 ? float3(s, -t, 1) : float3(-s, -t, -1);
            return normalize(d);
        }
        ENDCG

        // Fields: density, medium, veil, streak.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            float4 frag(v2f i) : SV_Target
            {
                TitanField field = TitanFieldAt(Direction(i.ndc), false);
                return float4(field.density, field.medium, field.veil, field.streak);
            }
            ENDCG
        }

        // Relief vector, encoded to 0..1.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            float4 frag(v2f i) : SV_Target
            {
                float3 p = Direction(i.ndc);
                float3 t1 = normalize(cross(p, abs(p.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 t2 = cross(p, t1);
                float h = TitanFieldAt(p, false).height;
                float h1 = TitanFieldAt(normalize(p + t1 * _Epsilon), false).height;
                float h2 = TitanFieldAt(normalize(p + t2 * _Epsilon), false).height;
                float3 gradient = (t1 * (h1 - h) + t2 * (h2 - h)) / _Epsilon;
                return float4(saturate(ReliefVector(gradient) * 0.5 + 0.5), 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
