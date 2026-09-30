Shader "Custom/TransparentCubeWithEdges"
{
    Properties
    {
        _FaceColor ("面颜色", Color) = (1, 1, 1, 1)
        _FaceAlpha ("面透明度", Range(0, 1)) = 0.3
        _EdgeColor ("棱颜色", Color) = (0, 1, 1, 1)
        _EdgeAlpha ("棱透明度", Range(0, 1)) = 1.0
        _EdgeWidth ("棱宽度", Range(0.001, 0.5)) = 0.05
        _MainTex ("面纹理", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off        // 双面渲染；只想从外部看可改为 Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 modelPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _FaceColor;
            float _FaceAlpha;
            fixed4 _EdgeColor;
            float _EdgeAlpha;
            float _EdgeWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.modelPos = v.vertex.xyz;          // 模型空间位置
                o.normal = v.normal;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // --- 1. 判断当前片元在哪个面上，计算到最近棱边的距离 ---
                float3 absNormal = abs(i.normal);
                float3 p = i.modelPos;
                float distToEdge;

                // 默认 Cube 中心在原点，边长 1，坐标范围 [-0.5, 0.5]
                if (absNormal.x > absNormal.y && absNormal.x > absNormal.z)
                {
                    // 面法线沿 X 轴，变化轴为 Y 和 Z
                    distToEdge = min(0.5 - abs(p.y), 0.5 - abs(p.z));
                }
                else if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
                {
                    // 面法线沿 Y 轴，变化轴为 X 和 Z
                    distToEdge = min(0.5 - abs(p.x), 0.5 - abs(p.z));
                }
                else
                {
                    // 面法线沿 Z 轴，变化轴为 X 和 Y
                    distToEdge = min(0.5 - abs(p.x), 0.5 - abs(p.y));
                }

                // 距离越小越靠近棱，生成 0~1 的棱边遮罩
                float edgeMask = 1.0 - smoothstep(0.0, _EdgeWidth, distToEdge);

                // --- 2. 计算面和棱的颜色与透明度 ---
                fixed4 tex = tex2D(_MainTex, i.uv);

                fixed4 faceCol = _FaceColor * tex;
                faceCol.a = _FaceAlpha * tex.a;

                fixed4 edgeCol = _EdgeColor;
                edgeCol.a = _EdgeAlpha;

                // --- 3. 混合：棱边处显示棱，其余显示面 ---
                fixed4 col = lerp(faceCol, edgeCol, edgeMask);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}