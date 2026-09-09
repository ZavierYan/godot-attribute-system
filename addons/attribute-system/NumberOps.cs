using System.Numerics;

namespace AttributeSystem;

/// <summary>
/// 基于泛型数学的数值工具。值类型别名指向的类型必须满足 INumber + IMinMaxValue
/// 约束——这是"用户只能配置数值类型"的编译期门槛。
/// </summary>
internal static class NumberOps<T> where T : INumber<T>, IMinMaxValue<T>
{
    /// <summary>别名类型的下极值（spec 未指定下限时使用）。</summary>
    public static T TypeMin => T.MinValue;

    /// <summary>别名类型的上极值（spec 未指定上限时使用）。</summary>
    public static T TypeMax => T.MaxValue;

    /// <summary>把 value 钳制到 [min, max] 区间。</summary>
    public static T Clamp(T value, T min, T max) => T.Clamp(value, min, max);
}
