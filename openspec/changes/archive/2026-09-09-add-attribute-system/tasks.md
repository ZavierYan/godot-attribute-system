## 1. 工程准备

- [x] 1.1 在 csproj 引入 NuGet 依赖（`NCalc` 最新稳定版、`gdUnit4.api`）并为本工程安装 gdUnit4 插件；验证：`dotnet build` 成功，Godot 编辑器中 gdUnit4 测试面板可用
- [x] 1.2 创建 `addons/attribute-system/AttributeValueType.cs`（单点 `global using AttributeValueType = System.Single;` 附切换说明注释）与命名空间 `AttributeSystem`，实现 `NumberOps<T>`（`INumber<T>` 约束，提供 `Clamp` 与类型极值）；验证：工程编译通过，`NumberOps.Clamp` 的 GdUnit4 单测通过

## 2. 定义层（attribute-definition）

- [x] 2.1 将现有空桩重写为不可变 `AttributeSpec`（主构造函数校验：SpecName 与 AttributeName 均非空白、`MinValue <= MaxValue`、Expression 非空白；Min/Max 缺省为类型极值，`Expression == null` 即 base）；验证：GdUnit4 覆盖非法 spec 场景（SpecName 空白/AttributeName 空白/最值倒置/空白表达式）与合法场景（base、derived，以及两个不同 SpecName 共享同一 AttributeName 的 spec 均可创建）
- [x] 2.2 实现 `AttributeSpecLibrary`（以 SpecName 为键：`Register` SpecName 重名抛异常、`Get` 缺失抛异常、`Contains`；多个 spec 共享 AttributeName 可登记）；验证：登记检索、SpecName 重名拒绝、缺失告知、共享 AttributeName 登记成功四个场景的测试通过

## 3. 表达式通道

- [x] 3.1 定义委托 `AttributeCalculator(IReadOnlyDictionary<string, AttributeValueType>)` 并实现 NCalc 封装：解析表达式、以 `ILogicalExpressionVisitor<T>` 遍历语法树收集 `Identifier` 节点得到依赖集合（内置函数名在语法树中不是 Identifier 节点，天然不参与依赖；表达式即依赖的唯一声明来源）、编译为 `AttributeCalculator`（结果经 `CreateChecked` 转换，失败带表达式上下文报错）；验证：`strength+0.5*dexterity` 提取出依赖 `{strength, dexterity}` 且以 `{strength: 10, dexterity: 6}` 求值得 13；语法错误 `strength *` 抛出含表达式内容的异常

## 4. 运行时核心

- [x] 4.1 实现 `Attribute`（base：钳制读写；derived：脏标记 + 懒求值 + 缓存，一次失效至多重算一次）与 `AttributeChangedEventArgs`（`Name` 即 AttributeName、`OldValue` 为失效前快照可空、`NewValue` 惰性属性 getter）；验证：单测覆盖钳制读写、连续两次读取仅求值一次（委托计数器）、从未读取不求值
- [x] 4.2 实现 `AttributeOwner`：属性表（按 AttributeName）与获取（缺失抛异常）、base 设值流程（同值直接返回→更新→快照 invocation list 发自身事件→按拓扑序将受影响 derived 标脏并发出失效通知）；验证：级联失效（`total = attack + 5` 随 `strength` 更新）、通知拓扑顺序、处理器内退订/订阅安全的测试通过
- [x] 4.3 实现 `AttributeOwnerBuilder`（`Require(specName)` 流式 + `Build()`：按 SpecName 查库→汇总 AttributeName 集合并拒绝同次构建重复→逐 derived 解析表达式与依赖（按属性名解析）→建反向依赖表 + DFS 三色标记拓扑排序做环检测（报出环路属性链）→构建 owner）；验证：缺失 spec、表达式未知引用、同次构建 AttributeName 重复、循环依赖四个构建错误场景与成功构建场景（含同属性名 spec 分属两个 owner 各自成功）的测试通过
- [x] 4.4 打通委托注入通道：Builder 支持为 derived 注入 `AttributeCalculator`，注入优先于 spec 表达式；验证：注入委托求值正确、注入优先级、委托结果钳制的测试通过

## 5. 观察者与求值完备性

- [x] 5.1 补全观察者语义测试：base 事件携带属性名/旧值/新值、同值设置不触发、退订后不再收到、derived 失效通知发出但 `NewValue` 未访问前不重算（计数器断言）、访问后得到新值；验证：全部测试通过
- [x] 5.2 补全求值语义测试：NCalc 内置函数（`Max`/`Min`/`Abs`）、derived 引用 derived、表达式与委托结果的最值钳制（越上限/越下限）、表达式声明的依赖集合边界（依赖之一变化才失效、无关属性不失效）；验证：全部测试通过

## 6. 收尾

- [x] 6.1 手动验证类型切换：把 `AttributeValueType.cs` 别名改为 `int` 重新编译并跑全部测试，随后改回 `float`；验证：两种类型下编译与测试均通过（验证记录：切 `int` 后测试全绿 → 切回 `float` 全绿；测试代码已做别名中立化）
- [x] 6.2 编写 `addons/attribute-system/README.md`：源码安装方式、值类型切换步骤（改一行 + 重编译）、SpecName 与 AttributeName 的分工（builder/library 用前者、owner 运行时与表达式用后者）、表达式语法（同时声明计算与依赖）与整型截断注意事项、奇异数值类型建议走委托注入、本期非目标；验证：文档覆盖以上全部要点

## 7. 检视修复（tmp/review.txt）

- [x] 7.1 修复注入路径绕过表达式构建期校验（被注入覆盖的表达式仍做语法 + 未知引用校验，仅校验、不进失效图）；新增 builder 层语法错误端到端测试与注入覆盖校验测试
- [x] 7.2 失效传播改为两阶段（先按拓扑序全部标脏、再分发通知），处理器异常向上传播且不残留过期缓存；异常语义回写 derived-attributes spec 并新增测试
- [x] 7.3 注入通道的显式依赖声明回写 derived-attributes spec（分通道表述，消除与"表达式为依赖唯一来源"的字面矛盾）
- [x] 7.4 轻量重构：共享 TestHelpers.Catch（删除 6 处重复含死代码）、Guard.NotBlank 收敛空白校验、builder 平行字典捆绑为 DerivedCalculation、Attribute 工厂改收 spec、共享 AttributeNames.Comparer、重写 NumberOpsTest 同值自比断言
- [x] 7.5 design 回写（D4 两阶段失效、D5 注入校验、D6 快照机制对齐实现、D7 注明 Test.Sdk 必要性）；全量测试 62/62 通过，openspec validate 通过

## 8. 范围修正：移除外部委托注入接口

- [x] 8.1 按需求澄清移除 `InjectCalculator` 公共接口与注入通道——"注入用于计算数值的方法"的本意是 builder 在构建期把表达式解析出的计算委托装配进 derived（内部装配缝 `DerivedCalculation` + `BindEvaluator`），不需要对外的运行时注入接口
- [x] 8.2 同步清理：删除 6 个注入相关测试；计数类断言改用构建后经 `BindEvaluator` 换装带计数的等价求值器观测；钳制下限场景改为表达式版（`vitality - 100` 钳到 0）
- [x] 8.3 spec/design/proposal/README 回写：移除双通道表述，依赖声明回归"表达式为唯一来源"；全量测试 56/56 通过，openspec validate 通过

## 9. 三轮复审修复

- [x] 9.1 paramName 约定统一：`ParamName` 一律为实际参数名（camelCase），消息文本指代公共字段用 PascalCase；同步 AttributeSpecTest 断言
- [x] 9.2 `ValidateSpecExpression` 直接返回 `DerivedCalculation`（消除依赖对两处拼写）；`Build()` 步骤编号重排（1-5 连续）
- [x] 9.3 断言风格统一（NumberOpsTest 改 `AssertBool`）；删除 DerivedAttributeTest 死导入
- [x] 9.4 N2 顺序语义落档与锁定：attribute-observation spec 新增"变更事件先于下游失效通知"requirement 与场景，ObservationTest 新增顺序测试
- [x] 9.5 文档对齐：design D3/D4 与 tasks 3.1/4.3 修正为实际机制（DFS 三色标记、无内置函数剔除步骤）；删除 D5 重复条目；新增 D8 落档公开 API 面与 fail-fast 决策理由
