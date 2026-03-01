#ifndef UNITY_CASCADED_OCCLUSION_MAPS_INCLUDED
#define UNITY_CASCADED_OCCLUSION_MAPS_INCLUDED

#if defined(SHADER_API_D3D11) || defined(SHADER_API_PS4) || defined(SHADER_API_PS5) || defined(SHADER_API_XBOXONE) || defined(SHADER_API_GAMECORE)
#define USE_STRUCTURED_BUFFER_FOR_CASCADED_OCCLUSION 1
#endif

float _CascadedOcclusionIntensity;

// x = page size;
// y = 1.0f / page size
// z = page mip level
float4 _CascadedOcclusionPageParams;

// x = region.x
// y = region.y
// z = 1.0f / region.width
// w = 1.0f / region.height
float4 _CascadedOcclusionRegionParams;

// x = tile.tileSize
// y = tile.tilingCount
// z = tile.textureSize
// w = unused
float4 _CascadedOcclusionTileParams;

// x = page size
// y = page size * tile size
// z = page mip level - 1
// w = mipmap bias
float4 _CascadedOcclusionFeedbackParams;

// x = depth bias
// y = normal bias
float4 _CascadedOcclusionBiasParams;

// x = softness
// y = softness near
// z = softness far
float4 _CascadedOcclusionPcssParams;

// LightDirection
float4 _CascadedOcclusionLightDirection;

// x = unsed
// y = unsed
// z = 5.0f / QualitySettings.shadowDistance
// w = -1.0f * (2.0f + fieldOfView / 180.0f * 2.0f)
float4 _CascadedOcclusionData;

// WorldToLocalMatrix
float4x4 _CascadedOcclusionLightMatrix;

#if USE_STRUCTURED_BUFFER_FOR_CASCADED_OCCLUSION
StructuredBuffer<float4x4> _CascadedOcclusionMatrices_SSBO;
#else
#define MAX_VISIBLE_SHADOW_UBO 64
float4x4 _CascadedOcclusionMatrices[MAX_VISIBLE_SHADOW_UBO];
#endif

UNITY_DECLARE_TEX2D(_CascadedOcclusionLookupTexture);
UNITY_DECLARE_SHADOWMAP(_CascadedOcclusionTileTexture);

float4 _CascadedOcclusionTileTexture_TexelSize;

int ComputeTileSlot(float4 page)
{
	return page.y * _CascadedOcclusionTileParams.y + page.x;
}

float4 EvaluateTileLOD(float2 uv)
{
    float2 page = floor(uv * _CascadedOcclusionFeedbackParams.x);

    float2 uvInt = uv * _CascadedOcclusionFeedbackParams.y;
    float2 dx = ddx(uvInt);
    float2 dy = ddy(uvInt);
    int mip = clamp(int(0.5 * log2(max(dot(dx, dx), dot(dy, dy))) + _CascadedOcclusionFeedbackParams.w + 0.5), 0, _CascadedOcclusionFeedbackParams.z);

    return float4(float3(page, mip), 1);
}

float2 TransformToIndirectionUV(float3 worldPos)
{
	float3 localPos = mul(_CascadedOcclusionLightMatrix, float4(worldPos, 1)).xyz;
	return (localPos.xy - _CascadedOcclusionRegionParams.xy) * _CascadedOcclusionRegionParams.zw;
}

float4 FetchIndirectionTile(float2 uv)
{
	float2 uvInt = uv - frac(uv * _CascadedOcclusionPageParams.x) * _CascadedOcclusionPageParams.y;
	float4 page = UNITY_SAMPLE_TEX2D_SAMPLER_LOD(_CascadedOcclusionLookupTexture, _CascadedOcclusionLookupTexture, uvInt, 0);
	page.w = max(1, page.w);
	return page;
}

float2 MapTileToMosaicUV(float2 page, float2 pageOffset)
{
	return (page.rg * (_CascadedOcclusionTileParams.x) + pageOffset * _CascadedOcclusionTileParams.x) / _CascadedOcclusionTileParams.z;
}

float3 ProjectWorldToOcclusionCoord(float3 positionWS, float4 page)
{
#if USE_STRUCTURED_BUFFER_FOR_CASCADED_OCCLUSION
	float4 ndcpos = mul(_CascadedOcclusionMatrices_SSBO[ComputeTileSlot(page)], float4(positionWS, 1));
#else
	float4 ndcpos = mul(_CascadedOcclusionMatrices[ComputeTileSlot(page)], float4(positionWS, 1));
#endif

	return float3(MapTileToMosaicUV(page, ndcpos.xy / ndcpos.w), ndcpos.z);
}

float3 ApplyOcclusionBias(float3 positionWS, float3 normalWS, float radius)
{
	float3 wLight = -_CascadedOcclusionLightDirection;
	float shadowCos = clamp(dot(normalWS, wLight), 1e-4f, 0.85f);
	float shadowSine = sqrt(1 - shadowCos * shadowCos);
	float shadowTan = min(1, shadowSine / shadowCos);

	positionWS = positionWS + wLight * shadowTan.xxx * _CascadedOcclusionBiasParams.xxx * radius;
	positionWS = positionWS + normalWS * shadowSine.xxx * _CascadedOcclusionBiasParams.yyy * radius;

	return positionWS;
}

float3 GetVirtualShadowTexcoord(float3 positionWS, float3 normalWS, float radius = 2.0f)
{
	float2 uv = TransformToIndirectionUV(positionWS);
	float4 page = FetchIndirectionTile(uv);

	return ProjectWorldToOcclusionCoord(ApplyOcclusionBias(positionWS, normalWS, 1 + radius * page.w), page);
}

inline float3 combineVirtualShadowcoordComponents (float2 baseUV, float2 deltaUV, float depth, float2 receiverPlaneDepthBias)
{
	float3 uv = float3( baseUV + deltaUV, depth );
	uv.z += dot (deltaUV, receiverPlaneDepthBias); // apply the depth bias
	return uv;
}

half SampleVirtualShadowMap_PCF3x3(float4 coord, float2 receiverPlaneDepthBias)
{
	const float2 offset = float2(0.5,0.5);
	float2 uv = (coord.xy * _CascadedOcclusionTileTexture_TexelSize.zw) + offset;
	float2 base_uv = (floor(uv) - offset) * _CascadedOcclusionTileTexture_TexelSize.xy;
	float2 st = frac(uv);

	float2 uw = float2( 3-2*st.x, 1+2*st.x );
	float2 u = float2( (2-st.x) / uw.x - 1, (st.x)/uw.y + 1 );
	u *= _CascadedOcclusionTileTexture_TexelSize.x;

	float2 vw = float2( 3-2*st.y, 1+2*st.y );
	float2 v = float2( (2-st.y) / vw.x - 1, (st.y)/vw.y + 1);
	v *= _CascadedOcclusionTileTexture_TexelSize.y;

    half shadow;
	half sum = 0;

    sum += uw[0] * vw[0] * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u[0], v[0]), coord.z, receiverPlaneDepthBias));
    sum += uw[1] * vw[0] * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u[1], v[0]), coord.z, receiverPlaneDepthBias));
    sum += uw[0] * vw[1] * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u[0], v[1]), coord.z, receiverPlaneDepthBias));
    sum += uw[1] * vw[1] * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u[1], v[1]), coord.z, receiverPlaneDepthBias));

    shadow = sum / 16.0f;

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
	return 1 - shadow;
#else
	return shadow;
#endif
}

half SampleVirtualShadowMap_PCF5x5(float4 coord, float2 receiverPlaneDepthBias)
{ 
#if defined(SHADOWS_NATIVE)
	const float2 offset = float2(0.5,0.5);
	float2 uv = (coord.xy * _CascadedOcclusionTileTexture_TexelSize.zw) + offset;
	float2 base_uv = (floor(uv) - offset) * _CascadedOcclusionTileTexture_TexelSize.xy;
	float2 st = frac(uv);

	float3 uw = float3( 4-3*st.x, 7, 1+3*st.x );
	float3 u = float3( (3-2*st.x) / uw.x - 2, (3+st.x)/uw.y, st.x/uw.z + 2 );
	u *= _CascadedOcclusionTileTexture_TexelSize.x;

	float3 vw = float3( 4-3*st.y, 7, 1+3*st.y );
	float3 v = float3( (3-2*st.y) / vw.x - 2, (3+st.y)/vw.y, st.y/vw.z + 2 );
	v *= _CascadedOcclusionTileTexture_TexelSize.y;

	half shadow = 0.0f;
	half sum = 0.0f;

	half3 accum = uw * vw.x;
	sum += accum.x * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.x,v.x), coord.z, receiverPlaneDepthBias));
    sum += accum.y * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.y,v.x), coord.z, receiverPlaneDepthBias));
    sum += accum.z * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.z,v.x), coord.z, receiverPlaneDepthBias));

	accum = uw * vw.y;
    sum += accum.x *  UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.x,v.y), coord.z, receiverPlaneDepthBias));
    sum += accum.y *  UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.y,v.y), coord.z, receiverPlaneDepthBias));
    sum += accum.z *  UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.z,v.y), coord.z, receiverPlaneDepthBias));

	accum = uw * vw.z;
    sum += accum.x * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.x,v.z), coord.z, receiverPlaneDepthBias));
    sum += accum.y * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.y,v.z), coord.z, receiverPlaneDepthBias));
    sum += accum.z * UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(u.z,v.z), coord.z, receiverPlaneDepthBias));

    shadow = sum / 144.0f;

#else // #if defined(SHADOWS_NATIVE)

	// when we don't have hardware PCF sampling, then the above 5x5 optimized PCF really does not work.
	// Fallback to a simple 3x3 sampling with averaged results.
 	half shadow = 0;
	float2 base_uv = coord.xy;
	float2 ts = _CascadedOcclusionTileTexture_TexelSize.xy;
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(-ts.x,-ts.y), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(    0,-ts.y), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2( ts.x,-ts.y), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(-ts.x,    0), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(    0,    0), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2( ts.x,    0), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(-ts.x, ts.y), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2(    0, ts.y), coord.z, receiverPlaneDepthBias));
	shadow += UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, combineVirtualShadowcoordComponents(base_uv, float2( ts.x, ts.y), coord.z, receiverPlaneDepthBias));
	shadow /= 9.0;

#endif // else of #if defined(SHADOWS_NATIVE)

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
	return 1 - shadow;
#else
	return shadow;
#endif
}

#if defined(_CASCADED_OCCLUSION_MAPS_PCSS)
#define VIRTUAL_SHADOW_MAPS_POISSON_COUNT 26

static const float2 Poisson[26] =
{
	float2( 0.0, 0.0),
	float2(-0.978698, -0.0884121),
	float2(-0.841121, 0.521165),
	float2(-0.71746, -0.50322),
	float2(-0.702933, 0.903134),
	float2(-0.663198, 0.15482),
	float2(-0.495102, -0.232887),
	float2(-0.364238, -0.961791),
	float2(-0.345866, -0.564379),
	float2(-0.325663, 0.64037),
	float2(-0.182714, 0.321329),
	float2(-0.142613, -0.0227363),
	float2(-0.0564287, -0.36729),
	float2(-0.0185858, 0.918882),
	float2(0.0381787, -0.728996),
	float2(0.16599, 0.093112),
	float2(0.253639, 0.719535),
	float2(0.369549, -0.655019),
	float2(0.423627, 0.429975),
	float2(0.530747, -0.364971),
	float2(0.566027, -0.940489),
	float2(0.639332, 0.0284127),
	float2(0.652089, 0.669668),
	float2(0.773797, 0.345012),
	float2(0.968871, 0.840449),
	float2(0.991882, -0.657338)
};

float2 VirtualShadowMapsBlockerSearch(float3 shadowCoord, float serachRadius, float angle)
{
	float blockerSum = 0.0;
	float numBlockers = 1e-4f;

	float2 rotation = float2(cos(angle), sin(angle));

	UNITY_UNROLL
	for (int i = 0; i < VIRTUAL_SHADOW_MAPS_POISSON_COUNT; i++)
	{
		float2 pos = Poisson[i];
		float2 samples = float2(pos.x * rotation.x - pos.y * rotation.y, pos.y * rotation.x + pos.x * rotation.y);

		float2 offset = samples * serachRadius;
		float3 biasedCoords = float3(shadowCoord.xy + offset, shadowCoord.z);

		float shadowMapDepth = UNITY_SAMPLE_TEX2D_SAMPLER_LOD(_CascadedOcclusionTileTexture, _CascadedOcclusionLookupTexture, biasedCoords.xy, 0).r;

#if defined(UNITY_REVERSED_Z)
		float sum = shadowMapDepth >= biasedCoords.z;
		blockerSum += shadowMapDepth * sum;
		numBlockers += sum;
#else
		float sum = shadowMapDepth <= biasedCoords.z;
		blockerSum += shadowMapDepth * sum;
		numBlockers += sum;
#endif
	}

	float avgBlockerDepth = blockerSum / numBlockers;

#if defined(UNITY_REVERSED_Z)
	avgBlockerDepth = 1.0 - avgBlockerDepth;
#endif

	return float2(avgBlockerDepth, numBlockers);
}

float SampleVirtualShadowMap_PCSS(float3 positionWS, float3 normalWS, float angle)
{
	float2 rotation = float2(cos(angle), sin(angle));

	float2 uv = TransformToIndirectionUV(positionWS);
	float4 page = FetchIndirectionTile(uv);

	float worldDistance = distance(_WorldSpaceCameraPos, positionWS);
	float samplerDst = (1.0 - min(worldDistance, 500.0f) / 500.0f);
	float filterRadius = lerp(1, _CascadedOcclusionPcssParams.x, samplerDst / page.w);

	float3 blockerPos = ApplyOcclusionBias(positionWS, normalWS, (1 + ceil(_CascadedOcclusionPcssParams.y) * page.w));
	float3 blockerCoord = ProjectWorldToOcclusionCoord(blockerPos, page);
	float2 blockerResults = VirtualShadowMapsBlockerSearch(blockerCoord, filterRadius * _CascadedOcclusionTileTexture_TexelSize.x, rotation);

	UNITY_BRANCH
	if (blockerResults.y < 0.1f)
		return 1.0;
	else if (blockerResults.y >= VIRTUAL_SHADOW_MAPS_POISSON_COUNT)
		return 0.0f;

#if defined(UNITY_REVERSED_Z)
	float penumbraRatio = ((1.0 - blockerCoord.z) - blockerResults.x) / (1 - blockerResults.x);
#else
	float penumbraRatio = (blockerCoord.z - blockerResults.x) / blockerResults.x;
#endif

	float total = 0;

	float filterSize = lerp(_CascadedOcclusionPcssParams.y, _CascadedOcclusionPcssParams.z, penumbraRatio / page.w);

	float3 worldPos = ApplyOcclusionBias(positionWS, normalWS, (1 + ceil(filterSize) * page.w));
	float3 shadowCoord = ProjectWorldToOcclusionCoord(worldPos, page);
	float2 baseCoord = _CascadedOcclusionTileTexture_TexelSize.xy * filterSize;

	UNITY_UNROLL
	for (int i = 0; i < VIRTUAL_SHADOW_MAPS_POISSON_COUNT; i++)
	{
		float2 pos = Poisson[i];
		float2 samples = float2(pos.x * rotation.x - pos.y * rotation.y, pos.y * rotation.x + pos.x * rotation.y);

		float2 offset = samples * baseCoord;
		float3 biasedCoords = float3(shadowCoord.xy + offset, shadowCoord.z);

		float shadow = UNITY_SAMPLE_SHADOW(_CascadedOcclusionTileTexture, biasedCoords);
		total += shadow;
	}

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
	return 1.0f - total / VIRTUAL_SHADOW_MAPS_POISSON_COUNT;
#else
	return total / VIRTUAL_SHADOW_MAPS_POISSON_COUNT;
#endif	
}

#endif

#endif
