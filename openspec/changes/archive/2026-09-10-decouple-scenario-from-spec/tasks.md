## 1. 定义层与组装层源码

- [x] 1.1 AttributeSpec 移除 SpecName/Expression/IsDerived，构造收为 (attributeName, minValue, maxValue)，保留空白名与最值倒置校验；编译通过
- [x] 1.2 AttributeSpecLibrary 键改 AttributeName，同名登记抛 InvalidOperationException，Get/TryGet/Contains 文案语义改属性名；编译通过
- [x] 1.3 AttributeOwnerBuilder：Require 改属性名，新增 WithExpression（格式即时抛），Build 删属性名查重、注入目标未 Require/重复注入在 Build 报错，DerivedCalculation 提升 internal，装配绑定移入 owner 构造；编译通过
- [x] 1.4 Attribute 移除 SourceSpecName 并新增 internal 克隆构造（共享 spec、初值重置）；AttributeOwner 新增 Clone()（跳过防御性拷贝的构造路径 + 重放绑定）；编译通过

## 2. 测试改写

- [x] 2.1 AttributeSpecTest：删 4 例（DerivedSpecWithExpressionIsValid/SameAttributeNameWithDifferentSpecNamesAreBothValid/BlankSpecNameIsRejected/BlankExpressionIsRejected），BaseSpecWithoutExpressionIsValid 断言重写为 SpecWithNameAndBoundsIsValid，其余构造签名适配；该套件绿
- [x] 2.2 AttributeSpecLibraryTest：4 例改键语义，"共享 AttributeName 可登记"反转为"同名拒绝"（TryGet/Contains 例补齐）；该套件绿
- [x] 2.3 AttributeOwnerTest：删 2 例（SameAttributeNameSpecsBuildIntoSeparateOwners/BuildWithDuplicateAttributeNameThrowsWithBothSpecNames），CreateLibrary 重构为单一 attack spec + 按 owner 注入，增 3 例（目标未 Require、重复注入、空白表达式即时抛）+ 1 例同名不同公式分属不同构建（新 spec 场景覆盖）；该套件绿
- [x] 2.4 新建 AttributeOwnerCloneTest：同构成/初值不继承/克隆间值独立/缓存独立/事件不泄漏/失效传播隔离/克隆可再克隆 共 7 例；该套件绿
- [x] 2.5 DerivedAttributeTest 与 ObservationTest 的 CreateLibrary/CreateChainOwner 改造为"约束 spec + WithExpression 注入"（DerivedTest 双 attack 注册改为单 spec + 双公式按 owner 注入）；两套件绿

## 3. 文档

- [x] 3.1 README：第 2 步（spec 无表达式）、要点（同名登记拒绝 + 场景=静态工厂）、第 3 步（Require 属性名 + WithExpression + Clone 用法与共享边界注意事项）、校验表（删重名行、改缺失措辞、补注入错误）、文件结构表
- [x] 3.2 主规格 Purpose 直接更新：attribute-definition 与 attribute-owner 的 Purpose 段去除双名/SpecName 表述（design D8）

## 4. 验证与归档

- [x] 4.1 dotnet build（仅既有 CS8600 警告）+ dotnet test 全绿（62 例：计划 60 + 2 个新增 spec 场景覆盖用例）
- [x] 4.2 openspec validate --strict 通过
- [x] 4.3 openspec archive 收尾，主规格同步核对（attribute-definition 三对 REMOVED+ADDED 落位、attribute-owner 一条 RENAMED+MODIFIED 与一条 REMOVED 落位、derived-attributes 两条 MODIFIED 落位）
