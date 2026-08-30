#!/usr/bin/env python3
"""Generates the placeholder prefabs for the three new devices and the node slot marker.

Written by hand because this repository is edited without a Unity Editor. Each prefab is the
minimum a device needs to exist in the scene: a root carrying the device script and a collider
so it can be clicked, plus one child cube so it is visible. Colours are applied at runtime by
each device's own Refresh(), so no new material assets are needed.

fileIDs are derived from md5 of a stable label, so re-running this produces byte identical
output and never disturbs references.
"""
import hashlib
import pathlib

REPO = pathlib.Path(__file__).resolve().parent.parent
CUBE_MESH = "{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}"
PLACEHOLDER_MAT = "{fileID: 2100000, guid: c587eed0a196d524ba0fc3eee5baf502, type: 2}"


def file_id(label):
    """A stable positive 63-bit id, the same shape Unity generates for prefab objects."""
    digest = hashlib.md5(label.encode()).hexdigest()
    return int(digest[:15], 16)


def script_guid(relative_path):
    meta = REPO / (relative_path + ".meta")
    for line in meta.read_text().splitlines():
        if line.startswith("guid:"):
            return line.split(":", 1)[1].strip()
    raise SystemExit("no guid in " + str(meta))


def game_object(fid, name, components, active=1):
    lines = [
        f"--- !u!1 &{fid}",
        "GameObject:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  serializedVersion: 6",
        "  m_Component:",
    ]
    lines += [f"  - component: {{fileID: {c}}}" for c in components]
    lines += [
        "  m_Layer: 0",
        f"  m_Name: {name}",
        "  m_TagString: Untagged",
        "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0",
        "  m_StaticEditorFlags: 0",
        f"  m_IsActive: {active}",
    ]
    return "\n".join(lines)


def transform(fid, go, children, father, pos=(0, 0, 0), scale=(1, 1, 1)):
    lines = [
        f"--- !u!4 &{fid}",
        "Transform:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {go}}}",
        "  serializedVersion: 2",
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
        f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}",
        f"  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}",
        "  m_ConstrainProportionsScale: 0",
        "  m_Children:" + (" []" if not children else ""),
    ]
    lines += [f"  - {{fileID: {c}}}" for c in children]
    lines += [
        f"  m_Father: {{fileID: {father}}}",
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
    ]
    return "\n".join(lines)


def mono_behaviour(fid, go, guid, class_name, fields):
    lines = [
        f"--- !u!114 &{fid}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {go}}}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}",
        "  m_Name: ",
        f"  m_EditorClassIdentifier: ParcelSort::ParcelSort.{class_name}",
    ]
    lines += [f"  {k}: {v}" for k, v in fields]
    return "\n".join(lines)


def box_collider(fid, go, size, center):
    return "\n".join([
        f"--- !u!65 &{fid}",
        "BoxCollider:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {go}}}",
        "  m_Material: {fileID: 0}",
        "  m_IncludeLayers:",
        "    serializedVersion: 2",
        "    m_Bits: 0",
        "  m_ExcludeLayers:",
        "    serializedVersion: 2",
        "    m_Bits: 0",
        "  m_LayerOverridePriority: 0",
        "  m_IsTrigger: 0",
        "  m_ProvidesContacts: 0",
        "  m_Enabled: 1",
        "  serializedVersion: 3",
        f"  m_Size: {{x: {size[0]}, y: {size[1]}, z: {size[2]}}}",
        f"  m_Center: {{x: {center[0]}, y: {center[1]}, z: {center[2]}}}",
    ])


def mesh_filter(fid, go):
    return "\n".join([
        f"--- !u!33 &{fid}",
        "MeshFilter:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {go}}}",
        f"  m_Mesh: {CUBE_MESH}",
    ])


def mesh_renderer(fid, go):
    return "\n".join([
        f"--- !u!23 &{fid}",
        "MeshRenderer:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {go}}}",
        "  m_Enabled: 1",
        "  m_CastShadows: 1",
        "  m_ReceiveShadows: 1",
        "  m_DynamicOccludee: 1",
        "  m_StaticShadowCaster: 0",
        "  m_MotionVectors: 1",
        "  m_LightProbeUsage: 1",
        "  m_ReflectionProbeUsage: 1",
        "  m_RayTracingMode: 2",
        "  m_RayTraceProcedural: 0",
        "  m_RayTracingAccelStructBuildFlagsOverride: 0",
        "  m_RayTracingAccelStructBuildFlags: 1",
        "  m_SmallMeshCulling: 1",
        "  m_ForceMeshLod: -1",
        "  m_MeshLodSelectionBias: 0",
        "  m_RenderingLayerMask: 1",
        "  m_RendererPriority: 0",
        "  m_Materials:",
        f"  - {PLACEHOLDER_MAT}",
        "  m_StaticBatchInfo:",
        "    firstSubMesh: 0",
        "    subMeshCount: 0",
        "  m_StaticBatchRoot: {fileID: 0}",
        "  m_ProbeAnchor: {fileID: 0}",
        "  m_LightProbeVolumeOverride: {fileID: 0}",
        "  m_ScaleInLightmap: 1",
        "  m_ReceiveGI: 1",
        "  m_PreserveUVs: 1",
        "  m_IgnoreNormalsForChartDetection: 0",
        "  m_ImportantGI: 0",
        "  m_StitchLightmapSeams: 1",
        "  m_SelectedEditorRenderState: 3",
        "  m_MinimumChartSize: 4",
        "  m_AutoUVMaxDistance: 0.5",
        "  m_AutoUVMaxAngle: 89",
        "  m_LightmapParameters: {fileID: 0}",
        "  m_GlobalIlluminationMeshLod: 0",
        "  m_SortingLayerID: 0",
        "  m_SortingLayer: 0",
        "  m_SortingOrder: 0",
        "  m_MaskInteraction: 0",
        "  m_AdditionalVertexStreams: {fileID: 0}",
    ])


def build(name, script_path, class_name, fields, body_parts, collider):
    """One root with the device script, a collider, and one or more visual child cubes."""
    guid = script_guid(script_path)
    root_go = file_id(name + "/root/go")
    root_tr = file_id(name + "/root/tr")
    root_mb = file_id(name + "/root/mb")
    root_bc = file_id(name + "/root/bc")

    child_transforms = []
    blocks = []
    for label, pos, scale in body_parts:
        c_go = file_id(f"{name}/{label}/go")
        c_tr = file_id(f"{name}/{label}/tr")
        c_mf = file_id(f"{name}/{label}/mf")
        c_mr = file_id(f"{name}/{label}/mr")
        child_transforms.append(c_tr)
        blocks.append(game_object(c_go, label, [c_tr, c_mf, c_mr]))
        blocks.append(transform(c_tr, c_go, [], root_tr, pos, scale))
        blocks.append(mesh_filter(c_mf, c_go))
        blocks.append(mesh_renderer(c_mr, c_go))

    doc = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]
    doc.append(game_object(root_go, name, [root_tr, root_mb, root_bc]))
    doc.append(transform(root_tr, root_go, child_transforms, 0))
    doc.append(mono_behaviour(root_mb, root_go, guid, class_name, fields))
    doc.append(box_collider(root_bc, root_go, collider[0], collider[1]))
    doc += blocks

    path = REPO / "Assets/Prefabs/Equipment" / (name + ".prefab")
    path.write_text("\n".join(doc) + "\n")
    print("  wrote", path.relative_to(REPO), "root fileID", root_go)
    return root_go


def main():
    ids = {}
    ids["booster"] = build(
        "BoosterDevice", "Assets/Scripts/Equipment/BoosterDevice.cs", "BoosterDevice",
        [("bodyRenderer", "{fileID: 0}")],
        # A low wedge sitting on the deck: it speeds the belt, it does not block it.
        [("Body", (0, 0.16, 0), (0.9, 0.12, 0.5)),
         ("Fin", (0, 0.3, 0), (0.28, 0.3, 0.28))],
        ((1.0, 0.6, 0.6), (0, 0.25, 0)))

    ids["scanner"] = build(
        "ScannerDevice", "Assets/Scripts/Equipment/ScannerDevice.cs", "ScannerDevice",
        [("bodyRenderer", "{fileID: 0}")],
        # An arch over the belt, so it reads as something parcels pass through.
        [("PostL", (-0.42, 0.34, 0), (0.12, 0.68, 0.12)),
         ("PostR", (0.42, 0.34, 0), (0.12, 0.68, 0.12)),
         ("Head", (0, 0.7, 0), (1.0, 0.16, 0.3))],
        ((1.0, 1.0, 0.5), (0, 0.45, 0)))

    ids["autoarm"] = build(
        "AutoArmDevice", "Assets/Scripts/Equipment/AutoArmDevice.cs", "AutoArmDevice",
        [("bodyRenderer", "{fileID: 0}")],
        # A pillar with a swinging arm, mounted on the diverter itself.
        [("Column", (0, 0.3, 0), (0.3, 0.6, 0.3)),
         ("Arm", (0.24, 0.6, 0), (0.62, 0.12, 0.16))],
        ((0.9, 1.0, 0.6), (0, 0.4, 0)))

    ids["nodeslot"] = build(
        "NodeSlotMarker", "Assets/Scripts/Equipment/NodeDeviceSlot.cs", "NodeDeviceSlot",
        [],
        [("Ring", (0, 0.02, 0), (0.9, 0.06, 0.9)),
         ("Cap", (0, 0.2, 0), (0.3, 0.3, 0.3))],
        ((0.95, 0.7, 0.95), (0, 0.2, 0)))

    print()
    print("root fileIDs for ModuleCatalog wiring:")
    for key, value in ids.items():
        print(f"  {key}: {value}")


if __name__ == "__main__":
    main()
