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
        library.Register(new AttributeSpec("strength", 0, 100));
        library.Register(new AttributeSpec("luck"));
        library.Register(new AttributeSpec("attack"));
        library.Register(new AttributeSpec("total"));
        return library;
    }

    private static AttributeOwner CreateChainOwner()
    {
        // strength → attack(strength*2) → total(attack+5)
        return new AttributeOwnerBuilder(CreateLibrary())
            .Require("strength")
            .Require("attack", "strength * 2")
            .Require("total", "attack + 5")
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
            .Require("strength")
            .Require("attack", "strength * 2")
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
    public void NeverReadDerivedStaysSilentUntilFirstRead()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);

        var eventCount = 0;
        AttributeValueType? oldValue = null;
        owner.Get("attack").ValueChanged += (_, e) =>
        {
            eventCount++;
            oldValue = e.OldValue;
        };

        // 场景「从未读取过的 derived 首次读取前不通知」：attack 出生即脏、从未读取，
        // 期间依赖变更并入待重算，不发出失效通知
        owner.SetValue("strength", (AttributeValueType)15);
        AssertThat(eventCount).IsEqual(0);

        // 首次读取得到按当前依赖值计算的结果
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)30);

        // 场景「失效后读取重算，再次变更重新通知」：读取已洗净缓存，
        // 再次变更重新通知，OldValue 为上次读取的缓存值
        owner.SetValue("strength", (AttributeValueType)20);
        AssertThat(eventCount).IsEqual(1);
        AssertBool(oldValue.HasValue).IsTrue();
        AssertThat(oldValue!.Value).IsEqual((AttributeValueType)30);
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
    public void ConsecutiveWritesWithoutReadCoalesceIntoOneNotification()
    {
        var owner = CreateChainOwner();
        owner.SetValue("strength", (AttributeValueType)10);
        // 初始读取洗净两个 derived：attack=20、total=25
        AssertThat(owner.GetValue("attack")).IsEqual((AttributeValueType)20);
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)25);

        var attackCount = 0;
        var totalCount = 0;
        owner.Get("attack").ValueChanged += (_, _) => attackCount++;
        owner.Get("total").ValueChanged += (_, _) => totalCount++;

        owner.SetValue("strength", (AttributeValueType)15);
        AssertThat(attackCount).IsEqual(1);
        AssertThat(totalCount).IsEqual(1);

        // 场景「连续变更且中间未读取合并为一次通知」：第二次写入并入同一次待重算，
        // 已脏分支被剪枝，不重复通知
        owner.SetValue("strength", (AttributeValueType)20);
        AssertThat(attackCount).IsEqual(1);
        AssertThat(totalCount).IsEqual(1);

        // 读取 total 得到级联两次写入的新值：剪枝后闭包失效仍完整（并集闭包保证）
        AssertThat(owner.GetValue("total")).IsEqual((AttributeValueType)45);
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
