# Exporta un villano de Camila a FBX + texturas PNG para Unity, sin modificar el .blend original.
# blender -b X.blend --python exportar.py -- Nombre alturaMetros carpetaSalida
import bpy, sys, os, json
import numpy as np
from mathutils import Matrix, Vector
nombre, altura, salida = sys.argv[sys.argv.index("--")+1:]
altura = float(altura)
tex_dir = os.path.join(salida, "Texturas"); os.makedirs(tex_dir, exist_ok=True)

def srgb_to_lin(c): return np.where(c <= 0.04045, c/12.92, ((c+0.055)/1.055)**2.4)
def lin_to_srgb(c): return np.where(c <= 0.0031308, c*12.92, 1.055*np.power(np.clip(c,0,None),1/2.4)-0.055)
def hue_sat(rgb, h, s, v):
    # mismo calculo que el nodo Hue/Saturation/Value de Blender (sobre color lineal)
    mx = rgb.max(-1); mn = rgb.min(-1); d = mx-mn
    H = np.zeros_like(mx); nz = d > 1e-8
    r,g,b = rgb[...,0],rgb[...,1],rgb[...,2]
    idx = nz & (mx==r); H[idx] = ((g-b)[idx]/d[idx]) % 6
    idx = nz & (mx==g) & (mx!=r); H[idx] = (b-r)[idx]/d[idx] + 2
    idx = nz & (mx==b) & (mx!=r) & (mx!=g); H[idx] = (r-g)[idx]/d[idx] + 4
    H = (H/6 + (h-0.5)) % 1.0
    S = np.where(mx>0, d/np.maximum(mx,1e-8), 0)*s; S = np.clip(S,0,1)
    V = mx*v
    i = np.floor(H*6).astype(int) % 6; f = H*6-np.floor(H*6)
    p = V*(1-S); q = V*(1-S*f); t = V*(1-S*(1-f))
    out = np.stack([np.choose(i,[V,q,p,p,t,V]), np.choose(i,[t,V,V,q,p,p]), np.choose(i,[p,p,t,V,V,q])], -1)
    return out

guardadas = {}
def guardar_textura(img, extra=None):
    key = (img.name, extra)
    if key in guardadas: return guardadas[key]
    base = os.path.splitext(img.name)[0].replace(".001","")
    if extra: base += "_ajustada"
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    if extra:
        hh, ss, vv = extra
        lin = srgb_to_lin(px[...,:3])
        px[...,:3] = np.clip(lin_to_srgb(np.clip(hue_sat(lin,hh,ss,vv),0,1)),0,1)
    nuevo = bpy.data.images.new(base+"_png", w, h, alpha=True)
    nuevo.pixels = px.ravel().tolist() if px.size < 4_000_000 else px.ravel()
    ruta = os.path.join(tex_dir, base + ".png")
    nuevo.filepath_raw = ruta; nuevo.file_format = 'PNG'; nuevo.save()
    guardadas[key] = base + ".png"
    return guardadas[key]

# 1) Materiales -> nombre claro + textura PNG
mapa = {}
for m in list(bpy.data.materials):
    if not m.users or not m.use_nodes: continue
    tex = None; ajuste = None
    for n in m.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image: tex = n.image
        if n.type == 'HUE_SAT':
            ajuste = tuple(n.inputs[k].default_value for k in ('Hue','Saturation','Value'))
    if not tex: continue
    archivo = guardar_textura(tex, ajuste)
    base = os.path.splitext(archivo)[0] if m.name.startswith("Material") else m.name  # Material.00x: se nombra por su textura
    final = f"{nombre} - {base}"; n = 2
    while final in mapa: final = f"{nombre} - {base} {n}"; n += 1
    m.name = final
    mapa[m.name] = archivo

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
# 2) Pies en el suelo, centrado y a la altura pedida
scene = bpy.context.scene
dg = bpy.context.evaluated_depsgraph_get()
pts = []
for o in scene.objects:
    if o.type == 'MESH' and o.visible_get():
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        mw = o.matrix_world
        co = np.empty(len(me.vertices)*3, dtype=np.float32); me.vertices.foreach_get("co", co)
        co = co.reshape(-1,3); co = np.c_[co, np.ones(len(co))] @ np.array(mw).T
        pts.append(co[:,:3]); ev.to_mesh_clear()
pts = np.vstack(pts); mn = pts.min(0); mx = pts.max(0)
alto_original = mx[2]-mn[2]
k = altura/alto_original
centro = Vector(((mn[0]+mx[0])/2, (mn[1]+mx[1])/2, mn[2]))
M = Matrix.Scale(k,4) @ Matrix.Translation(-centro)
for o in scene.objects:
    if o.parent is None: o.matrix_world = M @ o.matrix_world
bpy.ops.object.select_all(action='DESELECT')
for o in scene.objects:
    if o.type in {'MESH','ARMATURE'}: o.select_set(True); bpy.context.view_layer.objects.active = o
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# 3) FBX
fbx = os.path.join(salida, nombre + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH','ARMATURE'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=False,
    mesh_smooth_type='FACE', add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
    path_mode='STRIP', embed_textures=False)
json.dump({"modelo": nombre, "altura": altura, "alto_original": float(alto_original),
           "tiene_esqueleto": any(o.type=='ARMATURE' for o in scene.objects), "materiales": mapa},
          open(os.path.join(salida, nombre + "_materiales.json"), "w"), indent=2, ensure_ascii=False)
print("LISTO", nombre, "alto original", round(alto_original,2), "escala", round(k,4), "materiales", mapa)
