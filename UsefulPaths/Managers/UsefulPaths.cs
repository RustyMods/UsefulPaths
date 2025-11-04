using System.Text;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace UsefulPaths.Managers;

public static class UsefulPaths
{
    private static int hash;
    
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class Register_AirJordan
    {
        [UsedImplicitly]
        private static void Postfix(ObjectDB __instance)
        {
            if (!__instance || !ZNetScene.instance) return;
            AirJordan airJordan = ScriptableObject.CreateInstance<AirJordan>();
            airJordan.name = "SE_AirJordan";
            airJordan.m_icon = SpriteManager.WingedBoots;
            hash = airJordan.NameHash();
            if (__instance.m_StatusEffects.Contains(airJordan)) return;
            __instance.m_StatusEffects.Add(airJordan);
        }
    }

    [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.AddActiveEffects))]
    private static class Compendium_AddUsefulPath_Info
    {
        [UsedImplicitly]
        private static void Postfix(TextsDialog __instance)
        {
            if (UsefulPathsPlugin.ShowIcon) return;
            if (!Player.m_localPlayer.GetSEMan().HaveStatusEffect(hash)) return;
            string texts = __instance.m_texts[0].m_text;
    
            StatusEffect? se = Player.m_localPlayer.GetSEMan().GetStatusEffect(hash);
            
            texts += $"\n<color=orange>Useful Paths: {se.m_name}</color>\n";
            texts += se.GetTooltipString();
    
            __instance.m_texts[0].m_text = texts;
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.CustomFixedUpdate))]
    private static class Character_CustomFixedUpdate_Patch
    {
        private static float m_timer;
        [UsedImplicitly]
        private static void Postfix(Character __instance, float dt)
        {
            if (__instance.GetSEMan() is not { } man) return;
            m_timer += dt;
            if (m_timer < UsefulPathsPlugin.UpdateInterval) return;
            m_timer = 0.0f;
            if (!UsefulPathsPlugin.Enabled)
            {
                if (!man.HaveStatusEffect(hash)) return;
                man.RemoveStatusEffect(hash);
            }
            else
            {
                if (!__instance.IsPlayer() && (!__instance.IsTamed() || !UsefulPathsPlugin.ApplyToCreatures)) return;
                if (man.HaveStatusEffect(hash)) return;
                man.AddStatusEffect(hash);
            }
        }
    }
    
    [HarmonyPatch(typeof(Vagon), nameof(Vagon.SetMass))]
    private static class Vagon_SetMass_Patch
    {
        [UsedImplicitly]
        private static void Prefix(Vagon __instance, ref float mass)
        {
            if (__instance.m_attachedObject == null) return;
            if (!__instance.m_attachedObject.TryGetComponent(out Character character)) return;
            if (character.GetSEMan().GetStatusEffect(hash) is not AirJordan se) return;
            se.ModifyVagonMass(__instance.m_baseMass, ref mass);
        }
    }
}

public class AirJordan : StatusEffect
{
    private static readonly StringBuilder sb = new();

    private GroundTypes m_terrain = GroundTypes.None;
    private float m_timer;
    public override void UpdateStatusEffect(float dt)
    {
        base.UpdateStatusEffect(dt);
        m_timer += dt;
        if (m_timer < UsefulPathsPlugin.m_update.Value) return;
        m_timer = 0.0f;

        m_terrain = GetGround();
        m_name = m_terrain is GroundTypes.None ? "" : GetName();
        m_icon = UsefulPathsPlugin.ShowIcon ? GetIcon() : null;
    }
    public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
    {
        if (m_terrain is GroundTypes.None) return;
        float modifier = GetSpeedModifier(m_terrain);
        speed *= modifier;
    }

    public override void ModifyRunStaminaDrain(float baseDrain, ref float drain, Vector3 dir)
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

    public void ModifyVagonMass(float baseMass, ref float mass)
    {
        if (m_terrain is GroundTypes.None) return;
        float modifier = GetVagonModifier(m_terrain);
        mass = Mathf.Max(mass * modifier, baseMass);
    }
    
    private string GetName()
    {
        return Localization.instance.Localize(m_terrain switch
        {
            GroundTypes.Paved => "$piece_pavedroad",
            GroundTypes.Cultivated => "$piece_cultivate",
            GroundTypes.Wood => "$item_wood",
            GroundTypes.Stone => "$item_stone",
            GroundTypes.Grausten => "$item_grausten",
            _ => m_terrain.ToString()
        });
    }
    private Sprite? GetIcon() => m_terrain switch
    {
        GroundTypes.None => null,
        GroundTypes.Mud => SpriteManager.mud,
        GroundTypes.Paved => SpriteManager.paved,
        GroundTypes.Cultivated => SpriteManager.cultivated,
        GroundTypes.Wood => SpriteManager.wood,
        GroundTypes.Stone => SpriteManager.stone,
        GroundTypes.Dirt => SpriteManager.dirt,
        GroundTypes.Metal => SpriteManager.metal,
        GroundTypes.Grausten => SpriteManager.grausten,
        _ => SpriteManager.WingedBoots!
    };
    public override string GetTooltipString()
    {
        if (m_terrain is GroundTypes.None) return "";
        sb.Clear();

        var speed = GetSpeedModifier(m_terrain) * 100 - 100;
        var runStam = GetRunStaminaDrain(m_terrain) * 100 - 100;
        var stamRegen = GetStaminaRegen(m_terrain) * 100 - 100;
        var carry = GetMaxCarryWeight(m_terrain);
        var jump = GetJumpModifier(m_terrain) * 100 - 100;
        var vagon = GetVagonModifier(m_terrain) * 100 - 100;
        
        if (speed != 0f) sb.AppendFormat("{0}: <color=orange>{1:+0;-0}%</color>\n", "$item_movement_modifier", speed);
        if (runStam != 0f) sb.AppendFormat("{0}: <color=orange>{1:+0;-0}%</color>\n", "$se_runstamina", runStam);
        if (stamRegen != 0f) sb.AppendFormat("{0}: <color=orange>{1:+0;-0}%</color>\n", "$se_staminaregen", stamRegen);
        if (carry != 0f) sb.AppendFormat("{0}: <color=orange>{1:+0;-0}</color>\n", "$se_max_carryweight", carry);
        if (jump != 0f) sb.AppendFormat("{0}: <color=orange>{1:+0;-0}%</color>\n", "$se_jumpheight", jump);
        if (vagon != 0f) sb.AppendFormat("{0}: <color=orange>{1:+0;-0}%</color>", "$tool_cart Mass", vagon);
        return sb.ToString();
    }

    private static float GetVagonModifier(GroundTypes terrain)
    {
        return terrain switch
        {
            GroundTypes.Paved => UsefulPathsPlugin.m_vagonMass[GroundTypes.Paved].Value,
            GroundTypes.Dirt => UsefulPathsPlugin.m_vagonMass[GroundTypes.Dirt].Value,
            GroundTypes.Cultivated => UsefulPathsPlugin.m_vagonMass[GroundTypes.Cultivated].Value,
            GroundTypes.Mud => UsefulPathsPlugin.m_vagonMass[GroundTypes.Mud].Value,
            GroundTypes.Stone => UsefulPathsPlugin.m_vagonMass[GroundTypes.Stone].Value,
            GroundTypes.Wood => UsefulPathsPlugin.m_vagonMass[GroundTypes.Wood].Value,
            GroundTypes.Snow => UsefulPathsPlugin.m_vagonMass[GroundTypes.Snow].Value,
            GroundTypes.Metal => UsefulPathsPlugin.m_vagonMass[GroundTypes.Metal].Value,
            _ => 1f
        };
    }

    private static float GetSpeedModifier(GroundTypes terrain)
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
    
    private static float GetJumpModifier(GroundTypes terrain)
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
    
    private static float GetMaxCarryWeight(GroundTypes terrain)
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
            _ => 0f
        };
    }

    private static float GetRunStaminaDrain(GroundTypes terrain)
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

    private static float GetStaminaRegen(GroundTypes terrain)
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

    private static readonly int m_pieceLayer = LayerMask.NameToLayer("piece");
    private GroundTypes GetGround()
    {
        if (m_character == null || m_character.InWater() || m_character.InLiquid()) return GroundTypes.None;

        Collider? collider = m_character.GetLastGroundCollider();
        if (collider == null) return GroundTypes.None;

        if (collider.TryGetComponent(out Heightmap heightmap))
        {
            Vector3 point = m_character.transform.position;
            return heightmap.GetBiome(point) switch
            {
                Heightmap.Biome.Swamp => GroundTypes.Mud,
                Heightmap.Biome.Mountain or Heightmap.Biome.DeepNorth => GroundTypes.Snow,
                _ => GetGroundPaint()
            };

            GroundTypes GetGroundPaint()
            {
                heightmap.WorldToVertexMask(point, out int x, out int y);
                Color pixels = heightmap.m_paintMask.GetPixel(x, y);
                if (pixels.r > 0.5) return GroundTypes.Dirt;
                if (pixels.g > 0.5) return GroundTypes.Cultivated;
                if (pixels.b > 0.5) return GroundTypes.Paved;
                return GroundTypes.None;
            }
        }

        if (collider.gameObject.layer != m_pieceLayer) return GroundTypes.None;
        
        WearNTear? wear = collider.GetComponentInParent<WearNTear>();
        if (wear == null) return GroundTypes.None;

        return wear.m_materialType switch
        {
            WearNTear.MaterialType.Stone or WearNTear.MaterialType.Marble or WearNTear.MaterialType.Ancient => GroundTypes.Stone,
            WearNTear.MaterialType.Wood or WearNTear.MaterialType.HardWood => GroundTypes.Wood,
            WearNTear.MaterialType.Iron => GroundTypes.Metal,
            WearNTear.MaterialType.Ashstone => GroundTypes.Grausten,
            _ => GroundTypes.None
        };
    }
}

public enum GroundTypes
{
    None, Paved, Dirt, Cultivated, Mud, Snow, Metal, Stone, Wood, Grausten
}