using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeOwnerTest
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("hero-strength", "strength", 0, 100));
        library.Register(new AttributeSpec("hero-dexterity", "dexterity", 0, 100));
        library.Register(new AttributeSpec("warrior-attack", "attack", expression: "strength+0.5*dexterity"));
        library.Register(new AttributeSpec("mage-attack", "attack", expression: "intelligence*1.2"));
        library.Register(new AttributeSpec("mage-intelligence", "intelligence", 0, 100));
        return library;
    }

    // ---- 构建成功 ----

    [TestCase]
    public void BuildCreatesOwnerAccessibleByAttributeName()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("hero-strength")
            .Require("hero-dexterity")
            .Require("warrior-attack")
            .Build();

        AssertBool(owner.TryGet("strength", out var strength)).IsTrue();
        AssertBool(owner.TryGet("dexterity", out _)).IsTrue();
        AssertBool(owner.TryGet("attack", out var attack)).IsTrue();
        AssertBool(strength!.IsDerived).IsFalse();
        AssertBool(attack!.IsDerived).IsTrue();
        // SpecName 仅作调试信息
        AssertThat(attack.SourceSpecName).IsEqual("warrior-attack");
    }

    [TestCase]
    public void SameAttributeNameSpecsBuildIntoSeparateOwners()
    {
        var library = CreateLibrary();

        var warrior = new AttributeOwnerBuilder(library)
            .Require("hero-strength")
            .Require("hero-dexterity")
            .Require("warrior-attack")
            .Build();
        var mage = new AttributeOwnerBuilder(library)
            .Require("mage-intelligence")
            .Require("mage-attack")
            .Build();

        AssertBool(warrior.TryGet("attack", out var warriorAttack)).IsTrue();
        AssertBool(mage.TryGet("attack", out var mageAttack)).IsTrue();
        AssertThat(warriorAttack!.SourceSpecName).IsEqual("warrior-attack");
        AssertThat(mageAttack!.SourceSpecName).IsEqual("mage-attack");
    }

    // ---- 构建期错误 ----

    [TestCase]
    public void BuildWithMissingSpecThrowsWithSpecName()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary()).Require("hero-mana").Build());

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public void BuildWithUnknownExpressionReferenceThrowsWithExpressionAndName()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("hero-strength")
            .Require("warrior-attack")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("dexterity");
        AssertThat(caught.Message).Contains("strength+0.5*dexterity");
    }

    [TestCase]
    public void BuildWithDuplicateAttributeNameThrowsWithBothSpecNames()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("warrior-attack")
            .Require("mage-attack")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
        AssertThat(caught.Message).Contains("warrior-attack");
        AssertThat(caught.Message).Contains("mage-attack");
    }

    [TestCase]
    public void BuildWithCycleThrowsWithCycleChain()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("cycle-a", "a", expression: "b + 1"));
        library.Register(new AttributeSpec("cycle-b", "b", expression: "a + 1"));

        var caught = Catch(() => new AttributeOwnerBuilder(library).Require("cycle-a").Require("cycle-b").Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("循环依赖");
        AssertThat(caught.Message).Contains("a");
        AssertThat(caught.Message).Contains("b");
    }

    [TestCase]
    public void BuildWithSelfReferenceThrowsWithCycleChain()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("self", "x", expression: "x + 1"));

        var caught = Catch(() => new AttributeOwnerBuilder(library).Require("self").Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("循环依赖");
        AssertThat(caught.Message).Contains("x");
    }

    [TestCase]
    public void BuildWithoutAnyRequirementThrows()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary()).Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public void BuildWithSyntaxErrorThrowsAtBuild()
    {
        var library = CreateLibrary();
        library.Register(new AttributeSpec("broken-attack", "brokenAttack", expression: "strength *"));

        var caught = Catch(() => new AttributeOwnerBuilder(library)
            .Require("hero-strength")
            .Require("broken-attack")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength *");
    }

    // ---- 运行时访问 ----

    [TestCase]
    public void GetMissingAttributeNameThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("hero-strength").Build();

        var caught = Catch(() => owner.Get("mana"));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("mana");
    }

    [TestCase]
    public void BaseValueInitializesToClampedDefault()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("hero-strength").Build();

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)0);
    }

    // ---- base 读写与钳制 ----

    [TestCase]
    public void BaseValueBelowMinimumIsClamped()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("hero-strength").Build();

        owner.SetValue("strength", -(AttributeValueType)5);

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public void BaseValueInsideRangeIsKept()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("hero-strength").Build();

        owner.SetValue("strength", (AttributeValueType)42);

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)42);
    }

    [TestCase]
    public void BaseValueAboveMaximumIsClamped()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("hero-strength").Build();

        owner.SetValue("strength", (AttributeValueType)150);

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)100);
    }

    [TestCase]
    public void WritingDerivedAttributeThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("hero-strength")
            .Require("hero-dexterity")
            .Require("warrior-attack")
            .Build();

        var caught = Catch(() => owner.Get("attack").Value = (AttributeValueType)999);

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
    }
}
