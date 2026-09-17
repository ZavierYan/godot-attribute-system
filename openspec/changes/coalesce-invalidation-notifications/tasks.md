## 1. 源码：失效剪枝与脏状态查询

- [x] 1.1 `Attribute.cs` 新增 `internal bool IsDirty`（`IsDerived && !_hasCachedValue`，注释写明脏定义与剪枝不变量），同步 `AttributeChangedEventArgs` 与类文档注释中 OldValue 的措辞（失效前缓存值；"从未计算过则为 null"路径已不可达）。验证：`dotnet build` 0 错误（仅既有 CS8600）。
- [ ] 1.2 `AttributeOwner.InvalidateDependents` BFS 剪枝：`visited` 去重后在 `IsDerived` 分支内查 `IsDirty` 即 `continue`（不加入 affected、不展开下游），根统一处理不特判；方法注释重写（不变量、合并语义、并集闭包保证，作为后续变异路径的演进约束）。验证：`dotnet build` 0 错误；全量测试中仅 `ObservationTest.DerivedOldValueIsNullWhenNeverComputed` 预期失败（锁定旧语义，2.1 的改写对象），其余全绿。

## 2. 测试：合并语义锁定

- [ ] 2.1 改写 `ObservationTest.DerivedOldValueIsNullWhenNeverComputed` 为合并语义：从未读取期间写依赖不通知、首读得到新值、读取洗净后再次变更收到通知且 OldValue 为读取值。验证：该用例绿。
- [ ] 2.2 新增 2 例：`ObservationTest.ConsecutiveWritesWithoutReadCoalesceIntoOneNotification`（订阅 attack 与 total，两次写中间不读计数不变，读 total 得级联新值）、`RuntimeInjectionTest.ContributionOnDirtyTargetCoalescesNotification`（目标未读取时投贡献无通知、读取得含贡献值，读后再投收到通知且 OldValue 为上次读取值）。验证：`dotnet test` 全量 **116/116** 绿。

## 3. 文档与规格同步

- [ ] 3.1 `README.md` 观察节：订阅示例注释与要点补合并语义（已失效未重算期间不重复通知，读取重算后再次变更会重新通知；旧值为失效前缓存值）。验证：措辞与 delta 场景一一对应。
- [ ] 3.2 直改主规格 `openspec/specs/attribute-observation/spec.md` 的 Purpose，补"失效通知按脏状态迁移合并（已失效不重复通知）"句（delta 不携带 Purpose）。验证：`openspec validate --specs --strict` 绿。
- [ ] 3.3 收尾验证：`dotnet build`（仅既有 CS8600 @ CompiledExpression.cs:53）+ `dotnet test` 116/116 + `openspec validate coalesce-invalidation-notifications --strict` 全绿。
