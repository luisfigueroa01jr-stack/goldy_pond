# Exporta el .blend abierto a FBX para Unity sin modificar el archivo original.
# Uso: blender -b archivo.blend --python exportar_fbx.py -- salida.fbx paleta.json
import bpy, sys, json, os
args = sys.argv[sys.argv.index("--") + 1:]
out_fbx, out_json = args[0], args[1]
scene = bpy.context.scene
vl = bpy.context.view_layer

# 1) Colecciones visibles -> empties padre, para que Unity conserve la organizacion.
def visible_layer_colls(lc, parent_empty, out):
    for ch in lc.children:
        if ch.exclude or ch.collection.hide_render:
            continue
        out.append((ch.collection, parent_empty))
        visible_layer_colls(ch, ch.collection.name, out)
pairs = []
visible_layer_colls(vl.layer_collection, None, pairs)
# Si todo vive en una sola coleccion (objetos sueltos), la raiz lleva el nombre del archivo.
root_name = os.path.splitext(os.path.basename(out_fbx))[0]
root = bpy.data.objects.new(root_name, None)
scene.collection.objects.link(root)
empties = {}
for coll, parent_name in pairs:
    e = bpy.data.objects.new(coll.name, None)
    scene.collection.objects.link(e)
    e.parent = empties.get(parent_name, root)
    empties[coll.name] = e
for o in list(scene.objects):
    if o is root or o in empties.values() or o.parent is not None:
        continue
    if o.type in {'CAMERA', 'LIGHT'}:
        continue
    owner = next((c.name for c in o.users_collection if c.name in empties), None)
    mw = o.matrix_world.copy()
    o.parent = empties.get(owner, root)
    o.matrix_world = mw
# Una sola coleccion de primer nivel en objetos pequenos: no hace falta el nivel extra.
if len(pairs) == 1:
    e = empties[pairs[0][0].name]
    for ch in list(e.children):
        mw = ch.matrix_world.copy(); ch.parent = root; ch.matrix_world = mw
    bpy.data.objects.remove(e)

# 2) Paleta de materiales (color lineal de Blender + rugosidad/metal).
mats = {}
for o in scene.objects:
    for s in getattr(o, "material_slots", []):
        m = s.material
        if not m or m.name in mats:
            continue
        col, rough, metal = list(m.diffuse_color), 0.5, 0.0
        if m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'BSDF_PRINCIPLED':
                    col = list(n.inputs['Base Color'].default_value)
                    rough = n.inputs['Roughness'].default_value
                    metal = n.inputs['Metallic'].default_value
                    break
        mats[m.name] = {"color_lineal": [round(c, 5) for c in col], "rugosidad": round(rough, 3), "metal": round(metal, 3)}
lista = {}
if os.path.exists(out_json):
    with open(out_json) as f:
        lista = {m["nombre"]: m for m in json.load(f).get("materiales", [])}
for k, v in mats.items():
    lista[k] = {"nombre": k, **v}
with open(out_json, "w") as f:
    json.dump({"materiales": [lista[k] for k in sorted(lista)]}, f, ensure_ascii=False, indent=2)

# 3) FBX a escala 1 = 1 m, en los ejes nativos de Blender (Z arriba). Unity, con Bake Axis Conversion,
#    convierte a Y arriba dentro de las mallas y deja todas las raices en rotacion (0,0,0).
#    No se usa 'Apply Transform' (bake_space_transform): gira mal los modelos con Empties padre.
os.makedirs(os.path.dirname(out_fbx), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=out_fbx, use_selection=False, use_visible=False,
    object_types={'EMPTY', 'MESH', 'OTHER'}, use_mesh_modifiers=True,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='Y', axis_up='Z', bake_space_transform=False,
    mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
    bake_anim=False, path_mode='AUTO', embed_textures=False)
n_mesh = sum(1 for o in scene.objects if o.type in {'MESH', 'CURVE', 'FONT'})
print(f"EXPORTADO {out_fbx} objetos={n_mesh} materiales={len(mats)}")
