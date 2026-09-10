using System.Globalization;
using NCalc;
using NCalc.Factories;
using NCalc.Visitors;

namespace AttributeSystem;

/// <summary>derived 属性的统一求值委托：接收依赖属性当前值，返回原始计算结果。</summary>
public delegate AttributeValueType AttributeCalculator(IReadOnlyDictionary<string, AttributeValueType> dependencies);

/// <summary>
/// NCalc 表达式的构建期封装：一次解析（语法错误在此暴露）、提取依赖、编译为求值通道。
/// 表达式是依赖关系的唯一声明来源——标识符即依赖属性的 AttributeName，按字面精确匹配；
/// 内置函数名在语法树中不是 Identifier 节点，天然不参与依赖。
/// </summary>
internal sealed class CompiledExpression
{
    private readonly Expression _expression;

    public string ExpressionText { get; }

    /// <summary>表达式声明的依赖集合（按首次出现顺序去重）。</summary>
    public IReadOnlyList<string> Dependencies { get; }

    public CompiledExpression(string expressionText)
    {
        ExpressionText = expressionText;

        LogicalExpression ast;
        try
        {
            ast = LogicalExpressionFactory.Create(expressionText, null, CultureInfo.InvariantCulture);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"表达式解析失败：'{expressionText}'（{e.Message}）", e);
        }

        Dependencies = CollectIdentifiers(ast);

        _expression = new Expression(ast, null, null, CultureInfo.InvariantCulture);
    }

    /// <summary>用依赖属性的当前值求值；结果转换为 AttributeValueType，失败时抛出带表达式上下文的异常。</summary>
    public AttributeValueType Evaluate(IReadOnlyDictionary<string, AttributeValueType> dependencyValues)
    {
        foreach (var (name, value) in dependencyValues)
            _expression.Parameters[name] = value;

        object raw;
        try
        {
            raw = _expression.Evaluate();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"表达式求值失败：'{ExpressionText}'（{e.Message}）", e);
        }

        return ConvertResult(raw);
    }

    /// <summary>编译为统一求值委托（与委托注入通道同签名，owner 对两条通道无感知）。
    /// 委托捕获本实例且 <see cref="Evaluate"/> 每次写入 Parameters——非线程安全；
    /// 同族克隆实例共享此委托（见 AttributeOwner.Clone），并发求值会互相污染。</summary>
    public AttributeCalculator AsCalculator() => dependencyValues => Evaluate(dependencyValues);

    private static IReadOnlyList<string> CollectIdentifiers(LogicalExpression ast)
    {
        var visitor = new IdentifierCollector();
        ast.Accept(visitor);
        return visitor.Identifiers.Distinct().ToList();
    }

    private AttributeValueType ConvertResult(object? raw)
    {
        try
        {
            if (raw is null)
                throw new InvalidOperationException("表达式结果为 null，无法转换为数值。");
            if (raw is AttributeValueType v)
                return v;
            if (raw is double d)
                return AttributeValueType.CreateChecked(d);
            if (raw is float f)
                return AttributeValueType.CreateChecked(f);
            if (raw is int i)
                return AttributeValueType.CreateChecked(i);
            if (raw is long l)
                return AttributeValueType.CreateChecked(l);
            if (raw is decimal m)
                return AttributeValueType.CreateChecked(m);
            return AttributeValueType.CreateChecked(
                (double)Convert.ChangeType(raw, typeof(double), CultureInfo.InvariantCulture));
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"表达式 '{ExpressionText}' 的结果 '{raw}'（{raw?.GetType().Name ?? "null"}）无法转换为 AttributeValueType。", e);
        }
    }

    /// <summary>遍历 AST 收集全部标识符。函数名不是 Identifier 节点，只有实参参与依赖提取。</summary>
    private sealed class IdentifierCollector : ILogicalExpressionVisitor<bool>
    {
        public List<string> Identifiers { get; } = new();

        public bool Visit(Identifier identifier)
        {
            Identifiers.Add(identifier.Name);
            return true;
        }

        public bool Visit(UnaryExpression expression) => expression.Expression.Accept(this);

        public bool Visit(BinaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            return expression.RightExpression.Accept(this);
        }

        public bool Visit(TernaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            expression.MiddleExpression.Accept(this);
            return expression.RightExpression.Accept(this);
        }

        public bool Visit(Function function)
        {
            foreach (var argument in function.Parameters)
                argument.Accept(this);
            return true;
        }

        public bool Visit(ValueExpression expression) => true;

        public bool Visit(LogicalExpressionList list)
        {
            foreach (var item in list)
                item.Accept(this);
            return true;
        }
    }
}
