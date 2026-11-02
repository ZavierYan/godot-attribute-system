using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeSpecTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    // ---- 合法 spec ----

    [TestCase]
    public static void SpecWithNameAndBoundsIsValid()
    {
        var spec = new AttributeSpec("strength", 0, 999);

        AssertThat(spec.AttributeName).IsEqual("strength");
        AssertThat(spec.MinValue).IsEqual((AttributeValueType)0);
        AssertThat(spec.MaxValue).IsEqual((AttributeValueType)999);
    }

    [TestCase]
    public static void MinMaxDefaultToTypeExtremesWhenOmitted()
    {
        var spec = new AttributeSpec("power");

        AssertThat(spec.MinValue).IsEqual(NumberOps<AttributeValueType>.TypeMin);
        AssertThat(spec.MaxValue).IsEqual(NumberOps<AttributeValueType>.TypeMax);
    }

    // ---- 非法 spec 被拒绝 ----

    [TestCase]
    public static void BlankAttributeNameIsRejected()
    {
        var caught = Catch(() => _ = new AttributeSpec(" "));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("attributeName");
    }

    [TestCase]
    public static void MinValueGreaterThanMaxValueIsRejected()
    {
        var caught = Catch(() => _ = new AttributeSpec("attr", 100, 0));

        AssertThat(caught).IsInstanceOf<ArgumentException>();
        AssertThat(caught!.Message).Contains("MaxValue");
    }
}
