#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Busara.EditorMcp
{
    public static class McpCommands
    {
        [Serializable] sealed class TextResult { public string message; }
        [Serializable] sealed class Status
        {
            public string project, unityVersion, scene;
            public bool playing, paused, compiling, updating;
            public int processId;
        }
        [Serializable] public sealed class Item
        {
            public int id, parentId;
            public string name, type, path, value;
        }
        [Serializable] sealed class Items { public Item[] items; public bool truncated; }
        [Serializable] sealed class Inspection { public Item target; public Item[] components, properties; public bool truncated; }
        [Serializable] sealed class ScriptText { public string path, content; }
        static readonly Queue<Item> Logs = new Queue<Item>();

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
                Application.logMessageReceived += Log;
        }
        static void Log(string message, string stack, LogType type)
        {
            if (Logs.Count >= 500) Logs.Dequeue();
            Logs.Enqueue(new Item { type = type.ToString(), value = Cut(message, 8000), path = Cut(stack, 8000) });
        }
        static string Cut(string value, int max) => value == null ? "" : value.Substring(0, Math.Min(value.Length, max));
        static string Json(object value) => JsonUtility.ToJson(value);
        static string Done(string value) => Json(new TextResult { message = value });
        static int Limit(McpRequest request) => Math.Max(1, Math.Min(500, request.limit));
        static Item Describe(Object value) => new Item
        {
            id = value.GetInstanceID(), name = value.name, type = value.GetType().FullName,
            path = AssetDatabase.GetAssetPath(value),
            parentId = value is GameObject game && game.transform.parent != null ? game.transform.parent.gameObject.GetInstanceID() : 0
        };

        public static string Execute(McpRequest request)
        {
            bool undoable = request.op == "object.create" || request.op == "object.delete" || request.op == "object.reparent" ||
                request.op == "component.add" || request.op == "component.remove" || request.op == "property.set" || request.op == "prefab.instantiate";
            if (undoable) Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            try { return ExecuteCore(request); }
            finally
            {
                if (undoable) { Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group); Undo.IncrementCurrentGroup(); }
            }
        }

        static string ExecuteCore(McpRequest request)
        {
            switch (request.op)
            {
                case "status": return Json(new Status { project = BusaraMcpBridge.ProjectPath, unityVersion = Application.unityVersion,
                    scene = SceneManager.GetActiveScene().path, playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused,
                    compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating,
                    processId = System.Diagnostics.Process.GetCurrentProcess().Id });
                case "editor": return EditorState(request.action);
                case "hierarchy": return Hierarchy(request);
                case "selection":
                    if (request.ids != null) Selection.objects = request.ids.Select(Find).ToArray();
                    return Json(new Items { items = Selection.objects.Select(Describe).ToArray() });
                case "inspect": return Inspect(request);
                case "assets.find":
                    var found = AssetDatabase.FindAssets(request.query ?? "", new[] { "Assets" });
                    return Json(new Items { items = found.Take(Limit(request)).Select(guid => new Item { path = AssetDatabase.GUIDToAssetPath(guid), value = guid }).ToArray(), truncated = found.Length > Limit(request) });
                case "script.read":
                    string full = McpPaths.Asset(request.path, ".cs", true);
                    if (new FileInfo(full).Length > 524288) throw new IOException("Script exceeds 512 KiB.");
                    return Json(new ScriptText { path = request.path, content = File.ReadAllText(full, System.Text.Encoding.UTF8) });
                case "console": return Json(new Items { items = Logs.Reverse().Take(Limit(request)).ToArray(), truncated = Logs.Count > Limit(request) });
                case "job": return McpJobs.Get(request.jobId);
                case "screenshot": return Screenshot(request);
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("This mutation requires Edit Mode.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Editor is compiling or importing; wait until it is idle.");
            switch (request.op)
            {
                case "scene.create":
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(scene);
                    return Done("Created additive scene: " + scene.name);
                case "scene.open":
                    McpPaths.Asset(request.path, ".unity", true);
                    var opened = EditorSceneManager.OpenScene(request.path, OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(opened);
                    return Done("Opened additive scene: " + opened.path);
                case "scene.save":
                    var active = SceneManager.GetActiveScene();
                    string savePath = string.IsNullOrEmpty(request.path) ? active.path : request.path;
                    McpPaths.Asset(savePath, ".unity");
                    if (savePath != active.path) McpPaths.NewAsset(savePath, ".unity");
                    if (!EditorSceneManager.SaveScene(active, savePath)) throw new IOException("Unity could not save the scene.");
                    return Done("Saved " + savePath);
                case "scene.close":
                    Confirm(request);
                    if (SceneManager.sceneCount < 2) throw new InvalidOperationException("The last loaded scene cannot be closed.");
                    if (!EditorSceneManager.CloseScene(SceneManager.GetActiveScene(), true)) throw new InvalidOperationException("Unity could not close the active scene.");
                    return Done("Closed the active scene; unsaved changes were discarded.");
                case "object.create":
                    if (string.IsNullOrWhiteSpace(request.name)) throw new ArgumentException("name is required.");
                    var parent = request.parentId == 0 ? null : SceneObject(request.parentId).transform;
                    GameObject created;
                    if (string.IsNullOrEmpty(request.primitive) || request.primitive == "empty") created = new GameObject(request.name);
                    else
                    {
                        if (!Enum.TryParse(request.primitive, out PrimitiveType primitive) || !Enum.IsDefined(typeof(PrimitiveType), primitive)) throw new ArgumentException("Unknown primitive.");
                        created = GameObject.CreatePrimitive(primitive);
                        created.name = request.name;
                    }
                    Undo.RegisterCreatedObjectUndo(created, "MCP create GameObject");
                    if (parent != null) Undo.SetTransformParent(created.transform, parent, "MCP parent GameObject");
                    return Json(Describe(created));
                case "object.delete":
                    Confirm(request);
                    var deleted = SceneObject(request.id);
                    if (deleted.transform.parent == null) throw new InvalidOperationException("Scene root deletion is protected. Reparent deliberately before deleting.");
                    Undo.DestroyObjectImmediate(deleted);
                    return Done("Deleted with Undo.");
                case "object.reparent":
                    var child = SceneObject(request.id);
                    var newParent = request.parentId == 0 ? null : SceneObject(request.parentId).transform;
                    if (newParent != null && (newParent == child.transform || newParent.IsChildOf(child.transform))) throw new ArgumentException("Parenting cycle is forbidden.");
                    Undo.SetTransformParent(child.transform, newParent, "MCP reparent");
                    return Json(Describe(child));
                case "component.add":
                    var componentType = Resolve(request.type, typeof(Component));
                    if (componentType == typeof(Transform) || componentType == typeof(RectTransform)) throw new ArgumentException("Transform replacement is unsupported.");
                    var component = Undo.AddComponent(SceneObject(request.id), componentType);
                    if (component == null) throw new InvalidOperationException("Unity rejected the component.");
                    return Json(Describe(component));
                case "component.remove":
                    Confirm(request);
                    var removed = Find(request.id) as Component;
                    if (removed == null || removed is Transform) throw new ArgumentException("A non-Transform component ID is required.");
                    SceneObject(removed.gameObject.GetInstanceID());
                    Undo.DestroyObjectImmediate(removed);
                    return Done("Removed component with Undo.");
                case "property.set": return SetProperty(request);
                case "assets.import":
                    McpPaths.Asset(request.path, existing: true);
                    return McpJobs.Schedule("refresh", request, () => AssetDatabase.ImportAsset(request.path));
                case "assets.refresh": return McpJobs.Schedule("refresh", request, () => AssetDatabase.Refresh());
                case "assets.save": AssetDatabase.SaveAssets(); return Done("Saved dirty assets.");
                case "assets.folder":
                    McpPaths.NewAsset(request.path, null);
                    int slash = request.path.LastIndexOf('/');
                    string guid = AssetDatabase.CreateFolder(request.path.Substring(0, slash), request.path.Substring(slash + 1));
                    if (string.IsNullOrEmpty(guid)) throw new IOException("Unity could not create the folder.");
                    return Done("Created " + request.path);
                case "undo":
                    if (request.action == "undo") Undo.PerformUndo();
                    else if (request.action == "redo") Undo.PerformRedo();
                    else throw new ArgumentException("action must be undo or redo.");
                    return Done("Applied global " + request.action + ".");
                case "prefab.create":
                    McpPaths.NewAsset(request.path, ".prefab");
                    var prefab = PrefabUtility.SaveAsPrefabAsset(SceneObject(request.id), request.path, out bool success);
                    if (!success || prefab == null) throw new IOException("Prefab creation failed.");
                    return Json(Describe(prefab));
                case "prefab.instantiate":
                    McpPaths.Asset(request.path, ".prefab", true);
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(request.path);
                    if (source == null || !PrefabUtility.IsPartOfPrefabAsset(source)) throw new ArgumentException("Not a prefab asset.");
                    var prefabParent = request.parentId == 0 ? null : SceneObject(request.parentId).transform;
                    var instance = PrefabUtility.InstantiatePrefab(source, SceneManager.GetActiveScene()) as GameObject;
                    if (instance == null) throw new InvalidOperationException("Prefab instantiation failed.");
                    Undo.RegisterCreatedObjectUndo(instance, "MCP instantiate prefab");
                    if (prefabParent != null) Undo.SetTransformParent(instance.transform, prefabParent, "MCP parent prefab");
                    return Json(Describe(instance));
                case "material.create":
                    McpPaths.NewAsset(request.path, ".mat");
                    var shader = Shader.Find(request.shader);
                    if (shader == null) throw new ArgumentException("Shader not found.");
                    var material = new Material(shader);
                    AssetDatabase.CreateAsset(material, request.path);
                    AssetDatabase.SaveAssets();
                    return Json(Describe(material));
                case "scriptable.create":
                    McpPaths.NewAsset(request.path, ".asset");
                    var asset = ScriptableObject.CreateInstance(Resolve(request.type, typeof(ScriptableObject)));
                    AssetDatabase.CreateAsset(asset, request.path);
                    AssetDatabase.SaveAssets();
                    return Json(Describe(asset));
                case "script.write":
                    string destination = McpPaths.Asset(request.path, ".cs");
                    if (request.content == null || System.Text.Encoding.UTF8.GetByteCount(request.content) > 524288) throw new ArgumentException("content is required and limited to 512 KiB.");
                    bool overwrite = File.Exists(destination);
                    if (overwrite) Confirm(request);
                    if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new DirectoryNotFoundException("Parent folder must exist.");
                    return McpJobs.Schedule("compile", request, () =>
                    {
                        string checkedPath = McpPaths.Asset(request.path, ".cs");
                        if (!overwrite && File.Exists(checkedPath)) throw new IOException("Destination appeared after command acceptance; overwrite was not confirmed.");
                        File.WriteAllText(checkedPath, request.content, new System.Text.UTF8Encoding(false));
                        AssetDatabase.ImportAsset(request.path);
                        CompilationPipeline.RequestScriptCompilation();
                    });
                case "compile": return McpJobs.Schedule("compile", request, () => CompilationPipeline.RequestScriptCompilation());
                case "tests.start": return McpJobs.Tests(request);
                case "build.start": Confirm(request); return McpJobs.Build(request);
                default: throw new NotSupportedException("Unsupported operation: " + request.op);
            }
        }

        static string EditorState(string action)
        {
            switch (action)
            {
                case "play": if (EditorApplication.isCompiling) throw new InvalidOperationException("Compilation is in progress."); EditorApplication.isPlaying = true; break;
                case "stop": EditorApplication.isPlaying = false; break;
                case "pause": if (!EditorApplication.isPlaying) throw new InvalidOperationException("Pause requires Play Mode."); EditorApplication.isPaused = true; break;
                case "unpause": EditorApplication.isPaused = false; break;
                case "step": if (!EditorApplication.isPlaying || !EditorApplication.isPaused) throw new InvalidOperationException("Step requires paused Play Mode."); EditorApplication.Step(); break;
                default: throw new ArgumentException("Unknown editor action.");
            }
            return Done("Requested " + action + "; poll unity_status to observe the transition.");
        }

        static string Hierarchy(McpRequest request)
        {
            var all = new List<Item>();
            GameObject parent = request.parentId == 0 ? null : SceneObject(request.parentId);
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (parent != null && transform.parent != parent.transform) continue;
                        if (!string.IsNullOrEmpty(request.query) && transform.name.IndexOf(request.query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        all.Add(Describe(transform.gameObject));
                        if (all.Count > Limit(request)) return Json(new Items { items = all.Take(Limit(request)).ToArray(), truncated = true });
                    }
            }
            return Json(new Items { items = all.ToArray() });
        }

        public static Object Find(int id)
        {
            var value = EditorUtility.InstanceIDToObject(id);
            if (value == null) throw new ArgumentException("Object ID is stale or missing; query current IDs.");
            return value;
        }
        static GameObject SceneObject(int id)
        {
            var value = Find(id) as GameObject;
            if (value == null || EditorUtility.IsPersistent(value) || !value.scene.IsValid() || !value.scene.isLoaded)
                throw new ArgumentException("An editable loaded scene GameObject ID is required.");
            if ((value.hideFlags & HideFlags.NotEditable) != 0) throw new ArgumentException("Object is not editable.");
            return value;
        }
        static Type Resolve(string name, Type basis)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A fully qualified type name is required.");
            var matches = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false))
                .Where(type => type != null).Distinct().ToArray();
            if (matches.Length != 1 || !basis.IsAssignableFrom(matches[0]) || matches[0].IsAbstract || matches[0].ContainsGenericParameters)
                throw new ArgumentException("Type must identify exactly one concrete " + basis.Name + ".");
            return matches[0];
        }
        static void Confirm(McpRequest request)
        {
            if (request.confirm != "CONFIRM_DESTRUCTIVE") throw new ArgumentException("Explicit confirm=\"CONFIRM_DESTRUCTIVE\" is required.");
        }

        static string Inspect(McpRequest request)
        {
            var target = Find(request.id);
            var properties = new List<Item>();
            using (var serialized = new SerializedObject(target))
            {
                var iterator = serialized.GetIterator();
                while (iterator.NextVisible(true))
                {
                    properties.Add(new Item { path = iterator.propertyPath, name = iterator.displayName, type = iterator.propertyType.ToString(), value = PropertyValue(iterator) });
                    if (properties.Count > Limit(request)) break;
                }
            }
            return Json(new Inspection { target = Describe(target), components = target is GameObject game ? game.GetComponents<Component>().Where(c => c != null).Select(Describe).ToArray() : Array.Empty<Item>(),
                properties = properties.Take(Limit(request)).ToArray(), truncated = properties.Count > Limit(request) });
        }
        static string PropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Integer: return property.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Float: return property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.Enum: return property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue == null ? "0" : property.objectReferenceValue.GetInstanceID().ToString();
                case SerializedPropertyType.Vector2: return Json(property.vector2Value);
                case SerializedPropertyType.Vector3: return Json(property.vector3Value);
                case SerializedPropertyType.Vector4: return Json(property.vector4Value);
                case SerializedPropertyType.Color: return Json(property.colorValue);
                default: return "<inspect children or unsupported scalar>";
            }
        }
        static string SetProperty(McpRequest request)
        {
            var target = Find(request.id);
            if (target is GameObject game) SceneObject(game.GetInstanceID());
            else if (target is Component component) SceneObject(component.gameObject.GetInstanceID());
            else McpPaths.Asset(AssetDatabase.GetAssetPath(target), existing: true);
            if (string.IsNullOrEmpty(request.property)) throw new ArgumentException("property is required.");
            if (request.property == "m_Script" || request.property == "m_Father" || request.property == "m_GameObject" ||
                request.property == "m_CorrespondingSourceObject" || request.property.StartsWith("m_Prefab", StringComparison.Ordinal) ||
                request.property.StartsWith("m_Component", StringComparison.Ordinal) || request.property.StartsWith("m_Children", StringComparison.Ordinal))
                throw new ArgumentException("Use dedicated component and parenting tools instead.");
            using (var serialized = new SerializedObject(target))
            {
                var property = serialized.FindProperty(request.property);
                if (property == null || !property.editable) throw new ArgumentException("Serialized property is absent or read-only.");
                Undo.RecordObject(target, "MCP edit property");
                switch (property.propertyType)
                {
                    case SerializedPropertyType.String: property.stringValue = request.value; break;
                    case SerializedPropertyType.Boolean: property.boolValue = bool.Parse(request.value); break;
                    case SerializedPropertyType.Integer: property.longValue = long.Parse(request.value, CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Float:
                        double number = double.Parse(request.value, CultureInfo.InvariantCulture);
                        if (double.IsNaN(number) || double.IsInfinity(number)) throw new ArgumentException("Finite number required.");
                        property.doubleValue = number; break;
                    case SerializedPropertyType.Enum:
                        int index = int.Parse(request.value, CultureInfo.InvariantCulture);
                        if (index < 0 || index >= property.enumNames.Length) throw new ArgumentOutOfRangeException("value", "Enum index is out of range.");
                        property.enumValueIndex = index; break;
                    case SerializedPropertyType.ObjectReference:
                        int reference = int.Parse(request.value, CultureInfo.InvariantCulture);
                        property.objectReferenceValue = reference == 0 ? null : Find(reference); break;
                    case SerializedPropertyType.Vector2: property.vector2Value = JsonUtility.FromJson<Vector2>(request.value); break;
                    case SerializedPropertyType.Vector3: property.vector3Value = JsonUtility.FromJson<Vector3>(request.value); break;
                    case SerializedPropertyType.Vector4: property.vector4Value = JsonUtility.FromJson<Vector4>(request.value); break;
                    case SerializedPropertyType.Color: property.colorValue = JsonUtility.FromJson<Color>(request.value); break;
                    default: throw new NotSupportedException("Setting " + property.propertyType + " is unsupported.");
                }
                serialized.ApplyModifiedProperties();
                if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorUtility.SetDirty(target);
                return Done("Updated " + request.property + " with Undo. Save the scene/assets separately.");
            }
        }

        static string Screenshot(McpRequest request)
        {
            McpPaths.NewAsset(request.path, ".png");
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new NotSupportedException("Screenshots require a graphical Editor.");
            Texture2D image = null;
            RenderTexture render = null;
            RenderTexture previous = RenderTexture.active;
            Camera camera = null;
            RenderTexture priorTarget = null;
            try
            {
                if (request.view == "game")
                    throw new NotSupportedException("Reliable Game view frame capture is not implemented; use scene view capture. No file was created.");
                if (request.view != "scene" || SceneView.lastActiveSceneView == null)
                    throw new NotSupportedException("Open a Scene view before capturing.");
                camera = SceneView.lastActiveSceneView.camera;
                priorTarget = camera.targetTexture;
                int width = Mathf.Clamp(camera.pixelWidth, 1, 2048), height = Mathf.Clamp(camera.pixelHeight, 1, 2048);
                render = new RenderTexture(width, height, 24);
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(McpPaths.Asset(request.path, ".png"), image.EncodeToPNG());
                AssetDatabase.ImportAsset(request.path);
                return Done("Captured " + request.path);
            }
            finally
            {
                if (camera != null) camera.targetTexture = priorTarget;
                RenderTexture.active = previous;
                if (image != null) Object.DestroyImmediate(image);
                if (render != null) Object.DestroyImmediate(render);
            }
        }
    }
}
#endif
