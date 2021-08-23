Shader "Hidden/HMD_TMO"
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
			float _Switch;

			int _Debug;

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


			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 color = tex2D(_MainTex, i.uv);
				float lum = (0.2126 * color.r) + (0.7152 * color.g) + (0.0722 * color.b);
				float logLum = log(lum + 0.00001);
				
				fixed4 desaturatedColor = pow(color / lum, _Saturation);
				
				float V = Viewport(lum);
				float G = Global(logLum);
							
				//float4 result = desaturatedColor * sqrt(V*G);
				float4 result = desaturatedColor * (pow(V, 1.0-_Switch)*pow(G, _Switch));
				
				// Debug
				if (_Debug == 1) {
					float xMin = 0.1;
					float xMax = 0.5;
					float yMin = 0.1;
					float yMax = 0.4;
					float delta = 0.025;

					if (i.uv.x > xMin && i.uv.x < xMax && i.uv.y > yMin && i.uv.y < yMax)
					{
						float xW = (i.uv.x - xMin) / (xMax - xMin);
						float yW = (i.uv.y - yMin) / (yMax - yMin);

						// Log representation
						float normalizedLog = xW;
						float maxLogL = log(_MaxPanoramaLum + 0.00001);
						float minLogL = log(_MinPanoramaLum + 0.00001);
						float logL = (normalizedLog * (maxLogL - minLogL)) + minLogL;
						float l = exp(logL);

						float v = Viewport(l);
						float g = Global(logL);
						//float blend = sqrt(v*g);
						float blend = pow(v, 1.0 - _Switch)*pow(g, _Switch);

						result *= 0.3;

						// Viewport curve
						if (yW > v-delta && yW < v) {
							result.b = 1.0;
						}
						// Global curve
						if (yW > g-delta && yW < g) {
							result.g = 1.0;
						}
						// Blend curve
						if (yW > blend - delta && yW < blend) {
							result.r = 1.0;
						}
					}
				}

				return result;
			}
			ENDCG
		}
	}
}
