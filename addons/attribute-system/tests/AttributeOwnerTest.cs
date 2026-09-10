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
        library.Register(new AttributeSpec("strength", 0, 100));
        library.Register(new AttributeSpec("dexterity", 0, 100));
        library.Register(new AttributeSpec("intelligence", 0, 100));
        library.Register(new AttributeSpec("attack", 0, 999));
        return library;
    }

    // ---- 构建成功 ----

    [TestCase]
    public void BuildCreatesOwnerAccessibleByAttributeName()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("dexterity")
            .Require("attack", "strength+0.5*dexterity")
            .Build();

        AssertBool(owner.TryGet("strength", out var strength)).IsTrue();
        AssertBool(owner.TryGet("dexterity", out _)).IsTrue();
        AssertBool(owner.TryGet("attack", out var attack)).IsTrue();
        AssertBool(strength!.IsDerived).IsFalse();
        AssertBool(attack!.IsDerived).IsTrue();
    }

    [TestCase]
    public void SameAttributeNameWithDifferentExpressionsBuildsSeparately()
    {
        var library = CreateLibrary();

        var warrior = new AttributeOwnerBuilder(library)
            .Require("strength")
            .Require("dexterity")
            .Require("attack", "strength+0.5*dexterity")
            .Build();
        var mage = new AttributeOwnerBuilder(library)
            .Require("intelligence")
            .Require("attack", "intelligence*1.2")
            .Build();

        warrior.SetValue("strength", (AttributeValueType)10);
        warrior.SetValue("dexterity", (AttributeValueType)6);
        mage.SetValue("intelligence", (AttributeValueType)10);

        AssertThat(warrior.GetValue("attack")).IsEqual((AttributeValueType)13);
        AssertThat(mage.GetValue("attack")).IsEqual((AttributeValueType)12);
    }

    [TestCase]
    public void DuplicateBaseDeclarationIsIdempotent()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("strength")
            .Build();

        AssertBool(owner.TryGet("strength", out _)).IsTrue();
        AssertThat(owner.Attributes.Count()).IsEqual(1);
    }

    // ---- 构建期错误 ----

    [TestCase]
    public void BuildWithMissingSpecThrowsWithName()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary()).Require("hero-mana").Build());

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public void BuildWithUnknownExpressionReferenceThrowsWithExpressionAndName()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "strength+0.5*dexterity")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("dexterity");
        AssertThat(caught.Message).Contains("strength+0.5*dexterity");
    }

    [TestCase]
    public void BuildWithCycleThrowsWithCycleChain()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("a"));
        library.Register(new AttributeSpec("b"));

        var caught = Catch(() => new AttributeOwnerBuilder(library)
            .Require("a", "b + 1")
            .Require("b", "a + 1")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("循环依赖");
        AssertThat(caught.Message).Contains("a");
        AssertThat(caught.Message).Contains("b");
    }

    [TestCase]
    public void BuildWithSelfReferenceThrowsWithCycleChain()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("x"));

        var caught = Catch(() => new AttributeOwnerBuilder(library)
            .Require("x", "x + 1")
            .Build());

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
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "strength *")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength *");
    }

    // ---- 声明冲突校验 ----

    [TestCase]
    public void MixedBaseAndDerivedDeclarationThrowsAtBuild()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack")
            .Require("attack", "strength * 2")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public void MixedDeclarationInReverseOrderThrowsAtBuild()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "strength * 2")
            .Require("attack")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public void DuplicateDerivedDeclarationThrowsAtBuild()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "strength * 2")
            .Require("attack", "strength * 3")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public void DuplicateIdenticalExpressionDeclarationThrowsAtBuild()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "strength * 2")
            .Require("attack", "strength * 2")
            .Build());

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
        AssertThat(caught.Message).Contains("冲突");
    }

    [TestCase]
    public void BlankExpressionThrowsAtDeclaration()
    {
        var caught = Catch(() => new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "   "));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("expression");
    }

    // ---- 运行时访问 ----

    [TestCase]
    public void GetMissingAttributeNameThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("strength").Build();

        var caught = Catch(() => owner.Get("mana"));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("mana");
    }

    [TestCase]
    public void BaseValueInitializesToClampedDefault()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("strength").Build();

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)0);
    }

    // ---- base 读写与钳制 ----

    [TestCase]
    public void BaseValueBelowMinimumIsClamped()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("strength").Build();

        owner.SetValue("strength", -(AttributeValueType)5);

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public void BaseValueInsideRangeIsKept()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("strength").Build();

        owner.SetValue("strength", (AttributeValueType)42);

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)42);
    }

    [TestCase]
    public void BaseValueAboveMaximumIsClamped()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary()).Require("strength").Build();

        owner.SetValue("strength", (AttributeValueType)150);

        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)100);
    }

    [TestCase]
    public void WritingDerivedAttributeThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("dexterity")
            .Require("attack", "strength+0.5*dexterity")
            .Build();

        var caught = Catch(() => owner.Get("attack").Value = (AttributeValueType)999);

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
    }
}
