using GdUnit4;
using static GdUnit4.Assertions;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeOwnerCloneTest
{
    private static AttributeOwner CreatePrototype()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("strength", 0, 100));
        library.Register(new AttributeSpec("attack", 0, 999));
        return new AttributeOwnerBuilder(library)
            .Require("strength")
            .Require("attack", "strength * 2")
            .Build();
    }

    [TestCase]
    public void CloneHasSameCompositionAsPrototype()
    {
        var prototype = CreatePrototype();
        prototype.SetValue("strength", (AttributeValueType)10);

        var clone = prototype.Clone();
        clone.SetValue("strength", (AttributeValueType)10);

        AssertBool(clone.TryGet("strength", out var strength)).IsTrue();
        AssertBool(clone.TryGet("attack", out var attack)).IsTrue();
        AssertThat(strength!.MinValue).IsEqual(prototype.Get("strength").MinValue);
        AssertThat(attack!.MaxValue).IsEqual(prototype.Get("attack").MaxValue);
        AssertBool(strength.IsDerived).IsFalse();
        AssertBool(attack!.IsDerived).IsTrue();

        // 同一表达式照常求值
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)20);
    }

    [TestCase]
    public void CloneInheritsPrototypeBaseValues()
    {
        var prototype = CreatePrototype();
        prototype.SetValue("strength", (AttributeValueType)42);

        var clone = prototype.Clone();

        AssertThat(clone.GetValue("strength")).IsEqual((AttributeValueType)42);
        AssertThat(prototype.GetValue("strength")).IsEqual((AttributeValueType)42);
    }

    [TestCase]
    public void CloneDerivedRecalculatesFromInheritedBaseValues()
    {
        var prototype = CreatePrototype();
        prototype.SetValue("strength", (AttributeValueType)20);

        var clone = prototype.Clone();

        // 克隆的 attack 缓存为空，首次读取按继承的 strength 重算
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)40);
    }

    [TestCase]
    public void CloneBaseValuesAreIndependent()
    {
        var prototype = CreatePrototype();
        var first = prototype.Clone();
        var second = prototype.Clone();

        first.SetValue("strength", (AttributeValueType)42);

        AssertThat(second.GetValue("strength")).IsEqual((AttributeValueType)0);
        AssertThat(prototype.GetValue("strength")).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public void CloneCachesAreIndependent()
    {
        var prototype = CreatePrototype();
        prototype.SetValue("strength", (AttributeValueType)10);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)20); // 原型缓存 20

        var clone = prototype.Clone();
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)20);     // 克隆缓存为空，按继承的 strength=10 重算

        clone.SetValue("strength", (AttributeValueType)7);
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)14);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)20); // 原型缓存不受克隆影响

        prototype.SetValue("strength", (AttributeValueType)11);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)22);
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)14);     // 克隆缓存不受原型影响
    }

    [TestCase]
    public void PrototypeSubscriptionsDoNotLeakIntoClone()
    {
        var prototype = CreatePrototype();
        var prototypeEvents = 0;
        prototype.Get("strength").ValueChanged += (_, _) => prototypeEvents++;

        var clone = prototype.Clone();
        var cloneEvents = 0;
        clone.Get("strength").ValueChanged += (_, _) => cloneEvents++;

        clone.SetValue("strength", (AttributeValueType)10);

        AssertThat(cloneEvents).IsEqual(1);
        AssertThat(prototypeEvents).IsEqual(0);

        prototype.SetValue("strength", (AttributeValueType)10);

        AssertThat(prototypeEvents).IsEqual(1);
        AssertThat(cloneEvents).IsEqual(1);
    }

    [TestCase]
    public void InvalidationIsIsolatedPerInstance()
    {
        var prototype = CreatePrototype();
        prototype.SetValue("strength", (AttributeValueType)10);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)20);

        var clone = prototype.Clone();
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)20); // 按继承的 strength 重算

        var prototypeAttackEvents = 0;
        var cloneAttackEvents = 0;
        prototype.Get("attack").ValueChanged += (_, _) => prototypeAttackEvents++;
        clone.Get("attack").ValueChanged += (_, _) => cloneAttackEvents++;

        // 只触发原型的失效传播；克隆的 attack 不失效（缓存仍为 20）
        prototype.SetValue("strength", (AttributeValueType)15);

        AssertThat(prototypeAttackEvents).IsEqual(1);
        AssertThat(cloneAttackEvents).IsEqual(0);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)30);
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)20);
    }

    [TestCase]
    public void CloneCanBeClonedAgain()
    {
        var prototype = CreatePrototype();
        prototype.SetValue("strength", (AttributeValueType)42);

        var clone = prototype.Clone();
        clone.SetValue("strength", (AttributeValueType)7);

        var cloneOfClone = clone.Clone();

        // 再克隆继承该克隆彼时的当前值，构成与表达式不变
        AssertThat(cloneOfClone.GetValue("strength")).IsEqual((AttributeValueType)7);
        cloneOfClone.SetValue("strength", (AttributeValueType)10);
        AssertThat(cloneOfClone.GetValue("attack")).IsEqual((AttributeValueType)20);
        AssertThat(clone.GetValue("strength")).IsEqual((AttributeValueType)7);
    }
}
