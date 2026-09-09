using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class ObservationTest
{
    private static AttributeSpecLibrary CreateLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("hero-strength", "strength", 0, 100));
        library.Register(new AttributeSpec("hero-luck", "luck"));
        library.Register(new AttributeSpec("warrior-attack", "attack", expression: "strength * 2"));
        library.Register(new AttributeSpec("hero-total", "total", expression: "attack + 5"));
        return library;
    }

    private static AttributeOwner CreateChainOwner()
    {
        // strength → attack(strength*2) → total(attack+5)
        return new AttributeOwnerBuilder(CreateLibrary())
            .Require("hero-strength")
            .Require("warrior-attack")
            .Require("hero-total")
            .Build();
    }

    // ---- base 变更事件 ----

    [TestCase]
    public void BaseChangeEventCarriesNameOldAndNewValue()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);

        string? name = null;
        AttributeValueType? oldValue = null;
        AttributeValueType? newValue = null;
        owner.Get("strength").ValueChanged += (_, e) =>
        {
            name = e.Name;
            oldValue = e.OldValue;
            newValue = e.NewValue;
        };

        owner.SetValue("strength", (AttributeValueType)20);

        AssertThat(name).IsEqual("strength");
        AssertBool(oldValue.HasValue).IsTrue();
        AssertThat(oldValue!.Value).IsEqual((AttributeValueType)10);
        AssertBool(newValue.HasValue).IsTrue();
        AssertThat(newValue!.Value).IsEqual((AttributeValueType)20);
    }

    [TestCase]
    public void SettingSameValueRaisesNoEvent()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);

        var eventCount = 0;
        owner.Get("strength").ValueChanged += (_, _) => eventCount++;

        owner.SetValue("strength", (AttributeValueType)10);

        AssertThat(eventCount).IsEqual(0);
    }

    [TestCase]
    public void UnsubscribedHandlerStopsReceiving()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);

        var eventCount = 0;
        EventHandler<AttributeChangedEventArgs> handler = (_, _) => eventCount++;
        owner.Get("strength").ValueChanged += handler;
        owner.SetValue("strength", (AttributeValueType)20);
        AssertThat(eventCount).IsEqual(1);

        owner.Get("strength").ValueChanged -= handler;
        owner.SetValue("strength", (AttributeValueType)30);
        AssertThat(eventCount).IsEqual(1);
    }

    // ---- derived 失效通知 ----

    [TestCase]
    public void BaseChangeNotifiesDerivedSubscribers()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)20);

        var eventCount = 0;
        owner.Get("attack").ValueChanged += (_, _) => eventCount++;

        owner.SetValue("strength", (AttributeValueType)15);

        AssertThat(eventCount).IsEqual(1);
    }

    [TestCase]
    public void DerivedNewValueIsLazy()
    {
        var evaluations = 0;
        var owner = new AttributeOwnerBuilder(CreateLibrary())
            .Require("hero-strength")
            .Require("warrior-attack")
            .Build();
        owner.SetValue("strength", (AttributeValueType)10);
        // 计数求值器观测求值时机（失效图仍由表达式依赖决定）
        owner.Get("attack").BindEvaluator(() =>
        {
            evaluations++;
            return owner.GetValue("strength") * (AttributeValueType)2;
        });
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)20);
        AssertThat(evaluations).IsEqual(1);

        int evaluationsDuringEvent = -1;
        AttributeValueType newValue = default;
        owner.Get("attack").ValueChanged += (_, e) =>
        {
            // 通知发出时未访问 NewValue：不产生重算
            evaluationsDuringEvent = evaluations;
            newValue = e.NewValue;
        };

        owner.SetValue("strength", (AttributeValueType)15);

        AssertThat(evaluationsDuringEvent).IsEqual(1);
        AssertThat(newValue).IsEqual((AttributeValueType)30);
        AssertThat(evaluations).IsEqual(2);
    }

    [TestCase]
    public void DerivedOldValueIsNullWhenNeverComputed()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);

        AttributeValueType? oldValue = null;
        var hasOldValue = true;
        owner.Get("attack").ValueChanged += (_, e) =>
        {
            oldValue = e.OldValue;
            hasOldValue = e.OldValue.HasValue;
        };

        owner.SetValue("strength", (AttributeValueType)15);

        // attack 从未被读取，失效事件不携带旧值
        AssertBool(hasOldValue).IsFalse();
        AssertBool(oldValue.HasValue).IsFalse();
    }

    [TestCase]
    public void DerivedOldValueIsCacheSnapshotWhenComputed()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)20);

        AttributeValueType? oldValue = null;
        owner.Get("attack").ValueChanged += (_, e) => oldValue = e.OldValue;

        owner.SetValue("strength", (AttributeValueType)15);

        AssertBool(oldValue.HasValue).IsTrue();
        AssertThat(oldValue!.Value).IsEqual((AttributeValueType)20);
    }

    [TestCase]
    public void NotificationsFollowTopologicalOrder()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)25);

        var order = new List<string>();
        owner.Get("attack").ValueChanged += (_, e) => order.Add(e.Name);
        owner.Get("total").ValueChanged += (_, e) => order.Add(e.Name);

        owner.SetValue("strength", (AttributeValueType)15);

        AssertThat(order).IsEqual(new List<string> { "attack", "total" });
    }

    [TestCase]
    public void BaseEventPrecedesDependentInvalidationNotifications()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)25);

        var order = new List<string>();
        owner.Get("strength").ValueChanged += (_, e) => order.Add(e.Name);
        owner.Get("attack").ValueChanged += (_, e) => order.Add(e.Name);
        owner.Get("total").ValueChanged += (_, e) => order.Add(e.Name);

        owner.SetValue("strength", (AttributeValueType)15);

        // base 自身的变更事件先于下游全部失效通知
        AssertThat(order).IsEqual(new List<string> { "strength", "attack", "total" });
    }

    [TestCase]
    public void HandlerThrowStillInvalidatesAllDependents()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)25);

        owner.Get("attack").ValueChanged += (_, _) => throw new InvalidOperationException("bad handler");

        // 处理器异常向上传播给 SetValue 调用方，不被系统捕获
        var caught = Catch(() => owner.SetValue("strength", (AttributeValueType)15));
        AssertThat(caught).IsInstanceOf<InvalidOperationException>();

        // 但全部受影响 derived 已在通知前标脏：后续读取必得重算值，无静默旧值
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)30);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)35);
    }

    // ---- 分发过程安全 ----

    [TestCase]
    public void UnsubscribingInsideHandlerKeepsSnapshotDispatch()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);

        var aCount = 0;
        var bCount = 0;
        EventHandler<AttributeChangedEventArgs> handlerA = (_, _) => aCount++;
        EventHandler<AttributeChangedEventArgs> handlerB = (_, e) =>
        {
            bCount++;
            owner.Get(e.Name).ValueChanged -= handlerA; // 处理器内退订 A
        };
        owner.Get("strength").ValueChanged += handlerA;
        owner.Get("strength").ValueChanged += handlerB;

        owner.SetValue("strength", (AttributeValueType)20);
        // 当次分发按快照继续：A 仍收到第一次事件
        AssertThat(aCount).IsEqual(1);
        AssertThat(bCount).IsEqual(1);

        owner.SetValue("strength", (AttributeValueType)30);
        // 退订生效：A 不再收到
        AssertThat(aCount).IsEqual(1);
        AssertThat(bCount).IsEqual(2);
    }

    [TestCase]
    public void SubscribingInsideHandlerDoesNotFireForCurrentEvent()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);

        var cCount = 0;
        EventHandler<AttributeChangedEventArgs> handlerC = (_, _) => cCount++;
        owner.Get("strength").ValueChanged += (_, e) =>
            owner.Get(e.Name).ValueChanged += handlerC; // 处理器内订阅 C

        owner.SetValue("strength", (AttributeValueType)20);
        AssertThat(cCount).IsEqual(0);

        owner.SetValue("strength", (AttributeValueType)30);
        AssertThat(cCount).IsEqual(1);
    }
}
