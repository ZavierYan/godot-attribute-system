namespace AttributeSystem;

/// <summary>聚合栈贡献的类型：决定一条贡献进入哪个桶、以何种方式参与协议
/// (seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative)。桶内满足交换律，无顺序维度。</summary>
public enum ContributionKind
{
    /// <summary>平添值：表达式结果直接累加进加法池（与 seed 同段），可为负做抵消。</summary>
    Flat,

    /// <summary>加法百分比：全部同池求和得 addSum，统一以 (1 + addSum) 乘入；池内可正可负、相互抵消。</summary>
    Additive,

    /// <summary>乘法百分比：每条独立乘 (1 + m)，逐条复利、互不合并。</summary>
    Multiplicative,
}
