Shader "Hidden/StaticShadowMap/ShadowCaster"
{
	Properties
	{
		_MainTex("Albedo", 2D) = "white" {}
		_Cutoff("Cutoff", Range(0, 1)) = 0.01
	}
	CGINCLUDE
		#include "UnityCG.cginc"

		struct Attributes
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float2 uv	  : TEXCOORD0;
		};

		struct Varyings
		{   float4 vertex 	: SV_POSITION;
			float4 depth 	: TEXCOORD0;
			float2 uv	 	: TEXCOORD1;
			float2 worldPos	: TEXCOORD2;
		};

		UNITY_DECLARE_TEX2D(_MainTex);

		uniform float _Cutoff;

		Varyings vert(Attributes input)
		{
			Varyings output;
			output.vertex = UnityObjectToClipPos(input.vertex);
			output.uv = input.uv;
			output.depth = output.vertex;
			output.worldPos = mul(unity_ObjectToWorld, input.vertex);

			return output;
		}

		float DepthFrag(Varyings input) : SV_TARGET
		{
			float4 MainTex = UNITY_SAMPLE_TEX2D_SAMPLER(_MainTex, _MainTex, input.uv);
			clip(MainTex.a - _Cutoff);

		#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
			float shadowDepth = input.depth.z;
			shadowDepth += 1.0f / 65535.f; // For R16_Depth;
			shadowDepth += (abs(ddx(shadowDepth)) + abs(ddy(shadowDepth)));
			return 1 - (shadowDepth * 0.5f + 0.5f);
		#else
			float shadowDepth = input.depth.z;
			shadowDepth -= 1.0f / 65535.f; // For R16_Depth;
			shadowDepth -= (abs(ddx(shadowDepth)) + abs(ddy(shadowDepth)));
			return shadowDepth;
		#endif
		}

		float HeightFrag(Varyings input) : SV_TARGET
		{
			return input.worldPos.y;
		}
	ENDCG
	SubShader
	{
		LOD 100

		Pass
		{
			ZTest Lequal ZWrite On
			Cull Off
			Offset 1, 1

			Tags { "RenderType" = "DepthMap" }

			CGPROGRAM
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment DepthFrag

			ENDCG
		}

		Pass
		{
			ZTest Lequal ZWrite On
			Cull Off

			Tags { "RenderType" = "HeightMap" }

			CGPROGRAM
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment HeightFrag

			ENDCG
		}
	}
}
