Shader "Hidden/Adaptive Rendering/Draw Mosaic"
{
	Properties
	{
		_Padding("Padding", Range(0, 1)) = 0
	}
	CGINCLUDE
		#include "UnityCG.cginc"
   
		struct Attributes
		{
			float4 vertex	: POSITION;
			float2 uv		: TEXCOORD0;
		};

		struct Varyings
		{
			float2 uv		: TEXCOORD0;
			float4 vertex	: SV_POSITION;
		};

		UNITY_DECLARE_TEX2D(_MainTex);

		float _Padding;

		Varyings vert (Attributes v)
		{
			Varyings o;
			o.vertex = float4(mul(unity_ObjectToWorld, v.vertex).xyz, 1);
			o.uv = v.uv;
			return o;
		}
		
		float4 frag(Varyings i) : SV_TARGET
		{
			float2 scale = 1 + _Padding;
			float2 offset = scale * 0.5 - 0.5;
			float2 uv = saturate(i.uv * scale - offset);

			return UNITY_SAMPLE_TEX2D_SAMPLER_LOD(_MainTex, _MainTex, uv, 0);
		}
	ENDCG
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			ZTest Always ZWrite On
			Cull Off
		 
			CGPROGRAM

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			#pragma vertex vert
			#pragma fragment frag

			ENDCG
		}
	}
}
