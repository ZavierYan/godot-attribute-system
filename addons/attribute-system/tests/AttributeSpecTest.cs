using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeSpecTest
{
    // ---- 合法 spec ----

    [TestCase]
    public void BaseSpecWithoutExpressionIsValid()
    {
        var spec = new AttributeSpec("hero-strength", "strength", 0, 999);

        AssertThat(spec.SpecName).IsEqual("hero-strength");
        AssertThat(spec.AttributeName).IsEqual("strength");
        AssertThat(spec.MinValue).IsEqual((AttributeValueType)0);
        AssertThat(spec.MaxValue).IsEqual((AttributeValueType)999);
        AssertThat(spec.Expression).IsNull();
        AssertThat(spec.IsDerived).IsFalse();
    }

    [TestCase]
    public void DerivedSpecWithExpressionIsValid()
    {
        var spec = new AttributeSpec("warrior-attack", "attack", expression: "strength+0.5*dexterity");

        AssertThat(spec.IsDerived).IsTrue();
        AssertThat(spec.Expression).IsEqual("strength+0.5*dexterity");
    }

    [TestCase]
    public void SameAttributeNameWithDifferentSpecNamesAreBothValid()
    {
        var warrior = new AttributeSpec("warrior-attack", "attack", expression: "strength+0.5*dexterity");
        var mage = new AttributeSpec("mage-attack", "attack", expression: "intelligence*1.2");

        AssertThat(warrior.AttributeName).IsEqual(mage.AttributeName);
        AssertThat(warrior.SpecName).IsNotEqual(mage.SpecName);
    }

    [TestCase]
    public void MinMaxDefaultToTypeExtremesWhenOmitted()
    {
        var spec = new AttributeSpec("power", "power");

        AssertThat(spec.MinValue).IsEqual(NumberOps<AttributeValueType>.TypeMin);
        AssertThat(spec.MaxValue).IsEqual(NumberOps<AttributeValueType>.TypeMax);
    }

    // ---- 非法 spec 被拒绝 ----

    [TestCase]
    public void BlankSpecNameIsRejected()
    {
        var caught = Catch(() => new AttributeSpec("  ", "strength"));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("specName");
    }

    [TestCase]
    public void BlankAttributeNameIsRejected()
    {
        var caught = Catch(() => new AttributeSpec("hero-strength", " "));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("attributeName");
    }

    [TestCase]
    public void MinValueGreaterThanMaxValueIsRejected()
    {
        var caught = Catch(() => new AttributeSpec("spec", "attr", 100, 0));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("MaxValue");
    }

    [TestCase]
    public void BlankExpressionIsRejected()
    {
        var caught = Catch(() => new AttributeSpec("spec", "attr", expression: "   "));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("Expression");
    }
}
