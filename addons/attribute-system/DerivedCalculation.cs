namespace AttributeSystem;

/// <summary>一条 derived 属性的求值方案：依赖集合与统一求值委托。
/// builder 构建期产出；owner 装配时绑定并持有；克隆时经 owner 对新实例重放绑定。</summary>
internal sealed record DerivedCalculation(IReadOnlyList<string> Dependencies, AttributeCalculator Calculator);
