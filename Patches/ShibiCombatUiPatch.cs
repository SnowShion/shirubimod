using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using shirubimod.Scripts.Content;
using shirubimod.Scripts.Content.Powers;

namespace shirubimod.Scripts.Patches;

[HarmonyPatch]
public static class ShibiCombatUiPatch
{
    private const string FullReleaseButtonName = "ShibiFullReleaseButton";
    private const string FullReleaseButtonLabelName = "ShibiFullReleaseButtonLabel";
    private const string ToughnessFillName = "Fill";
    private const string ToughnessBarPrefix = "ShibiToughnessBar_";
    private const string FullReleaseButtonEnabledPath = "res://images/ui/combat/shirubi_full_release_button_enabled.png";
    private const string FullReleaseButtonDisabledPath = "res://images/ui/combat/shirubi_full_release_button_disabled.png";
    private const string FullReleaseButtonPressedPath = "res://images/ui/combat/shirubi_full_release_button_pressed.png";
    private const string ToughnessBarFramePath = "res://images/ui/combat/shirubi_toughness_bar_frame.png";
    private const string ToughnessBarFillPath = "res://images/ui/combat/shirubi_toughness_bar_fill.png";
    private const string FullReleaseTooltip = """
希比的部分攻击卡带有打断词条：
看破：打断进攻。
破铠：打断添加格挡。
沉默：打断施加状态和添加手牌。

怪物会根据等级获得韧性条：
小怪2点，精英3点，Boss4点。
受到相应点数的打断攻击后，怪物会被击晕。
怪物被击晕后，韧性条会有一回合封锁，无法再次被击晕。

怪物被击晕后，希比会获得奖励或 buff，一些能力牌也会给予额外 buff。
小怪/精英/Boss 被击晕后，希比获得 1/1/2 点能量，并抽 1/2/3 张牌。

希比的部分卡带有“魔力”词条，打出后会给予玛娜。
当玛娜达到17点后，可手动开启“魔力全开”状态。
开启魔力全开时，从抽牌堆中抽出3张带有“魔力”的卡，并获得3点能量。
魔力全开状态下，所有带有“魔力”的卡会解放更强力的效果。
魔力全开状态下打出七张带有“魔力”的卡牌后，魔力全开状态消失。
""";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
    public static void AfterCombatUiActivated(NCombatUi __instance, CombatState state)
    {
        var player = LocalContext.GetMe(state);
        if (player?.Character is not ShirubiCharacter)
        {
            return;
        }

        EnsureFullReleaseButton(__instance, player);
        foreach (var enemy in state.Enemies.Where(enemy => enemy.IsMonster && !enemy.IsDead))
        {
            UpdateToughnessBar(enemy);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
    public static void AfterCreatureNodeAdded(Creature creature)
    {
        if (creature.IsMonster)
        {
            UpdateToughnessBar(creature);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.RemoveCreatureNode))]
    public static void AfterCreatureNodeRemoved(NCreature node)
    {
        if (node.Entity.IsMonster)
        {
            ClearToughnessBar(node.Entity);
        }
    }

    public static void RefreshFullReleaseButton()
    {
        var room = NCombatRoom.Instance;
        var state = room?.Ui?.GetNodeOrNull<TextureButton>(FullReleaseButtonName);
        if (room == null || state == null)
        {
            return;
        }

        var player = room.CreatureNodes.FirstOrDefault(node => node.Entity.IsPlayer && LocalContext.IsMe(node.Entity))?.Entity.Player;
        if (player != null)
        {
            RefreshFullReleaseButton(state, player);
            PositionFullReleaseButton(state, player);
        }
    }

    public static void UpdateToughnessBar(Creature creature)
    {
        var room = NCombatRoom.Instance;
        if (room?.Ui == null || !creature.IsMonster)
        {
            return;
        }

        if (creature.IsDead)
        {
            ClearToughnessBar(creature);
            return;
        }

        var creatureNode = room.GetCreatureNode(creature);
        if (creatureNode == null)
        {
            return;
        }

        var barName = GetToughnessBarName(creature);
        var bar = room.Ui.GetNodeOrNull<Control>(barName);
        if (bar == null)
        {
            bar = CreateToughnessBar(barName);
            room.Ui.AddChild(bar);
        }

        var max = Math.Max(1, ShibiMechanics.GetMaxToughness(creature));
        var current = Math.Clamp((int)creature.GetPowerAmount<ShibiToughnessPower>(), 0, max);
        var fill = bar.GetNodeOrNull<TextureProgressBar>(ToughnessFillName);
        if (fill != null)
        {
            fill.MaxValue = max;
            fill.Value = current;
        }
        bar.Visible = current > 0 && creature.IsAlive;
        bar.GlobalPosition = creatureNode.GetTopOfHitbox() + new Vector2(-80f, -42f);
    }

    public static void ClearToughnessBar(Creature creature)
    {
        var bar = NCombatRoom.Instance?.Ui?.GetNodeOrNull<Control>(GetToughnessBarName(creature));
        if (bar == null)
        {
            return;
        }

        bar.Visible = false;
        bar.GetParent()?.RemoveChild(bar);
        bar.QueueFree();
    }

    private static void EnsureFullReleaseButton(NCombatUi ui, Player player)
    {
        var button = ui.GetNodeOrNull<TextureButton>(FullReleaseButtonName);
        if (button == null)
        {
            button = CreateFullReleaseButton();
            button.Pressed += () => TaskHelper.RunSafely(OnFullReleasePressed(player));
            ui.AddChild(button);
        }

        RefreshFullReleaseButton(button, player);
        PositionFullReleaseButton(button, player);
    }

    private static async System.Threading.Tasks.Task OnFullReleasePressed(Player player)
    {
        await ShibiMechanics.TryActivateFullReleaseFromButton(player);
        RefreshFullReleaseButton();
    }

    private static void RefreshFullReleaseButton(TextureButton button, Player player)
    {
        var mana = (int)player.Creature.GetPowerAmount<ShibiManaPower>();
        var active = player.Creature.GetPowerAmount<ShibiFullReleasePower>() > 0;
        var isMultiplayer = player.RunState.Players.Count > 1;
        var label = button.GetNodeOrNull<Label>(FullReleaseButtonLabelName);
        if (label != null)
        {
            label.Text = isMultiplayer
                ? "多人\n禁用"
                : active
                ? $"全开\n{mana}/{ShibiMechanics.ManaRequiredForFullRelease}"
                : $"{mana}/{ShibiMechanics.ManaRequiredForFullRelease}";
        }
        button.Disabled = isMultiplayer || mana < ShibiMechanics.ManaRequiredForFullRelease;
        button.Visible = player.Creature.IsAlive;
    }

    private static void PositionFullReleaseButton(TextureButton button, Player player)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode == null)
        {
            button.Position = new Vector2(250f, 735f);
            return;
        }

        button.GlobalPosition = creatureNode.GetTopOfHitbox() + new Vector2(-154f, 8f);
    }

    private static TextureButton CreateFullReleaseButton()
    {
        var button = new TextureButton
        {
            Name = FullReleaseButtonName,
            CustomMinimumSize = new Vector2(64f, 64f),
            Size = new Vector2(64f, 64f),
            TooltipText = FullReleaseTooltip,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        button.TextureNormal = LoadTexture(FullReleaseButtonEnabledPath);
        button.TextureDisabled = LoadTexture(FullReleaseButtonDisabledPath);
        button.TexturePressed = LoadTexture(FullReleaseButtonPressedPath);
        button.IgnoreTextureSize = true;
        button.StretchMode = TextureButton.StretchModeEnum.Scale;

        var label = new Label
        {
            Name = FullReleaseButtonLabelName,
            Size = new Vector2(64f, 64f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        button.AddChild(label);
        return button;
    }

    private static Control CreateToughnessBar(string name)
    {
        var bar = new Control
        {
            Name = name,
            Size = new Vector2(160f, 32f),
            CustomMinimumSize = new Vector2(160f, 32f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var fill = new TextureProgressBar
        {
            Name = ToughnessFillName,
            Position = Vector2.Zero,
            Size = new Vector2(160f, 32f),
            CustomMinimumSize = new Vector2(160f, 32f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        fill.TextureProgress = LoadTexture(ToughnessBarFillPath);
        bar.AddChild(fill);
        return bar;
    }

    private static Texture2D? LoadTexture(string path) =>
        ResourceLoader.Load<Texture2D>(path);

    private static string GetToughnessBarName(Creature creature) =>
        ToughnessBarPrefix + creature.GetHashCode();
}
