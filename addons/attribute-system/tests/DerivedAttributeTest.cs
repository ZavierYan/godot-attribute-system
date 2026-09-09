using GdUnit4;
using static GdUnit4.Assertions;

namespace AttributeSystem.Tests;

[TestSuite]
public class DerivedAttributeTest
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("hero-strength", "strength", 0, 100));
        library.Register(new AttributeSpec("hero-dexterity", "dexterity", 0, 100));
        library.Register(new AttributeSpec("hero-luck", "luck"));
        library.Register(new AttributeSpec("hero-vitality", "vitality", 0, 100));
        library.Register(new AttributeSpec("warrior-attack", "attack", expression: "strength * 2"));
        library.Register(new AttributeSpec("combined-attack", "attack", expression: "strength+0.5*dexterity"));
        library.Register(new AttributeSpec("hero-total", "total", expression: "attack + 5"));
        library.Register(new AttributeSpec("hero-net", "net", expression: "Max(attack - debuff, 0)"));
        library.Register(new AttributeSpec("hero-debuff", "debuff"));
        library.Register(new AttributeSpec("hero-deficit", "deficit", 0, 100, expression: "vitality - 100"));
        library.Register(new AttributeSpec("capped-attack", "cappedAttack", 0, 50, expression: "strength * 2"));
        return library;
    }

    [TestCase]
    public void SimpleArithmeticExpressionComputesFromDependencies()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("combined-attack")
            .Require("hero-strength")
            .Require("hero-dexterity")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);
        owner.SetValue("dexterity", (AttributeValueType)6);

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)13);
    }

    [TestCase]
    public void DerivedCanReferenceDerived()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("warrior-attack")
            .Require("hero-total")
            .Require("hero-strength")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);

        // attack = strength * 2 = 20；total = attack + 5 = 25
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)25);
    }

    [TestCase]
    public void BuiltInFunctionIsAvailableInExpression()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("hero-net")
            .Require("warrior-attack")
            .Require("hero-debuff")
            .Require("hero-strength")
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
        library.Register(new AttributeSpec("tracked", "tracked", 0, 100, expression: "vitality * 2"));

        var owner = new AttributeOwnerBuilder(library)
            .Require("tracked")
            .Require("hero-vitality")
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
            .Require("warrior-attack")
            .Require("hero-total")
            .Require("hero-strength")
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
            .Require("combined-attack")
            .Require("hero-strength")
            .Require("hero-dexterity")
            .Require("hero-luck")
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
            .Require("capped-attack")
            .Require("hero-strength")
            .Build();
        owner.SetValue("strength", (AttributeValueType)40);

        // strength*2 = 80，但上限 50
        AssertThat(owner.GetValue("cappedAttack")).IsEqual((AttributeValueType)50);
    }

    [TestCase]
    public void DerivedResultIsClampedToMinimum()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("hero-deficit")
            .Require("hero-vitality")
            .Build();

        // vitality(0) - 100 = -100，钳到下限 0
        AssertThat(owner.GetValue("deficit")).IsEqual((AttributeValueType)0);
    }
}
