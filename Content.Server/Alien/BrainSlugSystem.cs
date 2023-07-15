using System.Linq;
using Content.Server.Actions;
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
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Shared.Alien;
using Robust.Shared.Prototypes;
using Content.Shared.DoAfter;
using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Chat.Prototypes;
using Content.Server.Chat.Systems;
using Content.Server.Speech.Components;

namespace Content.Server.Alien
{
    public sealed class BrainSlugSystem : SharedBrainHuggingSystem
    {
        [Dependency] private SharedStunSystem _stunSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
        [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private readonly ChatSystem _chat = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<BrainHuggingComponent, ComponentStartup>(OnStartup);


            SubscribeLocalEvent<BrainHuggingComponent, BrainSlugJumpActionEvent>(OnJumpBrainSlug);
            SubscribeLocalEvent<BrainHuggingComponent, ThrowDoHitEvent>(OnBrainSlugDoHit);

            SubscribeLocalEvent<BrainHuggingComponent, BrainSlugActionEvent>(OnBrainSlugAction);
            SubscribeLocalEvent<BrainHuggingComponent, BrainHuggingDoAfterEvent>(BrainHuggingOnDoAfter);

            SubscribeLocalEvent<BrainHuggingComponent, DominateVictimActionEvent>(OnDominateVictimAction);

            SubscribeLocalEvent<BrainHuggingComponent, TormentHostActionEvent>(OnTormentHostAction);

            SubscribeLocalEvent<BrainHuggingComponent, ReleaseSlugActionEvent>(OnReleaseSlugAction);
            SubscribeLocalEvent<BrainSlugComponent, ReleaseSlugDoAfterEvent>(ReleaseSlugDoAfter);
        }


        protected void OnStartup(EntityUid uid, BrainHuggingComponent component, ComponentStartup args)
        {
            if (component.ActionBrainSlugJump != null)
                _actionsSystem.AddAction(uid, component.ActionBrainSlugJump, null);

            if (component.BrainSlugAction != null)
                _actionsSystem.AddAction(uid, component.BrainSlugAction, null);
        }


        private void OnBrainSlugDoHit(EntityUid uid, BrainHuggingComponent component, ThrowDoHitEvent args)
        {

            TryComp(uid, out BrainSlugComponent? defcomp);
            if (defcomp == null)
            {
                return;
            }


            if (!HasComp<HumanoidAppearanceComponent>(args.Target))
                return;


            var host = args.Target;


            defcomp.GuardianContainer = host.EnsureContainer<ContainerSlot>("GuardianContainer");


            defcomp.GuardianContainer.Insert(uid);
            DebugTools.Assert(defcomp.GuardianContainer.Contains(uid));



            defcomp.EquipedOn = args.Target;

            _popup.PopupEntity(Loc.GetString("Something jumped on you!"),
                args.Target, args.Target, PopupType.LargeCaution);
        }



        private void OnJumpBrainSlug(EntityUid uid, BrainHuggingComponent component, BrainSlugJumpActionEvent args)
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


        private void OnBrainSlugAction(EntityUid uid, BrainHuggingComponent component, BrainSlugActionEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            var target = args.Target;

            TryComp(uid, out BrainHuggingComponent? hugcomp);
            if (hugcomp == null)
            {
                return;
            }

            if (TryComp(target, out MobStateComponent? targetState))
            {

                switch (targetState.CurrentState)
                {
                    case MobState.Alive:
                    case MobState.Critical:
                        _popup.PopupEntity(Loc.GetString("Slug is sucking on your brain!"), uid, uid);
                        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(uid, hugcomp.BrainSlugTime, new BrainHuggingDoAfterEvent(), uid, target: target, used: uid)
                        {
                            BreakOnTargetMove = false,
                            BreakOnUserMove = true,
                        });
                        break;
                    default:
                        _popup.PopupEntity(Loc.GetString("The target is dead!"), uid, uid);
                        break;
                }

                return;
            }
        }


        private void BrainHuggingOnDoAfter(EntityUid uid, BrainHuggingComponent component, BrainHuggingDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled)
                return;

            else if (args.Args.Target != null)
            {
                var target = args.Target;
                if (target == null)
                {
                    return;
                }


                if (component.DominateVictimAction != null)
                    _actionsSystem.AddAction(uid, component.DominateVictimAction, null);

                if (component.ReleaseSlugAction != null)
                    _actionsSystem.AddAction(uid, component.ReleaseSlugAction, null);

                if (component.TormentHostSlugAction != null)
                    _actionsSystem.AddAction(uid, component.TormentHostSlugAction, null);

                if (component.ActionBrainSlugJump != null)
                    _actionsSystem.RemoveAction(uid, component.ActionBrainSlugJump, null);

                if (component.BrainSlugAction != null)
                    _actionsSystem.RemoveAction(uid, component.BrainSlugAction, null);


                if (TryComp(target, out MobStateComponent? mobState))
                {
                    if (mobState.CurrentState == MobState.Critical)
                    {
                        _popup.PopupEntity(Loc.GetString("Brain Slug is trying save your body!"), target.Value, target.Value);
                        var ichorInjection = new Solution(component.IchorChemical, component.HealRate);
                        ichorInjection.ScaleSolution(5.0f);
                        _bloodstreamSystem.TryAddToChemicals(target.Value, ichorInjection);
                    }
                }
            }



            _audioSystem.PlayPvs(component.SoundBrainHugging, uid);
        }

        private void OnDominateVictimAction(EntityUid uid, BrainHuggingComponent comp, DominateVictimActionEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            var target = args.Target;

            TryComp(uid, out BrainHuggingComponent? hugcomp);
            if (hugcomp == null)
            {
                return;
            }


            _popup.PopupEntity(Loc.GetString("Your limbs are stiff!"), uid, uid);
            _stunSystem.TryParalyze(args.Target, TimeSpan.FromSeconds(hugcomp.ParalyzeTime), true);
        }


        private void OnTormentHostAction(EntityUid uid, BrainHuggingComponent comp, TormentHostActionEvent args)
        {
            var target = args.Target;
            if (TryComp(target, out VocalComponent? scream))
            {
                if (scream != null)
                {
                    _popup.PopupEntity(Loc.GetString("YOU FEEL HELLISH PAIN, YOU WILL BE TURNED INSIDE OUT AND ROLLED ON THE FLOOR!"), target, target, PopupType.LargeCaution);
                    _chat.TryEmoteWithChat(target, scream.ScreamId);
                }
            }
        }

        private void OnReleaseSlugAction(EntityUid uid, BrainHuggingComponent comp, ReleaseSlugActionEvent args)
        {
            TryComp(uid, out BrainSlugComponent? defcomp);
            if (defcomp == null)
            {
                return;
            }

            var target = defcomp.EquipedOn;



            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(uid, comp.BrainSlugTime, new ReleaseSlugDoAfterEvent(), uid, target: target, used: uid)
            {
                BreakOnTargetMove = false,
                BreakOnUserMove = true,
            });
        }


        private void ReleaseSlugDoAfter(EntityUid uid, BrainSlugComponent component, ReleaseSlugDoAfterEvent args)
        {
            TryComp(uid, out BrainHuggingComponent? hugcomp);
            if (hugcomp == null)
            {
                return;
            }


            component.GuardianContainer.Remove(uid);
            DebugTools.Assert(!component.GuardianContainer.Contains(uid));

            if (hugcomp.ReleaseSlugAction != null)
                _actionsSystem.RemoveAction(uid, hugcomp.ReleaseSlugAction);

            if (hugcomp.DominateVictimAction != null)
                _actionsSystem.RemoveAction(uid, hugcomp.DominateVictimAction);

            if (hugcomp.TormentHostSlugAction != null)
                _actionsSystem.RemoveAction(uid, hugcomp.TormentHostSlugAction, null);

            if (hugcomp.ActionBrainSlugJump != null)
                _actionsSystem.AddAction(uid, hugcomp.ActionBrainSlugJump, null);

            if (hugcomp.BrainSlugAction != null)
                _actionsSystem.AddAction(uid, hugcomp.BrainSlugAction, null);
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
                    if (mobState.CurrentState is MobState.Dead)
                    {
                        return;
                    }
                }

                _popup.PopupEntity(Loc.GetString("You feel as if something is stirring inside you."), targetId, targetId);
            }
        }

    }
}
