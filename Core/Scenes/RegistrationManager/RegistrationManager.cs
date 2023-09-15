namespace Squiggles.Core.Scenes.Registration;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Squiggles.Core.Error;
using Squiggles.Core.Events;
using Squiggles.Core.WorldEntity;

public partial class RegistrationManager : Node {

  private struct InternalRegistry {
    public string Path;
    public Dictionary<string, Resource> Dict;
  }

  private static readonly Dictionary<string, InternalRegistry> _registries = new();
  public static Dictionary<string, WorldEntity> Entities { get; private set; } = new();

  public static void RegisterRegistryType(string type, string path) {
    var registry = new InternalRegistry {
      Path = path,
      Dict = new(),
    };
    _registries[type] = registry;
  }

  public static T GetResource<T>(string id) where T : Resource {
    var type_name = nameof(T);
    if (!_registries.ContainsKey(type_name)) {
      Print.Warn($"No registry found for type <{type_name}>");
      return null;
    }

    var registry = _registries[type_name];
    return !registry.Dict.ContainsKey(id) ? null : registry.Dict[id] as T;
  }

  public override void _Ready() {
    ReloadRegistries();
    EventBus.Data.OnModsLoaded += () => {
      Print.Debug("Mods loaded. Reloading registries");
      ReloadRegistries();
    };
  }

  public static void ReloadRegistries() {
    Print.Debug("Begin registration", typeof(RegistrationManager).FullName);
    foreach (var type in _registries.Keys) {
      var registry = _registries[type];
      registry.Dict = LoadRegistry(registry.Path, (Resource res) => res.ResourceName, null, type);
      _registries[type] = registry;
    }
  }

  private delegate string GetIDFor<T>(T obj) where T : Resource; // Delegates for callbacks are frickin awesome. Eat your heart out GDScript!
  private delegate void AddToDictionary<T>(Dictionary<string, T> dict, string id, T obj) where T : Resource;
  private static void DefaultAddToDict<T>(Dictionary<string, T> dict, string id, T obj) where T : Resource => dict[id] = obj;
  private static Dictionary<string, T> LoadRegistry<T>(string root_dir, GetIDFor<T> idGenCallback, AddToDictionary<T> addDict, string label = "unlabeled") where T : Resource {
    var registry = new Dictionary<string, T>();
    var dir = DirAccess.Open(root_dir);
    addDict ??= DefaultAddToDict;
    if (dir is null) {
      Print.Debug($"Failed to find root path for resource [{label}], expected path : {root_dir}");
      return registry; // empty registry
    }
    var files = GetAllFilesRecursive(dir).Where((s) => s.Contains(".tres"));

    foreach (var f in files) {
      var fileName = f.Replace(".remap", ""); // clear remap files to load default version
      var temp = GD.Load<T>(fileName);
      if (temp is not null) {
        addDict(registry, idGenCallback(temp), temp);
      }
      else {
        Print.Debug($"file '{fileName}' is not valid for type [{label}]");
      }
    }
    PrintRegistry(registry, label);
    return registry;
  }

  private static void PrintRegistry<T>(Dictionary<string, T> reg, string label) where T : Resource {
    Print.Debug($"----['{label}' Registry ({reg.Count} elements) ]----");
    foreach (var pair in reg) {
      Print.Debug($"] '{pair.Key}' : {pair.Value}");
    }
    Print.Debug($"----[End Registry ]----");
  }

  private static List<string> GetAllFilesRecursive(DirAccess dir) {
    var files = new List<string>();
    foreach (var f in dir.GetFiles()) {
      files.Add(dir.GetCurrentDir().PathJoin(f));
    }

    foreach (var d in dir.GetDirectories()) {
      if (d.StartsWith("_")) {
        continue; // allow special hidden folders
      }

      var subdir = DirAccess.Open(dir.GetCurrentDir().PathJoin(d));
      if (subdir is null) {
        continue;
      }

      files.AddRange(GetAllFilesRecursive(subdir));
    }
    return files;
  }

}
