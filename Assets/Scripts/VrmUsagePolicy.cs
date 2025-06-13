using UnityEngine;
using UniVRM10;
using UniGLTF.Extensions.VRMC_vrm;

public class VrmUsagePolicy
{
    public static VrmUsagePolicy Instance { get; } = new VrmUsagePolicy();

    public bool IsSexualExpressionAllowed { get; private set; } = true;
    public bool IsViolentExpressionAllowed { get; private set; } = true;
    public bool IsRedistributionAllowed { get; private set; } = false;
    public bool IsCommercialUsageAllowed { get; private set; } = false;

    public VRM10ObjectMeta CurrentMeta { get; private set; }

    private VrmUsagePolicy() {}

    public void UpdateFromMeta(VRM10ObjectMeta meta)
    {
        if (meta == null)
        {
            Debug.LogError("❌ VrmUsagePolicy.UpdateFromMeta: meta が null なの！");
            return;
        }

        CurrentMeta = meta;

        IsSexualExpressionAllowed = meta.SexualUsage;
        IsViolentExpressionAllowed = meta.ViolentUsage;
        IsRedistributionAllowed = meta.Redistribution;

        // ✅ 商用利用が「個人の営利」or「法人」なら許可
        IsCommercialUsageAllowed =
            meta.CommercialUsage == CommercialUsageType.personalProfit ||
            meta.CommercialUsage == CommercialUsageType.corporation;

        Debug.Log($"[VrmUsagePolicy] 性表現: {IsSexualExpressionAllowed}, 暴力: {IsViolentExpressionAllowed}, 再配布: {IsRedistributionAllowed}, 商用: {IsCommercialUsageAllowed}");
    }
}
