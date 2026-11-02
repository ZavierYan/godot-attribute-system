using GdUnit4;
using static GdUnit4.Assertions;

namespace AttributeSystem.Tests;

[TestSuite]
public class NumberOpsTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    [TestCase]
    public static void ClampKeepsValueInsideRange()
    {
        AssertThat(NumberOps<AttributeValueType>.Clamp((AttributeValueType)2, (AttributeValueType)0, (AttributeValueType)5)).IsEqual((AttributeValueType)2);
    }

    [TestCase]
    public static void ClampLimitsValueBelowMinimum()
    {
        AssertThat(NumberOps<AttributeValueType>.Clamp(-(AttributeValueType)5, (AttributeValueType)0, (AttributeValueType)100)).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public static void ClampLimitsValueAboveMaximum()
    {
        AssertThat(NumberOps<AttributeValueType>.Clamp((AttributeValueType)150, (AttributeValueType)0, (AttributeValueType)100)).IsEqual((AttributeValueType)100);
    }

    [TestCase]
    public static void TypeExtremesSpanTheValidRange()
    {
        AssertBool(NumberOps<AttributeValueType>.TypeMin < NumberOps<AttributeValueType>.TypeMax).IsTrue();

        var any = (AttributeValueType)7;
        AssertThat(NumberOps<AttributeValueType>.Clamp(any,
            NumberOps<AttributeValueType>.TypeMin,
            NumberOps<AttributeValueType>.TypeMax)).IsEqual(any);
    }
}
