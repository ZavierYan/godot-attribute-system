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
public class StackedProtocolTest
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("attackbase", 0, 100));
        library.Register(new AttributeSpec("effectattackbase", 0, 100));
        library.Register(new AttributeSpec("equipmentattackbase", 0, 100));
        library.Register(new AttributeSpec("attack", 0, 999));
        library.Register(new AttributeSpec("hp", 0, 100));
        return library;
    }

    /// <summary>仅含 attackbase/hp 来源的聚合栈 owner（贡献表达式用字面量的场景用）。</summary>
    private static AttributeOwner CreateStackedOwner()
        => new AttributeOwnerBuilder(CreateLibrary())
            .Require("attackbase")
            .Require("hp")
            .RequireStacked("attack", "attackbase")
            .Build();

    /// <summary>含 attackbase 与两个 flat 来源属性的聚合栈 owner（spec 场景的属性引用型贡献用）。</summary>
    private static AttributeOwner CreateFlatSourcesOwner()
        => new AttributeOwnerBuilder(CreateLibrary())
            .Require("attackbase")
            .Require("effectattackbase")
            .Require("equipmentattackbase")
            .RequireStacked("attack", "attackbase")
            .Build();

    private static void SeedFlatSources(AttributeOwner owner)
    {
        owner.SetValue("attackbase", (AttributeValueType)10);
        owner.SetValue("effectattackbase", (AttributeValueType)5);
        owner.SetValue("equipmentattackbase", (AttributeValueType)3);
    }

    // ---- flat 贡献叠加 ----

    [TestCase]
    public void FlatContributionsStackAdditively()
    {
        var owner = CreateFlatSourcesOwner();
        SeedFlatSources(owner);

        owner.Contribute("attack", ContributionKind.Flat, "effectattackbase");
        owner.Contribute("attack", ContributionKind.Flat, "equipmentattackbase");

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)18);
    }

    // ---- additive 贡献同池求和 ----

    [TestCase]
    public void AdditiveContributionsSumInOnePool()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)100);

        owner.Contribute("attack", ContributionKind.Additive, "0.5");
        owner.Contribute("attack", ContributionKind.Additive, "0.25");

        // 100 × (1 + 0.5 + 0.25)
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)175);
    }

    // ---- multiplicative 贡献独立复利 ----

    [TestCase]
    public void MultiplicativeContributionsCompoundIndependently()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)100);

        owner.Contribute("attack", ContributionKind.Multiplicative, "0.5");
        owner.Contribute("attack", ContributionKind.Multiplicative, "0.25");

        // 100 × 1.5 × 1.25（逐条复利，非同池相加）
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)187.5);
    }

    // ---- 三类贡献混合 ----

    [TestCase]
    public void AllThreeKindsCombineByProtocol()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)100);

        owner.Contribute("attack", ContributionKind.Flat, "50");
        owner.Contribute("attack", ContributionKind.Additive, "0.25");
        owner.Contribute("attack", ContributionKind.Multiplicative, "0.5");

        // (100 + 50) × 1.25 × 1.5
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)281.25);
    }

    // ---- 贡献项为动态表达式 ----

    [TestCase]
    public void ContributionExpressionMayBeDynamic()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)100);
        owner.SetValue("hp", (AttributeValueType)25);

        owner.Contribute("attack", ContributionKind.Additive, "hp < 30 ? 0.5 : 0");
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)150);

        // hp 变化使 attack 缓存失效重算
        owner.SetValue("hp", (AttributeValueType)60);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)100);
    }

    // ---- 桶内贡献交换律 ----

    [TestCase]
    public void BucketOrderDoesNotMatter()
    {
        var first = CreateFlatSourcesOwner();
        var second = CreateFlatSourcesOwner();
        SeedFlatSources(first);
        SeedFlatSources(second);

        first.Contribute("attack", ContributionKind.Flat, "effectattackbase");
        first.Contribute("attack", ContributionKind.Flat, "equipmentattackbase");
        second.Contribute("attack", ContributionKind.Flat, "equipmentattackbase");
        second.Contribute("attack", ContributionKind.Flat, "effectattackbase");

        AssertThat(first.GetValue("attack")).IsEqual((AttributeValueType)18);
        AssertThat(second.GetValue("attack")).IsEqual((AttributeValueType)18);
    }

    // ---- additive 池和为负：按协议求值并最终钳制 ----

    [TestCase]
    public void NegativeAdditivePoolClampsAtBounds()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)100);

        owner.Contribute("attack", ContributionKind.Additive, "-0.5");
        owner.Contribute("attack", ContributionKind.Additive, "-0.75");

        // 100 × (1 - 1.25) = -25，中间段不钳制，最终钳到 Min=0
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)0);
    }

    // ---- 撤销贡献后回退 ----

    [TestCase]
    public void RevokedContributionFallsBackToRemainder()
    {
        var owner = CreateFlatSourcesOwner();
        SeedFlatSources(owner);
        var five = owner.Contribute("attack", ContributionKind.Flat, "effectattackbase");
        owner.Contribute("attack", ContributionKind.Flat, "equipmentattackbase");
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)18);

        owner.RemoveContribution(five);

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)13);
    }
}
