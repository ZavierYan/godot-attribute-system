using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class CompiledExpressionTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    [TestCase]
    public static void ExtractsDependenciesFromIdentifiers()
    {
        var compiled = new CompiledExpression("strength+0.5*dexterity");

        AssertThat(compiled.Dependencies).Contains("strength", "dexterity");
        AssertThat(compiled.Dependencies).HasSize(2);
    }

    [TestCase]
    public static void EvaluatesArithmeticWithDependencyValues()
    {
        var compiled = new CompiledExpression("strength+0.5*dexterity");

        var result = compiled.Evaluate(new Dictionary<string, AttributeValueType>
        {
            ["strength"] = (AttributeValueType)10,
            ["dexterity"] = (AttributeValueType)6,
        });

        AssertThat(result).IsEqual((AttributeValueType)13);
    }

    [TestCase]
    public static void BuiltInFunctionNamesAreNotDependencies()
    {
        var compiled = new CompiledExpression("Max(attack - debuff, 0)");

        AssertThat(compiled.Dependencies).Contains("attack", "debuff");
        AssertThat(compiled.Dependencies).HasSize(2);
    }

    [TestCase]
    public static void EvaluatesBuiltInFunction()
    {
        var compiled = new CompiledExpression("Max(attack - debuff, 0)");

        var result = compiled.Evaluate(new Dictionary<string, AttributeValueType>
        {
            ["attack"] = (AttributeValueType)10,
            ["debuff"] = (AttributeValueType)15,
        });

        AssertThat(result).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public static void SyntaxErrorThrowsAtCompileTimeWithExpressionText()
    {
        var caught = Catch(() => _ = new CompiledExpression("strength *"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength *");
    }

    [TestCase]
    public static void UnboundDependencyFailsEvaluationWithExpressionText()
    {
        var compiled = new CompiledExpression("strength * 2");

        var caught = Catch(() => compiled.Evaluate(new Dictionary<string, AttributeValueType>()));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength * 2");
    }
}
