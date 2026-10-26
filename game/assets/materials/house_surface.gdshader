shader_type spatial;
render_mode diffuse_burley;
uniform vec4 base_color : source_color = vec4(0.64,0.59,0.47,1.0);
uniform bool timber = false;
varying vec3 local_point;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 cell=floor(p), f=fract(p);
    f=f*f*(3.0-2.0*f);
    return mix(mix(hash(cell),hash(cell+vec2(1,0)),f.x),
        mix(hash(cell+vec2(0,1)),hash(cell+vec2(1,1)),f.x),f.y);
}
void vertex() { local_point = VERTEX; }
void fragment() {
    vec3 p = local_point;
    float shade = .97;
    if (timber) {
        vec2 board = vec2(p.x*3.5,p.z*.65);
        float seam = step(.025,fract(board.x))*step(.012,fract(board.y));
        shade *= mix(.45,.87+.17*hash(floor(board)),seam);
        shade *= .96+.04*sin(p.z*65.0+sin(p.x*23.0));
    } else {
        vec2 wall = vec2(p.x+p.z,p.y);
        shade *= .94+.06*noise(wall*3.0);
    }
    ALBEDO = base_color.rgb*shade;
    ROUGHNESS = .94;
}
