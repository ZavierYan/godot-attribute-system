using GdUnit4;
using static GdUnit4.Assertions;

namespace AttributeSystem.Tests;

[TestSuite]
public class DerivedAttributeTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Strength, 0, 100));
        library.Register(new AttributeSpec(TestNames.Dexterity, 0, 100));
        library.Register(new AttributeSpec("luck"));
        library.Register(new AttributeSpec(TestNames.Vitality, 0, 100));
        library.Register(new AttributeSpec(TestNames.Attack, 0, 999));
        library.Register(new AttributeSpec(TestNames.Total));
        library.Register(new AttributeSpec("net"));
        library.Register(new AttributeSpec("debuff"));
        library.Register(new AttributeSpec("deficit", 0, 100));
        library.Register(new AttributeSpec("cappedAttack", 0, 50));
        return library;
    }

    [TestCase]
    public static void SimpleArithmeticExpressionComputesFromDependencies()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Attack, "strength+0.5*dexterity")
            .Require(TestNames.Strength)
            .Require(TestNames.Dexterity)
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)10);
        owner.SetValue(TestNames.Dexterity, (AttributeValueType)6);

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)13);
    }

    [TestCase]
    public static void DerivedCanReferenceDerived()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Require(TestNames.Total, "attack + 5")
            .Require(TestNames.Strength)
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)10);

        // attack = strength * 2 = 20；total = attack + 5 = 25
        AssertThat(owner.GetValue(TestNames.Total)).IsEqual((AttributeValueType)25);
    }

    [TestCase]
    public static void BuiltInFunctionIsAvailableInExpression()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("net", "Max(attack - debuff, 0)")
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Require("debuff")
            .Require(TestNames.Strength)
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)10);   // attack = 20
        owner.SetValue("debuff", (AttributeValueType)30);

        AssertThat(owner.GetValue("net")).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public static void DerivedIsLazyAndCachedPerInvalidation()
    {
        var evaluations = 0;
        var library = CreateLibrary();
        library.Register(new AttributeSpec(TestNames.Tracked, 0, 100));

        var owner = new AttributeOwnerBuilder(library)
            .Require(TestNames.Tracked, "vitality * 2")
            .Require(TestNames.Vitality)
            .Build();
        owner.SetValue(TestNames.Vitality, (AttributeValueType)10);

        // 换上带计数的等价求值器（内部装配缝，仅作观测；失效图仍由表达式依赖决定）
        owner.Get(TestNames.Tracked).BindEvaluator(() =>
        {
            evaluations++;
            return owner.GetValue(TestNames.Vitality) * (AttributeValueType)2;
        });

        // 从未读取：不求值
        AssertThat(evaluations).IsEqual(0);

        // 首次读取求值一次；连续读取走缓存
        AssertThat(owner.GetValue(TestNames.Tracked)).IsEqual((AttributeValueType)20);
        AssertThat(owner.GetValue(TestNames.Tracked)).IsEqual((AttributeValueType)20);
        AssertThat(evaluations).IsEqual(1);

        // 失效本身不重算；下次读取才重算一次
        owner.SetValue(TestNames.Vitality, (AttributeValueType)20);
        AssertThat(evaluations).IsEqual(1);
        AssertThat(owner.GetValue(TestNames.Tracked)).IsEqual((AttributeValueType)40);
        AssertThat(evaluations).IsEqual(2);
        AssertThat(owner.GetValue(TestNames.Tracked)).IsEqual((AttributeValueType)40);
        AssertThat(evaluations).IsEqual(2);
    }

    [TestCase]
    public static void InvalidationPropagatesTransitively()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Require(TestNames.Total, "attack + 5")
            .Require(TestNames.Strength)
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)10);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20);
        AssertThat(owner.GetValue(TestNames.Total)).IsEqual((AttributeValueType)25);

        owner.SetValue(TestNames.Strength, (AttributeValueType)15);

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)30);
        AssertThat(owner.GetValue(TestNames.Total)).IsEqual((AttributeValueType)35);
    }

    [TestCase]
    public static void UnrelatedAttributeIsUnaffected()
    {
        var evaluations = 0;
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Attack, "strength+0.5*dexterity")
            .Require(TestNames.Strength)
            .Require(TestNames.Dexterity)
            .Require("luck")
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)10);
        owner.SetValue(TestNames.Dexterity, (AttributeValueType)6);

        // 计数求值器观测缓存行为（失效图仍为表达式声明的 {strength, dexterity}）
        owner.Get(TestNames.Attack).BindEvaluator(() =>
        {
            evaluations++;
            return owner.GetValue(TestNames.Strength) * (AttributeValueType)2 - owner.GetValue(TestNames.Dexterity);
        });

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)14);
        AssertThat(evaluations).IsEqual(1);

        // 无关属性变化：attack 缓存不受影响，不重算
        owner.SetValue("luck", (AttributeValueType)7);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)14);
        AssertThat(evaluations).IsEqual(1);

        // 依赖之一变化：才重新求值
        owner.SetValue(TestNames.Dexterity, (AttributeValueType)8);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)12);
        AssertThat(evaluations).IsEqual(2);
    }

    [TestCase]
    public static void DerivedResultIsClampedToMaximum()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("cappedAttack", TestNames.StrengthTimesTwo)
            .Require(TestNames.Strength)
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)40);

        // strength*2 = 80，但上限 50
        AssertThat(owner.GetValue("cappedAttack")).IsEqual((AttributeValueType)50);
    }

    [TestCase]
    public static void DerivedResultIsClampedToMinimum()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("deficit", "vitality - 100")
            .Require(TestNames.Vitality)
            .Build();

        // vitality(0) - 100 = -100，钳到下限 0
        AssertThat(owner.GetValue("deficit")).IsEqual((AttributeValueType)0);
    }
}
