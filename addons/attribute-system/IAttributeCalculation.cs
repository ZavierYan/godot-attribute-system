namespace AttributeSystem;

/// <summary>一条 derived 属性的统一求值方案契约：依赖集合与统一求值委托。
/// plain 表达式（<see cref="DerivedCalculation"/>）与聚合栈（<see cref="StackedCalculation"/>）
/// 两形态实现同一签名，owner 的装配、求值、环检测、引用检查对形态无感知；
/// 仅贡献守卫处做一次类型判别。</summary>
internal interface IAttributeCalculation
{
    /// <summary>本计算声明的全部依赖属性名。plain 形态构建后不变；
    /// 聚合栈为随贡献增撤全量重算的动态并集。</summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>统一求值委托：接收依赖属性当前值，返回未钳制原始值（钳制由 Attribute 统一执行）。</summary>
    AttributeCalculator Calculator { get; }
}
