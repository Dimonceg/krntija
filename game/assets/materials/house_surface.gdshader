shader_type spatial;
render_mode diffuse_burley;
uniform vec4 base_color : source_color = vec4(0.64,0.59,0.47,1.0);
uniform bool timber = false;
varying vec3 local_point;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
void vertex() { local_point = VERTEX; }
void fragment() {
    vec3 p = local_point;
    float grain = sin(p.x*63.0)*sin(p.y*53.0+p.z*49.0);
    float detail = 1.0-smoothstep(.01,.06,length(fwidth(p)));
    float shade = .97 + .025*grain*detail;
    if (timber) {
        vec2 board = vec2(p.x*3.5,p.z*.65);
        float seam = step(.025,fract(board.x))*step(.012,fract(board.y));
        shade *= mix(.45,.87+.17*hash(floor(board)),seam);
        shade *= .96+.04*sin(p.z*65.0+sin(p.x*23.0));
    } else {
        float stain = hash(floor(p.xy*1.7)+floor(p.zy*1.7));
        shade *= .93+.07*stain;
    }
    ALBEDO = base_color.rgb*shade;
    ROUGHNESS = .94;
}
