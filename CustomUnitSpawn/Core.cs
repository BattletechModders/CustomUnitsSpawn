using BattleTech;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomUnitSpawn{
  public class TagReplaceRule {
    public float weight { get; set; } = 99999f;
    public List<string> replace { get; set; } = new List<string>();
  }
  public class Settings {
    public bool debugLog { get; set; } = false;
    public Dictionary<string, TagReplaceRule> tags_weights { get; set; } = new Dictionary<string, TagReplaceRule>();
  }
  public static class Core {
    public static string BaseDir { get; set; } = string.Empty;
    public static Harmony harmony { get; set; } = null;
    public static Settings Settings { get; set; } = new Settings();
    public static Contract currentContract { get; set; } = null;
    public static MethodBase GetMatchingUnitDefs { get; set; } = null;
    public static void FinishedLoading(List<string> loadOrder) {
      Log.TWL(0, "FinishedLoading", true);
      try {

      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
    public static T FindObject<T>(this GameObject go, string name) where T : Component {
      T[] arr = go.GetComponentsInChildren<T>(true);
      foreach (T component in arr) { if (component.gameObject.transform.name == name) { return component; } }
      return null;
    }

    public static void Init(string directory, string settingsJson) {
      Log.BaseDirectory = directory;
      Log.InitLog();
      Core.BaseDir = directory;
      Core.Settings = JsonConvert.DeserializeObject<CustomUnitSpawn.Settings>(settingsJson);
      Log.TWL(0, "Initing... " + directory + " version: " + Assembly.GetExecutingAssembly().GetName().Version, true);
      try {
        var harmony = new Harmony("ru.kmission.customunitspawn");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        TagSetQueryExtensions_GetMatchingUnitDefs.Perform(harmony);
      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
  }
}
