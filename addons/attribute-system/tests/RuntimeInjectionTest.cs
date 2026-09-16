using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class RuntimeInjectionTest
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("strength", 0, 100));
        library.Register(new AttributeSpec("luck", 0, 100));
        library.Register(new AttributeSpec("total", 0, 999));
        library.Register(new AttributeSpec("attack", 0, 50));
        library.Register(new AttributeSpec("ghost", 0, 100));
        return library;
    }

    private static AttributeOwner CreateOwnerWithStrength()
        => new AttributeOwnerBuilder(CreateLibrary()).Require("strength").Build();

    // ---- Add 族 ----

    [TestCase]
    public void AddBaseSupportsReadWriteAndClamping()
    {
        var owner = CreateOwnerWithStrength();

        owner.AddBase(new AttributeSpec("vitality", 0, 100), (AttributeValueType)5);

        AssertThat(owner.GetValue("vitality")).IsEqual((AttributeValueType)5);
        owner.SetValue("vitality", (AttributeValueType)8);
        AssertThat(owner.GetValue("vitality")).IsEqual((AttributeValueType)8);
        owner.SetValue("vitality", -(AttributeValueType)5);
        AssertThat(owner.GetValue("vitality")).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public void AddedPlainDerivedEvaluatesAndInvalidates()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("luck")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);
        owner.SetValue("luck", (AttributeValueType)4);

        owner.AddDerived(new AttributeSpec("total", 0, 999), "strength + luck");

        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)14);

        owner.SetValue("strength", (AttributeValueType)20);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)24);
    }

    [TestCase]
    public void AddedStackedReadsClampedSeed()
    {
        var owner = CreateOwnerWithStrength();

        owner.AddStacked(new AttributeSpec("attack", 0, 50), "strength");
        owner.SetValue("strength", (AttributeValueType)60);

        AssertBool(owner.Get("attack").IsDerived).IsTrue();
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)50);
    }

    // ---- 变异守卫 ----

    [TestCase]
    public void AddingDuplicateNameIsRejected()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength", (AttributeValueType)42)
            .Build();

        var caught = Catch(() => owner.AddBase(new AttributeSpec("strength", 0, 100)));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength");
        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)42);
    }

    [TestCase]
    public void AddedExpressionWithUnknownReferenceThrows()
    {
        var owner = CreateOwnerWithStrength();

        // ghost 已注册 spec 库但未加入 owner，属未知引用
        var caught = Catch(() => owner.AddDerived(
            new AttributeSpec("total", 0, 999), "strength + ghost"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength + ghost");
        AssertThat(caught.Message).Contains("ghost");
    }

    [TestCase]
    public void AddedExpressionWithSyntaxErrorThrows()
    {
        var owner = CreateOwnerWithStrength();

        var caught = Catch(() => owner.AddDerived(
            new AttributeSpec("total", 0, 999), "strength *"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength *");
    }

    // ---- 贡献与撤销 ----

    private static AttributeSpecLibrary CreateContributionLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("attackbase", 0, 100));
        library.Register(new AttributeSpec("attack", 0, 999));
        library.Register(new AttributeSpec("total", 0, 999));
        return library;
    }

    private static AttributeOwner CreateStackedOwner()
        => new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Build();

    [TestCase]
    public void ContributeInvalidatesTargetAndFiresEvent()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)10);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)10); // 建立缓存

        AttributeChangedEventArgs? received = null;
        owner.Get("attack").ValueChanged += (_, args) => received = args;

        owner.Contribute("attack", ContributionKind.Flat, "5");

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)15);
        AssertThat(received).IsNotNull();
        AssertThat(received!.Name).IsEqual("attack");
        AssertThat(received.OldValue).IsEqual((AttributeValueType)10);
        AssertThat(received.NewValue).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public void ContributionDependencyChangeInvalidates()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)10);
        owner.Contribute("attack", ContributionKind.Flat, "5");
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)15);

        owner.SetValue("attackbase", (AttributeValueType)7);

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)12);
    }

    [TestCase]
    public void ContributeToPlainDerivedThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Require("total", "attackbase + 1")
            .Build();

        var caught = Catch(() => owner.Contribute("total", ContributionKind.Flat, "1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("total");
    }

    [TestCase]
    public void ContributeToBaseThrows()
    {
        var owner = CreateStackedOwner();

        var caught = Catch(() => owner.Contribute("attackbase", ContributionKind.Flat, "1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attackbase");
    }

    [TestCase]
    public void ContributeWithUnknownReferenceThrows()
    {
        var owner = CreateStackedOwner();

        // ghost 未加入 owner，属未知引用
        var caught = Catch(() => owner.Contribute("attack", ContributionKind.Flat, "ghost + 1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("ghost + 1");
        AssertThat(caught.Message).Contains("ghost");
    }

    [TestCase]
    public void ContributeCycleThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Require("total", "attack + 1")
            .Build();
        owner.SetValue("attackbase", (AttributeValueType)10);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)10);

        // 贡献使 attack 依赖 total，而 total 已依赖 attack → 环
        var caught = Catch(() => owner.Contribute("attack", ContributionKind.Flat, "total * 0.1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("循环依赖");
        AssertThat(caught.Message).Contains("attack");
        AssertThat(caught.Message).Contains("total");
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)10); // 贡献未生效
    }

    [TestCase]
    public void RemoveContributionFallsBackAndRevokesHandle()
    {
        var owner = CreateStackedOwner();
        owner.SetValue("attackbase", (AttributeValueType)10);
        var five = owner.Contribute("attack", ContributionKind.Flat, "5");
        owner.Contribute("attack", ContributionKind.Flat, "3");
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)18);

        owner.RemoveContribution(five);

        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)13);
        AssertBool(five.Revoked).IsTrue();
    }

    [TestCase]
    public void RemoveRevokedHandleThrows()
    {
        var owner = CreateStackedOwner();
        var handle = owner.Contribute("attack", ContributionKind.Flat, "5");
        owner.RemoveContribution(handle);

        var caught = Catch(() => owner.RemoveContribution(handle));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public void RemoveHandleFromOtherOwnerThrows()
    {
        var first = CreateStackedOwner();
        var second = CreateStackedOwner();
        var handle = first.Contribute("attack", ContributionKind.Flat, "5");

        var caught = Catch(() => second.RemoveContribution(handle));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
    }

    // ---- 移除属性 ----

    [TestCase]
    public void RemoveUnreferencedAttributeSucceeds()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "strength * 2")
            .Require("luck")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)20); // 建立缓存

        owner.RemoveAttribute("luck");

        var caught = Catch(() => owner.Get("luck"));
        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("luck");

        // 其余属性不受影响：strength 照常读写、attack 照常失效重算
        owner.SetValue("strength", (AttributeValueType)12);
        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)12);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)24);
    }

    [TestCase]
    public void RemoveReferencedAttributeThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength", (AttributeValueType)42)
            .Require("attack", "strength * 2")
            .Build();

        var caught = Catch(() => owner.RemoveAttribute("strength"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength");
        AssertThat(caught.Message).Contains("attack");

        // 拒绝后原属性仍在原处、可读写、值不变
        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)42);
        owner.SetValue("strength", (AttributeValueType)8);
        AssertThat(owner.GetValue("strength")).IsEqual((AttributeValueType)8);
    }

    [TestCase]
    public void RemoveAfterRevokingContributionsSucceeds()
    {
        var owner = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Build();
        owner.AddBase(new AttributeSpec("effectattackbase", 0, 100), (AttributeValueType)5);
        owner.SetValue("attackbase", (AttributeValueType)10);

        var handle = owner.Contribute("attack", ContributionKind.Flat, "effectattackbase");
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)15);

        // 贡献在位时移除被引用者 → 拒绝并指明引用方
        var blocked = Catch(() => owner.RemoveAttribute("effectattackbase"));
        AssertThat(blocked).IsInstanceOf<InvalidOperationException>();
        AssertThat(blocked!.Message).Contains("effectattackbase");
        AssertThat(blocked.Message).Contains("attack");

        owner.RemoveContribution(handle);
        owner.RemoveAttribute("effectattackbase");

        var caught = Catch(() => owner.Get("effectattackbase"));
        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)10); // 回退 seed
    }

    // ---- 克隆族隔离 ----

    [TestCase]
    public void MutationDoesNotAffectExistingClones()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Require("total", "attack + 1")
            .Build();
        prototype.SetValue("attackbase", (AttributeValueType)10);
        AssertThat(prototype.GetValue("total")).IsEqual((AttributeValueType)11); // 建立缓存

        var clone = prototype.Clone(); // 变异前克隆

        // 原型变异：加属性 + 投贡献
        prototype.AddBase(new AttributeSpec("effectattackbase", 0, 100), (AttributeValueType)5);
        prototype.Contribute("attack", ContributionKind.Flat, "effectattackbase");

        // 克隆构成不变：无新属性，原成员俱在
        AssertBool(clone.TryGet("attack", out _)).IsTrue();
        AssertBool(clone.TryGet("total", out _)).IsTrue();
        AssertBool(clone.TryGet("effectattackbase", out _)).IsFalse();
        AssertThat(clone.Attributes.Count()).IsEqual(3);

        // 克隆读取仍按原构成求值
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)10);
        AssertThat(clone.GetValue("total")).IsEqual((AttributeValueType)11);

        // 原型自身反映变异
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)15);
        AssertThat(prototype.GetValue("total")).IsEqual((AttributeValueType)16);

        // 失效不串族：克隆写 base 只触发克隆重算，原型贡献值不受波及
        clone.SetValue("attackbase", (AttributeValueType)20);
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)20);
        AssertThat(clone.GetValue("total")).IsEqual((AttributeValueType)21);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public void MutationOnCloneDoesNotAffectPrototype()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Build();
        prototype.SetValue("attackbase", (AttributeValueType)10);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)10);

        var clone = prototype.Clone();

        // 克隆上变异：加 base、加聚合栈、投贡献
        clone.AddBase(new AttributeSpec("morale", 0, 100), (AttributeValueType)3);
        clone.AddStacked(new AttributeSpec("burst", 0, 50), "attackbase");
        clone.Contribute("attack", ContributionKind.Flat, "morale");

        // 原型构成不变
        AssertBool(prototype.TryGet("attack", out _)).IsTrue();
        AssertBool(prototype.TryGet("morale", out _)).IsFalse();
        AssertBool(prototype.TryGet("burst", out _)).IsFalse();

        // 原型值不受克隆变异影响
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)10);

        // 克隆反映变异
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)13);
        AssertThat(clone.GetValue("burst")).IsEqual((AttributeValueType)10);

        // 失效不串族：各自写各自的 base，各自按自己构成重算、互不波及
        prototype.SetValue("attackbase", (AttributeValueType)14);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)14);
        clone.SetValue("attackbase", (AttributeValueType)20);
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)23);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)14);
    }

    [TestCase]
    public void CloneAfterMutationInheritsInjections()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Build();
        prototype.AddBase(new AttributeSpec("effectattackbase", 0, 100), (AttributeValueType)5);
        prototype.SetValue("attackbase", (AttributeValueType)10);
        prototype.Contribute("attack", ContributionKind.Flat, "effectattackbase");
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)15);

        var clone = prototype.Clone();

        // 构成与贡献继承：新属性在、base 继承彼时当前值、attack 求值含贡献
        AssertBool(clone.TryGet("effectattackbase", out _)).IsTrue();
        AssertThat(clone.GetValue("effectattackbase")).IsEqual((AttributeValueType)5);
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)15);

        // 实例隔离：写克隆的新 base，只有克隆的重算变化，原型不受影响
        clone.SetValue("effectattackbase", (AttributeValueType)8);
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)18);
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public void PrototypeMutationAfterCloningSparesThatClone()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require("attackbase")
            .RequireStacked("attack", "attackbase")
            .Build();
        prototype.SetValue("attackbase", (AttributeValueType)10);
        var handle = prototype.Contribute("attack", ContributionKind.Flat, "5");
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)15);

        var clone = prototype.Clone(); // 此刻起原型的表重新进入共享集

        // 原型在克隆之后再变异：撤销贡献 + 加新属性
        prototype.RemoveContribution(handle);
        prototype.AddBase(new AttributeSpec("morale", 0, 100), (AttributeValueType)3);

        // 克隆的构成与值保持原样（贡献构成原样、无新属性）
        AssertBool(clone.TryGet("morale", out _)).IsFalse();
        AssertThat(clone.GetValue("attack")).IsEqual((AttributeValueType)15);

        // 原型自身反映变异
        AssertBool(prototype.TryGet("morale", out _)).IsTrue();
        AssertThat(prototype.GetValue("attack")).IsEqual((AttributeValueType)10);
    }
}
