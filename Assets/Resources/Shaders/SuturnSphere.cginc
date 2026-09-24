#ifndef SUTURN_SPHERE_INCLUDED
#define SUTURN_SPHERE_INCLUDED

// Flat-shaded planets: the mesh is only a proxy. It is inflated past the faceted silhouette
// and by the rim width in screen space, and the fragment stage ray-casts the true sphere,
// so the disc and its atmosphere rim stay round on a low-poly mesh.
// The including shader declares _RimPixels.

struct SphereV2F
{
    float4 vertex : SV_POSITION;
    float3 objectPosition : TEXCOORD0;
    float3 worldPosition : TEXCOORD1;
    float radius : TEXCOORD2;
};

SphereV2F SphereVert(float4 vertex : POSITION)
{
    SphereV2F o;
    o.radius = length(vertex.xyz);
    float3 shell = vertex.xyz * 1.02;
    o.vertex = UnityObjectToClipPos(float4(shell, 1));
    float3 worldNormal = mul((float3x3)unity_ObjectToWorld, vertex.xyz);
    float2 clipNormal = mul((float3x3)UNITY_MATRIX_VP, worldNormal).xy;
    clipNormal /= max(length(clipNormal), 1e-5);
    o.vertex.xy += clipNormal * (_RimPixels + 2.0) * 2.0 / _ScreenParams.xy * o.vertex.w;
    o.objectPosition = shell;
    o.worldPosition = mul(unity_ObjectToWorld, float4(shell, 1)).xyz;
    return o;
}

// Returns the unit-sphere point under the pixel. Outside the disc it is the closest point
// on the limb, which still drives rim lighting; pixelsOutside is then positive.
float3 RaycastSphere(SphereV2F i, out float pixelsOutside)
{
    float3 rayWorld = unity_OrthoParams.w > 0.5 ? -UNITY_MATRIX_V[2].xyz :
        normalize(i.worldPosition - _WorldSpaceCameraPos.xyz);
    float3 rayDirection = normalize(mul((float3x3)unity_WorldToObject, rayWorld));
    float b = dot(i.objectPosition, rayDirection);
    float centerDistance = sqrt(max(dot(i.objectPosition, i.objectPosition) - b * b, 0.0));
    float halfChord = sqrt(max(i.radius * i.radius - centerDistance * centerDistance, 0.0));
    pixelsOutside = (centerDistance - i.radius) / max(fwidth(centerDistance), 1e-6);
    return normalize(i.objectPosition + rayDirection * (-b - halfChord));
}

// Flat-style step, antialiased over one pixel.
float HardStep(float edge, float x)
{
    float w = max(fwidth(x), 1e-5) * 0.5;
    return smoothstep(edge - w, edge + w, x);
}

#endif
