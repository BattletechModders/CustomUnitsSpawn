using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using BattleTech.Save;
using BattleTech.Save.Test;
using HarmonyLib;
using HBS.Collections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using System.Threading;
using IRBTModUtils;
using BattleTech.Rendering;
using BattleTech.UI;

namespace CustomUnitSpawn {
  public class ContractsRandomizerData {
    public static ContractsRandomizerData instance { get; set; } = new ContractsRandomizerData();
    public static readonly string CONTRACT_RANDOMIZER_DATA_STAT_NAME = "contract_randomizer_data";
    public Dictionary<string, ContractRandomizerData> data { get; set; } = new Dictionary<string, ContractRandomizerData>();
    private static Dictionary<string,Contract> hydratedContracts { get; set; } = new Dictionary<string, Contract>();
    private static HashSet<WeakReference<Contract>> createdContracts { get; set; } = new HashSet<WeakReference<Contract>>();
    public static void RegisterCreation(Contract contract) {
      createdContracts.Add(new WeakReference<Contract>(contract));
    }
    public static void PrepareDehydrate() {
      HashSet<WeakReference<Contract>> to_del = new HashSet<WeakReference<Contract>>();
      Log.TWL(0, $"ContractsRandomizerData.PrepareDehydrate {createdContracts.Count}");
      foreach (var contract_ref in createdContracts) {
        if(contract_ref.TryGetTarget(out var contract) == false) { to_del.Add(contract_ref); continue; }
        if (contract == null) { to_del.Add(contract_ref); continue; }
        try {
          if (string.IsNullOrEmpty(contract.GUID)) {
            Log.WL(1, $"contract without GUID: {contract.Name}:{contract.mapName}");
            continue;
          }
          if (instance.data.ContainsKey(contract.GUID)) { continue; }
          var rnddata = new ContractRandomizerData();
          Log.WL(1, $"new seed: {contract.Name}:{contract.mapName}:{contract.GUID} {rnddata.seed}");
          instance.data.Add(contract.GUID, rnddata);
        } catch (Exception) {
          to_del.Add(contract_ref);
        }
      }
      foreach(var td in to_del) { createdContracts.Remove(td); }
    }
    public static void RegisterHydration(Contract contract) {
      if (string.IsNullOrEmpty(contract.GUID)) { return; }
      hydratedContracts[contract.GUID] = contract;
    }
    public void Rehydrate() {
      HashSet<string> to_del = new HashSet<string>();
      foreach(var contract in instance.data) {
        if (hydratedContracts.ContainsKey(contract.Key) == false) { to_del.Add(contract.Key); }
      //  if (string.IsNullOrEmpty(contract.GUID)) { continue; }
      //  if (data.ContainsKey(contract.GUID)) { continue; }
      //  data.Add(contract.GUID, new ContractRandomizerData());
      }
      foreach (var guid in to_del) { instance.data.Remove(guid); }
      hydratedContracts.Clear();
    }
  }
  public class ContractRandomizerData {
    public int seed { get; set; }
    public static System.Random rnd { get; set; } = new System.Random();
    public ContractRandomizerData() {
      seed = rnd.Next();
    }
  }
  [HarmonyPatch(typeof(Contract))]
  [HarmonyPatch("Hydrate")]
  [HarmonyPatch(MethodType.Normal)]
  public static class Contract_Hydrate {
    public static void Postfix(Contract __instance) {
      try {
        Log.TWL(0, $"Contract.Hydrate {__instance.Name}:{__instance.mapName}:{__instance.mapMood} GUID:'{__instance.GUID}'");
        ContractsRandomizerData.RegisterHydration(__instance);
        ContractsRandomizerData.RegisterCreation(__instance);
      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(Contract))]
  [HarmonyPatch(MethodType.Constructor)]
  [HarmonyPatch(new Type[] { typeof(string), typeof(string), typeof(string), typeof(ContractTypeValue), typeof(GameInstance), typeof(ContractOverride), typeof(GameContext), typeof(bool), typeof(int), typeof(int), typeof(int?) } )]
  public static class Contract_Constructor_long {
    public static void Postfix(Contract __instance) {
      try {
        ContractsRandomizerData.RegisterCreation(__instance);
        //Log.TWL(0, "Contract.Constructor");
      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(Contract))]
  [HarmonyPatch(MethodType.Constructor)]
  [HarmonyPatch(new Type[] {  })]
  public static class Contract_Constructor_short {
    public static void Postfix(Contract __instance) {
      try {
        ContractsRandomizerData.RegisterCreation(__instance);
        //Log.TWL(0, "Contract.Constructor");
      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
  }
  //[HarmonyPatch(typeof(Contract))]
  //[HarmonyPatch("Dehydrate")]
  //[HarmonyPatch(MethodType.Normal)]
  //public static class Contract_Dehydrate {
  //  public static void Postfix(Contract __instance) {
  //    try {
  //      Log.TWL(0, $"Contract.Dehydrate {__instance.Name}:{__instance.mapName}:{__instance.mapMood} GUID:{__instance.GUID}");
  //      if (string.IsNullOrEmpty(__instance.GUID) == false) {
  //        if (ContractsRandomizerData.instance.data.TryGetValue(__instance.GUID, out var rnddata) == false) {
  //          rnddata = new ContractRandomizerData();
  //          Log.WL(1, $"add contract seed to save:{rnddata.seed}");
  //          ContractsRandomizerData.instance.data.Add(__instance.GUID, rnddata);
  //        }
  //      }
  //    } catch (Exception e) {
  //      Log.TWL(0, e.ToString(), true);
  //    }
  //  }
  //}
  [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
  public static class SimGameState_Rehydrate {
    static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave) {
      Log.TWL(0, $"SimGameState.Rehydrate {gameInstanceSave.FileID} save contains data for next contracts");
      try {
        var contract_rnd = __instance.CompanyStats.GetStatistic(ContractsRandomizerData.CONTRACT_RANDOMIZER_DATA_STAT_NAME);
        if (contract_rnd == null) {
          contract_rnd = __instance.CompanyStats.AddStatistic<string>(ContractsRandomizerData.CONTRACT_RANDOMIZER_DATA_STAT_NAME, "{}");
          Log.WL(1, $"this save does not contains data about contract's seeds");
        } else {
          ContractsRandomizerData.instance = JsonConvert.DeserializeObject<ContractsRandomizerData>(contract_rnd.Value<string>());
        }
        ContractsRandomizerData.instance.Rehydrate();
        foreach (var seed in ContractsRandomizerData.instance.data) {
          Log.WL(1, $"contract GUID:{seed.Key} seed: {seed.Value.seed}");
        }
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
      }
    }
  }
  [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
  public static class SimGameState_Dehydrate {
    static void Prefix(SimGameState __instance, SimGameSave save, SerializableReferenceContainer references) {
      Log.TWL(0,$"SimGameState.Dehydrate {save.FileID} saving contract's seeds");
      ContractsRandomizerData.PrepareDehydrate();
      try {
        var contract_rnd = __instance.CompanyStats.GetStatistic(ContractsRandomizerData.CONTRACT_RANDOMIZER_DATA_STAT_NAME);
        if (contract_rnd == null) { contract_rnd = __instance.CompanyStats.AddStatistic<string>(ContractsRandomizerData.CONTRACT_RANDOMIZER_DATA_STAT_NAME, "{}"); }
        foreach (var seed in ContractsRandomizerData.instance.data) {
          Log.WL(1, $"contract GUID:{seed.Key} seed: {seed.Value.seed}");
        }
        contract_rnd.SetValue<string>(JsonConvert.SerializeObject(ContractsRandomizerData.instance));
      } catch (Exception e) {
        Log.TWL(0,e.ToString());
      }
    }
  }
  [HarmonyPatch(typeof(SimGameState))]
  [HarmonyPatch("OnLanceConfiguratorAccept")]
  [HarmonyPatch(MethodType.Normal)]
  public static class SimGameState_OnLanceConfiguratorAccept {
    public static void InitSeed(this Contract contract) {
      if (string.IsNullOrEmpty(contract.GUID)) { return; }
      Log.TWL(0, $"Contract:InitSeed {(contract == null ? "null" : "'" + contract.GUID + "'")}");
      if (ContractsRandomizerData.instance.data.TryGetValue(contract.GUID, out var rnddata) == false) {
        rnddata = new ContractRandomizerData();
        ContractsRandomizerData.instance.data.Add(contract.GUID, rnddata);
        Log.WL(1, $"add new seed");
      }
      Log.WL(1, $"rnd data:{rnddata.seed}");
      RandomizeHelper.enabled = true;
      RandomizeHelper.state = RandomizeHelper.State.ReplaceWithStack;
      if (RandomizeHelper.seed != rnddata.seed) {
        Log.WL(1, $"reiniting seed:{rnddata.seed}");
        RandomizeHelper.InitSeed(rnddata.seed);
      } else {
        Log.WL(1, $"seed already properly set");
      }
    }
    public static void Prefix(SimGameState __instance) {
      try {
        var contract = !__instance.HasTravelContract ? __instance.SelectedContract : __instance.ActiveTravelContract;
        if (contract == null) { return; }
        contract.InitSeed();
      } catch (Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(AudioEventManager))]
  [HarmonyPatch("PlayLoadingMusic")]
  [HarmonyPatch(MethodType.Normal)]
  public static class AudioEventManager_PlayLoadingMusic {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(SGRoomManager))]
  [HarmonyPatch("AmbientVOTimer")]
  [HarmonyPatch(MethodType.Normal)]
  public static class SGRoomManager_AmbientVOTimer {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(SetupCoroutine))]
  [HarmonyPatch("InvokeMoveNext")]
  [HarmonyPatch(MethodType.Normal)]
  public static class SetupCoroutine_InvokeMoveNext {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
    public static Exception Finalizer(Exception __exception) {
      if (__exception != null) {
        BattleTech.GameInstance.gameInfoLogger?.LogError(__exception.ToString());
      }
      return null;
    }
  }
  [HarmonyPatch(typeof(SimGameHolodisplay))]
  [HarmonyPatch("Update")]
  [HarmonyPatch(MethodType.Normal)]
  public static class SimGameHolodisplay_Update {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(StateRandom))]
  [HarmonyPatch("OnStateEnter")]
  [HarmonyPatch(MethodType.Normal)]
  public static class StateRandom_OnStateEnter {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(ScrollingScreen))]
  [HarmonyPatch("OnWillRenderObject")]
  [HarmonyPatch(MethodType.Normal)]
  public static class ScrollingScreen_OnWillRenderObject {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(MechEdgeSelection))]
  [HarmonyPatch("OnEnable")]
  [HarmonyPatch(MethodType.Normal)]
  public static class MechEdgeSelection_OnEnable {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(FogScattering))]
  [HarmonyPatch("RefreshBoundaryTex")]
  [HarmonyPatch(MethodType.Normal)]
  public static class FogScattering_RefreshBoundaryTex {
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
    }
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(Contract))]
  [HarmonyPatch("Begin")]
  [HarmonyPatch(MethodType.Normal)]
  public static class Contract_Begin {
    [HarmonyPriority(-400)]
    public static void Prefix(Contract __instance, ref RandomizeHelper.State __state) {
      __instance.InitSeed();
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Replace;
    }
    [HarmonyPriority(800)]
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(LoadRequest))]
  [HarmonyPatch("Finish")]
  [HarmonyPatch(MethodType.Normal)]
  public static class LoadRequest_Finish {
    [HarmonyPriority(-400)]
    public static void Prefix(ref RandomizeHelper.State __state) {
      __state = RandomizeHelper.state;
      RandomizeHelper.state = RandomizeHelper.State.Replace;
    }
    [HarmonyPriority(800)]
    public static void Postfix(ref RandomizeHelper.State __state) {
      RandomizeHelper.state = __state;
    }
  }
  [HarmonyPatch(typeof(LoadingCamera))]
  [HarmonyPatch("Awake")]
  [HarmonyPatch(MethodType.Normal)]
  public static class LoadingCamera_Awake {
    [HarmonyPriority(-400)]
    public static void Prefix() {
      RandomizeHelper.state = RandomizeHelper.State.Passthrough;
      RandomizeHelper.enabled = false;
    }
  }
  public class RandomizeHelper {
    public static bool enabled { get; set; } = false;
    public enum State { Passthrough, Replace, ReplaceWithStack }
    public static State state { get; set; } = State.Passthrough;
    private static System.Random rnd = new System.Random();
    public static int seed { get; private set; } = -1;
    public static void InitSeed(int seed) {
      rnd = new System.Random(seed);
      RandomizeHelper.seed = seed;
    }
    public static float Range(float min, float max) {
      if (min > max) { (min, max) = (max, min); }
      return (float)rnd.NextDouble() * (max - min) + min;
    }
    public static int Range(int min, int max) {
      if (min > max) { (min, max) = (max, min); }
      return rnd.Next(min, max);
    }
  }
  [HarmonyPatch(typeof(UnityEngine.Random))]
  [HarmonyPatch("Range")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(float), typeof(float) })]
  public static class Random_Range_float {
    public static void Prefix(float min, float max, ref float __result, ref bool __runOriginal) {
      if (RandomizeHelper.enabled == false) { return; }
      if (RandomizeHelper.state == RandomizeHelper.State.Passthrough) { return; }
      __runOriginal = false;
      __result = RandomizeHelper.Range(min, max);
    }
    public static void Postfix(float min, float max, ref float __result) {
      if (RandomizeHelper.enabled == false) { return; }
      if (RandomizeHelper.state == RandomizeHelper.State.Passthrough) { return; }
      Log.WL(0,$"Random.Range float({min},{max}) = {__result}");
      if (RandomizeHelper.state == RandomizeHelper.State.ReplaceWithStack) { Log.WL(0, Environment.StackTrace); }
    }
  }
  [HarmonyPatch(typeof(UnityEngine.Random))]
  [HarmonyPatch("Range")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(int), typeof(int) })]
  public static class Random_Range {
    public static void Prefix(int min, int max, ref int __result, ref bool __runOriginal) {
      if (RandomizeHelper.enabled == false) { return; }
      if (RandomizeHelper.state == RandomizeHelper.State.Passthrough) { return; }
      __runOriginal = false;
      __result = RandomizeHelper.Range(min, max);
    }
    public static void Postfix(int min, int max,ref int __result) {
      if (RandomizeHelper.enabled == false) { return; }
      if (RandomizeHelper.state == RandomizeHelper.State.Passthrough) { return; }
      Log.WL(0, $"Random.Range int({min},{max}) = {__result}");
      if (RandomizeHelper.state == RandomizeHelper.State.ReplaceWithStack) { Log.WL(0, Environment.StackTrace); }
    }
  }
  [HarmonyPatch(typeof(LanceOverride))]
  [HarmonyPatch("RequestLance")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(MetadataDatabase), typeof(int), typeof(DateTime?), typeof(TagSet), typeof(Contract) })]
  public static class LanceOverride_RequestLance {
    public static void Prefix(LanceOverride __instance, MetadataDatabase mdd, int requestedDifficulty, DateTime? currentDate, TagSet companyTags, Contract contract) {
      try {
        Log.TWL(0, $"LanceOverride.RequestLance contract:{(contract == null ? "null" : (contract.mapName + ":" + contract.GUID))}");
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
  [HarmonyPatch(typeof(TagSetQueryExtensions))]
  [HarmonyPatch("CanRandomlySelectUnitDef")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(MetadataDatabase), typeof(UnitDef_MDD), typeof(DateTime?), typeof(TagSet) })]
  public static class TagSetQueryExtensions_CanRandomlySelectUnitDef {
    public static void Prefix(ref bool __runOriginal, MetadataDatabase mdd, UnitDef_MDD unitDef, DateTime? currentDate, TagSet companyTags, ref bool __result) {
      try {
        __runOriginal = false;
        if (currentDate.HasValue && DebugBridge.EnforceMinAppearnceDateForUnits) {
          DateTime dateTime = currentDate.Value;
          DateTime? date = unitDef.GetDate();
          if ((date.HasValue ? (dateTime < date.GetValueOrDefault() ? 1 : 0) : 0) != 0) {
            Debug.LogWarning((object)string.Format("Rejecting unit [{0}] because currentDate[{1}] < MinAppearanceDate[{2}]", (object)unitDef.UnitDefID, (object)currentDate.Value, (object)unitDef.GetDate()));
            __result = false;
            return;
          }
        }
        if (companyTags == null || !DebugBridge.EnforceRequiredCompanyTagsForUnits || companyTags.ContainsAll(unitDef.GetRequiredToSpawnCompanyTagSet())) {
          __result = true;
          return;
        }
        Debug.LogWarning((object)string.Format("Rejecting unit [{0}] because CompanTags did not include all the required Tags[{1}]", (object)unitDef.UnitDefID, (object)unitDef.GetRequiredToSpawnCompanyTagSet()));
        __result = false;
        return;
      } catch (Exception e) {
        Debug.LogException(e);
      }
      __result = false;
    }
  }
  public static class TagSetQueryExtensions_GetMatchingUnitDefs {
    public static MethodInfo target() { return AccessTools.Method(typeof(TagSetQueryExtensions), "GetMatchingUnitDefs"); }
    private static MethodInfo original { get; set; } = null;
    public class tagItem {
      public List<string> tags = new List<string>();
      public int index = 0;
      public float weight = 0f;
      public bool is_excluded = false;
      public string tag = string.Empty;
      public tagItem(string tag, bool is_excluded, TagReplaceRule rrule) {
        this.tag = tag;
        this.is_excluded = is_excluded;
        this.weight = (rrule != null? rrule.weight : 9999f);
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
      try {
        List<UnitDef_MDD> result = new List<UnitDef_MDD>();
        result.AddRange(__result);
        if (result.Count == 0) {
          List<string> tags = new List<string>();
          tagItterator tagsVariants = new tagItterator();
          foreach (var tag in requiredTags) {
            tagItem item = null;
            if (Core.Settings.required_tags_weights.TryGetValue(tag, out var tag_info)) {
              item = new tagItem(tag, false, tag_info);
            } else {
              item = new tagItem(tag, false, null);
            }
            tagsVariants.Add(item);
          }
          foreach (var tag in excludedTags) {
            tagItem item = null;
            if (Core.Settings.excluded_tags_weights.TryGetValue(tag, out var tag_info)) {
              item = new tagItem(tag, true, tag_info);
            } else {
              item = new tagItem(tag, true, null);
            }
            tagsVariants.Add(item);
          }
          tagsVariants.items.Sort((a, b) => { return a.weight.CompareTo(b.weight); });
          foreach (var item in tagsVariants.items) {
            Log.W(1, $"tag:{item.tag}({item.weight}) type:{(item.is_excluded ? "exclude" : "require")} ");
            foreach (var ntag in item.tags) { Log.W(1, $"'{ntag}'"); }
            Log.WL(0, "");
          }
          //TagSet effective_requiredTags = new TagSet(requiredTags);
          //TagSet effective_excludedTags = new TagSet(excludedTags);
          //tagItterator tagsVariants = new tagItterator();
          int itteration = 0;
          while (tagsVariants.increment()) {
            TagSet temp_requiredTags = new TagSet();
            TagSet temp_excludedTags = new TagSet();
            foreach (var tag in tagsVariants.items) {
              if (tag.is_excluded) {
                if (string.IsNullOrEmpty(tag.curtag) == false) { temp_excludedTags.Add(tag.curtag); }
              } else {
                if (string.IsNullOrEmpty(tag.curtag) == false) { temp_requiredTags.Add(tag.curtag); }
              }
              //if (temp_requiredTags.Contains(tag.tag)) {
              //  temp_requiredTags.Remove(tag.tag);
              //  if (string.IsNullOrEmpty(tag.curtag) == false) { temp_requiredTags.Add(tag.curtag); }
              //} else if (temp_excludedTags.Contains(tag.tag)) {
              //  temp_excludedTags.Remove(tag.tag);
              //  if (string.IsNullOrEmpty(tag.curtag) == false) { temp_excludedTags.Add(tag.curtag); }
              //}
            }
            result = mdd.GetMatchingDataByTagSet<UnitDef_MDD>(TagSetType.UnitDef, temp_requiredTags, temp_excludedTags, "UnitDef", "", checkOwnership, "UnitDefID");
            result.RemoveAll((Predicate<UnitDef_MDD>)(unitDef => !mdd.CanRandomlySelectUnitDef(unitDef, currentDate, companyTags)));
            Log.WL(1, $"[{itteration}] requiredTags:{temp_requiredTags.ToString()} excludedTags:{temp_excludedTags.ToString()} result:{result.Count}", true);
            ++itteration;
            if (result.Count != 0) { break; }
          }
          __result.Clear();
          __result.AddRange(result);
        }
      }catch(Exception e) {
        Log.TWL(0,e.ToString());
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