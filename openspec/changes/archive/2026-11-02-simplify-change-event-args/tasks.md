# Tasks

## 1. 源码与测试同步迁移（同一编译单元，编译须整体恢复）

- [x] 1.1 `Attribute.cs`：删除 `AttributeChangedEventArgs` 类；`ValueChanged` 改为 `event Action<Attribute, AttributeValueType?>?`；事件注释按 design D1/D3 重写（惰性契约收拢指向 `Value`）——验证：该文件无 args 残留
- [x] 1.2 `Attribute.cs`：`Value` setter 触发点改为 `ValueChanged?.Invoke(this, oldValue)`，`RaiseInvalidated` 改为 `Invoke(this, oldSnapshot)`；删除 provider 与 memoization 相关逻辑，注释同步——验证：两处触发点签名与新事件一致
- [x] 1.3 `ObservationTest.cs`：迁移全部 args 用法（`e.Name` → 事件首参数的 `.Name`、`e.OldValue` → 第二参数、`e.NewValue` → 读取属性），惰性时序测试改写为 handler 内读 `attr.Value`、断言逐条同构（design D4）——验证：本文件测试通过
- [x] 1.4 `RuntimeInjectionTest.cs`：迁移两处 args 用法（贡献路径的 Name/OldValue/NewValue 断言）——验证：本文件测试通过，编译整体恢复

## 2. 文档

- [x] 2.1 `README.md` 观察节：两处订阅示例改为新签名，注明"事件携带属性与旧值，新值经读取获得（derived 惰性求值）"——验证：示例代码与实现签名一致

## 3. 全量验证

- [x] 3.1 运行全量 GdUnit4 测试通过（Godot 路径见 AGENTS.md）；grep `addons/` 下 `AttributeChangedEventArgs` 零命中

## Workflow follow-up

- Archive the change after the project's review requirements are satisfied.
