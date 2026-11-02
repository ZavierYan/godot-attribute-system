// ============================================================================
// AttributeValueType —— 测试侧的值类型别名声明
//
// global using 别名不跨程序集：别名声明点在插件程序集
// （addons/attribute-system/AttributeValueType.cs），而测试源码编译进 Godot 主程序集，
// 需要自带同款声明。切换值类型时，两处同步修改。
// ============================================================================
global using AttributeValueType = System.Single;
