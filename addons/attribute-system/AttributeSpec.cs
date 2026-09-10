namespace AttributeSystem;

/// <summary>
/// 以不可变数据描述一个属性的静态构成：身份（<see cref="AttributeName"/>）与约束（最值）。
/// <para><see cref="AttributeName"/>：属性在 <see cref="AttributeOwner"/> 中的名称，也是表达式
/// 引用其它属性时使用的标识——owner 运行时与表达式只认 AttributeName，且其在
/// <see cref="AttributeSpecLibrary"/> 内唯一。</para>
/// <para>spec 不携带计算表达式：一个属性在某次构建中是 base 还是 derived，
/// 由 <see cref="AttributeOwnerBuilder"/> 构建时的表达式声明决定。</para>
/// </summary>
public sealed class AttributeSpec
{
    /// <summary>属性在 owner 中的名称，库内唯一。</summary>
    public string AttributeName { get; }

    /// <summary>下限，未指定时为别名类型极值（钳制退化为 no-op）。</summary>
    public AttributeValueType MinValue { get; }

    /// <summary>上限，未指定时为别名类型极值（钳制退化为 no-op）。</summary>
    public AttributeValueType MaxValue { get; }

    public AttributeSpec(string attributeName,
        AttributeValueType? minValue = null, AttributeValueType? maxValue = null)
    {
        Guard.NotBlank(attributeName, nameof(attributeName));

        var min = minValue ?? NumberOps<AttributeValueType>.TypeMin;
        var max = maxValue ?? NumberOps<AttributeValueType>.TypeMax;
        if (min > max)
            throw new ArgumentException($"MinValue({min}) 不能大于 MaxValue({max})。", nameof(minValue));

        AttributeName = attributeName.Trim();
        MinValue = min;
        MaxValue = max;
    }
}
