#!/usr/bin/env python3
"""Copy in-game UI from CityScene to other map scenes."""

from __future__ import annotations

import re
import sys
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Scenes/CityScene.unity"
TARGETS = [
    ROOT / "Assets/Scenes/WesternScene.unity",
    ROOT / "Assets/Scenes/CityMapScene.unity",
]

UI_ROOT_NAMES = {"Canvas", "ChatManager", "EventSystem", "MinimapSystem"}
UI_REMOVE_PREFIXES = ("Canvas", "ChatManager", "EventSystem", "MinimapSystem")

BLOCK_HEADER = re.compile(r"^--- !u!(\d+) &(\d+)( stripped)?\n", re.MULTILINE)
FILEID_REF = re.compile(r"\{fileID: (\d+)")
GO_NAME = re.compile(r"^  m_Name: (.+)$", re.MULTILINE)
GO_ID_IN_COMPONENT = re.compile(r"^  m_GameObject: \{fileID: (\d+)\}", re.MULTILINE)
FATHER_ZERO = "m_Father: {fileID: 0}"
CHILDREN_LINE = re.compile(r"^  m_Children:\n((?:  - \{fileID: \d+\}\n)*)", re.MULTILINE)
COMPONENTS_LINE = re.compile(r"^  m_Component:\n((?:  - component: \{fileID: \d+\}\n)*)", re.MULTILINE)
COMP_REF = re.compile(r"component: \{fileID: (\d+)\}")
SCENE_ROOTS = re.compile(r"^SceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n((?:  - \{fileID: \d+\}\n)*)", re.MULTILINE)


class Block:
    def __init__(self, type_id: int, obj_id: int, text: str, stripped: bool = False):
        self.type_id = type_id
        self.obj_id = obj_id
        self.text = text
        self.stripped = stripped


def parse_scene(path: Path) -> tuple[str, dict[int, Block]]:
    content = path.read_text(encoding="utf-8")
    matches = list(BLOCK_HEADER.finditer(content))
    blocks: dict[int, Block] = {}

    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(content)
        type_id = int(match.group(1))
        obj_id = int(match.group(2))
        stripped = match.group(3) is not None
        blocks[obj_id] = Block(type_id, obj_id, content[start:end], stripped)

    return content, blocks


def block_refs(text: str) -> set[int]:
    return {int(value) for value in FILEID_REF.findall(text)}


def gameobject_name(blocks: dict[int, Block], go_id: int) -> str | None:
    block = blocks.get(go_id)
    if not block or block.type_id != 1:
        return None
    match = GO_NAME.search(block.text)
    return match.group(1).strip() if match else None


def gameobject_component_ids(blocks: dict[int, Block], go_id: int) -> list[int]:
    block = blocks.get(go_id)
    if not block:
        return []
    match = COMPONENTS_LINE.search(block.text)
    if not match:
        return []
    return [int(value) for value in COMP_REF.findall(match.group(1))]


def transform_children(blocks: dict[int, Block], transform_id: int) -> list[int]:
    block = blocks.get(transform_id)
    if not block:
        return []
    match = CHILDREN_LINE.search(block.text)
    if not match:
        return []
    return [int(value) for value in FILEID_REF.findall(match.group(1))]


def transform_gameobject_id(blocks: dict[int, Block], transform_id: int) -> int | None:
    block = blocks.get(transform_id)
    if not block:
        return None
    match = GO_ID_IN_COMPONENT.search(block.text)
    return int(match.group(1)) if match else None


def find_root_transforms(blocks: dict[int, Block]) -> dict[str, int]:
    roots: dict[str, int] = {}
    for obj_id, block in blocks.items():
        if block.type_id not in (4, 224):
            continue
        if FATHER_ZERO not in block.text:
            continue
        go_id = transform_gameobject_id(blocks, obj_id)
        if go_id is None:
            continue
        name = gameobject_name(blocks, go_id)
        if not name:
            continue
        if name in UI_ROOT_NAMES or should_remove_name(name):
            roots[name.split(" ")[0] if name.startswith("Canvas ") else name] = obj_id
            if name in UI_ROOT_NAMES:
                roots[name] = obj_id
    return roots


def should_remove_name(name: str) -> bool:
    for prefix in UI_REMOVE_PREFIXES:
        if name == prefix or name.startswith(prefix + " "):
            return True
    return False


def collect_prefab_instance(blocks: dict[int, Block], prefab_id: int) -> set[int]:
    collected = {prefab_id}
    if prefab_id not in blocks:
        return collected
    for ref in block_refs(blocks[prefab_id].text):
        if ref in blocks:
            collected.add(ref)
    return collected


def collect_gameobject_tree(blocks: dict[int, Block], root_transform_id: int) -> set[int]:
    collected: set[int] = set()
    queue: deque[int] = deque([root_transform_id])

    while queue:
        transform_id = queue.popleft()
        if transform_id in collected or transform_id not in blocks:
            continue
        collected.add(transform_id)

        go_id = transform_gameobject_id(blocks, transform_id)
        if go_id is not None and go_id not in collected:
            collected.add(go_id)
            for component_id in gameobject_component_ids(blocks, go_id):
                if component_id in blocks:
                    collected.add(component_id)

        for child_id in transform_children(blocks, transform_id):
            queue.append(child_id)

        for ref in block_refs(blocks[transform_id].text):
            if ref in blocks and ref not in collected:
                # Include UI components referenced by this transform subtree.
                ref_block = blocks[ref]
                if ref_block.type_id in (114, 222, 223, 224, 4, 1):
                    collected.add(ref)

    # Expand one more pass for nested references among collected UI objects.
    changed = True
    while changed:
        changed = False
        for obj_id in list(collected):
            for ref in block_refs(blocks[obj_id].text):
                if ref in blocks and ref not in collected:
                    collected.add(ref)
                    changed = True

    return collected


def collect_source_ui(blocks: dict[int, Block]) -> set[int]:
    roots = find_root_transforms(blocks)
    collected: set[int] = set()

    for name in ("Canvas", "ChatManager", "EventSystem"):
        transform_id = roots.get(name)
        if transform_id is not None:
            collected |= collect_gameobject_tree(blocks, transform_id)

    minimap_prefab = None
    for obj_id, block in blocks.items():
        if block.type_id == 1001 and "guid: 6b70cdce753374749bf430c0cccba3a6" in block.text:
            minimap_prefab = obj_id
            break
    if minimap_prefab is not None:
        collected |= collect_prefab_instance(blocks, minimap_prefab)

    return collected


def remove_target_ui(blocks: dict[int, Block]) -> set[int]:
    roots = find_root_transforms(blocks)
    removed: set[int] = set()

    for name, transform_id in list(roots.items()):
        go_id = transform_gameobject_id(blocks, transform_id)
        go_name = gameobject_name(blocks, go_id) if go_id else name
        if go_name and should_remove_name(go_name):
            removed |= collect_gameobject_tree(blocks, transform_id)

    for obj_id, block in blocks.items():
        if block.type_id == 1001 and "guid: 6b70cdce753374749bf430c0cccba3a6" in block.text:
            removed |= collect_prefab_instance(blocks, obj_id)

    return removed


def remap_blocks(blocks: dict[int, Block], ids: set[int], offset: int) -> tuple[dict[int, Block], dict[int, int]]:
    mapping = {old_id: old_id + offset for old_id in ids}
    cloned: dict[int, Block] = {}

    for old_id in ids:
        block = blocks[old_id]
        text = block.text
        for src, dst in sorted(mapping.items(), key=lambda item: item[0], reverse=True):
            text = text.replace(f"{{fileID: {src}}}", f"{{fileID: {dst}}}")
        text = text.replace(f"--- !u!{block.type_id} &{old_id}", f"--- !u!{block.type_id} &{mapping[old_id]}")
        cloned[mapping[old_id]] = Block(block.type_id, mapping[old_id], text, block.stripped)

    return cloned, mapping


def normalize_ui_text(text: str) -> str:
    replacements = [
        ("m_LocalScale: {x: 0.14093007, y: 0.37545922, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}"),
        ("m_LocalScale: {x: 0.14093, y: 0.37546, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}"),
        ("m_LocalScale: {x: 4.24, y: 1.55, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}"),
        ("m_LocalScale: {x: 5.28, y: 1.6, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}"),
    ]
    for old, new in replacements:
        text = text.replace(old, new)
    return text


def build_name_map(blocks: dict[int, Block], ids: set[int]) -> dict[str, int]:
    mapping: dict[str, int] = {}
    for obj_id in ids:
        block = blocks.get(obj_id)
        if not block or block.type_id != 1:
            continue
        name = gameobject_name(blocks, obj_id)
        if name:
            mapping[name] = obj_id
    return mapping


def find_component_for_name(blocks: dict[int, Block], ids: set[int], object_name: str, type_id: int = 114) -> int | None:
    for obj_id in ids:
        block = blocks.get(obj_id)
        if not block or block.type_id != type_id:
            continue
        go_match = GO_ID_IN_COMPONENT.search(block.text)
        if not go_match:
            continue
        go_id = int(go_match.group(1))
        if gameobject_name(blocks, go_id) == object_name:
            return obj_id
    return None


def patch_game_manager(blocks: dict[int, Block], ui_ids: set[int]) -> None:
    panel = find_component_for_name(blocks, ui_ids, "GameOverPanel")
    center = find_component_for_name(blocks, ui_ids, "CenterAnnounceText")
    timer = find_component_for_name(blocks, ui_ids, "TimerText")
    objective = find_component_for_name(blocks, ui_ids, "ObjectiveStatusText")

    for obj_id, block in blocks.items():
        if block.type_id != 114 or "Assembly-CSharp::GameManager" not in block.text:
            continue
        text = block.text
        if panel is not None:
            text = re.sub(r"  gameOverPanel: \{fileID: \d+\}", f"  gameOverPanel: {{fileID: {panel}}}", text)
        if center is not None:
            text = re.sub(r"  centerText: \{fileID: \d+\}", f"  centerText: {{fileID: {center}}}", text)
        if timer is not None:
            text = re.sub(r"  timerText: \{fileID: \d+\}", f"  timerText: {{fileID: {timer}}}", text)
        if objective is not None:
            text = re.sub(
                r"  objectiveStatusText: \{fileID: \d+\}",
                f"  objectiveStatusText: {{fileID: {objective}}}",
                text,
            )
        text = re.sub(r"  roleIndicatorText: \{fileID: \d+\}", "  roleIndicatorText: {fileID: 0}", text)
        blocks[obj_id] = Block(block.type_id, block.obj_id, text, block.stripped)


def patch_chat_manager(blocks: dict[int, Block], ui_ids: set[int]) -> None:
    chat_input = None
    chat_log = None
    for obj_id in ui_ids:
        block = blocks.get(obj_id)
        if not block or block.type_id != 114:
            continue
        if "TMP_InputField" in block.text:
            go_match = GO_ID_IN_COMPONENT.search(block.text)
            if go_match and gameobject_name(blocks, int(go_match.group(1))) == "ChatInputField":
                chat_input = obj_id
        if "TextMeshProUGUI" in block.text:
            go_match = GO_ID_IN_COMPONENT.search(block.text)
            if go_match and gameobject_name(blocks, int(go_match.group(1))) == "ChatLogText":
                chat_log = obj_id

    for obj_id, block in blocks.items():
        if block.type_id != 114 or "Assembly-CSharp::ChatManager" not in block.text:
            continue
        text = block.text
        if chat_input is not None:
            text = re.sub(r"  chatInput: \{fileID: \d+\}", f"  chatInput: {{fileID: {chat_input}}}", text)
        if chat_log is not None:
            text = re.sub(r"  chatLog: \{fileID: \d+\}", f"  chatLog: {{fileID: {chat_log}}}", text)
        text = re.sub(r"  isLobbyScene: \d+", "  isLobbyScene: 0", text)
        blocks[obj_id] = Block(block.type_id, block.obj_id, text, block.stripped)


def get_root_transform_ids(blocks: dict[int, Block], ids: set[int]) -> list[int]:
    root_ids: list[int] = []
    for obj_id in ids:
        block = blocks.get(obj_id)
        if not block or block.type_id not in (4, 224):
            continue
        if FATHER_ZERO in block.text:
            root_ids.append(obj_id)
    return root_ids


def rebuild_scene(original_content: str, blocks: dict[int, Block], removed: set[int], added: dict[int, Block], new_roots: list[int]) -> str:
    matches = list(BLOCK_HEADER.finditer(original_content))
    parts: list[str] = []
    last = 0

    for match in matches:
        start = match.start()
        parts.append(original_content[last:start])
        obj_id = int(match.group(2))
        if obj_id not in removed:
            parts.append(blocks[obj_id].text if obj_id in blocks else original_content[start:matches[matches.index(match) + 1].start() if matches.index(match) + 1 < len(matches) else len(original_content)])
        last = matches[matches.index(match) + 1].start() if matches.index(match) + 1 < len(matches) else len(original_content)

    # Fallback: rebuild by filtering blocks in order.
    kept_text = []
    for match in matches:
        obj_id = int(match.group(2))
        if obj_id in removed:
            continue
        kept_text.append(blocks[obj_id].text)

    insert_text = "".join(block.text for block in added.values())
    merged = "".join(kept_text)

    roots_match = SCENE_ROOTS.search(merged)
    if roots_match is None:
        raise RuntimeError("SceneRoots section not found")

    existing_roots = [int(value) for value in FILEID_REF.findall(roots_match.group(1))]
    filtered_roots = [root for root in existing_roots if root not in removed]
    for root_id in new_roots:
        if root_id not in filtered_roots:
            filtered_roots.append(root_id)

    new_roots_text = "SceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n"
    for root_id in filtered_roots:
        new_roots_text += f"  - {{fileID: {root_id}}}\n"

    merged = merged[: roots_match.start()] + new_roots_text + insert_text
    return merged


def sync_target(source_blocks: dict[int, Block], target_path: Path, offset: int) -> None:
    _, target_blocks = parse_scene(target_path)
    source_ui = collect_source_ui(source_blocks)
    removed = remove_target_ui(target_blocks)

    cloned, _ = remap_blocks(source_blocks, source_ui, offset)
    for obj_id, block in cloned.items():
        cloned[obj_id] = Block(block.type_id, block.obj_id, normalize_ui_text(block.text), block.stripped)

    merged_blocks = {obj_id: block for obj_id, block in target_blocks.items() if obj_id not in removed}
    merged_blocks.update(cloned)

    ui_ids = set(cloned.keys())
    patch_game_manager(merged_blocks, ui_ids)
    patch_chat_manager(merged_blocks, ui_ids)

    original_content, _ = parse_scene(target_path)
    new_roots = get_root_transform_ids(cloned, set(cloned.keys()))
    output = rebuild_scene_simple(original_content, target_blocks, merged_blocks, removed, cloned, new_roots)
    target_path.write_text(output, encoding="utf-8")
    print(f"Synced {target_path.name}: removed {len(removed)} blocks, added {len(cloned)} blocks")


def rebuild_scene_simple(
    original_content: str,
    original_blocks: dict[int, Block],
    merged_blocks: dict[int, Block],
    removed: set[int],
    added: dict[int, Block],
    new_roots: list[int],
) -> str:
    matches = list(BLOCK_HEADER.finditer(original_content))
    chunks: list[str] = []

    for index, match in enumerate(matches):
        obj_id = int(match.group(2))
        if obj_id in removed:
            continue
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(original_content)
        if obj_id in merged_blocks:
            chunks.append(merged_blocks[obj_id].text)
        else:
            chunks.append(original_content[start:end])

    merged = "".join(chunks)
    roots_match = SCENE_ROOTS.search(merged)
    if roots_match is None:
        raise RuntimeError("SceneRoots section not found")

    existing_roots = [int(value) for value in FILEID_REF.findall(roots_match.group(1))]
    filtered_roots = [root for root in existing_roots if root not in removed]
    for root_id in new_roots:
        if root_id not in filtered_roots:
            filtered_roots.append(root_id)

    new_roots_text = "SceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n"
    for root_id in filtered_roots:
        new_roots_text += f"  - {{fileID: {root_id}}}\n"

    insert_text = "".join(block.text for _, block in sorted(added.items()))
    return merged[: roots_match.start()] + new_roots_text + insert_text


def main() -> int:
    if not SOURCE.exists():
        print(f"Missing source scene: {SOURCE}")
        return 1

    _, source_blocks = parse_scene(SOURCE)
    source_ui = collect_source_ui(source_blocks)
    print(f"CityScene UI blocks: {len(source_ui)}")

    for index, target in enumerate(TARGETS):
        if not target.exists():
            print(f"Skip missing target: {target}")
            continue
        sync_target(source_blocks, target, offset=2_000_000_000 + index * 10_000_000)

    return 0


if __name__ == "__main__":
    sys.exit(main())
