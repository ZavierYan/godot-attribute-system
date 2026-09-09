using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class CompiledExpressionTest
{
    [TestCase]
    public void ExtractsDependenciesFromIdentifiers()
    {
        var compiled = new CompiledExpression("strength+0.5*dexterity");

        AssertThat(compiled.Dependencies).Contains("strength", "dexterity");
        AssertThat(compiled.Dependencies).HasSize(2);
    }

    [TestCase]
    public void EvaluatesArithmeticWithDependencyValues()
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
    public void BuiltInFunctionNamesAreNotDependencies()
    {
        var compiled = new CompiledExpression("Max(attack - debuff, 0)");

        AssertThat(compiled.Dependencies).Contains("attack", "debuff");
        AssertThat(compiled.Dependencies).HasSize(2);
    }

    [TestCase]
    public void EvaluatesBuiltInFunction()
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
    public void SyntaxErrorThrowsAtCompileTimeWithExpressionText()
    {
        var caught = Catch(() => new CompiledExpression("strength *"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength *");
    }

    [TestCase]
    public void UnboundDependencyFailsEvaluationWithExpressionText()
    {
        var compiled = new CompiledExpression("strength * 2");

        var caught = Catch(() => compiled.Evaluate(new Dictionary<string, AttributeValueType>()));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength * 2");
    }
}
