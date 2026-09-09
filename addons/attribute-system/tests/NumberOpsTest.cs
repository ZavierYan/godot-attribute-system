using GdUnit4;
using static GdUnit4.Assertions;

namespace AttributeSystem.Tests;

[TestSuite]
public class NumberOpsTest
{
    [TestCase]
    public void ClampKeepsValueInsideRange()
    {
        AssertThat(NumberOps<AttributeValueType>.Clamp((AttributeValueType)2, (AttributeValueType)0, (AttributeValueType)5)).IsEqual((AttributeValueType)2);
    }

    [TestCase]
    public void ClampLimitsValueBelowMinimum()
    {
        AssertThat(NumberOps<AttributeValueType>.Clamp(-(AttributeValueType)5, (AttributeValueType)0, (AttributeValueType)100)).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public void ClampLimitsValueAboveMaximum()
    {
        AssertThat(NumberOps<AttributeValueType>.Clamp((AttributeValueType)150, (AttributeValueType)0, (AttributeValueType)100)).IsEqual((AttributeValueType)100);
    }

    [TestCase]
    public void TypeExtremesSpanTheValidRange()
    {
        AssertBool(NumberOps<AttributeValueType>.TypeMin < NumberOps<AttributeValueType>.TypeMax).IsTrue();

        var any = (AttributeValueType)7;
        AssertThat(NumberOps<AttributeValueType>.Clamp(any,
            NumberOps<AttributeValueType>.TypeMin,
            NumberOps<AttributeValueType>.TypeMax)).IsEqual(any);
    }
}
