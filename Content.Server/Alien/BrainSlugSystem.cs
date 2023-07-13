using System.Linq;
using Content.Server.Actions;
using Content.Server.NPC.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;

namespace Content.Server.Alien
{
    public sealed class BrainSlugSystem : EntitySystem
    {
        [Dependency] private SharedStunSystem _stunSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedCombatModeSystem _combat = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly ActionsSystem _action = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<BrainSlugComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<BrainSlugComponent, MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<BrainSlugComponent, ThrowDoHitEvent>(OnBrainSlugDoHit);
            SubscribeLocalEvent<BrainSlugComponent, GotEquippedEvent>(OnGotEquipped);
            SubscribeLocalEvent<BrainSlugComponent, GotUnequippedEvent>(OnGotUnequipped);
            SubscribeLocalEvent<BrainSlugComponent, GotEquippedHandEvent>(OnGotEquippedHand);
            SubscribeLocalEvent<BrainSlugComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<BrainSlugComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
            SubscribeLocalEvent<BrainSlugComponent, BrainSlugJumpActionEvent>(OnJumpBrainSlug);

        }

        private void OnStartup(EntityUid uid, BrainSlugComponent component, ComponentStartup args)
        {
            _action.AddAction(uid, component.ActionBrainSlugJump, null);
        }

        private void OnBrainSlugDoHit(EntityUid uid, BrainSlugComponent component, ThrowDoHitEvent args)
        {
            if (component.IsDeath)
                return;
            if (!HasComp<HumanoidAppearanceComponent>(args.Target))
                return;

            _inventory.TryGetSlotEntity(args.Target, "head", out var headItem);
            if (HasComp<IngestionBlockerComponent>(headItem))
                return;

            var equipped = _inventory.TryEquip(args.Target, uid, "mask", true);
            if (!equipped)
                return;

            component.EquipedOn = args.Target;

            _popup.PopupEntity(Loc.GetString("The facehugger has latched onto your face!"),
                args.Target, args.Target, PopupType.LargeCaution);

            _popup.PopupEntity(Loc.GetString("You have latched onto his face!",
                    ("entity", args.Target)),
                uid, uid, PopupType.LargeCaution);

            _popup.PopupEntity(Loc.GetString("The facehugger is eating his face!",
                ("entity", args.Target)), args.Target, Filter.PvsExcept(uid), true, PopupType.Large);

            //EntityManager.RemoveComponent<CombatModeComponent>(uid);
            _stunSystem.TryParalyze(args.Target, TimeSpan.FromSeconds(component.ParalyzeTime), true);
            _damageableSystem.TryChangeDamage(args.Target, component.Damage, origin: args.User);
        }

        private void OnGotEquipped(EntityUid uid, BrainSlugComponent component, GotEquippedEvent args)
        {
            if (args.Slot != "mask")
                return;
            component.EquipedOn = args.Equipee;
            //EntityManager.RemoveComponent<CombatModeComponent>(uid);
        }

        private void OnUnequipAttempt(EntityUid uid, BrainSlugComponent component, BeingUnequippedAttemptEvent args)
        {
            if (args.Slot != "mask")
                return;
            if (component.EquipedOn != args.Unequipee)
                return;
            _popup.PopupEntity(Loc.GetString("You can't remove the facehugger from your face."),
                args.Unequipee, args.Unequipee, PopupType.Large);
            args.Cancel();
        }

        private void OnGotEquippedHand(EntityUid uid, BrainSlugComponent component, GotEquippedHandEvent args)
        {
            if (component.IsDeath)
                return;
            _damageableSystem.TryChangeDamage(args.User, component.Damage);
            _popup.PopupEntity(Loc.GetString("The facehugger has bitten your hand!"),
                args.User, args.User);
        }

        private void OnGotUnequipped(EntityUid uid, BrainSlugComponent component, GotUnequippedEvent args)
        {
            if (args.Slot != "mask")
                return;
            component.EquipedOn = new EntityUid();
            //var combatMode = EntityManager.AddComponent<CombatModeComponent>(uid);
            //_combat.SetInCombatMode(uid, true, combatMode);
            //EntityManager.AddComponent<NPCMeleeCombatComponent>(uid);
        }

        private void OnMeleeHit(EntityUid uid, BrainSlugComponent component, MeleeHitEvent args)
        {
            if (!args.HitEntities.Any())
                return;

            foreach (var entity in args.HitEntities)
            {
                if (!HasComp<HumanoidAppearanceComponent>(entity))
                    return;


                _inventory.TryGetSlotEntity(entity, "head", out var headItem);
                if (HasComp<IngestionBlockerComponent>(headItem))
                    return;

                var random = new Random();
                var shouldEquip = random.Next(1, 101) <= BrainSlugComponent.ChansePounce;
                if (!shouldEquip)
                    return;

                var equipped = _inventory.TryEquip(entity, uid, "mask", true);
                if (!equipped)
                    return;

                component.EquipedOn = entity;

                _popup.PopupEntity(Loc.GetString("The facehugger has latched onto your face!"),
                    entity, entity, PopupType.LargeCaution);

                _popup.PopupEntity(Loc.GetString("You have latched onto his face!", ("entity", entity)),
                    uid, uid, PopupType.LargeCaution);

                _popup.PopupEntity(Loc.GetString("The facehugger is eating his face!",
                    ("entity", entity)), entity, Filter.PvsExcept(entity), true, PopupType.Large);

                //EntityManager.RemoveComponent<CombatModeComponent>(uid);
                _stunSystem.TryParalyze(entity, TimeSpan.FromSeconds(component.ParalyzeTime), true);
                _damageableSystem.TryChangeDamage(entity, component.Damage, origin: entity);

            }
        }

        private static void OnMobStateChanged(EntityUid uid, BrainSlugComponent component, MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Dead)
            {
                component.IsDeath = true;
            }
        }

        public sealed class BrainSlugJumpActionEvent : WorldTargetActionEvent
        {

        };


        private void OnJumpBrainSlug(EntityUid uid, BrainSlugComponent component, BrainSlugJumpActionEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            var xform = Transform(uid);
            var mapCoords = args.Target.ToMap(EntityManager);
            Logger.Info(xform.MapPosition.ToString());
            Logger.Info(mapCoords.ToString());
            var direction = mapCoords.Position - xform.MapPosition.Position;
            Logger.Info(direction.ToString());

            _throwing.TryThrow(uid, direction, 7F, uid, 10F);
            if (component.SoundBrainSlugJump != null)
            {
                _audioSystem.PlayPvs(component.SoundBrainSlugJump, uid, component.SoundBrainSlugJump.Params);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var comp in EntityQuery<BrainSlugComponent>())
            {
                comp.Accumulator += frameTime;

                if (comp.Accumulator <= comp.DamageFrequency)
                    continue;

                comp.Accumulator = 0;

                if (comp.EquipedOn is not { Valid: true } targetId)
                    continue;
                if (TryComp(targetId, out MobStateComponent? mobState))
                {
                    if (mobState.CurrentState is not MobState.Alive)
                    {
                        _inventory.TryUnequip(targetId, "mask", true, true);
                        comp.EquipedOn = new EntityUid();
                        return;
                    }
                }

                _popup.PopupEntity(Loc.GetString("You feel as if something is stirring inside you."),
                    targetId, targetId, PopupType.LargeCaution);
            }
        }
    }
}
