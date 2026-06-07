import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const SOURCE = path.join(ROOT, "Assets/Scenes/CityScene.unity");
const TARGETS = [
  path.join(ROOT, "Assets/Scenes/WesternScene.unity"),
  path.join(ROOT, "Assets/Scenes/CityMapScene.unity"),
];

const UI_ROOT_NAMES = new Set(["Canvas", "ChatManager", "EventSystem", "MinimapSystem"]);

const BLOCK_HEADER = /^--- !u!(\d+) &(\d+)( stripped)?\r?\n/gm;
const FILEID_REF = /\{fileID: (\d+)/g;
const GO_NAME = /^  m_Name: (.+)$/m;
const GO_ID_IN_COMPONENT = /^  m_GameObject: \{fileID: (\d+)\}/m;
const FATHER_ZERO = "m_Father: {fileID: 0}";
const CHILDREN_LINE = /^  m_Children:\r?\n((?:  - \{fileID: \d+\}\r?\n)*)/m;
const COMPONENTS_LINE = /^  m_Component:\r?\n((?:  - component: \{fileID: \d+\}\r?\n)*)/m;
const COMP_REF = /component: \{fileID: (\d+)\}/g;

function compareIdDesc(a, b) {
  if (a.length !== b.length) return b.length - a.length;
  return a < b ? 1 : a > b ? -1 : 0;
}

function remapId(oldId, offset) {
  return (BigInt(oldId) + BigInt(offset)).toString();
}

function parseScene(filePath) {
  const content = fs.readFileSync(filePath, "utf8");
  const blocks = new Map();
  const matches = [...content.matchAll(BLOCK_HEADER)];

  for (let i = 0; i < matches.length; i++) {
    const match = matches[i];
    const start = match.index;
    const end = i + 1 < matches.length ? matches[i + 1].index : content.length;
    const objId = match[2];
    blocks.set(objId, {
      typeId: Number(match[1]),
      objId,
      text: content.slice(start, end),
      stripped: Boolean(match[3]),
    });
  }

  return { content, blocks };
}

function blockRefs(text) {
  const refs = new Set();
  for (const match of text.matchAll(FILEID_REF)) refs.add(match[1]);
  return refs;
}

function gameObjectName(blocks, goId) {
  const block = blocks.get(goId);
  if (!block || block.typeId !== 1) return null;
  const match = block.text.match(GO_NAME);
  return match ? match[1].trim() : null;
}

function gameObjectComponentIds(blocks, goId) {
  const block = blocks.get(goId);
  if (!block) return [];
  const match = block.text.match(COMPONENTS_LINE);
  if (!match) return [];
  return [...match[1].matchAll(COMP_REF)].map((m) => m[1]);
}

function transformChildren(blocks, transformId) {
  const block = blocks.get(transformId);
  if (!block) return [];
  const match = block.text.match(CHILDREN_LINE);
  if (!match) return [];
  return [...match[1].matchAll(FILEID_REF)].map((m) => m[1]);
}

function transformGameObjectId(blocks, transformId) {
  const block = blocks.get(transformId);
  if (!block) return null;
  const match = block.text.match(GO_ID_IN_COMPONENT);
  return match ? match[1] : null;
}

function shouldRemoveName(name) {
  if (name === "Canvas" || name.startsWith("Canvas ")) return true;
  if (name === "ChatManager" || name.startsWith("ChatManager ")) return true;
  if (name === "EventSystem" || name.startsWith("EventSystem ")) return true;
  if (name === "MinimapSystem") return true;
  return false;
}

function findRootTransforms(blocks) {
  const roots = new Map();
  for (const [objId, block] of blocks) {
    if (![4, 224].includes(block.typeId) || !block.text.includes(FATHER_ZERO)) continue;
    const goId = transformGameObjectId(blocks, objId);
    if (goId == null) continue;
    const name = gameObjectName(blocks, goId);
    if (!name) continue;
    if (UI_ROOT_NAMES.has(name) || shouldRemoveName(name)) roots.set(name, objId);
  }
  return roots;
}

function collectPrefabInstance(blocks, prefabId) {
  const collected = new Set([prefabId]);
  const block = blocks.get(prefabId);
  if (!block) return collected;
  for (const ref of blockRefs(block.text)) {
    if (blocks.has(ref)) collected.add(ref);
  }
  return collected;
}

function collectGameObjectTree(blocks, rootTransformId) {
  const collected = new Set();
  const queue = [rootTransformId];

  while (queue.length) {
    const transformId = queue.shift();
    if (collected.has(transformId) || !blocks.has(transformId)) continue;
    collected.add(transformId);

    const goId = transformGameObjectId(blocks, transformId);
    if (goId != null && !collected.has(goId)) {
      collected.add(goId);
      for (const componentId of gameObjectComponentIds(blocks, goId)) {
        if (blocks.has(componentId)) collected.add(componentId);
      }
    }

    for (const childId of transformChildren(blocks, transformId)) queue.push(childId);
  }

  return collected;
}

function collectSourceUi(blocks) {
  const roots = findRootTransforms(blocks);
  const collected = new Set();

  for (const name of ["Canvas", "ChatManager", "EventSystem"]) {
    const transformId = roots.get(name);
    if (transformId != null) {
      for (const id of collectGameObjectTree(blocks, transformId)) collected.add(id);
    }
  }

  for (const [objId, block] of blocks) {
    if (block.typeId === 1001 && block.text.includes("guid: 6b70cdce753374749bf430c0cccba3a6")) {
      for (const id of collectPrefabInstance(blocks, objId)) collected.add(id);
    }
  }

  return collected;
}

function removeTargetUi(blocks) {
  const roots = findRootTransforms(blocks);
  const removed = new Set();

  for (const [name, transformId] of roots) {
    const goId = transformGameObjectId(blocks, transformId);
    const goName = goId != null ? gameObjectName(blocks, goId) : name;
    if (goName && shouldRemoveName(goName)) {
      for (const id of collectGameObjectTree(blocks, transformId)) removed.add(id);
    }
  }

  for (const [objId, block] of blocks) {
    if (block.typeId === 1001 && block.text.includes("guid: 6b70cdce753374749bf430c0cccba3a6")) {
      for (const id of collectPrefabInstance(blocks, objId)) removed.add(id);
    }
  }

  return removed;
}

function remapBlocks(blocks, ids, offset) {
  const mapping = new Map([...ids].map((oldId) => [oldId, remapId(oldId, offset)]));
  const cloned = new Map();

  for (const oldId of ids) {
    const block = blocks.get(oldId);
    let text = block.text;
    const sorted = [...mapping.entries()].sort((a, b) => compareIdDesc(a[0], b[0]));
    for (const [src, dst] of sorted) {
      text = text.replaceAll(`{fileID: ${src}}`, `{fileID: ${dst}}`);
    }
    text = text.replace(`--- !u!${block.typeId} &${oldId}`, `--- !u!${block.typeId} &${mapping.get(oldId)}`);
    const newId = mapping.get(oldId);
    cloned.set(newId, { ...block, objId: newId, text });
  }

  return cloned;
}

function normalizeUiText(block) {
  let text = block.text
    .replaceAll("m_LocalScale: {x: 0.14093007, y: 0.37545922, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}")
    .replaceAll("m_LocalScale: {x: 0.14093, y: 0.37546, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}")
    .replaceAll("m_LocalScale: {x: 4.24, y: 1.55, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}")
    .replaceAll("m_LocalScale: {x: 5.28, y: 1.6, z: 1}", "m_LocalScale: {x: 1, y: 1, z: 1}");

  if (block.typeId === 1 && text.includes("m_Name: CornerRoleText")) {
    text = text.replace(/m_IsActive: 1/, "m_IsActive: 0");
  }

  return text;
}

function findComponentForName(blocks, ids, objectName) {
  for (const objId of ids) {
    const block = blocks.get(objId);
    if (!block || block.typeId !== 114) continue;
    const goMatch = block.text.match(GO_ID_IN_COMPONENT);
    if (!goMatch) continue;
    if (gameObjectName(blocks, goMatch[1]) === objectName) return objId;
  }
  return null;
}

function findGameObjectId(blocks, ids, objectName) {
  for (const objId of ids) {
    if (blocks.get(objId)?.typeId === 1 && gameObjectName(blocks, objId) === objectName) return objId;
  }
  return null;
}

function findGameManagerComponentId(blocks) {
  for (const [objId, block] of blocks) {
    if (block.typeId === 114 && block.text.includes("Assembly-CSharp::GameManager")) {
      return objId;
    }
  }
  return null;
}

function patchGameManager(blocks, uiIds) {
  const panel = findGameObjectId(blocks, uiIds, "GameOverPanel");
  const center = findComponentForName(blocks, uiIds, "CenterAnnounceText");
  const timer = findComponentForName(blocks, uiIds, "TimerText");
  const objective = findComponentForName(blocks, uiIds, "ObjectiveStatusText");

  for (const [objId, block] of blocks) {
    if (block.typeId !== 114 || !block.text.includes("Assembly-CSharp::GameManager")) continue;
    let text = block.text;
    if (panel != null) text = text.replace(/  gameOverPanel: \{fileID: \d+\}/, `  gameOverPanel: {fileID: ${panel}}`);
    if (center != null) text = text.replace(/  centerText: \{fileID: \d+\}/, `  centerText: {fileID: ${center}}`);
    if (timer != null) text = text.replace(/  timerText: \{fileID: \d+\}/, `  timerText: {fileID: ${timer}}`);
    if (objective != null) {
      text = text.replace(/  objectiveStatusText: \{fileID: \d+\}/, `  objectiveStatusText: {fileID: ${objective}}`);
    }
    text = text.replace(/  roleIndicatorText: \{fileID: \d+\}/, "  roleIndicatorText: {fileID: 0}");
    blocks.set(objId, { ...block, text });
  }
}

function patchExitButton(blocks, uiIds, gameManagerId) {
  if (gameManagerId == null) return;

  for (const objId of uiIds) {
    const block = blocks.get(objId);
    if (!block || block.typeId !== 114 || !block.text.includes("UnityEngine.UI::UnityEngine.UI.Button")) {
      continue;
    }

    const goMatch = block.text.match(GO_ID_IN_COMPONENT);
    if (!goMatch || gameObjectName(blocks, goMatch[1]) !== "ExitButton") continue;

    let text = block.text;
    if (!text.includes("OnClickExit")) continue;

    text = text.replace(
      /- m_Target: \{fileID: \d+\}\r?\n        m_TargetAssemblyTypeName: GameManager, Assembly-CSharp\r?\n        m_MethodName: OnClickExit/,
      `- m_Target: {fileID: ${gameManagerId}}\n        m_TargetAssemblyTypeName: GameManager, Assembly-CSharp\n        m_MethodName: OnClickExit`
    );
    blocks.set(objId, { ...block, text });
  }
}

function patchChatManager(blocks, uiIds) {
  let chatInput = null;
  let chatLog = null;

  for (const objId of uiIds) {
    const block = blocks.get(objId);
    if (!block || block.typeId !== 114) continue;
    const goMatch = block.text.match(GO_ID_IN_COMPONENT);
    if (!goMatch) continue;
    const goName = gameObjectName(blocks, goMatch[1]);
    if (goName === "ChatInputField" && block.text.includes("TMP_InputField")) chatInput = objId;
    if (goName === "ChatLogText" && block.text.includes("TextMeshProUGUI")) chatLog = objId;
  }

  for (const [objId, block] of blocks) {
    if (block.typeId !== 114 || !block.text.includes("Assembly-CSharp::ChatManager")) continue;
    let text = block.text;
    if (chatInput != null) text = text.replace(/  chatInput: \{fileID: \d+\}/, `  chatInput: {fileID: ${chatInput}}`);
    if (chatLog != null) text = text.replace(/  chatLog: \{fileID: \d+\}/, `  chatLog: {fileID: ${chatLog}}`);
    text = text.replace(/  isLobbyScene: \d+/, "  isLobbyScene: 0");
    blocks.set(objId, { ...block, text });
  }
}

function getRootTransformIds(blocks, ids) {
  const roots = [];
  for (const objId of ids) {
    const block = blocks.get(objId);
    if (!block || ![4, 224].includes(block.typeId)) continue;
    if (block.text.includes(FATHER_ZERO)) roots.push(objId);
  }
  return roots;
}

function rebuildScene(originalContent, mergedBlocks, removed, added, newRoots) {
  const rootsMarker = originalContent.lastIndexOf("--- !u!1660057539 &9223372036854775807");
  if (rootsMarker === -1) throw new Error("SceneRoots section not found");

  const matches = [...originalContent.matchAll(BLOCK_HEADER)];
  const preamble = matches.length > 0 ? originalContent.slice(0, matches[0].index) : "";
  const chunks = [];

  for (let i = 0; i < matches.length; i++) {
    if (matches[i].index >= rootsMarker) break;
    const objId = matches[i][2];
    if (removed.has(objId)) continue;
    if (mergedBlocks.has(objId)) {
      chunks.push(mergedBlocks.get(objId).text);
      continue;
    }

    const start = matches[i].index;
    const end = i + 1 < matches.length ? matches[i + 1].index : rootsMarker;
    chunks.push(originalContent.slice(start, end));
  }

  const merged = chunks.join("");
  const originalRootsSection = originalContent.slice(rootsMarker);
  const existingRoots = [...originalRootsSection.matchAll(FILEID_REF)].map((m) => m[1]);
  const filteredRoots = existingRoots.filter((root) => !removed.has(root));
  for (const rootId of newRoots) {
    if (!filteredRoots.includes(rootId)) filteredRoots.push(rootId);
  }

  const nl = originalContent.includes("\r\n") ? "\r\n" : "\n";
  const insertText = [...added.values()]
    .sort((a, b) => compareIdDesc(a.objId, b.objId))
    .map((b) => b.text)
    .join("");
  const rootsHeader = `--- !u!1660057539 &9223372036854775807${nl}`;
  const newRootsText =
    `SceneRoots:${nl}  m_ObjectHideFlags: 0${nl}  m_Roots:${nl}` +
    filteredRoots.map((rootId) => `  - {fileID: ${rootId}}${nl}`).join("");

  return preamble + merged + insertText + rootsHeader + newRootsText;
}

const YAML_PREAMBLE = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

function postProcessScene(content) {
  let text = content.replace(/\r\n/g, "\n").replace(
    /m_LightingSettings: \{fileID: \d+, guid: [0-9a-f]+, type: 2\}/g,
    "m_LightingSettings: {fileID: 0}"
  );

  if (!text.startsWith("%YAML 1.1")) {
    text = YAML_PREAMBLE + text.replace(/^\n+/, "");
  }

  return text;
}

function validateScene(filePath) {
  const content = fs.readFileSync(filePath, "utf8");
  const ids = [];
  for (const match of content.matchAll(/^--- !u!\d+ &(\d+)/gm)) ids.push(match[1]);

  const seen = new Map();
  const dups = [];
  for (const id of ids) {
    const count = (seen.get(id) || 0) + 1;
    seen.set(id, count);
    if (count === 2) dups.push(id);
  }

  if (!content.startsWith("%YAML 1.1")) {
    throw new Error(`${path.basename(filePath)}: YAML header missing`);
  }

  if (!content.includes("--- !u!1660057539 &9223372036854775807")) {
    throw new Error(`${path.basename(filePath)}: SceneRoots header missing`);
  }

  const rootsIndex = content.lastIndexOf("--- !u!1660057539 &9223372036854775807");
  const afterRoots = content.slice(rootsIndex + "--- !u!1660057539 &9223372036854775807".length).trim();
  if (!afterRoots.startsWith("SceneRoots:")) {
    throw new Error(`${path.basename(filePath)}: SceneRoots section malformed`);
  }

  if (content.slice(rootsIndex).includes("--- !u!")) {
    const trailing = content.slice(content.indexOf("SceneRoots:") + content.slice(rootsIndex).split("SceneRoots:")[1].length);
    if (trailing.trim().length > 0 && trailing.includes("--- !u!")) {
      throw new Error(`${path.basename(filePath)}: content found after SceneRoots`);
    }
  }

  if (dups.length > 0) {
    throw new Error(`${path.basename(filePath)}: ${dups.length} duplicate object IDs (e.g. ${dups.slice(0, 3).join(", ")})`);
  }

  return { blocks: ids.length, rootsAtEnd: content.trimEnd().endsWith("}") || content.trimEnd().match(/\{fileID: \d+\}$/) };
}

function syncTarget(sourceBlocks, targetPath, offset) {
  const { content, blocks: targetBlocks } = parseScene(targetPath);
  const removed = removeTargetUi(targetBlocks);
  const cloned = remapBlocks(sourceBlocks, collectSourceUi(sourceBlocks), offset);

  for (const [objId, block] of cloned) {
    cloned.set(objId, { ...block, text: normalizeUiText(block) });
  }

  const mergedBlocks = new Map();
  for (const [objId, block] of targetBlocks) {
    if (!removed.has(objId)) mergedBlocks.set(objId, block);
  }
  for (const [objId, block] of cloned) mergedBlocks.set(objId, block);

  const clonedUiIds = new Set(cloned.keys());
  const gameManagerId = findGameManagerComponentId(mergedBlocks);
  patchGameManager(mergedBlocks, clonedUiIds);
  patchChatManager(mergedBlocks, clonedUiIds);
  patchExitButton(mergedBlocks, clonedUiIds, gameManagerId);

  const newRoots = getRootTransformIds(cloned, clonedUiIds);
  let output = rebuildScene(content, mergedBlocks, removed, cloned, newRoots);
  output = postProcessScene(output);
  fs.writeFileSync(targetPath, output, "utf8");

  const stats = validateScene(targetPath);
  console.log(`Synced ${path.basename(targetPath)}: removed ${removed.size}, added ${cloned.size}, blocks ${stats.blocks}`);
}

const { blocks: sourceBlocks } = parseScene(SOURCE);
console.log(`CityScene UI blocks: ${collectSourceUi(sourceBlocks).size}`);

for (let i = 0; i < TARGETS.length; i++) {
  const target = TARGETS[i];
  if (!fs.existsSync(target)) {
    console.log(`Skip missing target: ${target}`);
    continue;
  }
  syncTarget(sourceBlocks, target, 2_000_000_000 + i * 10_000_000);
}
