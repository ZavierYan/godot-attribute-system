namespace AttributeSystem;

/// <summary>
/// 以不可变数据描述一个属性。
/// <para><see cref="SpecName"/>：在 <see cref="AttributeSpecLibrary"/> 中注册/检索的唯一键，
/// 供 <see cref="AttributeOwnerBuilder"/> 构建期选定定义使用。</para>
/// <para><see cref="AttributeName"/>：属性在 <see cref="AttributeOwner"/> 中的名称，也是表达式
/// 引用其它属性时使用的标识——owner 运行时与表达式只认 AttributeName。多个 spec 允许共享
/// 同一 AttributeName，以在不同场景下为同名属性提供不同计算方式。</para>
/// <para><see cref="Expression"/> 非空即定义 derived 属性；为 <c>null</c> 定义 base 属性。</para>
/// </summary>
public sealed class AttributeSpec
{
    /// <summary>spec 的注册/检索键，库内唯一。</summary>
    public string SpecName { get; }

    /// <summary>属性在 owner 中的名称，库内可重复。</summary>
    public string AttributeName { get; }

    /// <summary>下限，未指定时为别名类型极值（钳制退化为 no-op）。</summary>
    public AttributeValueType MinValue { get; }

    /// <summary>上限，未指定时为别名类型极值（钳制退化为 no-op）。</summary>
    public AttributeValueType MaxValue { get; }

    /// <summary>字符串计算表达式；同时声明计算方法与依赖关系。base 属性为 <c>null</c>。</summary>
    public string? Expression { get; }

    /// <summary>是否为 derived 属性。</summary>
    public bool IsDerived => Expression is not null;

    public AttributeSpec(string specName, string attributeName,
        AttributeValueType? minValue = null, AttributeValueType? maxValue = null,
        string? expression = null)
    {
        Guard.NotBlank(specName, nameof(specName));
        Guard.NotBlank(attributeName, nameof(attributeName));
        if (expression is not null && string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression 若提供则不能为空或纯空白字符。", nameof(expression));

        var min = minValue ?? NumberOps<AttributeValueType>.TypeMin;
        var max = maxValue ?? NumberOps<AttributeValueType>.TypeMax;
        if (min > max)
            throw new ArgumentException($"MinValue({min}) 不能大于 MaxValue({max})。", nameof(minValue));

        SpecName = specName.Trim();
        AttributeName = attributeName.Trim();
        MinValue = min;
        MaxValue = max;
        Expression = expression?.Trim();
    }
}
