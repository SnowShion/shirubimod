using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using shirubimod.Scripts.Content.Cards;
using shirubimod.Scripts.Content.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;

namespace shirubimod.Scripts.Content;

[RegisterCharacter]
public sealed class ShirubiCharacter : ModCharacterTemplate<ShirubiCardPool, ShirubiRelicPool, ShirubiPotionPool>
{
    // 角色基础属性。之后想调整开局血量、金币，就改这里。
    public override int StartingHp => 80;
    public override int StartingGold => 99;
    public override CharacterGender Gender => CharacterGender.Feminine;

    // 角色选择界面、地图涂鸦等位置使用的颜色。
    public override Color NameColor => new(0.95f, 0.78f, 0.96f);
    public override Color MapDrawingColor => new(0.95f, 0.45f, 0.95f);

    // 角色攻击/施法动画的延迟。当前沿用较短延迟，后续替换角色动画时可以微调。
    public override float AttackAnimDelay => 0.3f;
    public override float CastAnimDelay => 0.3f;

    // 没有单独指定的资源会继续从铁甲战士那里补齐，例如能量 UI 和部分 VFX。
    public override string PlaceholderCharacterId => "IRONCLAD";
    // 暂时禁用商店角色场景覆盖，让游戏走原版商店加载流程。
    // 之前手写的静态商人场景会让商店房间加载时黑屏；返回空路径可避开 RitsuLib 的资源覆盖补丁。
    public override string CustomMerchantAnimPath => "";

    public override CharacterAssetProfile AssetProfile => new(
        Ui: new CharacterUiAssetSet(
            IconTexturePath: "res://images/ui/top_panel/character_icon_shirubi.png",
            IconOutlineTexturePath: "res://images/ui/top_panel/character_icon_shirubi.png",
            IconPath: "res://scenes/ui/character_icons/shirubimod_character_shirubi_character_icon.tscn",
            CharacterSelectBgPath: "res://scenes/screens/char_select/char_select_bg_shirubi.tscn",
            CharacterSelectIconPath: "res://images/packed/character_select/char_select_shirubi.png",
            CharacterSelectLockedIconPath: "res://images/packed/character_select/char_select_shirubi_locked.png"
        )
    );

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        var texture = ResourceLoader.Load<Texture2D>("res://images/characters/shirubi_combat.png");
        if (texture == null)
        {
            return null;
        }

        var root = new NCreatureVisuals
        {
            Name = "Shirubi"
        };

        var sprite = new Sprite2D
        {
            Texture = texture,
            Position = new Vector2(0, -192),
            Scale = new Vector2(0.25f, 0.25f)
        };
        root.AddUniqueChild(sprite, "Visuals");

        var bounds = new Control
        {
            Position = new Vector2(-120, -380),
            Size = new Vector2(240, 380),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddUniqueChild(bounds, "Bounds");

        var centerPos = new Marker2D
        {
            Position = new Vector2(0, -190)
        };
        root.AddUniqueChild(centerPos, "CenterPos");

        var intentPos = new Marker2D
        {
            Position = new Vector2(20, -360)
        };
        root.AddUniqueChild(intentPos, "IntentPos");

        return root;
    }

    // 建筑师模式或部分攻击表现会读取这组 VFX。
    // 后续如果加入希比自己的攻击特效，可以把这些路径换成自定义资源路径。
    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_bloody_impact",
        "vfx/vfx_rock_shatter"
    ];

    [Obsolete]
    // 希比新开一局时的初始牌组。
    // 想增删起始牌，改这里的类型列表；卡牌具体效果在 Content/Cards/ShibiBasicCards.cs。
    protected override IReadOnlyList<Type> StartingDeckTypes =>
    [
        typeof(ShibiStrike),
        typeof(ShibiStrike),
        typeof(ShibiStrike),
        typeof(ShibiStrike),
        typeof(ShibiDefend),
        typeof(ShibiDefend),
        typeof(ShibiDefend),
        typeof(ShibiDefend),
        typeof(ShibiNeedleThrust),
        typeof(ShibiGuardedStep),
        typeof(ShibiButterflyGuard)
    ];

    // 希比新开一局时的初始遗物。
    [Obsolete]
    protected override IReadOnlyList<Type> StartingRelicTypes => [typeof(GoldenButterflyKnot)];
}
