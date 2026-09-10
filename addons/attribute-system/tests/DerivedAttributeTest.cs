using GdUnit4;
using static GdUnit4.Assertions;

namespace AttributeSystem.Tests;

[TestSuite]
public class DerivedAttributeTest
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("strength", 0, 100));
        library.Register(new AttributeSpec("dexterity", 0, 100));
        library.Register(new AttributeSpec("luck"));
        library.Register(new AttributeSpec("vitality", 0, 100));
        library.Register(new AttributeSpec("attack", 0, 999));
        library.Register(new AttributeSpec("total"));
        library.Register(new AttributeSpec("net"));
        library.Register(new AttributeSpec("debuff"));
        library.Register(new AttributeSpec("deficit", 0, 100));
        library.Register(new AttributeSpec("cappedAttack", 0, 50));
        return library;
    }

    [TestCase]
    public void SimpleArithmeticExpressionComputesFromDependencies()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("attack", "strength+0.5*dexterity")
            .Require("strength")
            .Require("dexterity")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);
        owner.SetValue("dexterity", (AttributeValueType)6);

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)13);
    }

    [TestCase]
    public void DerivedCanReferenceDerived()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("attack", "strength * 2")
            .Require("total", "attack + 5")
            .Require("strength")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);

        // attack = strength * 2 = 20；total = attack + 5 = 25
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)25);
    }

    [TestCase]
    public void BuiltInFunctionIsAvailableInExpression()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("net", "Max(attack - debuff, 0)")
            .Require("attack", "strength * 2")
            .Require("debuff")
            .Require("strength")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);   // attack = 20
        owner.SetValue("debuff", (AttributeValueType)30);

        AssertThat(owner.GetValue("net")).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public void DerivedIsLazyAndCachedPerInvalidation()
    {
        var evaluations = 0;
        var library = CreateLibrary();
        library.Register(new AttributeSpec("tracked", 0, 100));

        var owner = new AttributeOwnerBuilder(library)
            .Require("tracked", "vitality * 2")
            .Require("vitality")
            .Build();
        owner.SetValue("vitality", (AttributeValueType)10);

        // 换上带计数的等价求值器（内部装配缝，仅作观测；失效图仍由表达式依赖决定）
        owner.Get("tracked").BindEvaluator(() =>
        {
            evaluations++;
            return owner.GetValue("vitality") * (AttributeValueType)2;
        });

        // 从未读取：不求值
        AssertThat(evaluations).IsEqual(0);

        // 首次读取求值一次；连续读取走缓存
        AssertThat(owner.GetValue("tracked")).IsEqual((AttributeValueType)20);
        AssertThat(owner.GetValue("tracked")).IsEqual((AttributeValueType)20);
        AssertThat(evaluations).IsEqual(1);

        // 失效本身不重算；下次读取才重算一次
        owner.SetValue("vitality", (AttributeValueType)20);
        AssertThat(evaluations).IsEqual(1);
        AssertThat(owner.GetValue("tracked")).IsEqual((AttributeValueType)40);
        AssertThat(evaluations).IsEqual(2);
        AssertThat(owner.GetValue("tracked")).IsEqual((AttributeValueType)40);
        AssertThat(evaluations).IsEqual(2);
    }

    [TestCase]
    public void InvalidationPropagatesTransitively()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("attack", "strength * 2")
            .Require("total", "attack + 5")
            .Require("strength")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)20);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)25);

        owner.SetValue("strength", (AttributeValueType)15);

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)30);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)35);
    }

    [TestCase]
    public void UnrelatedAttributeIsUnaffected()
    {
        var evaluations = 0;
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("attack", "strength+0.5*dexterity")
            .Require("strength")
            .Require("dexterity")
            .Require("luck")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);
        owner.SetValue("dexterity", (AttributeValueType)6);

        // 计数求值器观测缓存行为（失效图仍为表达式声明的 {strength, dexterity}）
        owner.Get("attack").BindEvaluator(() =>
        {
            evaluations++;
            return owner.GetValue("strength") * (AttributeValueType)2 - owner.GetValue("dexterity");
        });

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)14);
        AssertThat(evaluations).IsEqual(1);

        // 无关属性变化：attack 缓存不受影响，不重算
        owner.SetValue("luck", (AttributeValueType)7);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)14);
        AssertThat(evaluations).IsEqual(1);

        // 依赖之一变化：才重新求值
        owner.SetValue("dexterity", (AttributeValueType)8);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)12);
        AssertThat(evaluations).IsEqual(2);
    }

    [TestCase]
    public void DerivedResultIsClampedToMaximum()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("cappedAttack", "strength * 2")
            .Require("strength")
            .Build();
        owner.SetValue("strength", (AttributeValueType)40);

        // strength*2 = 80，但上限 50
        AssertThat(owner.GetValue("cappedAttack")).IsEqual((AttributeValueType)50);
    }

    [TestCase]
    public void DerivedResultIsClampedToMinimum()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("deficit", "vitality - 100")
            .Require("vitality")
            .Build();

        // vitality(0) - 100 = -100，钳到下限 0
        AssertThat(owner.GetValue("deficit")).IsEqual((AttributeValueType)0);
    }
}
