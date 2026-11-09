"""Convert the supplied buildings using Blender, without requiring 3ds Max at runtime.

blender -b --python Tools/ConvertirModelosEpocas.py -- --rv MODEL.obj
    --max MODEL.max --max-importer PATH_TO_io_scene_max --output ASSET_FOLDER
The optional MAX importer is https://github.com/nrgsille76/io_scene_max (1.9.2).
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys

import bpy
from bpy_extras.node_shader_utils import PrincipledBSDFWrapper
from mathutils import Matrix, Vector


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def export(epoch, source, destination, y_up=False):
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    objects = [o.evaluated_get(graph) for o in sorted(bpy.context.scene.objects, key=lambda o: o.name) if o.type == "MESH"]
    # Read evaluated vertices and transforms together: the MAX importer leaves some matrices pending evaluation.
    corners = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
    low = Vector([min(v[i] for v in corners) for i in range(3)])
    high = Vector([max(v[i] for v in corners) for i in range(3)])
    size = high - low
    centre = (high + low) * 0.5
    # This MAX stores its building with Y up. OBJ import converts to Blender Z up. Both become Unity Y up.
    # Each mapping changes handedness; reverse triangle winding below.
    axis = Matrix(((1, 0, 0), (0, 1, 0), (0, 0, -1))) if y_up else Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))
    normalize = axis @ Matrix.Diagonal(Vector([1 / size[i] for i in range(3)]))
    folder = destination / epoch
    folder.mkdir(parents=True, exist_ok=True)
    materials, submeshes, material_ids = [], [], {}
    vertices, normals, uvs, lookup = [], [], [], {}

    def material_id(material):
        name = material.name if material else "Default"
        if name in material_ids:
            return material_ids[name]
        color, texture = [1, 1, 1, 1], ""
        if material:
            wrapper = PrincipledBSDFWrapper(material, is_readonly=True)
            color = list(wrapper.base_color[:3]) + [1]
            image = wrapper.base_color_texture.image if wrapper.base_color_texture else None
            if image:
                path = Path(bpy.path.abspath(image.filepath))
                if not path.is_file():
                    raise RuntimeError(f"Missing diffuse texture: {path}")
                texture = "Texturas/" + path.name
                (folder / "Texturas").mkdir(exist_ok=True)
                shutil.copyfile(path, folder / texture)
                color = [1, 1, 1, 1]
        index = len(materials)
        material_ids[name] = index
        materials.append(dict(name=name, color=color, texture=texture))
        submeshes.append(dict(triangles=[]))
        return index

    for obj in objects:
        mesh = obj.to_mesh()
        mesh.calc_loop_triangles()
        transform = normalize @ obj.matrix_world.to_3x3()
        normal_transform = transform.inverted().transposed()
        uv_layer = mesh.uv_layers.active
        for tri in mesh.loop_triangles:
            material = obj.data.materials[tri.material_index] if obj.data.materials else None
            slot = material_id(material)
            indices = []
            face_normal = (normal_transform @ tri.normal).normalized()
            for loop_index in tri.loops:
                loop = mesh.loops[loop_index]
                point = normalize @ (obj.matrix_world @ mesh.vertices[loop.vertex_index].co - centre)
                normal = (normal_transform @ mesh.corner_normals[loop_index].vector).normalized()
                # A few decimated railing corners retain an inward smoothing normal from the source mesh.
                if normal.dot(face_normal) < 0:
                    normal = face_normal
                uv = uv_layer.data[loop_index].uv if uv_layer else (0, 0)
                key = tuple(round(v, 7) for group in (point, normal, uv) for v in group)
                if key not in lookup:
                    lookup[key] = len(vertices) // 3
                    vertices.extend(key[:3])
                    normals.extend(key[3:6])
                    uvs.extend(key[6:8])
                indices.append(lookup[key])
            submeshes[slot]["triangles"].extend((indices[0], indices[2], indices[1]))
        obj.to_mesh_clear()

    data = dict(source=source.name, sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                vertices=vertices, normals=normals, uv=uvs, submeshes=submeshes, materials=materials)
    (folder / "modelo.json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
    print(f"EPOCH {epoch}: {len(vertices)//3} vertices, {sum(len(s['triangles'])//3 for s in submeshes)} triangles, {len(materials)} materials")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rv", type=Path, required=True)
    parser.add_argument("--max", type=Path, required=True)
    parser.add_argument("--max-importer", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    sys.path.insert(0, str(args.max_importer.resolve()))
    from source import import_max

    class Reporter:
        def report(self, level, message):
            print(level, message)

    clear()
    import_max.load(Reporter(), bpy.context, filepath=str(args.max.resolve()), use_image_search=True)
    # The pavement and four very dense street lamps are surroundings; the app already supplies its own park.
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH" or obj.name not in {"structure", "fence"}:
            bpy.data.objects.remove(obj, do_unlink=True)
    if not bpy.data.objects.get("structure") or not bpy.data.objects.get("fence"):
        raise RuntimeError("The MAX importer did not recover the building structure and roof railing")
    fence = bpy.data.objects["fence"]
    modifier = fence.modifiers.new("Mobile roof railing", "DECIMATE")
    modifier.ratio = 0.065
    export("1950", args.max, args.output, y_up=True)

    clear()
    bpy.ops.wm.obj_import(filepath=str(args.rv.resolve()), forward_axis="NEGATIVE_Z", up_axis="Y")
    export("1980", args.rv, args.output)


if __name__ == "__main__":
    main()
