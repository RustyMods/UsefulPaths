using System.Text;
using HarmonyLib;
using UnityEngine;

namespace UsefulPaths.Managers;

public static class UsefulPaths
{
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class Register_AirJordan
    {
        private static void Postfix(ObjectDB __instance)
        {
            if (!__instance || !ZNetScene.instance) return;
            AirJordan airJordan = ScriptableObject.CreateInstance<AirJordan>();
            airJordan.name = "SE_AirJordan";
            airJordan.m_icon = SpriteManager.WingedBoots;
            
            if (__instance.m_StatusEffects.Contains(airJordan)) return;
            __instance.m_StatusEffects.Add(airJordan);
        }
    }

    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.AddActiveEffects))]
    private static class Compendium_AddUsefulPath_Info
    {
        private static void Postfix(TextsDialog __instance)
        {
            if (UsefulPathsPlugin.m_showIcon.Value is UsefulPathsPlugin.Toggle.On) return;
            if (!Player.m_localPlayer.GetSEMan().HaveStatusEffect("SE_AirJordan".GetStableHashCode())) return;
            string texts = __instance.m_texts[0].m_text;
    
            StatusEffect? se = Player.m_localPlayer.GetSEMan().GetStatusEffect("SE_AirJordan".GetStableHashCode());
            
            texts += $"\n<color=orange>Useful Paths: {se.m_name}</color>\n";
            texts += se.GetTooltipString();
    
            __instance.m_texts[0].m_text = texts;
        }
    }

    private static float m_timer;
    public static void UpdateStatusEffect(float dt)
    {
        if (!Player.m_localPlayer) return;

        m_timer += dt;
        if (m_timer < UsefulPathsPlugin.m_update.Value) return;
        m_timer = 0.0f;
        
        SEMan man = Player.m_localPlayer.GetSEMan();
        if (man == null) return;
        
        if (UsefulPathsPlugin.m_enabled.Value is UsefulPathsPlugin.Toggle.Off)
        {
            if (man.HaveStatusEffect("SE_AirJordan".GetStableHashCode()))
            {
                man.RemoveStatusEffect("SE_AirJordan".GetStableHashCode());
            }
        }
        else
        {
            if (man.HaveStatusEffect("SE_AirJordan".GetStableHashCode())) return;
            man.AddStatusEffect("SE_AirJordan".GetStableHashCode());
        }
    }
}

public class AirJordan : StatusEffect
{
    private float m_timer;
    
    private GroundTypes m_terrain = GroundTypes.None;
    
    public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
    {
        if (m_terrain is GroundTypes.None) return;
        float modifier = GetSpeedModifier(m_terrain);
        speed *= modifier;
    }

    public override void ModifyRunStaminaDrain(float baseDrain, ref float drain)
    {
        if (m_terrain is GroundTypes.None) return;
        float modifier = GetRunStaminaDrain(m_terrain);
        drain *= modifier;
    }

    public override void ModifyStaminaRegen(ref float staminaRegen)
    {
        if (m_terrain is GroundTypes.None) return;
        float modifier = GetStaminaRegen(m_terrain);
        staminaRegen *= modifier;
    }

    public override void ModifyMaxCarryWeight(float baseLimit, ref float limit)
    {
        if (m_terrain is GroundTypes.None) return;
        float modifier = GetMaxCarryWeight(m_terrain);
        limit += modifier;
    }

    public override void ModifyJump(Vector3 baseJump, ref Vector3 jump)
    {
        if (m_terrain is GroundTypes.None) return;
        float modifier = GetJumpModifier(m_terrain);
        jump = new Vector3(jump.x * modifier, jump.y * modifier, jump.z * modifier);
    }

    public override void UpdateStatusEffect(float dt)
    {
        base.UpdateStatusEffect(dt);
        m_timer += dt;
        if (m_timer < UsefulPathsPlugin.m_update.Value) return;
        m_timer = 0.0f;
        
        m_terrain = GetTerrain();
        m_name = m_terrain is GroundTypes.None ? "" : m_terrain.ToString();
        m_icon = UsefulPathsPlugin.m_showIcon.Value is UsefulPathsPlugin.Toggle.On ? m_terrain is GroundTypes.None ? null : SpriteManager.WingedBoots : null;
    }

    private string GetName()
    {
        return Localization.instance.Localize(m_terrain switch
        {
            GroundTypes.Paved => "$piece_pavedroad",
            GroundTypes.Cultivated => "$piece_cultivate",
            _ => m_terrain.ToString()
        });
    }

    public override string GetTooltipString()
    {
        if (m_terrain is GroundTypes.None) return "";
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(FormatTooltip("$item_movement_modifier", GetSpeedModifier(m_terrain)));
        stringBuilder.Append(FormatTooltip("$se_runstamina", GetRunStaminaDrain(m_terrain)));
        stringBuilder.Append(FormatTooltip("$se_staminaregen", GetStaminaRegen(m_terrain)));
        stringBuilder.Append(FormatTooltip("$se_max_carryweight", GetMaxCarryWeight(m_terrain)));
        stringBuilder.Append(FormatTooltip("$se_jumpheight", GetJumpModifier(m_terrain)));
        
        return Localization.instance.Localize(stringBuilder.ToString());
    }

    private string FormatTooltip(string key, float modifier)
    {
        if (key is "$se_max_carryweight")
        {
            string symbol = modifier > 0f ? "+" : "";
            return modifier == 0f ? "" : $"{key}: <color=orange>{symbol}{(int)modifier}\n";
        }
        else
        {
            float value = modifier * 100 - 100;
            string symbol = modifier > 1f ? "+" : "";
            return value == 0 ? "" :  $"{key}: <color=orange>{symbol}{(int)value}%</color>\n";
        }
    }

    private float GetSpeedModifier(GroundTypes terrain)
    {
        return terrain switch
        {
            GroundTypes.Paved => UsefulPathsPlugin.m_speed[GroundTypes.Paved].Value,
            GroundTypes.Dirt => UsefulPathsPlugin.m_speed[GroundTypes.Dirt].Value,
            GroundTypes.Cultivated => UsefulPathsPlugin.m_speed[GroundTypes.Cultivated].Value,
            GroundTypes.Mud => UsefulPathsPlugin.m_speed[GroundTypes.Mud].Value,
            GroundTypes.Stone => UsefulPathsPlugin.m_speed[GroundTypes.Stone].Value,
            GroundTypes.Wood => UsefulPathsPlugin.m_speed[GroundTypes.Wood].Value,
            GroundTypes.Snow => UsefulPathsPlugin.m_speed[GroundTypes.Snow].Value,
            GroundTypes.Metal => UsefulPathsPlugin.m_speed[GroundTypes.Metal].Value,
            _ => 1f
        };
    }
    
    private float GetJumpModifier(GroundTypes terrain)
    {
        return terrain switch
        {
            GroundTypes.Paved => UsefulPathsPlugin.m_jump[GroundTypes.Paved].Value,
            GroundTypes.Dirt => UsefulPathsPlugin.m_jump[GroundTypes.Dirt].Value,
            GroundTypes.Cultivated => UsefulPathsPlugin.m_jump[GroundTypes.Cultivated].Value,
            GroundTypes.Mud => UsefulPathsPlugin.m_jump[GroundTypes.Mud].Value,
            GroundTypes.Stone => UsefulPathsPlugin.m_jump[GroundTypes.Stone].Value,
            GroundTypes.Wood => UsefulPathsPlugin.m_jump[GroundTypes.Wood].Value,
            GroundTypes.Snow => UsefulPathsPlugin.m_jump[GroundTypes.Snow].Value,
            GroundTypes.Metal => UsefulPathsPlugin.m_jump[GroundTypes.Metal].Value,
            _ => 1f
        };
    }
    
    private float GetMaxCarryWeight(GroundTypes terrain)
    {
        return terrain switch
        {
            GroundTypes.Paved => UsefulPathsPlugin.m_carryWeight[GroundTypes.Paved].Value,
            GroundTypes.Dirt => UsefulPathsPlugin.m_carryWeight[GroundTypes.Dirt].Value,
            GroundTypes.Cultivated => UsefulPathsPlugin.m_carryWeight[GroundTypes.Cultivated].Value,
            GroundTypes.Mud => UsefulPathsPlugin.m_carryWeight[GroundTypes.Mud].Value,
            GroundTypes.Stone => UsefulPathsPlugin.m_carryWeight[GroundTypes.Stone].Value,
            GroundTypes.Wood => UsefulPathsPlugin.m_carryWeight[GroundTypes.Wood].Value,
            GroundTypes.Snow => UsefulPathsPlugin.m_carryWeight[GroundTypes.Snow].Value,
            GroundTypes.Metal => UsefulPathsPlugin.m_carryWeight[GroundTypes.Metal].Value,
            _ => 1f
        };
    }

    private float GetRunStaminaDrain(GroundTypes terrain)
    {
        return terrain switch
        {
            GroundTypes.Paved => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Paved].Value,
            GroundTypes.Dirt => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Dirt].Value,
            GroundTypes.Cultivated => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Cultivated].Value,
            GroundTypes.Mud => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Mud].Value,
            GroundTypes.Stone => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Stone].Value,
            GroundTypes.Wood => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Wood].Value,
            GroundTypes.Snow => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Snow].Value,
            GroundTypes.Metal => UsefulPathsPlugin.m_runStaminaDrain[GroundTypes.Metal].Value,
            _ => 1f
        };
    }

    private float GetStaminaRegen(GroundTypes terrain)
    {
        return terrain switch
        {
            GroundTypes.Paved => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Paved].Value,
            GroundTypes.Dirt => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Dirt].Value,
            GroundTypes.Cultivated => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Cultivated].Value,
            GroundTypes.Mud => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Mud].Value,
            GroundTypes.Stone => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Stone].Value,
            GroundTypes.Wood => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Wood].Value,
            GroundTypes.Snow => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Snow].Value,
            GroundTypes.Metal => UsefulPathsPlugin.m_staminaRegen[GroundTypes.Metal].Value,
            _ => 1f
        };
    }

    private GroundTypes GetTerrain()
    {
        if (m_character == null) return GroundTypes.None;
        if (m_character is not Player) return GroundTypes.None;
        if (!m_character.TryGetComponent(out FootStep component)) return GroundTypes.None;
        if (component.m_character == null) return GroundTypes.None;
        FootStep.GroundMaterial material = component.GetGroundMaterial(component.m_character, component.m_character.transform.position);
        if (material is FootStep.GroundMaterial.Grass or FootStep.GroundMaterial.GenericGround or FootStep.GroundMaterial.Ashlands)
        {
            TerrainModifier.PaintType paint = GetPaintType(component.m_character);
            return paint switch
            {   
                TerrainModifier.PaintType.Dirt => GroundTypes.Dirt,
                TerrainModifier.PaintType.Cultivate => GroundTypes.Cultivated,
                TerrainModifier.PaintType.Paved => GroundTypes.Paved,
                _ => WorldGenerator.instance.GetBiome(m_character.transform.position) is Heightmap.Biome.Mountain ? GroundTypes.Snow : GroundTypes.None
            };
        }

        return material switch
        {
            FootStep.GroundMaterial.Stone => GroundTypes.Stone,
            FootStep.GroundMaterial.Wood => GroundTypes.Wood,
            FootStep.GroundMaterial.Snow => GroundTypes.Snow,
            FootStep.GroundMaterial.Mud => GroundTypes.Mud,
            FootStep.GroundMaterial.Metal => GroundTypes.Metal,
            _ => GroundTypes.None
        };
    }
    
    private TerrainModifier.PaintType GetPaintType(Character character)
    {
        Collider ground = character.GetLastGroundCollider();
        if (ground == null) return TerrainModifier.PaintType.Reset;
        if (!ground.TryGetComponent(out Heightmap component)) return TerrainModifier.PaintType.Reset;
        
        component.WorldToVertexMask(character.transform.position, out int x, out int y);
        Color pixels = component.m_paintMask.GetPixel(x, y);
        if (pixels.r > 0.5) return TerrainModifier.PaintType.Dirt;
        if (pixels.g > 0.5) return TerrainModifier.PaintType.Cultivate;
        if (pixels.b > 0.5) return TerrainModifier.PaintType.Paved;
        return TerrainModifier.PaintType.Reset;
    }
}

public enum GroundTypes
{
    None, Paved, Dirt, Cultivated, Mud, Snow, Metal, Stone, Wood
}