using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeOwnerTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Strength, 0, 100));
        library.Register(new AttributeSpec(TestNames.Dexterity, 0, 100));
        library.Register(new AttributeSpec("intelligence", 0, 100));
        library.Register(new AttributeSpec(TestNames.Attack, 0, 999));
        return library;
    }

    // ---- 构建成功 ----

    [TestCase]
    public static void BuildCreatesOwnerAccessibleByAttributeName()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Dexterity)
            .Require(TestNames.Attack, TestNames.StrengthPlusHalfDexterity)
            .Build();

        AssertBool(owner.TryGet(TestNames.Strength, out var strength)).IsTrue();
        AssertBool(owner.TryGet(TestNames.Dexterity, out _)).IsTrue();
        AssertBool(owner.TryGet(TestNames.Attack, out var attack)).IsTrue();
        AssertBool(strength.IsDerived).IsFalse();
        AssertBool(attack.IsDerived).IsTrue();
    }

    [TestCase]
    public static void SameAttributeNameWithDifferentExpressionsBuildsSeparately()
    {
        var library = CreateLibrary();

        var warrior = new AttributeOwnerBuilder(library)
            .Require(TestNames.Strength)
            .Require(TestNames.Dexterity)
            .Require(TestNames.Attack, TestNames.StrengthPlusHalfDexterity)
            .Build();
        var mage = new AttributeOwnerBuilder(library)
            .Require("intelligence")
            .Require(TestNames.Attack, "intelligence*1.2")
            .Build();

        warrior.SetValue(TestNames.Strength, (AttributeValueType)10);
        warrior.SetValue(TestNames.Dexterity, (AttributeValueType)6);
        mage.SetValue("intelligence", (AttributeValueType)10);

        AssertThat(warrior.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)13);
        AssertThat(mage.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)12);
    }

    [TestCase]
    public static void DuplicateBaseDeclarationIsIdempotent()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Strength)
            .Build();

        AssertBool(owner.TryGet(TestNames.Strength, out _)).IsTrue();
        AssertThat(owner.Attributes.Count()).IsEqual(1);
    }

    // ---- 构建期错误 ----

    [TestCase]
    public static void BuildWithMissingSpecThrowsWithName()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary()).Require("hero-mana").Build());

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public static void BuildWithUnknownExpressionReferenceThrowsWithExpressionAndName()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, TestNames.StrengthPlusHalfDexterity)
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Dexterity);
        AssertThat(caught.Message).Contains(TestNames.StrengthPlusHalfDexterity);
    }

    [TestCase]
    public static void BuildWithCycleThrowsWithCycleChain()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("a"));
        library.Register(new AttributeSpec("b"));

        var caught = Catch(() => _ = new AttributeOwnerBuilder(library)
            .Require("a", "b + 1")
            .Require("b", "a + 1")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("循环依赖");
        AssertThat(caught.Message).Contains("a");
        AssertThat(caught.Message).Contains("b");
    }

    [TestCase]
    public static void BuildWithSelfReferenceThrowsWithCycleChain()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("x"));

        var caught = Catch(() => _ = new AttributeOwnerBuilder(library)
            .Require("x", "x + 1")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("循环依赖");
        AssertThat(caught.Message).Contains("x");
    }

    [TestCase]
    public static void BuildWithoutAnyRequirementThrows()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary()).Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public static void BuildWithSyntaxErrorThrowsAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, "strength *")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength *");
    }

    // ---- 初值声明 ----

    [TestCase]
    public static void BaseMemberWithInitialValueStartsFromIt()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, (AttributeValueType)15)
            .Build();

        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public static void OutOfRangeInitialValueIsClamped()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, -(AttributeValueType)5)
            .Build();

        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public static void MergeOfValuelessAndValuedDeclarationsTakesExplicitValueInAnyOrder()
    {
        var first = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Strength, (AttributeValueType)20)
            .Build();
        var second = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, (AttributeValueType)20)
            .Require(TestNames.Strength)
            .Build();

        AssertBool(first.TryGet(TestNames.Strength, out _)).IsTrue();
        AssertThat(first.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)20);
        AssertThat(second.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)20);
    }

    [TestCase]
    public static void DuplicateIdenticalInitialValueDeclarationIsIdempotent()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, (AttributeValueType)15)
            .Require(TestNames.Strength, (AttributeValueType)15)
            .Build();

        AssertBool(owner.TryGet(TestNames.Strength, out _)).IsTrue();
        AssertThat(owner.Attributes.Count()).IsEqual(1);
        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public static void DuplicateNaNInitialValueDeclarationsAreIdempotent()
    {
        // 初值一致性比较经 EqualityComparer<T>.Default：NaN 与 NaN 视为一致而非矛盾初值。
        // 断言依赖浮点别名（NaN 只在 float/double 别名下存在）。
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, AttributeValueType.NaN)
            .Require(TestNames.Strength, AttributeValueType.NaN)
            .Build();

        AssertBool(owner.TryGet(TestNames.Strength, out _)).IsTrue();
        AssertThat(owner.Attributes.Count()).IsEqual(1);
        AssertBool(float.IsNaN(owner.GetValue(TestNames.Strength))).IsTrue();
    }

    [TestCase]
    public static void ConflictingInitialValuesThrowAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, (AttributeValueType)15)
            .Require(TestNames.Strength, (AttributeValueType)20)
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Strength);
        AssertThat(caught.Message).Contains("冲突");
    }

    // ---- 声明冲突校验 ----

    [TestCase]
    public static void MixedBaseAndDerivedDeclarationThrowsAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack)
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public static void MixedDeclarationInReverseOrderThrowsAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Require(TestNames.Attack)
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public static void DuplicateDerivedDeclarationThrowsAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Require(TestNames.Attack, "strength * 3")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public static void DuplicateIdenticalExpressionDeclarationThrowsAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public static void BlankExpressionThrowsAtDeclaration()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, "   "));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("expression");
    }

    // ---- 聚合栈声明 ----

    [TestCase]
    public static void StackedBuildWithoutContributionsReadsClampedSeedValue()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Strength, 0, 100));
        library.Register(new AttributeSpec(TestNames.Attack, 0, 50));

        var owner = new AttributeOwnerBuilder(library)
            .Require(TestNames.Strength)
            .RequireStacked(TestNames.Attack, TestNames.Strength)
            .Build();

        owner.SetValue(TestNames.Strength, (AttributeValueType)60);

        AssertBool(owner.Get(TestNames.Attack).IsDerived).IsTrue();
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)50);
    }

    [TestCase]
    public static void DuplicateIdenticalSeedDeclarationIsIdempotent()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .RequireStacked(TestNames.Attack, TestNames.StrengthTimesTwo)
            .RequireStacked(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Build();

        AssertBool(owner.TryGet(TestNames.Attack, out _)).IsTrue();
        AssertThat(owner.Attributes.Count()).IsEqual(2);
    }

    [TestCase]
    public static void MixedPlainAndStackedDeclarationThrowsAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, TestNames.StrengthTimesTwo)
            .RequireStacked(TestNames.Attack, TestNames.StrengthTimesTwo)
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public static void DifferentSeedDeclarationsThrowAtBuild()
    {
        var caught = Catch(() => _ = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .RequireStacked(TestNames.Attack, TestNames.StrengthTimesTwo)
            .RequireStacked(TestNames.Attack, "strength * 3")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
        AssertThat(caught.Message).Contains("冲突");
    }

    // ---- 运行时访问 ----

    [TestCase]
    public static void GetMissingAttributeNameThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require(TestNames.Strength).Build();

        var caught = Catch(() => owner.Get("mana"));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("mana");
    }

    [TestCase]
    public static void BaseValueInitializesToClampedDefault()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require(TestNames.Strength).Build();

        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)0);
    }

    // ---- base 读写与钳制 ----

    [TestCase]
    public static void BaseValueBelowMinimumIsClamped()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require(TestNames.Strength).Build();

        owner.SetValue(TestNames.Strength, -(AttributeValueType)5);

        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public static void BaseValueInsideRangeIsKept()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require(TestNames.Strength).Build();

        owner.SetValue(TestNames.Strength, (AttributeValueType)42);

        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)42);
    }

    [TestCase]
    public static void BaseValueAboveMaximumIsClamped()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require(TestNames.Strength).Build();

        owner.SetValue(TestNames.Strength, (AttributeValueType)150);

        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)100);
    }

    [TestCase]
    public static void WritingDerivedAttributeThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Dexterity)
            .Require(TestNames.Attack, TestNames.StrengthPlusHalfDexterity)
            .Build();

        var caught = Catch(() => owner.Get(TestNames.Attack).Value = (AttributeValueType)999);

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
    }
}
