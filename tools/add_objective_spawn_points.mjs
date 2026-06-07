import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

const TARGETS = [
  { path: path.join(ROOT, "Assets/Scenes/WesternScene.unity"), idBase: 2_910_000_000 },
  { path: path.join(ROOT, "Assets/Scenes/CityMapScene.unity"), idBase: 2_920_000_000 },
];

const TERMINAL_PREFAB =
  "terminalPrefab: {fileID: 8610726216972713655, guid: a154ecb30c61b5149bfd721a74dda660, type: 3}";
const ESCAPE_PREFAB =
  "escapeZonePrefab: {fileID: 2526776303773404714, guid: 8b3053412105d85469aa11a702d1dd9a, type: 3}";

const TERMINAL_POSITIONS = [
  [-30, 0.1, -30], [-10, 0.1, -30], [10, 0.1, -30], [30, 0.1, -30],
  [-30, 0.1, -10], [-10, 0.1, -10], [10, 0.1, -10], [30, 0.1, -10],
  [-20, 0.1, 20], [20, 0.1, 20],
];

const ESCAPE_POSITIONS = [
  [-40, 0.1, 40], [0, 0.1, 50], [40, 0.1, 40],
];

function spawnPointYaml(goId, transId, name, position, parentTransId) {
  const [x, y, z] = position;
  return `--- !u!1 &${goId}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: ${transId}}
  m_Layer: 0
  m_Name: ${name}
  m_TagString: Untagged
  m_Icon: {fileID: 5132851093641282708, guid: 0000000000000000d000000000000000, type: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${transId}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${goId}}
  serializedVersion: 2
  m_LocalRotation: {x: -0, y: -0, z: -0, w: 1}
  m_LocalPosition: {x: ${x}, y: ${y}, z: ${z}}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: ${parentTransId}}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
`;
}

function buildSpawnHierarchy(idBase) {
  const rootGo = String(idBase + 1);
  const rootTrans = String(idBase + 2);
  const childTransIds = [];
  const blocks = [];

  for (let i = 0; i < TERMINAL_POSITIONS.length; i++) {
    const goId = String(idBase + 100 + i * 10 + 1);
    const transId = String(idBase + 100 + i * 10 + 2);
    childTransIds.push(transId);
    blocks.push(spawnPointYaml(goId, transId, `TerminalSpawn_${i}`, TERMINAL_POSITIONS[i], rootTrans));
  }

  for (let i = 0; i < ESCAPE_POSITIONS.length; i++) {
    const goId = String(idBase + 200 + i * 10 + 1);
    const transId = String(idBase + 200 + i * 10 + 2);
    childTransIds.push(transId);
    blocks.push(spawnPointYaml(goId, transId, `EscapeSpawn_${i}`, ESCAPE_POSITIONS[i], rootTrans));
  }

  const childrenLines = childTransIds.map((id) => `  - {fileID: ${id}}`).join("\n");

  const rootYaml = `--- !u!1 &${rootGo}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: ${rootTrans}}
  m_Layer: 0
  m_Name: ObjectiveSpawnPoints
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${rootTrans}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${rootGo}}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
${childrenLines}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
`;

  const terminalTransIds = childTransIds.slice(0, TERMINAL_POSITIONS.length);
  const escapeTransIds = childTransIds.slice(TERMINAL_POSITIONS.length);

  return {
    yaml: rootYaml + blocks.join(""),
    rootTrans,
    terminalTransIds,
    escapeTransIds,
  };
}

function patchGameManager(content, terminalTransIds, escapeTransIds) {
  let text = content;

  text = text.replace(/terminalPrefab: \{fileID: 0\}/, TERMINAL_PREFAB);
  text = text.replace(/escapeZonePrefab: \{fileID: 0\}/, ESCAPE_PREFAB);

  const terminalList = terminalTransIds.map((id) => `  - {fileID: ${id}}`).join("\n");
  const escapeList = escapeTransIds.map((id) => `  - {fileID: ${id}}`).join("\n");

  text = text.replace(/terminalSpawnPoints:\s*\[\s*\]/, `terminalSpawnPoints:\n${terminalList}`);
  text = text.replace(/escapeSpawnPoints:\s*\[\s*\]/, `escapeSpawnPoints:\n${escapeList}`);

  return text;
}

function addToSceneRoots(content, rootTransId) {
  const marker = "--- !u!1660057539 &9223372036854775807";
  const rootsIndex = content.lastIndexOf(marker);
  if (rootsIndex === -1) throw new Error("SceneRoots not found");

  const rootsSection = content.slice(rootsIndex);
  if (rootsSection.includes(`{fileID: ${rootTransId}}`)) return content;

  const insertBefore = rootsSection.indexOf("\n", rootsSection.indexOf("m_Roots:"));
  const line = `  - {fileID: ${rootTransId}}\n`;
  const updatedRoots = rootsSection.slice(0, insertBefore + 1) + line + rootsSection.slice(insertBefore + 1);
  return content.slice(0, rootsIndex) + updatedRoots;
}

function processScene(target) {
  let content = fs.readFileSync(target.path, "utf8");

  if (content.includes("m_Name: ObjectiveSpawnPoints")) {
    console.log(`Skip ${path.basename(target.path)}: ObjectiveSpawnPoints already exists`);
    return;
  }

  const { yaml, rootTrans, terminalTransIds, escapeTransIds } = buildSpawnHierarchy(target.idBase);
  content = patchGameManager(content, terminalTransIds, escapeTransIds);

  const marker = "--- !u!1660057539 &9223372036854775807";
  const rootsIndex = content.lastIndexOf(marker);
  if (rootsIndex === -1) throw new Error(`${target.path}: SceneRoots not found`);

  content = content.slice(0, rootsIndex) + yaml + content.slice(rootsIndex);
  content = addToSceneRoots(content, rootTrans);

  if (!content.startsWith("%YAML 1.1")) {
    throw new Error(`${target.path}: YAML header missing`);
  }

  fs.writeFileSync(target.path, content, "utf8");
  console.log(
    `Added spawn points to ${path.basename(target.path)}: terminals=${terminalTransIds.length}, escapes=${escapeTransIds.length}`
  );
}

for (const target of TARGETS) {
  processScene(target);
}
