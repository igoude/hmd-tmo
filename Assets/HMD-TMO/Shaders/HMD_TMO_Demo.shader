Shader "Hidden/HMD_TMO_Demo"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			sampler2D _MainTex;
	
			// Global TMO
			int _NbBins;
			StructuredBuffer<uint> _Histogram;

			// Viewport TMO
			float4 _KeyValues;

			// Panorama constant
			float _MinPanoramaLum;
			float _MaxPanoramaLum;
			float _AvgPanoramaLum;
			float _Saturation;
			//float _Switch;

			//int _Debug;
			int _Output;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
		
			// Viewport TMO: Reinhard
			float Viewport(float lum) {
				float Ld = (_KeyValues[3] / _KeyValues[2]) * lum;
				return (Ld*(1.0 + (Ld / (_KeyValues[1] * _KeyValues[1])))) / (1.0 + Ld);
			}

			// Global TMO: CDF
			float Global(float logLum) {
				float logMin = log(_MinPanoramaLum + 0.00001);
				float logMax = log(_MaxPanoramaLum + 0.00001);
				logLum = max(min(logLum, logMax), logMin);

				float normalizedLogLum = (logLum - logMin) / (logMax - logMin);
				int bin = int(normalizedLogLum * float(_NbBins - 1));

				return float(_Histogram[bin]) / float(_Histogram[_NbBins - 1]);
			}

			// Reinhard Panorama
			float Reinhard(float lum) {
				float Ld = (_KeyValues[3] / _AvgPanoramaLum) * lum;
				return (Ld*(1.0 + (Ld / (_MaxPanoramaLum * _MaxPanoramaLum)))) / (1.0 + Ld);
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 color = tex2D(_MainTex, i.uv);
				float lum = (0.2126 * color.r) + (0.7152 * color.g) + (0.0722 * color.b);
				float logLum = log(lum + 0.00001);
				
				fixed4 desaturatedColor = pow(color / lum, _Saturation);
				float result = 0.0;

				// Demonstration output
				// HDR
				if (_Output == 0) {
					result = logLum + log(_MinPanoramaLum + 0.00001) + _KeyValues[3] * 10.0;
				}
				// Linear
				else if (_Output == 1) {
					result = (lum - _MinPanoramaLum) / (_MaxPanoramaLum - _MinPanoramaLum);
				}
				// Log
				else if (_Output == 6) {
					float logMin = log(_MinPanoramaLum + 0.00001);
					float logMax = log(_MaxPanoramaLum + 0.00001);
					result = (logLum - logMin) / (logMax - logMin);
				}
				// Global
				else if (_Output == 4) {
					result = Reinhard(lum);
				}
				// Viewport
				else if (_Output == 5) {
					result = Viewport(lum);
				}
				// Yu
				else if (_Output == 3) {
					result = (_KeyValues[3] / _KeyValues[2]) * lum;
					//result = Viewport(lum);
				}
				// Our
				else if (_Output == 2) {
					result = sqrt(Viewport(lum) * Global(logLum));
				}
				
				return result * desaturatedColor;
			}
			ENDCG
		}
	}
}
