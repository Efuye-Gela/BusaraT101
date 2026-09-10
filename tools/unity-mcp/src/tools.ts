import { z } from "zod";

const id = z.number().int().refine(value => value !== 0, "Use a nonzero Unity instance ID returned by this Editor session.");
const assetPath = z.string().max(1024).regex(/^Assets\/[^\\:]+$/, "Use an Assets/ relative path with forward slashes.").refine(value => !value.split("/").some(part => part === "." || part === ".." || part === ""), "No traversal or empty segments.");
const confirm = z.literal("CONFIRM_DESTRUCTIVE");
const limit = z.number().int().min(1).max(500).default(100);
const name = z.string().min(1).max(200);
const empty = z.object({}).strict();

export const tools = [
  { name: "unity_status", op: "status", description: "Get project, Unity version, play/compile state and active scene.", schema: empty, readOnly: true },
  { name: "unity_editor", op: "editor", description: "Enter/exit play mode, pause, unpause, or step one frame. Changes runtime state.", schema: z.object({ action: z.enum(["play", "stop", "pause", "unpause", "step"]) }).strict() },
  { name: "unity_hierarchy", op: "hierarchy", description: "List loaded scene GameObjects; optional name substring and parent instance ID. Results are capped.", schema: z.object({ query: z.string().max(200).default(""), parentId: z.number().int().default(0), limit }).strict(), readOnly: true },
  { name: "unity_selection", op: "selection", description: "Read selection or set it to live Unity instance IDs.", schema: z.object({ ids: z.array(id).max(100).optional() }).strict() },
  { name: "unity_scene_create", op: "scene.create", description: "Create an empty scene additively without discarding current unsaved scenes.", schema: empty },
  { name: "unity_scene_open", op: "scene.open", description: "Open an existing Assets scene additively.", schema: z.object({ path: assetPath }).strict() },
  { name: "unity_scene_save", op: "scene.save", description: "Save the active scene. For an unsaved scene supply a new .unity Assets path. Existing different files are never overwritten.", schema: z.object({ path: assetPath.optional() }).strict() },
  { name: "unity_scene_close", op: "scene.close", description: "Close the active additive scene, discarding any unsaved changes. Requires exact destructive confirmation and at least one other loaded scene.", schema: z.object({ confirm }).strict() },
  { name: "unity_object_create", op: "object.create", description: "Create an Undoable GameObject, optionally a primitive and/or parented.", schema: z.object({ name, primitive: z.enum(["empty", "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"]).default("empty"), parentId: z.number().int().default(0) }).strict() },
  { name: "unity_object_delete", op: "object.delete", description: "Destroy a scene GameObject using Undo. Requires exact confirmation; root objects are protected.", schema: z.object({ id, confirm }).strict() },
  { name: "unity_object_reparent", op: "object.reparent", description: "Undoably reparent a scene GameObject; parentId=0 moves it to scene root. Cycles are rejected.", schema: z.object({ id, parentId: z.number().int() }).strict() },
  { name: "unity_component_add", op: "component.add", description: "Undoably add a component by fully qualified CLR type name.", schema: z.object({ id, type: z.string().min(1).max(500) }).strict() },
  { name: "unity_component_remove", op: "component.remove", description: "Undoably remove a component instance (not a GameObject ID). Transform deletion is forbidden.", schema: z.object({ id, confirm }).strict() },
  { name: "unity_inspect", op: "inspect", description: "Inspect an object/component's serialized properties and attached component IDs. Values are represented as strings; propertyType determines encoding.", schema: z.object({ id, limit }).strict(), readOnly: true },
  { name: "unity_property_set", op: "property.set", description: "Set one serialized property with Undo. value is text: scalar, JSON vector/color, or instance ID for ObjectReference (0=null). Unsupported property kinds fail explicitly.", schema: z.object({ id, property: z.string().min(1).max(500), value: z.string().max(65536) }).strict() },
  { name: "unity_assets_find", op: "assets.find", description: "Search AssetDatabase with Unity filters such as t:Prefab. Restricts results to Assets.", schema: z.object({ query: z.string().max(500), limit }).strict(), readOnly: true },
  { name: "unity_assets_import", op: "assets.import", description: "Import a confined existing Assets path.", schema: z.object({ path: assetPath }).strict() },
  { name: "unity_assets_refresh", op: "assets.refresh", description: "Queue AssetDatabase refresh; script import may cause domain reload. Returns a persisted async job ID.", schema: empty },
  { name: "unity_assets_save", op: "assets.save", description: "Save dirty asset changes to disk. This writes all dirty assets in the Editor, not scenes.", schema: empty },
  { name: "unity_assets_folder", op: "assets.folder", description: "Create a new Assets subfolder. Parent folder must exist; refuses overwrite or reparse points.", schema: z.object({ path: assetPath }).strict() },
  { name: "unity_undo", op: "undo", description: "Undo or redo the most recent global Editor Undo group, including manual user edits. File writes/imports/builds are not undoable.", schema: z.object({ action: z.enum(["undo", "redo"]) }).strict() },
  { name: "unity_prefab_create", op: "prefab.create", description: "Save a scene GameObject as a new prefab asset. Existing paths are rejected.", schema: z.object({ id, path: assetPath }).strict() },
  { name: "unity_prefab_instantiate", op: "prefab.instantiate", description: "Instantiate an existing prefab into the active scene using Undo.", schema: z.object({ path: assetPath, parentId: z.number().int().default(0) }).strict() },
  { name: "unity_material_create", op: "material.create", description: "Create a new material asset using an available shader by name.", schema: z.object({ path: assetPath, shader: z.string().min(1).max(500) }).strict() },
  { name: "unity_scriptable_create", op: "scriptable.create", description: "Create a new ScriptableObject asset by fully qualified concrete CLR type.", schema: z.object({ path: assetPath, type: z.string().min(1).max(500) }).strict() },
  { name: "unity_script_read", op: "script.read", description: "Read a UTF-8 .cs file beneath Assets; reparse points/symlinks are refused.", schema: z.object({ path: assetPath }).strict(), readOnly: true },
  { name: "unity_script_write", op: "script.write", description: "Write UTF-8 C# under Assets and queue import/compile as a persisted job. Replacing a file requires confirmation. No arbitrary shell/code-evaluation endpoint, but scripts execute with Editor privileges after compilation.", schema: z.object({ path: assetPath, content: z.string().max(524288), confirm: confirm.optional() }).strict() },
  { name: "unity_compile", op: "compile", description: "Request script compilation and return a persisted asynchronous job ID.", schema: empty },
  { name: "unity_console", op: "console", description: "Read recent log/error messages captured since bridge startup. Not historical Console entries from before bridge startup.", schema: z.object({ limit }).strict(), readOnly: true },
  { name: "unity_screenshot", op: "screenshot", description: "Capture Scene view to a new Assets .png path in a graphical Editor. Game view capture explicitly returns unsupported. Returns asset path, not a mocked image.", schema: z.object({ view: z.enum(["scene", "game"]), path: assetPath }).strict() },
  { name: "unity_tests_start", op: "tests.start", description: "Start Unity Test Framework EditMode or PlayMode tests and return persisted job ID. Optional full test names; results are polled with unity_job.", schema: z.object({ mode: z.enum(["EditMode", "PlayMode"]), tests: z.array(z.string().max(500)).max(100).default([]) }).strict() },
  { name: "unity_build_start", op: "build.start", description: "Build enabled saved scenes to Library/BusaraMcp/Builds/{jobId}. Only installed supported targets; requires confirmation. Poll unity_job even while the build blocks Unity's main thread.", schema: z.object({ target: z.enum(["StandaloneWindows64", "StandaloneLinux64", "StandaloneOSX", "Android", "WebGL"]), confirm }).strict() },
  { name: "unity_job", op: "job", description: "Read last-known async job state directly from confined Library job files, including during builds/domain reload. Does not require a responsive Editor and never retries operations.", schema: z.object({ jobId: z.string().regex(/^[a-f0-9]{32}$/) }).strict(), readOnly: true }
] as const;
