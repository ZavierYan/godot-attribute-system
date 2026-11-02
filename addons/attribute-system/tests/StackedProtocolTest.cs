using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

/// <summary>
/// 聚合栈协议行为锁定（对应 derived-attributes delta「聚合栈 derived 与类型化贡献」requirement）：
/// 协议 (seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative)，最终统一钳制、中间段不钳制。
/// 全部数值选用二进制精确的浮点数，断言精确相等。
/// 「无贡献时等于 seed 的钳制值」场景已由 AttributeOwnerTest.StackedBuildWithoutContributionsReadsClampedSeedValue
/// 逐字锁定（seed 60、Max 50 → 50），此处不再重复。
/// </summary>
[TestSuite]
public class StackedProtocolTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.AttackBase, 0, 100));
        library.Register(new AttributeSpec(TestNames.EffectAttackBase, 0, 100));
        library.Register(new AttributeSpec(TestNames.EquipmentAttackBase, 0, 100));
        library.Register(new AttributeSpec(TestNames.Attack, 0, 999));
        library.Register(new AttributeSpec("hp", 0, 100));
        return library;
    }

    /// <summary>仅含 attackbase/hp 来源的聚合栈 owner（贡献表达式用字面量的场景用）。</summary>
    private static AttributeOwner CreateStackedOwner()
        => new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.AttackBase)
            .Require("hp")
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Build();

    /// <summary>含 attackbase 与两个 flat 来源属性的聚合栈 owner（spec 场景的属性引用型贡献用）。</summary>
    private static AttributeOwner CreateFlatSourcesOwner()
        => new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.AttackBase)
            .Require(TestNames.EffectAttackBase)
            .Require(TestNames.EquipmentAttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Build();

    private static void SeedFlatSources(AttributeOwner owner)
    {
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        owner.SetValue(TestNames.EffectAttackBase, (AttributeValueType)5);
        owner.SetValue(TestNames.EquipmentAttackBase, (AttributeValueType)3);
    }

    // ---- flat 贡献叠加 ----

    [TestCase]
    public static void FlatContributionsStackAdditively()
    {
        var owner = CreateFlatSourcesOwner();
        SeedFlatSources(owner);

        owner.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EffectAttackBase);
        owner.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EquipmentAttackBase);

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)18);
    }

    // ---- additive 贡献同池求和 ----

    [TestCase]
    public static void AdditiveContributionsSumInOnePool()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)100);

        owner.Contribute(TestNames.Attack, ContributionKind.Additive, "0.5");
        owner.Contribute(TestNames.Attack, ContributionKind.Additive, "0.25");

        // 100 × (1 + 0.5 + 0.25)
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)175);
    }

    // ---- multiplicative 贡献独立复利 ----

    [TestCase]
    public static void MultiplicativeContributionsCompoundIndependently()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)100);

        owner.Contribute(TestNames.Attack, ContributionKind.Multiplicative, "0.5");
        owner.Contribute(TestNames.Attack, ContributionKind.Multiplicative, "0.25");

        // 100 × 1.5 × 1.25（逐条复利，非同池相加）
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)187.5);
    }

    // ---- 三类贡献混合 ----

    [TestCase]
    public static void AllThreeKindsCombineByProtocol()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)100);

        owner.Contribute(TestNames.Attack, ContributionKind.Flat, "50");
        owner.Contribute(TestNames.Attack, ContributionKind.Additive, "0.25");
        owner.Contribute(TestNames.Attack, ContributionKind.Multiplicative, "0.5");

        // (100 + 50) × 1.25 × 1.5
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)281.25);
    }

    // ---- 贡献项为动态表达式 ----

    [TestCase]
    public static void ContributionExpressionMayBeDynamic()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)100);
        owner.SetValue("hp", (AttributeValueType)25);

        owner.Contribute(TestNames.Attack, ContributionKind.Additive, "hp < 30 ? 0.5 : 0");
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)150);

        // hp 变化使 attack 缓存失效重算
        owner.SetValue("hp", (AttributeValueType)60);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)100);
    }

    // ---- 桶内贡献交换律 ----

    [TestCase]
    public static void BucketOrderDoesNotMatter()
    {
        var first = CreateFlatSourcesOwner();
        var second = CreateFlatSourcesOwner();
        SeedFlatSources(first);
        SeedFlatSources(second);

        first.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EffectAttackBase);
        first.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EquipmentAttackBase);
        second.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EquipmentAttackBase);
        second.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EffectAttackBase);

        AssertThat(first.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)18);
        AssertThat(second.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)18);
    }

    // ---- additive 池和为负：按协议求值并最终钳制 ----

    [TestCase]
    public static void NegativeAdditivePoolClampsAtBounds()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)100);

        owner.Contribute(TestNames.Attack, ContributionKind.Additive, "-0.5");
        owner.Contribute(TestNames.Attack, ContributionKind.Additive, "-0.75");

        // 100 × (1 - 1.25) = -25，中间段不钳制，最终钳到 Min=0
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)0);
    }

    // ---- 撤销贡献后回退 ----

    [TestCase]
    public static void RevokedContributionFallsBackToRemainder()
    {
        var owner = CreateFlatSourcesOwner();
        SeedFlatSources(owner);
        var five = owner.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EffectAttackBase);
        owner.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EquipmentAttackBase);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)18);

        owner.RemoveContribution(five);

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)13);
    }
}
