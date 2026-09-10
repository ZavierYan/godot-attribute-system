namespace AttributeSystem;

/// <summary>属性名的比较语义：全插件统一 Ordinal 精确匹配，收敛于此。</summary>
internal static class AttributeNames
{
    internal static readonly StringComparer Comparer = StringComparer.Ordinal;
}
