using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeSpecTest
{
    // ---- 合法 spec ----

    [TestCase]
    public void SpecWithNameAndBoundsIsValid()
    {
        var spec = new AttributeSpec("strength", 0, 999);

        AssertThat(spec.AttributeName).IsEqual("strength");
        AssertThat(spec.MinValue).IsEqual((AttributeValueType)0);
        AssertThat(spec.MaxValue).IsEqual((AttributeValueType)999);
    }

    [TestCase]
    public void MinMaxDefaultToTypeExtremesWhenOmitted()
    {
        var spec = new AttributeSpec("power");

        AssertThat(spec.MinValue).IsEqual(NumberOps<AttributeValueType>.TypeMin);
        AssertThat(spec.MaxValue).IsEqual(NumberOps<AttributeValueType>.TypeMax);
    }

    // ---- 非法 spec 被拒绝 ----

    [TestCase]
    public void BlankAttributeNameIsRejected()
    {
        var caught = Catch(() => new AttributeSpec(" "));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("attributeName");
    }

    [TestCase]
    public void MinValueGreaterThanMaxValueIsRejected()
    {
        var caught = Catch(() => new AttributeSpec("attr", 100, 0));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("MaxValue");
    }
}
