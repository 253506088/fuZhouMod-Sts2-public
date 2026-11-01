using System.Reflection;
using HarmonyLib;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>旧游戏追加 Mod 存档字段后补足联网编号位数，避免字段编号被截断。</summary>
[HarmonyPatch]
internal static class SavedPropertyNetIdPatch
{
    /// <summary>新版不存在旧接口时跳过整个补丁类，避免中断后续补丁的注册。</summary>
    private static bool Prepare()
        => AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") != null;

    /// <summary>只适配正式版旧缓存入口；新版由游戏自己的序列化缓存处理。</summary>
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var cacheType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache");
        if (cacheType == null) yield break;
        var method = AccessTools.DeclaredMethod(cacheType, "CachePropertiesForType", [typeof(Type)]);
        if (method != null) yield return method;
    }

    /// <summary>保留所有已有编号，只在新增字段超出编码容量时增加位数。</summary>
    private static void Postfix(MethodBase __originalMethod)
    {
        var cacheType = __originalMethod.DeclaringType;
        if (cacheType == null || AccessTools.Field(cacheType, "_netIdToPropertyNameMap")?.GetValue(null) is not List<string> names
            || names.Count == 0) return;
        var bitSize = AccessTools.Property(cacheType, "NetIdBitSize");
        if (bitSize?.GetValue(null) is not int current) return;
        int required = (int)Math.Ceiling(Math.Log2(Math.Max(2, names.Count)));
        if (required <= current) return;
        bitSize.SetValue(null, required);
        MainFile.Logger.Info($"【联机存档】旧版字段编号编码扩容：字段数={names.Count}，位数={current}→{required}；已有编号保持不变。");
    }
}
