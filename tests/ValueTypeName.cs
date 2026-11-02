// ============================================================================
// AttributeValueType —— 测试工程侧的值类型别名声明
//
// global using 别名不跨程序集：插件在 addons/attribute-system/AttributeValueType.cs
// 中的声明对测试程序集不可见，测试需自带同款声明。
// 切换值类型时，与插件侧声明两处同步修改。
// ============================================================================
global using AttributeValueType = System.Single;
