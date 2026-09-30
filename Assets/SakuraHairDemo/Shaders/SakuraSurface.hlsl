#ifndef SAKURA_HAIR_SURFACE_INCLUDED
#define SAKURA_HAIR_SURFACE_INCLUDED

float3 SakuraHash(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
    q += dot(q, q.yxz + 33.33);
    return frac((q.xxy + q.yzz) * q.zyx);
}

void SakuraSurface_float(float2 UV, float Seconds, UnityTexture2D BaseMap,
    UnityTexture2D PetalMap, UnityTexture2D PetalMask, float4 BaseTint,
    float4 PetalTint, float EffectStrength, float PetalSize, float Density,
    float Speed, float3 FlowDirection, float Flutter, float Glow,
    float WorldTiling, float InteriorDepth, float DepthFade,
    float3 VolumeCenterWS, float3 VolumeExtentsWS,
    float3 PositionWS, float3 ViewDirectionWS,
    out float3 BaseColor, out float3 Emission)
{
    BaseColor = SAMPLE_TEXTURE2D(BaseMap.tex, BaseMap.samplerstate, UV).rgb * BaseTint.rgb;
    Emission = 0.0;
    float strength = saturate(EffectStrength);
    if (strength <= 0.0 || Density <= 0.0) return;

    // Hair is a window into a small virtual volume. The surface contributes
    // only its silhouette and viewing ray, never UVs or normals for petals.
    float3 volumeCenter = VolumeCenterWS;
    float3 worldExtent = max(abs(VolumeExtentsWS), .005);
    float3 planeNormal = normalize(_WorldSpaceCameraPos - volumeCenter);
    float3 right = cross(float3(0, 1, 0), planeNormal);
    right = dot(right, right) > .0001 ? normalize(right) : float3(1, 0, 0);
    float3 up = normalize(cross(planeNormal, right));
    float3 ray = -normalize(ViewDirectionWS);
    float denominator = dot(ray, planeNormal);
    if (abs(denominator) < .001) return;
    float3 direction = length(FlowDirection) > .0001 ? normalize(FlowDirection) : float3(0, -1, 0);
    float flutter = saturate(Flutter);
    float depthExtent = max(dot(abs(planeNormal), worldExtent), .001);
    float3 accumulatedColor = 0;
    float accumulatedWeight = 0;
    float transparency = 1;

    // Analytic ray / billboard intersections, not texture projection on hair.
    // Each candidate owns one 3D position, fall speed, rotation and flutter phase.
    [loop] for (int index = 0; index < 96; index++)
    {
        float3 random = SakuraHash(float2(index + .5, 17.23));
        float3 traits = SakuraHash(float2(index + 97.5, 51.91));
        float visibility = saturate((Density - traits.z) * 32.0);
        if (visibility <= 0) continue;
        float rate = lerp(.7, 1.4, traits.y);
        float3 cycle = frac(random + direction * Seconds * max(Speed, 0) * rate / (2 * worldExtent));
        float3 offset = (cycle * 2 - 1) * worldExtent;
        float phase = traits.x * 6.2831853;
        offset.x += sin(Seconds * 1.3 * rate + phase) * .007 * flutter;
        offset.z += cos(Seconds * .9 * rate + phase) * .004 * flutter;
        float depth = dot(offset, planeNormal);
        offset += planeNormal * depth * (max(InteriorDepth, 0) - 1);
        float3 petalCenter = volumeCenter + offset;
        // Solve on the whole ray line: folds of the hair silhouette must not
        // cut, rotate, or stretch a petal behind this virtual window.
        float t = dot(petalCenter - PositionWS, planeNormal) / denominator;
        float3 delta = PositionWS + ray * t - petalCenter;
        float2 local = float2(dot(delta, right), dot(delta, up));
        float size = PetalSize / max(WorldTiling, 1) * lerp(.7, 1.15, traits.x);
        if (dot(local, local) > size * size) continue;
        float angle = phase + Seconds * lerp(-.8, .8, traits.y) * flutter;
        float s, c; sincos(angle, s, c);
        local = float2(c * local.x - s * local.y, s * local.x + c * local.y);
        local.x /= lerp(1, .55 + .35 * sin(Seconds * 1.8 * rate + phase), flutter);
        float2 petalUV = local / max(float2(size * .7, size), .0001) + .5;
        if (any(petalUV < 0) || any(petalUV > 1)) continue;
        // Explicit LOD: texture reads are inside a divergent candidate loop.
        float mask = SAMPLE_TEXTURE2D_LOD(PetalMask.tex, PetalMask.samplerstate, petalUV, 0).r;
        float3 color = SAMPLE_TEXTURE2D_LOD(PetalMap.tex, PetalMap.samplerstate, petalUV, 0).rgb;
        color = lerp(color, float3(1, .75, .82), .38) * PetalTint.rgb;
        // Fade only the wrapping boundaries along the travel direction.
        float3 edge = smoothstep(0, .07, cycle) * (1 - smoothstep(.93, 1, cycle));
        float edgeFade = dot(abs(direction), edge) / max(dot(abs(direction), float3(1, 1, 1)), .001);
        float farAmount = saturate(.5 - depth / (2 * depthExtent));
        color *= lerp(1, 1 - saturate(DepthFade), farAmount);
        float opacity = mask * strength * visibility * edgeFade;
        accumulatedColor += color * opacity;
        accumulatedWeight += opacity;
        transparency *= 1 - opacity;
    }
    float coverage = 1 - transparency;
    BaseColor *= transparency;
    // Independent petal shading: hair strand normals cannot emboss them.
    Emission = accumulatedColor / max(accumulatedWeight, .0001) * coverage * (1 + max(Glow, 0));
}
#endif
