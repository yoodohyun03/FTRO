import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

const SCENES = [
  path.join(ROOT, "Assets/Scenes/WesternScene.unity"),
  path.join(ROOT, "Assets/Scenes/CityMapScene.unity"),
];

function repairScene(filePath) {
  let content = fs.readFileSync(filePath, "utf8");

  const beforeLighting = content;
  content = content.replace(
    /m_LightingSettings: \{fileID: \d+, guid: [0-9a-f]+, type: 2\}/g,
    "m_LightingSettings: {fileID: 0}"
  );

  // Match working scenes: LF line endings only.
  content = content.replace(/\r\n/g, "\n");

  if (content === beforeLighting.replace(/\r\n/g, "\n") && !beforeLighting.includes("m_LightingSettings: {fileID: 0}")) {
    console.warn(`No lighting settings change for ${path.basename(filePath)}`);
  }

  fs.writeFileSync(filePath, content, "utf8");

  const ids = [...content.matchAll(/^--- !u!\d+ &(\d+)/gm)].map((m) => m[1]);
  const seen = new Set();
  let dups = 0;
  for (const id of ids) {
    if (seen.has(id)) dups++;
    seen.add(id);
  }

  console.log(
    `Repaired ${path.basename(filePath)}: blocks=${ids.length}, dups=${dups}, lf=${(content.match(/\n/g) || []).length}, lighting0=${content.includes("m_LightingSettings: {fileID: 0}")}`
  );
}

for (const scene of SCENES) {
  repairScene(scene);
}
