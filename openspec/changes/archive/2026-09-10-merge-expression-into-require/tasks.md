## 1. 源码与规格

- [x] 1.1 AttributeOwnerBuilder：新增 `Require(attributeName, expression)` 重载，两入口收敛到私有 AddRequired（幂等/冲突规则），`_duplicateExpressionNames` 更名 `_conflictedNames`，Build 开头统一抛冲突；移除 WithExpression 与"注入目标不在集合内"检查；编译通过
- [x] 1.2 立 change 工件（proposal/design/tasks/attribute-owner delta）且 `openspec validate merge-expression-into-require --strict` 通过

## 2. 测试与文档

- [x] 2.1 四个套件全部 `.Require(n).WithExpression(n, e)` 调用点改为 `.Require(n, e)`；OwnerTest 三个错误用例改写（空白表达式即时抛、base/derived 混合 Build 抛、两条表达式 Build 抛）并新增"重复 base 声明幂等"；dotnet test 全绿（63/63）
- [x] 2.2 README 第 3 步示例与校验表（删"注入目标"行、冲突行改措辞、空白行改措辞，补幂等说明）；attribute-owner 主规格 Purpose 校验清单直改
- [x] 2.3 `openspec validate --specs --strict` 通过

## 3. 收尾

- [x] 3.1 sync 主规格（REMOVED+ADDED 落位核对）并归档 change，`--archived` 复验通过
