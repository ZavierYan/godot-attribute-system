namespace AttributeSystem.Tests;

/// <summary>
/// 测试间共享的高频字面量（属性名 / 原型名 / 表达式），收敛为常量。
/// 只收纳各测试文件中重复 4 次及以上的字面量；低频字面量保留原样以保证用例自读性。
/// </summary>
internal static class TestNames
{
    // ---- 属性名 ----
    public const string Strength = "strength";
    public const string Dexterity = "dexterity";
    public const string Attack = "attack";
    public const string Vitality = "vitality";
    public const string Total = "total";
    public const string Tracked = "tracked";
    public const string AttackBase = "attackbase";
    public const string EffectAttackBase = "effectattackbase";
    public const string EquipmentAttackBase = "equipmentattackbase";
    public const string Morale = "morale";

    // ---- 原型名 ----
    public const string Warrior = "warrior";
    public const string Rogue = "rogue";

    // ---- 表达式 ----
    public const string StrengthPlusHalfDexterity = "strength+0.5*dexterity";
    public const string StrengthTimesTwo = "strength * 2";
}
