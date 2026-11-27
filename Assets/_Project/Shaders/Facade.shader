// Building façades and roofs from one atlas, for merged map tiles (Built-in pipeline, Quest-cheap).
// Mesh data (written by FacadeBuilder):
//   uv0.x = bays along the wall (1 bay = one window module, integer at both wall ends)
//   uv0.y = height above the ground in metres
//   color.r = wall tint index / 7      color.g = style / 3 (0 house, 1 shops, 2 flats, 3 house with garage)
//   color.b = 1 on roofs               color.a = 1 if the wall has windows, 0 for a blind wall
// Atlas: 4 columns (styles) x 2 rows (bottom: ground floor, top: upper storeys); one cell = one bay x one storey.
// Wall pixels have alpha 1 and take the building's tint; windows, doors and shutters keep their colour.
Shader "PermisVR/Facade"
{
    Properties
    {
        _MainTex ("Facade atlas", 2D) = "white" {}
        _StoreyM ("Storey height (m)", Float) = 3
        _ContactShade ("Darkening at the foot of walls", Range(0, 1)) = 0.3
        _ContactHeightM ("Height of the darkening (m)", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150

        CGPROGRAM
        #pragma surface surf Lambert noforwardadd nolppv nometa
        #pragma target 3.0

        sampler2D _MainTex;
        float _StoreyM;
        float _ContactShade;
        float _ContactHeightM;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        static const fixed3 WallTints[8] =
        {
            fixed3(0.93, 0.89, 0.80), // cream render
            fixed3(0.86, 0.80, 0.69), // beige
            fixed3(0.80, 0.80, 0.78), // light grey
            fixed3(0.96, 0.95, 0.92), // white
            fixed3(0.90, 0.79, 0.62), // pale ochre
            fixed3(0.91, 0.82, 0.76), // pale pink
            fixed3(0.70, 0.46, 0.36), // brick
            fixed3(0.78, 0.74, 0.66)  // stone
        };

        static const fixed3 RoofTints[8] =
        {
            fixed3(0.62, 0.31, 0.22), fixed3(0.36, 0.37, 0.40), fixed3(0.58, 0.29, 0.21), fixed3(0.40, 0.41, 0.43),
            fixed3(0.66, 0.35, 0.24), fixed3(0.33, 0.34, 0.36), fixed3(0.55, 0.30, 0.23), fixed3(0.45, 0.45, 0.46)
        };

        float Hash(float n)
        {
            return frac(sin(n * 12.9898) * 43758.5453);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            int tint = (int)round(IN.color.r * 7.0);
            if (IN.color.b > 0.5)
            {
                o.Albedo = RoofTints[tint];
                return;
            }

            fixed3 wall = WallTints[tint];
            // Fake ambient occlusion where the wall meets the ground (no baked lighting).
            fixed contact = lerp(1.0 - _ContactShade, 1.0, saturate(IN.uv_MainTex.y / _ContactHeightM));
            if (IN.color.a < 0.5)
            {
                o.Albedo = wall * 0.97 * contact;
                return;
            }

            float2 coord = IN.uv_MainTex;
            float bay = floor(coord.x);
            float storeyCoord = coord.y / _StoreyM;
            float storey = floor(storeyCoord);
            int style = (int)round(IN.color.g * 3.0);

            // Ground floor: mostly the style's own module, with an entrance door now and then.
            int column = style;
            if (storey < 0.5)
            {
                int ground = style == 2 ? 0 : style;
                column = Hash(bay + tint * 17.0 + style * 5.0) < 0.28 ? 2 : ground;
                if (style == 3 && Hash(bay * 3.1 + tint) > 0.6) column = 0;
            }

            float row = storey < 0.5 ? 0.0 : 1.0;
            float2 cell = float2((column + frac(coord.x)) / 4.0, (row + frac(storeyCoord)) / 2.0);
            // Gradients of the continuous coordinates: no mip seams at the cell edges.
            float2 continuous = float2(coord.x / 4.0, storeyCoord / 2.0);
            fixed4 texel = tex2Dgrad(_MainTex, cell, ddx(continuous), ddy(continuous));
            o.Albedo = lerp(texel.rgb, texel.rgb * wall * 1.08, texel.a) * contact;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
