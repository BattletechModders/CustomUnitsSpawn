using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using HarmonyLib;
using HBS.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace CustomUnitSpawn {
  [HarmonyPatch(typeof(LanceOverride))]
  [HarmonyPatch("RequestLance")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(MetadataDatabase), typeof(int), typeof(DateTime?), typeof(TagSet), typeof(Contract) })]
  public static class LanceOverride_RequestLance {
    public static void Prefix(LanceOverride __instance, MetadataDatabase mdd, int requestedDifficulty, DateTime? currentDate, TagSet companyTags, Contract contract) {
      try {
        Log.TWL(0, $"LanceOverride.RequestLance contract:{(contract == null ? "null" : (contract.mapName+":"+contract.GUID))}");
        Core.currentContract = contract;
      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(LanceOverride))]
  [HarmonyPatch("RequestLanceComplete")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(MetadataDatabase), typeof(DateTime?), typeof(TagSet) })]
  public static class LanceOverride_RequestLanceComplete {
    public static void Prefix(LanceOverride __instance, MetadataDatabase mdd, DateTime? currentDate, TagSet companyTags) {
      try {
        Log.TWL(0, $"LanceOverride.RequestLanceComplete contract:{(Core.currentContract == null ? "null" : (Core.currentContract.mapName + ":" + Core.currentContract.GUID))}");
        Core.currentContract = null;
      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
  }
  public static class TagSetQueryExtensions_GetMatchingUnitDefs {
    public static MethodInfo target() { return AccessTools.Method(typeof(TagSetQueryExtensions), "GetMatchingUnitDefs"); }
    private static MethodInfo original { get; set; } = null;
    public class tagItem {
      public List<string> tags = new List<string>();
      public int index = 0;
      public string tag = string.Empty;
      public tagItem(string tag, TagReplaceRule rrule) {
        this.tag = tag;
        tags = new List<string>();
        tags.Add(tag);
        if(rrule != null)tags.AddRange(rrule.replace);
      }
      public string curtag { get { return tags[index]; } }
    }
    public class tagItterator {
      public List<tagItem> items { get; set; } = new List<tagItem>();
      public void Add(tagItem item) {
        items.Add(item);
        item.index = 0;
      }
      public bool increment() {
        for(int t = 0; t < items.Count; ++t) {
          if (items[t].index < (items[t].tags.Count-1)) {
            ++items[t].index;
            for(int tt = t-1; tt >= 0; --tt) {
              items[tt].index = 0;
            }
            return true;
          }
        }
        return false;
      }
    }
    public static void postfix(MetadataDatabase mdd, TagSet requiredTags, TagSet excludedTags, bool checkOwnership, DateTime? currentDate, TagSet companyTags,ref List<UnitDef_MDD> __result) {
      if (__result == null) { return; }
      Log.TWL(0, $"TagSetQueryExtensions.GetMatchingUnitDefs result:{__result.Count}");
      List<UnitDef_MDD> result = new List<UnitDef_MDD>();
      result.AddRange(__result);
      if (result.Count == 0) {
        List<string> tags = new List<string>();
        tags.AddRange(requiredTags);
        tags.AddRange(excludedTags);
        tags.Sort(
          (a, b) => {
            float a_weight = 9999f;
            float b_weight = 9999f;
            if(Core.Settings.tags_weights.TryGetValue(a, out var a_tag_info)) {
              a_weight = a_tag_info.weight;
            }
            if (Core.Settings.tags_weights.TryGetValue(b, out var b_tag_info)) {
              b_weight = b_tag_info.weight;
            }
            return a_weight.CompareTo(b_weight);
          }
        );
        TagSet effective_requiredTags = new TagSet(requiredTags);
        TagSet effective_excludedTags = new TagSet(excludedTags);
        tagItterator tagsVariants = new tagItterator();
        foreach (var tag in tags) {
          tagItem item = null;
          if (Core.Settings.tags_weights.TryGetValue(tag, out var tag_info)) {
            item = new tagItem(tag, tag_info);
          } else {
            item = new tagItem(tag, null);
          }
          tagsVariants.Add(item);
          Log.W(1, $"tag:{item.tag} ");
          foreach (var ntag in item.tags) { Log.W(1, ntag); }
          Log.WL(0, "");
        }
        int itteration = 0;
        while (tagsVariants.increment()) {
          TagSet temp_requiredTags = new TagSet(requiredTags);
          TagSet temp_excludedTags = new TagSet(excludedTags);
          foreach(var tag in tagsVariants.items) {
            if (temp_requiredTags.Contains(tag.tag)) {
              temp_requiredTags.Remove(tag.tag);
              if (string.IsNullOrEmpty(tag.curtag) == false) { temp_requiredTags.Add(tag.curtag); }
            } else if (temp_excludedTags.Contains(tag.tag)) {
              temp_excludedTags.Remove(tag.tag);
              if (string.IsNullOrEmpty(tag.curtag) == false) { temp_excludedTags.Add(tag.curtag); }
            }
          }
          result = mdd.GetMatchingDataByTagSet<UnitDef_MDD>(TagSetType.UnitDef, temp_requiredTags, temp_excludedTags, "UnitDef", "", checkOwnership, "UnitDefID");
          result.RemoveAll((Predicate<UnitDef_MDD>)(unitDef => !mdd.CanRandomlySelectUnitDef(unitDef, currentDate, companyTags)));
          Log.WL(1, $"[{itteration}] requiredTags:{temp_requiredTags.ToString()} excludedTags:{temp_excludedTags.ToString()} result:{result.Count}",true);
          ++itteration;
          if (result.Count != 0) { break; }
        }
        __result.Clear();
        __result.AddRange(result);
      }
    }
    public static MethodInfo patch() {
      return AccessTools.Method(typeof(TagSetQueryExtensions_GetMatchingUnitDefs), nameof(postfix));
    }
    public static HarmonyMethod harmonyMethod() {
      return new HarmonyMethod(patch(), -1000, null, null, true);
    }
    public static void Perform(Harmony harmony) {
      original = harmony.Patch(target());
      harmony.Patch(target(), null, harmonyMethod(), null, null, null);
    }
  }
}