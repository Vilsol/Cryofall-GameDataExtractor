using AtomicTorch.CBND.CoreMod.Bootstrappers;
using AtomicTorch.CBND.CoreMod.Characters;
using AtomicTorch.CBND.CoreMod.CharacterStatusEffects;
using AtomicTorch.CBND.CoreMod.Items;
using AtomicTorch.CBND.CoreMod.Items.Ammo;
using AtomicTorch.CBND.CoreMod.Items.DataLogs.Base;
using AtomicTorch.CBND.CoreMod.Items.Devices;
using AtomicTorch.CBND.CoreMod.Items.Equipment;
using AtomicTorch.CBND.CoreMod.Items.Explosives;
using AtomicTorch.CBND.CoreMod.Items.Food;
using AtomicTorch.CBND.CoreMod.Items.Generic;
using AtomicTorch.CBND.CoreMod.Items.Medical;
using AtomicTorch.CBND.CoreMod.Items.Seeds;
using AtomicTorch.CBND.CoreMod.Items.Tools;
using AtomicTorch.CBND.CoreMod.Items.Tools.Crowbars;
using AtomicTorch.CBND.CoreMod.Items.Tools.Lights;
using AtomicTorch.CBND.CoreMod.Items.Tools.Toolboxes;
using AtomicTorch.CBND.CoreMod.Items.Tools.WateringCans;
using AtomicTorch.CBND.CoreMod.Items.Weapons;
using AtomicTorch.CBND.CoreMod.Objects;
using AtomicTorch.CBND.CoreMod.Quests;
using AtomicTorch.CBND.CoreMod.Skills;
using AtomicTorch.CBND.CoreMod.StaticObjects.Deposits;
using AtomicTorch.CBND.CoreMod.StaticObjects.Explosives;
using AtomicTorch.CBND.CoreMod.StaticObjects.Loot;
using AtomicTorch.CBND.CoreMod.StaticObjects.Minerals;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Barrels;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Beds;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Crates;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Floors;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.LandClaim;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Lights;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers;
using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.TradingStations;
using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation;
using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Plants;
using AtomicTorch.CBND.CoreMod.Stats;
using AtomicTorch.CBND.CoreMod.Systems;
using AtomicTorch.CBND.CoreMod.Systems.Construction;
using AtomicTorch.CBND.CoreMod.Systems.Crafting;
using AtomicTorch.CBND.CoreMod.Systems.Droplists;
using AtomicTorch.CBND.CoreMod.Systems.Resources;
using AtomicTorch.CBND.CoreMod.Systems.Weapons;
using AtomicTorch.CBND.CoreMod.Technologies;
using AtomicTorch.CBND.GameApi.Data.Characters;
using AtomicTorch.CBND.GameApi.Data.Items;
using AtomicTorch.CBND.GameApi.Data.Weapons;
using AtomicTorch.CBND.GameApi.Data.World;
using AtomicTorch.CBND.GameApi.Scripting;
using AtomicTorch.CBND.GameApi.Scripting.Network;
using AtomicTorch.GameEngine.Common.Primitives;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static AtomicTorch.CBND.CoreMod.Systems.Crafting.Recipe;

namespace AtomicTorch.CBND.GameApi.Data {

    // Requirements:
    // Change DropItemsList.entries to public
    // Change DropItemsList.Entry to public

    [PrepareOrder(afterType: typeof(BootstrapperClientOptions))]
    public class BootstrapperClientGameDataExtractor : BaseBootstrapper {

        private static BootstrapperClientGameDataExtractor instance;

        public override void ClientInitialize() {
            instance = this;
            BootstrapperClientGame.InitEndCallback += GameInitHandler;
        }

        private static void GameInitHandler(ICharacter currentCharacter) {
            var result = new Dictionary<string, object>();

            foreach(IProtoEntity entity in Api.FindProtoEntities<IProtoEntity>()) {
                // Api.Logger.Dev(entity.ShortId);
                result[entity.Id] = instance.ParseEntity(entity);
            }

            Api.Logger.Dev("Total: " + result.Count);

            var settingsStorage = Api.Client.Storage.GetStorage("ExtractedGameOutputData");
            settingsStorage.RegisterType(typeof(string));
            settingsStorage.Save(instance.ToJson(result));
        }

        public Dictionary<string, object> ParseEntity(IProtoEntity entity) {
            var result = new Dictionary<string, object>();

            result["name"] = entity.Name;
            result["short_id"] = entity.ShortId;
            result["base_class"] = entity.GetType().BaseType.Name;

            if (entity is IProtoItem) {
                result["IProtoItem"] = ParsetItem((IProtoItem)entity);
            }

            if(entity is TechNode) {
                var inner = new Dictionary<string, object>();
                inner["description"] = ((TechNode)entity).Description;
                inner["group_id"] = ((TechNode)entity).Group.Id;
                inner["hierarchy_level"] = ((TechNode)entity).HierarchyLevel;
                inner["lp_price"] = ((TechNode)entity).LearningPointsPrice;

                if(((TechNode)entity).RequiredNode != null) {
                    inner["required_node"] = ((TechNode)entity).RequiredNode.Name;
                }

                result["TechNode"] = inner;
            }

            if(entity is TechGroup) {
                var inner = new Dictionary<string, object>();
                inner["description"] = ((TechGroup)entity).Description;
                inner["tier"] = (byte)((TechGroup)entity).Tier;
                inner["primary"] = ((TechGroup)entity).IsPrimary;
                inner["lp_price"] = ((TechGroup)entity).LearningPointsPrice;
                result["TechGroup"] = inner;
            }

            if(entity is IProtoSkill) {
                var inner = new Dictionary<string, object>();
                inner["description"] = ((IProtoSkill)entity).Description;
                inner["category"] = ((IProtoSkill)entity).Category.Name;
                inner["max"] = ((IProtoSkill)entity).MaxLevel;

                if(entity is ProtoSkill) {
                    inner["lp_shared"] = ((ProtoSkill)entity).IsSharingLearningPointsWithPartyMembers;
                }

                var xp_required = new List<object>();
                for(var i = 1; i <= ((IProtoSkill)entity).MaxLevel; i++) {
                    xp_required.Add(((IProtoSkill)entity).GetExperienceForLevel((byte)i));
                }
                inner["xp_required"] = xp_required;

                var effects = new List<object>();
                foreach(ISkillEffect effect in ((IProtoSkill)entity).GetEffects()) {
                    var it = new Dictionary<string, object>();
                    it["level"] = effect.Level;
                    it["type"] = effect.GetType().ToString();
                    it["description"] = effect.Description;

                    if(effect is StatEffect) {
                        it["stat"] = ((StatEffect)effect).StatName;

                        var value_bonus = new List<object>();
                        for(var i = 1; i <= ((IProtoSkill)entity).MaxLevel; i++) {
                            value_bonus.Add(((StatEffect)effect).CalcTotalValueBonus((byte)i));
                        }
                        it["value_bonus"] = value_bonus;

                        var percent_bonus = new List<object>();
                        for(var i = 1; i <= ((IProtoSkill)entity).MaxLevel; i++) {
                            percent_bonus.Add(((StatEffect)effect).CalcTotalPercentBonus((byte)i));
                        }
                        it["percent_bonus"] = percent_bonus;
                    }

                    effects.Add(it);
                }
                inner["effects"] = effects;
                result["IProtoSkill"] = inner;
            }

            if(entity is IProtoCharacterMob) {
                var inner = new Dictionary<string, object>();
                inner["move_speed"] = ((IProtoCharacterMob)entity).StatMoveSpeed;
                inner["drops"] = ParseDropList(((IProtoCharacterMob)entity).LootDroplist);
                result["IProtoCharacterMob"] = inner;
            }

            if(entity is Recipe) {
                var inner = new Dictionary<string, object>();
                inner["type"] = ((Recipe)entity).RecipeType.ToString();
                inner["duration"] = ((Recipe)entity).OriginalDuration;
                inner["cancellable"] = ((Recipe)entity).IsCancellable;
                inner["auto_unlocked"] = ((Recipe)entity).IsAutoUnlocked;

                var stations = new List<object>();
                if(entity is BaseRecipeForStation) {
                    foreach(IProtoStaticWorldObject station in ((BaseRecipeForStation)entity).StationTypes) {
                        stations.Add(station.Id);
                    }
                }
                inner["stations"] = stations;

                var inputs = new List<object>();
                foreach(ProtoItemWithCount item in ((Recipe)entity).InputItems) {
                    var it = new Dictionary<string, object>();
                    it["id"] = item.ProtoItem.Id;
                    it["count"] = item.Count;
                    inputs.Add(it);
                }
                inner["inputs"] = inputs;

                var outputs = new List<object>();
                foreach(OutputItem item in ((Recipe)entity).OutputItems.Items) {
                    var it = new Dictionary<string, object>();
                    it["id"] = item.ProtoItem.Id;
                    it["count"] = item.Count;
                    it["count_rng"] = item.CountRandom;
                    it["probability"] = item.Probability;
                    outputs.Add(it);
                }
                inner["outputs"] = outputs;

                result["Recipe"] = inner;
            }

            if(entity is IProtoQuest) {
                var inner = new Dictionary<string, object>();
                inner["description"] = ((IProtoQuest)entity).Description;
                inner["hints"] = ((IProtoQuest)entity).Hints;
                inner["reward_lp"] = ((IProtoQuest)entity).RewardLearningPoints;

                var requirements = new List<object>();
                foreach(IQuestRequirement item in ((IProtoQuest)entity).Requirements) {
                    var it = new Dictionary<string, object>();
                    it["description"] = item.Description;
                    it["reversible"] = item.IsReversible;
                    requirements.Add(it);
                }
                inner["requirements"] = requirements;

                var prerequisites = new List<object>();
                foreach(IProtoQuest item in ((IProtoQuest)entity).Prerequisites) {
                    prerequisites.Add(item.Id);
                }
                inner["prerequisites"] = requirements;

                result["IProtoQuest"] = inner;
            }

            if(entity is IProtoStaticWorldObject) {
                var inner = new Dictionary<string, object>();
                inner["tooltip"] = ((IProtoStaticWorldObject)entity).InteractionTooltipText;
                inner["interactable"] = ((IProtoStaticWorldObject)entity).IsInteractableObject;
                inner["kind"] = ((IProtoStaticWorldObject)entity).Kind;
                inner["struct_points"] = ((IProtoStaticWorldObject)entity).StructurePointsMax;
                inner["struct_def_coef"] = ((IProtoStaticWorldObject)entity).StructureExplosiveDefenseCoef;
                result["IProtoStaticWorldObject"] = inner;
            }

            if(entity is IDamageableProtoWorldObject) {
                var inner = new Dictionary<string, object>();
                inner["obstacle_block_coef"] = ((IDamageableProtoWorldObject)entity).ObstacleBlockDamageCoef;
                result["IDamageableProtoWorldObject"] = inner;
            }

            if(entity is IProtoObjectDeposit) {
                var inner = new Dictionary<string, object>();
                inner["lifetime_seconds"] = ((IProtoObjectDeposit)entity).LifetimeTotalDurationSeconds;
                result["IProtoObjectDeposit"] = inner;
            }

            if(entity is IProtoObjectExplosive) {
                var inner = new Dictionary<string, object>();
                inner["struct_damage"] = ((IProtoObjectExplosive)entity).StructureDamage;
                inner["struct_pen_coef"] = ((IProtoObjectExplosive)entity).StructureDefensePenetrationCoef;
                result["IProtoObjectExplosive"] = inner;
            }

            if(entity is IProtoObjectLoot) {
                var inner = new Dictionary<string, object>();
                inner["drops"] = ParseDropList(((IProtoObjectLoot)entity).LootDroplist);
                result["IProtoObjectLoot"] = inner;
            }

            if(entity is IProtoObjectMineral) {
                var inner = new Dictionary<string, object>();
                inner["drops_1"] = ParseDropList(((IProtoObjectMineral)entity).DropItemsConfig.Stage1);
                inner["drops_2"] = ParseDropList(((IProtoObjectMineral)entity).DropItemsConfig.Stage2);
                inner["drops_3"] = ParseDropList(((IProtoObjectMineral)entity).DropItemsConfig.Stage3);
                inner["drops_4"] = ParseDropList(((IProtoObjectMineral)entity).DropItemsConfig.Stage4);
                result["IProtoObjectMineral"] = inner;
            }

            if(entity is IProtoObjectStructure) {
                var inner = new Dictionary<string, object>();
                inner["description"] = ((IProtoObjectStructure)entity).Description;
                inner["description_upgrade"] = ((IProtoObjectStructure)entity).DescriptionUpgrade;
                inner["auto_unlocked"] = ((IProtoObjectStructure)entity).IsAutoUnlocked;
                inner["category"] = ((IProtoObjectStructure)entity).Category.ToString();
                inner["repair_allowed"] = ((IProtoObjectStructure)entity).ConfigRepair.IsAllowed;
                inner["repair_seconds"] = ((IProtoObjectStructure)entity).ConfigRepair.StageDurationSeconds;
                inner["build_allowed"] = ((IProtoObjectStructure)entity).ConfigBuild.IsAllowed;
                inner["build_seconds"] = ((IProtoObjectStructure)entity).ConfigBuild.StageDurationSeconds;

                var build_requirements = new List<object>();
                foreach(ProtoItemWithCount item in ((IProtoObjectStructure)entity).ConfigBuild.StageRequiredItems) {
                    var it = new Dictionary<string, object>();
                    it["id"] = item.ProtoItem.Id;
                    it["count"] = item.Count;
                    build_requirements.Add(it);
                }
                inner["build_requirements"] = build_requirements;

                var repair_requirements = new List<object>();
                foreach(ProtoItemWithCount item in ((IProtoObjectStructure)entity).ConfigRepair.StageRequiredItems) {
                    var it = new Dictionary<string, object>();
                    it["id"] = item.ProtoItem.Id;
                    it["count"] = item.Count;
                    repair_requirements.Add(it);
                }
                inner["repair_requirements"] = repair_requirements;

                var upgrades = new List<object>();
                foreach(IConstructionUpgradeEntryReadOnly item in ((IProtoObjectStructure)entity).ConfigUpgrade.Entries) {
                    var it = new Dictionary<string, object>();
                    it["id"] = item.ProtoStructure.Id;
                    var requirements = new List<object>();
                    foreach(ProtoItemWithCount innerItems in item.RequiredItems) {
                        var it2 = new Dictionary<string, object>();
                        it2["id"] = innerItems.ProtoItem.Id;
                        it2["count"] = innerItems.Count;
                        requirements.Add(it2);
                    }
                    it["requirements"] = requirements;
                    upgrades.Add(it);
                }
                inner["upgrades"] = upgrades;
                result["IProtoObjectStructure"] = inner;
            }

            if(entity is IProtoObjectGatherableVegetation) {
                var inner = new Dictionary<string, object>();
                inner["growth_stages"] = ((IProtoObjectGatherableVegetation)entity).GrowthStagesCount;
                inner["drops"] = ParseDropList(((IProtoObjectGatherableVegetation)entity).GatherDroplist);
                result["IProtoObjectGatherableVegetation"] = inner;
            }

            if(entity is IProtoObjectGatherable) {
                var inner = new Dictionary<string, object>();
                inner["gather_seconds"] = ((IProtoObjectGatherable)entity).DurationGatheringSeconds;
                result["IProtoObjectGatherable"] = inner;
            }

            if(entity is IProtoObjectPlant) {
                var inner = new Dictionary<string, object>();
                inner["harvest_count"] = ((IProtoObjectPlant)entity).NumberOfHarvests;
                result["IProtoObjectPlant"] = inner;
            }

            if(entity is IProtoStatusEffect) {
                var inner = new Dictionary<string, object>();
                inner["description"] = ((IProtoStatusEffect)entity).Description;
                inner["removed_on_respawn"] = ((IProtoStatusEffect)entity).IsRemovedOnRespawn;
                inner["kind"] = ((IProtoStatusEffect)entity).Kind.ToString();

                var stat_multipliers = new Dictionary<string, object>();
                foreach(StatName name in ((IProtoStatusEffect)entity).ProtoEffects.Multipliers.Keys) {
                    stat_multipliers[name.ToString()] = ((IProtoStatusEffect)entity).ProtoEffects.Multipliers[name];
                }
                inner["stat_multipliers"] = stat_multipliers;

                var stat_values = new Dictionary<string, object>();
                foreach(StatName name in ((IProtoStatusEffect)entity).ProtoEffects.Values.Keys) {
                    stat_values[name.ToString()] = ((IProtoStatusEffect)entity).ProtoEffects.Values[name];
                }
                inner["stat_values"] = stat_values;

                result["IProtoStatusEffect"] = inner;
            }

            if(entity is IProtoCharacterCore) {
                var inner = new Dictionary<string, object>();
                inner["default_health_max"] = ((IProtoCharacterCore)entity).StatDefaultHealthMax;

                var stat_multipliers = new Dictionary<string, object>();
                foreach(StatName name in ((IProtoCharacterCore)entity).ProtoCharacterDefaultEffects.Multipliers.Keys) {
                    stat_multipliers[name.ToString()] = ((IProtoCharacterCore)entity).ProtoCharacterDefaultEffects.Multipliers[name];
                }
                inner["stat_multipliers"] = stat_multipliers;

                var stat_values = new Dictionary<string, object>();
                foreach(StatName name in ((IProtoCharacterCore)entity).ProtoCharacterDefaultEffects.Values.Keys) {
                    stat_values[name.ToString()] = ((IProtoCharacterCore)entity).ProtoCharacterDefaultEffects.Values[name];
                }
                inner["stat_values"] = stat_values;

                result["IProtoCharacterCore"] = inner;
            }

            if(entity is IProtoObjectHeatSource) {
                var inner = new Dictionary<string, object>();
                inner["heat_intensity"] = ((IProtoObjectHeatSource)entity).HeatIntensity;
                inner["heat_radius_max"] = ((IProtoObjectHeatSource)entity).HeatRadiusMax;
                inner["heat_radius_min"] = ((IProtoObjectHeatSource)entity).HeatRadiusMin;
                result["IProtoObjectHeatSource"] = inner;
            }

            if(entity is IProtoObjectPsiSource) {
                var inner = new Dictionary<string, object>();
                inner["psi_intensity"] = ((IProtoObjectPsiSource)entity).PsiIntensity;
                inner["psi_radius_max"] = ((IProtoObjectPsiSource)entity).PsiRadiusMax;
                inner["psi_radius_min"] = ((IProtoObjectPsiSource)entity).PsiRadiusMin;
                result["IProtoObjectPsiSource"] = inner;
            }

            if(entity is IProtoObjectTradingStation) {
                var inner = new Dictionary<string, object>();
                inner["lots_count"] = ((IProtoObjectTradingStation)entity).LotsCount;
                inner["slots_count"] = ((IProtoObjectTradingStation)entity).StockItemsContainerSlotsCount;
                result["IProtoObjectTradingStation"] = inner;
            }

            if(entity is IProtoObjectExtractor) {
                var inner = new Dictionary<string, object>();
                inner["liquid_capacity"] = ((IProtoObjectExtractor)entity).LiquidCapacity;
                inner["liquid_production_per_second"] = ((IProtoObjectExtractor)entity).LiquidProductionAmountPerSecond;
                result["IProtoObjectExtractor"] = inner;
            }

            if(entity is IProtoObjectManufacturer) {
                var inner = new Dictionary<string, object>();
                inner["fuel_slots_count"] = ((IProtoObjectManufacturer)entity).ContainerFuelSlotsCount;
                inner["input_slots_count"] = ((IProtoObjectManufacturer)entity).ContainerInputSlotsCount;
                inner["output_slots_count"] = ((IProtoObjectManufacturer)entity).ContainerOutputSlotsCount;
                inner["auto_select_recipe"] = ((IProtoObjectManufacturer)entity).IsAutoSelectRecipe;
                inner["produce_byproducts"] = ((IProtoObjectManufacturer)entity).IsFuelProduceByproducts;
                result["IProtoObjectManufacturer"] = inner;
            }

            if(entity is IProtoObjectMulchbox) {
                var inner = new Dictionary<string, object>();
                inner["organic_capacity"] = ((IProtoObjectMulchbox)entity).OrganicCapacity;
                result["Dictionary"] = inner;
            }

            if(entity is IProtoObjectWell) {
                var inner = new Dictionary<string, object>();
                inner["water_capacity"] = ((IProtoObjectWell)entity).WaterCapacity;
                inner["water_production_per_second"] = ((IProtoObjectWell)entity).WaterProductionAmountPerSecond;
                result["IProtoObjectWell"] = inner;
            }

            if(entity is IProtoObjectLight) {
                var inner = new Dictionary<string, object>();
                inner["fuel_capacity"] = ((IProtoObjectLight)entity).FuelCapacity;
                inner["fuel_type"] = ((IProtoObjectLight)entity).FuelItemsContainerPrototype.FuelType.ToString();
                result["IProtoObjectLight"] = inner;
            }

            if(entity is IProtoObjectLandClaim) {
                var inner = new Dictionary<string, object>();
                inner["destruction_timeout_seconds"] = ((IProtoObjectLandClaim)entity).DestructionTimeout.Seconds;
                inner["land_claim_grace_padding"] = ((IProtoObjectLandClaim)entity).LandClaimGraceAreaPaddingSizeOneDirection;
                inner["land_claim_size"] = ((IProtoObjectLandClaim)entity).LandClaimSize;
                inner["land_claim_with_grace_area_size"] = ((IProtoObjectLandClaim)entity).LandClaimWithGraceAreaSize;
                inner["safe_slots_count"] = ((IProtoObjectLandClaim)entity).SafeItemsSlotsCount;
                result["IProtoObjectLandClaim"] = inner;
            }

            if(entity is IProtoObjectFloor) {
                var inner = new Dictionary<string, object>();
                inner["move_speed_multiplier"] = ((IProtoObjectFloor)entity).CharacterMoveSpeedMultiplier;
                result["IProtoObjectFloor"] = inner;
            }

            if(entity is IProtoObjectCrate) {
                var inner = new Dictionary<string, object>();
                inner["slots_count"] = ((IProtoObjectCrate)entity).ItemsSlotsCount;
                result["IProtoObjectCrate"] = inner;
            }

            if(entity is IProtoObjectBed) {
                var inner = new Dictionary<string, object>();
                inner["respawn_cooldown_seconds"] = ((IProtoObjectBed)entity).RespawnCooldownDurationSeconds;
                result["IProtoObjectBed"] = inner;
            }

            if(entity is IProtoObjectBarrel) {
                var inner = new Dictionary<string, object>();
                inner["liquid_capacity"] = ((IProtoObjectBarrel)entity).LiquidCapacity;
                result["IProtoObjectBarrel"] = inner;
            }

            return result;
        }

        public List<object> ParseDropList(IReadOnlyDropItemsList list) {
            var result = new List<object>();

            if(list is DropItemsList) {
                foreach(ValueWithWeight<DropItemsList.Entry> item in ((DropItemsList)list).entries) {
                    var it = new Dictionary<string, object>();

                    if(item.Value.EntryItem != null) {
                        it["id"] = item.Value.EntryItem.ProtoItem.Id;
                        it["count"] = item.Value.EntryItem.Count;
                        it["count_rng"] = item.Value.EntryItem.CountRandom;
                    }

                    if(item.Value.EntryNestedList != null) {
                        it["nested"] = ParseDropList(item.Value.EntryNestedList);
                    }

                    // TODO Condition
                    if(item.Value.Condition != null) {
                        it["has_condition"] = true;
                    }

                    it["probability"] = item.Value.Probability;

                    result.Add(it);
                }
                return result;
            }
            
            foreach(IProtoItem item in list.EnumerateAllItems()) {
                var it = new Dictionary<string, object>();
                it["id"] = item.Id;
                result.Add(it);
            }

            return result;
        }

        public Dictionary<string, object> ParsetItem(IProtoItem item) {
            var result = new Dictionary<string, object>();

            result["description"] = item.Description;
            result["stackable"] = item.IsStackable;
            result["max_per_stack"] = item.MaxItemsPerStack;

            if(item is IProtoItemWithDurablity) {
                var cast = (IProtoItemWithDurablity)item;
                var inner = new Dictionary<string, object>();
                inner["durability_max"] = cast.DurabilityMax;
                result["IProtoItemWithDurablity"] = inner;
            }

            if(item is IProtoItemAmmo) {
                var cast = (IProtoItemAmmo)item;
                var inner = new Dictionary<string, object>();

                var damage_description = new Dictionary<string, object>();
                damage_description["armor_piercing_coef"] = cast.DamageDescription.ArmorPiercingCoef;
                damage_description["damage_value"] = cast.DamageDescription.DamageValue;
                damage_description["damage_multiplier"] = cast.DamageDescription.FinalDamageMultiplier;
                damage_description["range_max"] = cast.DamageDescription.RangeMax;
                damage_description["suppress_weapon_effect"] = cast.IsSuppressWeaponSpecialEffect;
                inner["damage_description"] = damage_description;

                var damage_proportions = new List<object>();
                foreach(DamageProportion prop in cast.DamageDescription.DamageProportions) {
                    var it = new Dictionary<string, object>();
                    it["type"] = prop.DamageType.ToString();
                    it["proportion"] = prop.Proportion;
                    damage_proportions.Add(it);
                }
                inner["damage_proportions"] = damage_proportions;

                result["IProtoItemAmmo"] = inner;
            }

            if(item is IProtoItemEquipment) {
                var cast = (IProtoItemEquipment)item;
                var inner = new Dictionary<string, object>();
                inner["type"] = cast.EquipmentType.ToString();

                var stat_multipliers = new Dictionary<string, object>();
                foreach(StatName name in cast.ProtoEffects.Multipliers.Keys) {
                    stat_multipliers[name.ToString()] = cast.ProtoEffects.Multipliers[name];
                }
                inner["stat_multipliers"] = stat_multipliers;

                var stat_values = new Dictionary<string, object>();
                foreach(StatName name in cast.ProtoEffects.Values.Keys) {
                    stat_values[name.ToString()] = cast.ProtoEffects.Values[name];
                }
                inner["stat_values"] = stat_values;

                result["IProtoItemEquipment"] = inner;
            }

            if(item is IProtoItemEquipmentDevice) {
                var cast = (IProtoItemEquipmentDevice)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemEquipmentDevice"] = inner;
            }

            if(item is IProtoItemEquipmentHead) {
                var cast = (IProtoItemEquipmentHead)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemEquipmentHead"] = inner;
            }

            if(item is IProtoItemEquipmentChest) {
                var cast = (IProtoItemEquipmentChest)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemEquipmentChest"] = inner;
            }

            if(item is IProtoItemEquipmentLegs) {
                var cast = (IProtoItemEquipmentLegs)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemEquipmentLegs"] = inner;
            }

            if(item is IProtoItemEquipmentImplant) {
                var cast = (IProtoItemEquipmentImplant)item;
                var inner = new Dictionary<string, object>();
                inner["biomaterial_to_install"] = cast.BiomaterialAmountRequiredToInstall;
                inner["biomaterial_to_uninstall"] = cast.BiomaterialAmountRequiredToUninstall;
                result["IProtoItemEquipmentImplant"] = inner;
            }

            if(item is IProtoItemEquipmentFullBody) {
                var cast = (IProtoItemEquipmentFullBody)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemEquipmentFullBody"] = inner;
            }

            if(item is IProtoItemEquipmentHeadWithLight) {
                var cast = (IProtoItemEquipmentHeadWithLight)item;
                var inner = new Dictionary<string, object>();
                inner["light_color"] = cast.ItemLightConfig.Color.ToString();
                inner["light_size_x"] = cast.ItemLightConfig.Size.X;
                inner["light_size_y"] = cast.ItemLightConfig.Size.Y;
                result["IProtoItemEquipmentHeadWithLight"] = inner;
            }

            if(item is IProtoItemExplosive) {
                var cast = (IProtoItemExplosive)item;
                var inner = new Dictionary<string, object>();
                inner["deploy_distance_max"] = cast.DeployDistanceMax;
                inner["deploy_duration_seconds"] = cast.DeployDuration.Seconds;
                inner["explosive_id"] = cast.ObjectExplosiveProto.Id;
                result["IProtoItemExplosive"] = inner;
            }

            if(item is IProtoItemFood) {
                var cast = (IProtoItemFood)item;
                var inner = new Dictionary<string, object>();
                inner["freshness_duration_seconds"] = cast.FreshnessDuration.Seconds;
                inner["freshness_max"] = cast.FreshnessMaxValue;
                inner["food_restore"] = cast.FoodRestore;
                inner["health_restore"] = cast.HealthRestore;
                inner["stamina_restore"] = cast.StaminaRestore;
                inner["water_restore"] = cast.WaterRestore;
                result["IProtoItemFood"] = inner;
            }

            if(item is ProtoItemMedical) {
                var cast = (ProtoItemMedical)item;
                var inner = new Dictionary<string, object>();
                inner["medical_toxicity"] = cast.MedicalToxicity;
                inner["food_restore"] = cast.FoodRestore;
                inner["health_restore"] = cast.HealthRestore;
                inner["stamina_restore"] = cast.StaminaRestore;
                inner["water_restore"] = cast.WaterRestore;
                result["ProtoItemMedical"] = inner;
            }

            if(item is IProtoItemSeed) {
                var cast = (IProtoItemSeed)item;
                var inner = new Dictionary<string, object>();
                inner["plant_id"] = cast.ObjectPlantProto.Id;
                result["IProtoItemSeed"] = inner;
            }

            if(item is IProtoItemToolWoodcutting) {
                var cast = (IProtoItemToolWoodcutting)item;
                var inner = new Dictionary<string, object>();
                inner["damage_to_tree"] = cast.DamageToTree;
                result["IProtoItemToolWoodcutting"] = inner;
            }

            if(item is IProtoItemToolCrowbar) {
                var cast = (IProtoItemToolCrowbar)item;
                var inner = new Dictionary<string, object>();
                inner["deconstruction_speed_multiplier"] = cast.DeconstructionSpeedMultiplier;
                result["IProtoItemToolCrowbar"] = inner;
            }

            if(item is IProtoItemToolLight) {
                var cast = (IProtoItemToolLight)item;
                var inner = new Dictionary<string, object>();
                inner["light_color"] = cast.ItemLightConfig.Color.ToString();
                inner["light_size_x"] = cast.ItemLightConfig.Size.X;
                inner["light_size_y"] = cast.ItemLightConfig.Size.Y;
                result["IProtoItemToolLight"] = inner;
            }

            if(item is IProtoItemToolMining) {
                var cast = (IProtoItemToolMining)item;
                var inner = new Dictionary<string, object>();
                inner["damage_to_minerals"] = cast.DamageToMinerals;
                result["IProtoItemToolMining"] = inner;
            }

            if(item is IProtoItemToolToolbox) {
                var cast = (IProtoItemToolToolbox)item;
                var inner = new Dictionary<string, object>();
                inner["construction_speed_multiplier"] = cast.ConstructionSpeedMultiplier;
                result["IProtoItemToolToolbox"] = inner;
            }

            if(item is IProtoItemToolWateringCan) {
                var cast = (IProtoItemToolWateringCan)item;
                var inner = new Dictionary<string, object>();
                inner["duration_seconds"] = cast.ActionDurationWateringSeconds;
                inner["water_capacity"] = cast.WaterCapacity;
                result["IProtoItemToolWateringCan"] = inner;
            }

            if(item is IProtoItemWeapon) {
                var cast = (IProtoItemWeapon)item;
                var inner = new Dictionary<string, object>();
                inner["ammo_capacity"] = cast.AmmoCapacity;
                inner["ammo_per_shot"] = cast.AmmoConsumptionPerShot;
                inner["ammo_reload_duration_seconds"] = cast.AmmoReloadDuration;
                inner["armor_piercing_multiplier"] = cast.ArmorPiercingMultiplier;
                inner["can_damage_structures"] = cast.CanDamageStructures.ToString();
                inner["damage_apply_delay"] = cast.DamageApplyDelay;
                inner["damage_multiplier"] = cast.DamageMultiplier;
                inner["fire_interval"] = cast.FireInterval;
                inner["is_looped"] = cast.IsLoopedAttackAnimation;
                inner["range_multiplier"] = cast.RangeMultipier;
                inner["ready_delay_duration_seconds"] = cast.ReadyDelayDuration;
                result["IProtoItemWeapon"] = inner;
            }

            if(item is IProtoItemWeaponMelee) {
                var cast = (IProtoItemWeaponMelee)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemWeaponMelee"] = inner;
            }

            if(item is IProtoItemWeaponRanged) {
                var cast = (IProtoItemWeaponRanged)item;
                var inner = new Dictionary<string, object>();
                inner["recoil_duration"] = cast.CharacterAnimationAimingRecoilDuration;
                inner["recoil_power"] = cast.CharacterAnimationAimingRecoilPower;
                inner["recoil_power_add_coef"] = cast.CharacterAnimationAimingRecoilPowerAddCoef;
                result["IProtoItemWeaponRanged"] = inner;
            }

            if(item is IProtoItemWeaponEnergy) {
                var cast = (IProtoItemWeaponEnergy)item;
                var inner = new Dictionary<string, object>();
                inner["energy_per_shot"] = cast.EnergyUsePerShot;
                result["IProtoItemWeaponEnergy"] = inner;
            }

            if(item is IProtoItemFertilizer) {
                var cast = (IProtoItemFertilizer)item;
                var inner = new Dictionary<string, object>();
                inner["plant_growth_speed_multiplier"] = cast.PlantGrowthSpeedMultiplier;
                inner["fertilizer_description"] = cast.FertilizerShortDescription;
                result["IProtoItemFertilizer"] = inner;
            }

            if(item is IProtoItemFuel) {
                var cast = (IProtoItemFuel)item;
                var inner = new Dictionary<string, object>();
                inner["fuel_amount"] = cast.FuelAmount;
                result["IProtoItemFuel"] = inner;
            }

            if(item is IProtoItemFuelElectricity) {
                var cast = (IProtoItemFuelElectricity)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemFuelElectricity"] = inner;
            }

            if(item is IProtoItemFuelOil) {
                var cast = (IProtoItemFuelOil)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemFuelOil"] = inner;
            }

            if(item is IProtoItemFuelRefined) {
                var cast = (IProtoItemFuelRefined)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemFuelRefined"] = inner;
            }

            if(item is IProtoItemFuelSolid) {
                var cast = (IProtoItemFuelSolid)item;
                var inner = new Dictionary<string, object>();
                result["IProtoItemFuelSolid"] = inner;
            }

            if(item is IProtoItemWithFuel) {
                var cast = (IProtoItemWithFuel)item;
                var inner = new Dictionary<string, object>();
                inner["fuel_amount_initial"] = cast.ItemFuelConfig.FuelAmountInitial;
                inner["fuel_capacity"] = cast.ItemFuelConfig.FuelCapacity;
                inner["fuel_per_second"] = cast.ItemFuelConfig.FuelUsePerSecond;
                inner["refill_duration"] = cast.ItemFuelConfig.RefillDuration;
                inner["fuel_title"] = cast.ItemFuelConfig.FuelTitle;
                
                var fuel_items = new List<object>();
                foreach(IProtoItem it in cast.ItemFuelConfig.FuelProtoItemsList) {
                    fuel_items.Add(it.Id);
                }
                inner["fuel_items"] = fuel_items;

                result["IProtoItemWithFuel"] = inner;
            }

            if(item is IProtoItemDataLog) {
                var cast = (IProtoItemDataLog)item;
                var inner = new Dictionary<string, object>();
                inner["text"] = cast.Text;
                result["IProtoItemDataLog"] = inner;
            }

            if(item is IProtoItemPowerBank) {
                var cast = (IProtoItemPowerBank)item;
                var inner = new Dictionary<string, object>();
                inner["energy_capacity"] = cast.EnergyCapacity;
                result["IProtoItemPowerBank"] = inner;
            }

            if(item is IProtoItemLiquidStorage) {
                var cast = (IProtoItemLiquidStorage)item;
                var inner = new Dictionary<string, object>();
                inner["capacity"] = cast.Capacity;
                inner["liquid_type"] = cast.LiquidType;
                result["IProtoItemLiquidStorage"] = inner;
            }

            if(item is IProtoItemOrganic) {
                var cast = (IProtoItemOrganic)item;
                var inner = new Dictionary<string, object>();
                inner["organic_value"] = cast.OrganicValue;
                result["IProtoItemOrganic"] = inner;
            }

            if(item is IProtoItemUsableFromContainer) {
                var cast = (IProtoItemUsableFromContainer)item;
                var inner = new Dictionary<string, object>();
                inner["use_caption"] = cast.ItemUseCaption;
                result["IProtoItemUsableFromContainer"] = inner;
            }

            return result;
        }

        public string ToJson(object item) {
            StringBuilder stringBuilder = new StringBuilder();
            AppendValue(stringBuilder, item, "ROOT");
            return stringBuilder.ToString();
        }

        public void AppendValue(StringBuilder stringBuilder, object item, string parentKey) {
            if(item == null) {
                stringBuilder.Append("null");
                return;
            }

            Type type = item.GetType();
            if(type == typeof(string)) {
                stringBuilder.Append('"');
                string str = (string)item;
                for(int i = 0; i < str.Length; ++i)
                    if(str[i] < ' ' || str[i] == '"' || str[i] == '\\') {
                        stringBuilder.Append('\\');
                        int j = "\"\\\n\r\t\b\f".IndexOf(str[i]);
                        if(j >= 0)
                            stringBuilder.Append("\"\\nrtbf"[j]);
                        else
                            stringBuilder.AppendFormat("u{0:X4}", (UInt32)str[i]);
                    } else
                        stringBuilder.Append(str[i]);
                stringBuilder.Append('"');
            } else if(type == typeof(byte) ||
                    type == typeof(int) ||
                    type == typeof(uint) ||
                    type == typeof(sbyte) ||
                    type == typeof(short) ||
                    type == typeof(ushort) ||
                    type == typeof(long) ||
                    type == typeof(ulong)) {
                stringBuilder.Append(item.ToString());
            } else if(type == typeof(float)) {
                stringBuilder.Append(((float)item).ToString(System.Globalization.CultureInfo.InvariantCulture));
            } else if(type == typeof(double)) {
                stringBuilder.Append(((double)item).ToString(System.Globalization.CultureInfo.InvariantCulture));
            } else if(type == typeof(bool)) {
                stringBuilder.Append(((bool)item) ? "true" : "false");
            } else if(type.IsEnum) {
                stringBuilder.Append('"');
                stringBuilder.Append(item.ToString());
                stringBuilder.Append('"');
            } else if(item is IList) {
                stringBuilder.Append('[');
                bool isFirst = true;
                IList list = item as IList;
                for(int i = 0; i < list.Count; i++) {
                    if(isFirst)
                        isFirst = false;
                    else
                        stringBuilder.Append(',');
                    AppendValue(stringBuilder, list[i], "LIST");
                }
                stringBuilder.Append(']');
            } else if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                Type keyType = type.GetGenericArguments()[0];

                //Refuse to output dictionary keys that aren't of type string
                if(keyType != typeof(string)) {
                    stringBuilder.Append("{}");
                    return;
                }

                stringBuilder.Append('{');
                IDictionary dict = item as IDictionary;
                bool isFirst = true;
                foreach(object key in dict.Keys) {
                    if(isFirst)
                        isFirst = false;
                    else
                        stringBuilder.Append(',');
                    stringBuilder.Append('\"');
                    stringBuilder.Append((string)key);
                    stringBuilder.Append("\":");
                    AppendValue(stringBuilder, dict[key], (string)key);
                }
                stringBuilder.Append('}');
            } else {
                stringBuilder.Append("\"ERROR, Unknown Type: " + type.ToString() + " \"");
                Api.Logger.Error("[\"" + parentKey + "\"] Unknown Type: " + type);
            }
        }

    }

}