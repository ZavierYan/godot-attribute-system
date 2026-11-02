using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class RuntimeInjectionTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Strength, 0, 100));
        library.Register(new AttributeSpec("luck", 0, 100));
        library.Register(new AttributeSpec(TestNames.Total, 0, 999));
        library.Register(new AttributeSpec(TestNames.Attack, 0, 50));
        library.Register(new AttributeSpec("ghost", 0, 100));
        return library;
    }

    private static AttributeOwner CreateOwnerWithStrength()
        => new AttributeOwnerBuilder(CreateLibrary()).Require(TestNames.Strength).Build();

    // ---- Add 族 ----

    [TestCase]
    public static void AddBaseSupportsReadWriteAndClamping()
    {
        var owner = CreateOwnerWithStrength();

        owner.AddBase(new AttributeSpec(TestNames.Vitality, 0, 100), (AttributeValueType)5);

        AssertThat(owner.GetValue(TestNames.Vitality)).IsEqual((AttributeValueType)5);
        owner.SetValue(TestNames.Vitality, (AttributeValueType)8);
        AssertThat(owner.GetValue(TestNames.Vitality)).IsEqual((AttributeValueType)8);
        owner.SetValue(TestNames.Vitality, -(AttributeValueType)5);
        AssertThat(owner.GetValue(TestNames.Vitality)).IsEqual((AttributeValueType)0);
    }

    [TestCase]
    public static void AddedPlainDerivedEvaluatesAndInvalidates()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require("luck")
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)10);
        owner.SetValue("luck", (AttributeValueType)4);

        owner.AddDerived(new AttributeSpec(TestNames.Total, 0, 999), "strength + luck");

        AssertThat(owner.GetValue(TestNames.Total)).IsEqual((AttributeValueType)14);

        owner.SetValue(TestNames.Strength, (AttributeValueType)20);
        AssertThat(owner.GetValue(TestNames.Total)).IsEqual((AttributeValueType)24);
    }

    [TestCase]
    public static void AddedStackedReadsClampedSeed()
    {
        var owner = CreateOwnerWithStrength();

        owner.AddStacked(new AttributeSpec(TestNames.Attack, 0, 50), TestNames.Strength);
        owner.SetValue(TestNames.Strength, (AttributeValueType)60);

        AssertBool(owner.Get(TestNames.Attack).IsDerived).IsTrue();
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)50);
    }

    // ---- 变异守卫 ----

    [TestCase]
    public static void AddingDuplicateNameIsRejected()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, (AttributeValueType)42)
            .Build();

        var caught = Catch(() => owner.AddBase(new AttributeSpec(TestNames.Strength, 0, 100)));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Strength);
        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)42);
    }

    [TestCase]
    public static void AddedExpressionWithUnknownReferenceThrows()
    {
        var owner = CreateOwnerWithStrength();

        // ghost 已注册 spec 库但未加入 owner，属未知引用
        var caught = Catch(() => owner.AddDerived(
            new AttributeSpec(TestNames.Total, 0, 999), "strength + ghost"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength + ghost");
        AssertThat(caught.Message).Contains("ghost");
    }

    [TestCase]
    public static void AddedExpressionWithSyntaxErrorThrows()
    {
        var owner = CreateOwnerWithStrength();

        var caught = Catch(() => owner.AddDerived(
            new AttributeSpec(TestNames.Total, 0, 999), "strength *"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("strength *");
    }

    // ---- 贡献与撤销 ----

    private static AttributeSpecLibrary CreateContributionLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.AttackBase, 0, 100));
        library.Register(new AttributeSpec(TestNames.Attack, 0, 999));
        library.Register(new AttributeSpec(TestNames.Total, 0, 999));
        return library;
    }

    private static AttributeOwner CreateStackedOwner()
        => new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Build();

    [TestCase]
    public static void ContributeInvalidatesTargetAndFiresEvent()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10); // 建立缓存

        Attribute? received = null;
        AttributeValueType? receivedOldValue = null;
        owner.Get(TestNames.Attack).ValueChanged += (attribute, oldValue) =>
        {
            received = attribute;
            receivedOldValue = oldValue;
        };

        owner.Contribute(TestNames.Attack, ContributionKind.Flat, "5");

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);
        AssertThat(received).IsNotNull();
        AssertThat(received!.Name).IsEqual(TestNames.Attack);
        AssertBool(receivedOldValue.HasValue).IsTrue();
        AssertThat(receivedOldValue!.Value).IsEqual((AttributeValueType)10);
        AssertThat(received.Value).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public static void ContributionOnDirtyTargetCoalescesNotification()
    {
        var owner = CreateOwnerWithStrength();
        owner.AddStacked(new AttributeSpec(TestNames.Attack, 0, 50), TestNames.Strength);
        owner.SetValue(TestNames.Strength, (AttributeValueType)10); // attack 从未读取：出生即脏

        var eventCount = 0;
        Attribute? received = null;
        AttributeValueType? receivedOldValue = null;
        owner.Get(TestNames.Attack).ValueChanged += (attribute, oldValue) =>
        {
            eventCount++;
            received = attribute;
            receivedOldValue = oldValue;
        };

        // 场景「合并语义（贡献路径）」：目标已脏，贡献并入同一次待重算，整波剪枝无通知
        owner.Contribute(TestNames.Attack, ContributionKind.Flat, "5");
        AssertThat(eventCount).IsEqual(0);

        // 读取即得含贡献的新值
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);

        // 读取洗净后再投贡献：重新通知，OldValue 为上次读取的缓存值
        owner.Contribute(TestNames.Attack, ContributionKind.Flat, "3");
        AssertThat(eventCount).IsEqual(1);
        AssertThat(received).IsNotNull();
        AssertThat(received!.Name).IsEqual(TestNames.Attack);
        AssertBool(receivedOldValue.HasValue).IsTrue();
        AssertThat(receivedOldValue!.Value).IsEqual((AttributeValueType)15);
        AssertThat(received.Value).IsEqual((AttributeValueType)18);
    }

    [TestCase]
    public static void ContributionDependencyChangeInvalidates()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        owner.Contribute(TestNames.Attack, ContributionKind.Flat, "5");
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);

        owner.SetValue(TestNames.AttackBase, (AttributeValueType)7);

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)12);
    }

    [TestCase]
    public static void ContributeToPlainDerivedThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Require(TestNames.Total, "attackbase + 1")
            .Build();

        var caught = Catch(() => owner.Contribute(TestNames.Total, ContributionKind.Flat, "1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Total);
    }

    [TestCase]
    public static void ContributeToBaseThrows()
    {
        var owner = CreateStackedOwner();

        var caught = Catch(() => owner.Contribute(TestNames.AttackBase, ContributionKind.Flat, "1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.AttackBase);
    }

    [TestCase]
    public static void ContributeWithUnknownReferenceThrows()
    {
        var owner = CreateStackedOwner();

        // ghost 未加入 owner，属未知引用
        var caught = Catch(() => owner.Contribute(TestNames.Attack, ContributionKind.Flat, "ghost + 1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("ghost + 1");
        AssertThat(caught.Message).Contains("ghost");
    }

    [TestCase]
    public static void ContributeCycleThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Require(TestNames.Total, "attack + 1")
            .Build();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10);

        // 贡献使 attack 依赖 total，而 total 已依赖 attack → 环
        var caught = Catch(() => owner.Contribute(TestNames.Attack, ContributionKind.Flat, "total * 0.1"));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("循环依赖");
        AssertThat(caught.Message).Contains(TestNames.Attack);
        AssertThat(caught.Message).Contains(TestNames.Total);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10); // 贡献未生效
    }

    [TestCase]
    public static void RemoveContributionFallsBackAndRevokesHandle()
    {
        var owner = CreateStackedOwner();
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        var five = owner.Contribute(TestNames.Attack, ContributionKind.Flat, "5");
        owner.Contribute(TestNames.Attack, ContributionKind.Flat, "3");
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)18);

        owner.RemoveContribution(five);

        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)13);
        AssertBool(five.Revoked).IsTrue();
    }

    [TestCase]
    public static void RemoveRevokedHandleThrows()
    {
        var owner = CreateStackedOwner();
        var handle = owner.Contribute(TestNames.Attack, ContributionKind.Flat, "5");
        owner.RemoveContribution(handle);

        var caught = Catch(() => owner.RemoveContribution(handle));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public static void RemoveHandleFromOtherOwnerThrows()
    {
        var first = CreateStackedOwner();
        var second = CreateStackedOwner();
        var handle = first.Contribute(TestNames.Attack, ContributionKind.Flat, "5");

        var caught = Catch(() => second.RemoveContribution(handle));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
    }

    // ---- 移除属性 ----

    [TestCase]
    public static void RemoveUnreferencedAttributeSucceeds()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength)
            .Require(TestNames.Attack, "strength * 2")
            .Require("luck")
            .Build();
        owner.SetValue(TestNames.Strength, (AttributeValueType)10);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20); // 建立缓存

        owner.RemoveAttribute("luck");

        var caught = Catch(() => owner.Get("luck"));
        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("luck");

        // 其余属性不受影响：strength 照常读写、attack 照常失效重算
        owner.SetValue(TestNames.Strength, (AttributeValueType)12);
        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)12);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)24);
    }

    [TestCase]
    public static void RemoveReferencedAttributeThrows()
    {
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require(TestNames.Strength, (AttributeValueType)42)
            .Require(TestNames.Attack, "strength * 2")
            .Build();

        var caught = Catch(() => owner.RemoveAttribute(TestNames.Strength));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Strength);
        AssertThat(caught.Message).Contains(TestNames.Attack);

        // 拒绝后原属性仍在原处、可读写、值不变
        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)42);
        owner.SetValue(TestNames.Strength, (AttributeValueType)8);
        AssertThat(owner.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)8);
    }

    [TestCase]
    public static void RemoveAfterRevokingContributionsSucceeds()
    {
        var owner = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Build();
        owner.AddBase(new AttributeSpec(TestNames.EffectAttackBase, 0, 100), (AttributeValueType)5);
        owner.SetValue(TestNames.AttackBase, (AttributeValueType)10);

        var handle = owner.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EffectAttackBase);
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);

        // 贡献在位时移除被引用者 → 拒绝并指明引用方
        var blocked = Catch(() => owner.RemoveAttribute(TestNames.EffectAttackBase));
        AssertThat(blocked).IsInstanceOf<InvalidOperationException>();
        AssertThat(blocked!.Message).Contains(TestNames.EffectAttackBase);
        AssertThat(blocked.Message).Contains(TestNames.Attack);

        owner.RemoveContribution(handle);
        owner.RemoveAttribute(TestNames.EffectAttackBase);

        var caught = Catch(() => owner.Get(TestNames.EffectAttackBase));
        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(owner.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10); // 回退 seed
    }

    // ---- 克隆族隔离 ----

    [TestCase]
    public static void MutationDoesNotAffectExistingClones()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Require(TestNames.Total, "attack + 1")
            .Build();
        prototype.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        AssertThat(prototype.GetValue(TestNames.Total)).IsEqual((AttributeValueType)11); // 建立缓存

        var clone = prototype.Clone(); // 变异前克隆

        // 原型变异：加属性 + 投贡献
        prototype.AddBase(new AttributeSpec(TestNames.EffectAttackBase, 0, 100), (AttributeValueType)5);
        prototype.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EffectAttackBase);

        // 克隆构成不变：无新属性，原成员俱在
        AssertBool(clone.TryGet(TestNames.Attack, out _)).IsTrue();
        AssertBool(clone.TryGet(TestNames.Total, out _)).IsTrue();
        AssertBool(clone.TryGet(TestNames.EffectAttackBase, out _)).IsFalse();
        AssertThat(clone.Attributes.Count()).IsEqual(3);

        // 克隆读取仍按原构成求值
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10);
        AssertThat(clone.GetValue(TestNames.Total)).IsEqual((AttributeValueType)11);

        // 原型自身反映变异
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);
        AssertThat(prototype.GetValue(TestNames.Total)).IsEqual((AttributeValueType)16);

        // 失效不串族：克隆写 base 只触发克隆重算，原型贡献值不受波及
        clone.SetValue(TestNames.AttackBase, (AttributeValueType)20);
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)20);
        AssertThat(clone.GetValue(TestNames.Total)).IsEqual((AttributeValueType)21);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public static void MutationOnCloneDoesNotAffectPrototype()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Build();
        prototype.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10);

        var clone = prototype.Clone();

        // 克隆上变异：加 base、加聚合栈、投贡献
        clone.AddBase(new AttributeSpec(TestNames.Morale, 0, 100), (AttributeValueType)3);
        clone.AddStacked(new AttributeSpec("burst", 0, 50), TestNames.AttackBase);
        clone.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.Morale);

        // 原型构成不变
        AssertBool(prototype.TryGet(TestNames.Attack, out _)).IsTrue();
        AssertBool(prototype.TryGet(TestNames.Morale, out _)).IsFalse();
        AssertBool(prototype.TryGet("burst", out _)).IsFalse();

        // 原型值不受克隆变异影响
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10);

        // 克隆反映变异
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)13);
        AssertThat(clone.GetValue("burst")).IsEqual((AttributeValueType)10);

        // 失效不串族：各自写各自的 base，各自按自己构成重算、互不波及
        prototype.SetValue(TestNames.AttackBase, (AttributeValueType)14);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)14);
        clone.SetValue(TestNames.AttackBase, (AttributeValueType)20);
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)23);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)14);
    }

    [TestCase]
    public static void CloneAfterMutationInheritsInjections()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Build();
        prototype.AddBase(new AttributeSpec(TestNames.EffectAttackBase, 0, 100), (AttributeValueType)5);
        prototype.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        prototype.Contribute(TestNames.Attack, ContributionKind.Flat, TestNames.EffectAttackBase);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);

        var clone = prototype.Clone();

        // 构成与贡献继承：新属性在、base 继承彼时当前值、attack 求值含贡献
        AssertBool(clone.TryGet(TestNames.EffectAttackBase, out _)).IsTrue();
        AssertThat(clone.GetValue(TestNames.EffectAttackBase)).IsEqual((AttributeValueType)5);
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);

        // 实例隔离：写克隆的新 base，只有克隆的重算变化，原型不受影响
        clone.SetValue(TestNames.EffectAttackBase, (AttributeValueType)8);
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)18);
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);
    }

    [TestCase]
    public static void PrototypeMutationAfterCloningSparesThatClone()
    {
        var prototype = new AttributeOwnerBuilder(CreateContributionLibrary())
            .Require(TestNames.AttackBase)
            .RequireStacked(TestNames.Attack, TestNames.AttackBase)
            .Build();
        prototype.SetValue(TestNames.AttackBase, (AttributeValueType)10);
        var handle = prototype.Contribute(TestNames.Attack, ContributionKind.Flat, "5");
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);

        var clone = prototype.Clone(); // 此刻起原型的表重新进入共享集

        // 原型在克隆之后再变异：撤销贡献 + 加新属性
        prototype.RemoveContribution(handle);
        prototype.AddBase(new AttributeSpec(TestNames.Morale, 0, 100), (AttributeValueType)3);

        // 克隆的构成与值保持原样（贡献构成原样、无新属性）
        AssertBool(clone.TryGet(TestNames.Morale, out _)).IsFalse();
        AssertThat(clone.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)15);

        // 原型自身反映变异
        AssertBool(prototype.TryGet(TestNames.Morale, out _)).IsTrue();
        AssertThat(prototype.GetValue(TestNames.Attack)).IsEqual((AttributeValueType)10);
    }
}
