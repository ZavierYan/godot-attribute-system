using GdUnit4;
using static GdUnit4.Assertions;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeOwnerCloneTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    private static AttributeOwner CreatePrototype()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Strength, 0, 100));
        library.Register(new AttributeSpec(TestNames.Attack, 0, 999));
        return new AttributeOwnerBuilder(library)
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, "strength * 2")
            .Build();
    }

    [TestCase]
    public static void CloneHasSameCompositionAsPrototype()
    {
        var prototype = CreatePrototype();
        prototype.SetValue(TestNames.Strength, (AttributeValueType)10);

        var clone = prototype.Clone();
        clone.SetValue(TestNames.Strength, (AttributeValueType)10);

        AssertBool(clone.TryGet(TestNames.Strength, out var strength)).IsTrue();
        AssertBool(clone.TryGet(TestNames.Attack, out var attack)).IsTrue();
        AssertThat(strength.MinValue).IsEqual(prototype.Get(TestNames.Strength).MinValue);
        AssertThat(attack.MaxValue).IsEqual(prototype.Get(TestNames.Attack).MaxValue);
        AssertBool(strength.IsDerived).IsFalse();
        AssertBool(attack.IsDerived).IsTrue();

        // 同一表达式照常求值
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20);
    }

    [TestCase]
    public static void CloneInheritsPrototypeBaseValues()
    {
        var prototype = CreatePrototype();
        prototype.SetValue(TestNames.Strength, (AttributeValueType)42);

        var clone = prototype.Clone();

        AssertThat(clone.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)42);
        AssertThat(prototype.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)42);
    }

    [TestCase]
    public static void CloneDerivedRecalculatesFromInheritedBaseValues()
    {
        var prototype = CreatePrototype();
        prototype.SetValue(TestNames.Strength, (AttributeValueType)20);

        var clone = prototype.Clone();

        // 克隆的 attack 缓存为空，首次读取按继承的 strength 重算
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)40);
    }

    [TestCase]
    public static void CloneBaseValuesAreIndependent()
    {
        var prototype = CreatePrototype();
        var first = prototype.Clone();
        var second = prototype.Clone();

        first.SetValue(TestNames.Strength, (AttributeValueType)42);

        AssertThat(second.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)0);
        AssertThat(prototype.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public static void CloneCachesAreIndependent()
    {
        var prototype = CreatePrototype();
        prototype.SetValue(TestNames.Strength, (AttributeValueType)10);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20); // 原型缓存 20

        var clone = prototype.Clone();
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20);     // 克隆缓存为空，按继承的 strength=10 重算

        clone.SetValue(TestNames.Strength, (AttributeValueType)7);
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)14);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20); // 原型缓存不受克隆影响

        prototype.SetValue(TestNames.Strength, (AttributeValueType)11);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)22);
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)14);     // 克隆缓存不受原型影响
    }

    [TestCase]
    public static void PrototypeSubscriptionsDoNotLeakIntoClone()
    {
        var prototype = CreatePrototype();
        var prototypeEvents = 0;
        prototype.Get(TestNames.Strength).ValueChanged += (_, _) => prototypeEvents++;

        var clone = prototype.Clone();
        var cloneEvents = 0;
        clone.Get(TestNames.Strength).ValueChanged += (_, _) => cloneEvents++;

        clone.SetValue(TestNames.Strength, (AttributeValueType)10);

        AssertThat(cloneEvents).IsEqual(1);
        AssertThat(prototypeEvents).IsEqual(0);

        prototype.SetValue(TestNames.Strength, (AttributeValueType)10);

        AssertThat(prototypeEvents).IsEqual(1);
        AssertThat(cloneEvents).IsEqual(1);
    }

    [TestCase]
    public static void InvalidationIsIsolatedPerInstance()
    {
        var prototype = CreatePrototype();
        prototype.SetValue(TestNames.Strength, (AttributeValueType)10);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20);

        var clone = prototype.Clone();
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20); // 按继承的 strength 重算

        var prototypeAttackEvents = 0;
        var cloneAttackEvents = 0;
        prototype.Get(TestNames.Attack).ValueChanged += (_, _) => prototypeAttackEvents++;
        clone.Get(TestNames.Attack).ValueChanged += (_, _) => cloneAttackEvents++;

        // 只触发原型的失效传播；克隆的 attack 不失效（缓存仍为 20）
        prototype.SetValue(TestNames.Strength, (AttributeValueType)15);

        AssertThat(prototypeAttackEvents).IsEqual(1);
        AssertThat(cloneAttackEvents).IsEqual(0);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)30);
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20);
    }

    [TestCase]
    public static void CloneCanBeClonedAgain()
    {
        var prototype = CreatePrototype();
        prototype.SetValue(TestNames.Strength, (AttributeValueType)42);

        var clone = prototype.Clone();
        clone.SetValue(TestNames.Strength, (AttributeValueType)7);

        var cloneOfClone = clone.Clone();

        // 再克隆继承该克隆彼时的当前值，构成与表达式不变
        AssertThat(cloneOfClone.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)7);
        cloneOfClone.SetValue(TestNames.Strength, (AttributeValueType)10);
        AssertThat(cloneOfClone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20);
        AssertThat(clone.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)7);
    }
}
