"""Normalize Blender's extra colour attribute for textured vertex-colour assets."""
import json
import struct

def normalize_palette(path):
    raw=path.read_bytes()
    size,kind=struct.unpack_from('<II',raw,12)
    assert kind==0x4e4f534a
    data=json.loads(raw[20:20+size])
    for mesh in data.get('meshes',[]):
        for primitive in mesh['primitives']:
            attrs=primitive['attributes']
            # With texture-only export Blender inserts white COLOR_0; the named
            # Color layer requested by export_all_vertex_colors is COLOR_1.
            if 'COLOR_1' in attrs:attrs['COLOR_0']=attrs.pop('COLOR_1')
            assert 'COLOR_0' in attrs
    payload=json.dumps(data,separators=(',',':')).encode()
    payload+=b' '*((-len(payload))%4)
    tail=raw[20+size:]
    path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(payload)+len(tail))+
                     struct.pack('<II',len(payload),kind)+payload+tail)
