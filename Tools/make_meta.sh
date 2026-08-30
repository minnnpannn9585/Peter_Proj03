#!/usr/bin/env bash
# Generates Unity .meta files for assets that do not have one yet.
#
# Unity normally writes these itself, but this repository is edited without an Editor, and a
# committed asset with no .meta gets a fresh random GUID on first import - which silently breaks
# every scene and prefab reference to it. Creating the .meta here keeps GUIDs stable from the
# first commit.
#
# GUIDs are derived from the asset path via md5, so re-running this is idempotent and two
# machines produce the same GUID for the same file.
set -euo pipefail
cd "$(dirname "$0")/.."

guid_for() {
  printf '%s' "$1" | md5sum | cut -c1-32
}

emit_script_meta() {
  cat >"$2" <<EOF
fileFormatVersion: 2
guid: $1
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
}

emit_asmdef_meta() {
  cat >"$2" <<EOF
fileFormatVersion: 2
guid: $1
AssemblyDefinitionImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
}

emit_folder_meta() {
  cat >"$2" <<EOF
fileFormatVersion: 2
guid: $1
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
}

emit_default_meta() {
  cat >"$2" <<EOF
fileFormatVersion: 2
guid: $1
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
}

emit_prefab_meta() {
  cat >"$2" <<EOF
fileFormatVersion: 2
guid: $1
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF
}

created=0

# Directories first, so a new folder is importable.
while IFS= read -r dir; do
  rel="${dir#./}"
  [ -f "${rel}.meta" ] && continue
  emit_folder_meta "$(guid_for "$rel")" "${rel}.meta"
  echo "  + ${rel}.meta"
  created=$((created + 1))
done < <(find ./Assets -type d -not -path './Assets' | sort)

while IFS= read -r file; do
  rel="${file#./}"
  [ -f "${rel}.meta" ] && continue
  guid="$(guid_for "$rel")"
  case "$rel" in
    *.cs)      emit_script_meta "$guid" "${rel}.meta" ;;
    *.asmdef)  emit_asmdef_meta "$guid" "${rel}.meta" ;;
    *.prefab)  emit_prefab_meta "$guid" "${rel}.meta" ;;
    *)         emit_default_meta "$guid" "${rel}.meta" ;;
  esac
  echo "  + ${rel}.meta"
  created=$((created + 1))
done < <(find ./Assets -type f -not -name '*.meta' | sort)

echo "make_meta: created ${created} meta file(s)"
