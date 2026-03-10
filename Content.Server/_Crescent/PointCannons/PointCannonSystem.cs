using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Content.Server.Administration;
using Content.Server.Popups;
using Content.Server.Shuttles.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.PointCannons;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using System;
using Content.Server._Crescent.Hardpoint;
using Content.Server.Power.Components;
using Content.Shared._Crescent.CCvars;
using Content.Shared._Crescent.Hardpoints;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Crescent.Radar;

namespace Content.Server.PointCannons;

public sealed class PointCannonSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _playerMan = default!;
    [Dependency] private readonly TransformSystem _formSys = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSys = default!;
    [Dependency] private readonly PopupSystem _popSys = default!;
    [Dependency] private readonly QuickDialogSystem _dialogSys = default!;
    [Dependency] private readonly GunSystem _gunSys = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConSys = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsSys = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HardpointSystem _hardpoint = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private float _accumulatedFrameTime;
    private float _uiTps;

    private readonly HashSet<EntityUid> _activeConsoles = new();

    private readonly Dictionary<EntityUid, float> _gridUpdateCooldown = new();
    private const float GridUpdateCooldownTime = 0.5f; // seconds
    private int CannonCheckRange = 25;
    private HashSet<EntityUid> QueuedGrids = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CrescentCVars.PointCannonUiTps, (float val) => { _uiTps = val; }, true);

        SubscribeLocalEvent<TargetingConsoleComponent, ActivatableUIOpenAttemptEvent>(OnConsoleOpenAttempt);
        SubscribeLocalEvent<TargetingConsoleComponent, BoundUserInterfaceMessageAttempt>(BUIValidation);
        SubscribeLocalEvent<TargetingConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
        SubscribeLocalEvent<TargetingConsoleComponent, BoundUIClosedEvent>(OnConsoleClosed);
        SubscribeLocalEvent<TargetingConsoleComponent, TargetingConsoleFireMessage>(OnConsoleFire);
        SubscribeLocalEvent<TargetingConsoleComponent, TargetingConsoleGroupChangedMessage>(OnConsoleGroupChanged);
        SubscribeLocalEvent<TargetingConsoleComponent, FireControlConsoleRefreshServerMessage>(OnRefreshServer);
        SubscribeLocalEvent<TargetingConsoleComponent, ComponentRemove>(OnConsoleDelete);
        SubscribeLocalEvent<TargetingConsoleComponent, AnchorStateChangedEvent>(OnConsoleAnchor);

        SubscribeLocalEvent<PointCannonComponent, EntityTerminatingEvent>(OnCannonDetach);
        SubscribeLocalEvent<PointCannonComponent, EntParentChangedMessage>(OnCannonDetach);
        SubscribeLocalEvent<PointCannonComponent, ReAnchorEvent>(OnCannonDetach);

        SubscribeLocalEvent<PointCannonLinkToolComponent, UseInHandEvent>(OnLinkToolHandUse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulatedFrameTime += frameTime;
        float targetTime = _uiTps > 0 ? 1.0f / _uiTps : 1.0f;
        if (_accumulatedFrameTime < targetTime)
            return;
        _accumulatedFrameTime -= targetTime;
        if (_accumulatedFrameTime > targetTime)
            _accumulatedFrameTime = 0;

        foreach (var uid in _activeConsoles)
        {
            if (!TryComp<TargetingConsoleComponent>(uid, out var console))
            {
                _activeConsoles.Remove(uid);
                continue;
            }
            UpdateConsoleState(uid, console);
        }

        var toRemove = new List<EntityUid>();
        foreach (var (uid, timer) in _gridUpdateCooldown)
        {
            if (timer <= 0)
            {
                if (!TryComp<TargetingConsoleComponent>(uid, out var consoleComp))
                    continue;
                ProcessGridShapeChange(uid, consoleComp);
                toRemove.Add(uid);
            }
            else
            {
                _gridUpdateCooldown[uid] = timer - frameTime;
            }
        }
        foreach (var id in toRemove)
        {
            _gridUpdateCooldown.Remove(id);
        }
    }

    private void ProcessGridShapeChange(EntityUid console, TargetingConsoleComponent component)
    {
            UnlinkAllCannonsFromConsole(console, component);
            LinkAllCannonsToConsole(console, component);
    }

    private void OnRefreshServer(EntityUid console, TargetingConsoleComponent component, FireControlConsoleRefreshServerMessage args)
    {
            if (_gridUpdateCooldown.ContainsKey(console))
                _gridUpdateCooldown[console] = GridUpdateCooldownTime; // reset timer
            else
                _gridUpdateCooldown[console] = GridUpdateCooldownTime;     
    }

    private void UnlinkAllCannonsFromConsole(EntityUid console, TargetingConsoleComponent comp)
    {
        // Create a complete snapshot of all cannons before unlinking anything
        var allLinks = new List<(string group, EntityUid cannon)>();
        
        foreach (var (group, cannons) in comp.CannonGroups)
        {
            foreach (var cannon in cannons)
            {
                allLinks.Add((group, cannon));
            }
        }
        
        // Now unlink using the snapshot
        foreach (var (group, cannon) in allLinks)
        {
            UnlinkConsole(cannon, console, comp);
        }
    }

    private void OnConsoleDelete<T>(EntityUid console, TargetingConsoleComponent comp, ref T args)
    {
        UnlinkAllCannonsFromConsole(console, comp);
        _activeConsoles.Remove(console);
    }

    private void OnConsoleAnchor(EntityUid console, TargetingConsoleComponent comp, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            OnConsoleDelete(console, comp, ref args);
            return;
        }
    }

    public void LinkAllCannonsToConsole(EntityUid console, TargetingConsoleComponent comp)
    {
        var gridUid = Transform(console).GridUid;
        if (gridUid is null)
            return;
        var cannonList = new HashSet<Entity<PointCannonComponent>>();
        _lookup.GetGridEntities(gridUid.Value, cannonList);
        foreach (var cannon in cannonList)
        {
            if (!Transform(cannon.Owner).Anchored)
                continue;
            if (!TryComp<HardpointAnchorableOnlyComponent>(cannon.Owner, out var anchorComp) || anchorComp.anchoredTo is null)
                continue;
            LinkCannon(cannon.Owner, console, comp, MetaData(cannon.Owner).EntityName);
        }
    }

    private void OnConsoleOpenAttempt(EntityUid uid, TargetingConsoleComponent component, ActivatableUIOpenAttemptEvent args)
    {
        var uis = _uiSys.GetActorUis(args.User);
        var ourGridUid = Transform(uid).GridUid;
        if (ourGridUid is null)
        {
            args.Cancel();
            return;
        }

        foreach (var (_, key) in uis)
        {
            if (key is ShuttleConsoleUiKey)
            {
                args.Cancel();
                _popSys.PopupEntity(Loc.GetString("targeting-rejection-shuttle-console"), args.User, args.User, PopupType.LargeCaution);
                return;
            }
        }
    }

    private void BUIValidation(EntityUid uid, TargetingConsoleComponent component, BoundUserInterfaceMessageAttempt args)
    {
        var uis = _uiSys.GetActorUis(args.Actor);
        foreach (var (_, key) in uis)
        {
            if (key is ShuttleConsoleUiKey)
            {
                args.Cancel();
                return;
            }
        }
    }

    private void OnConsoleOpened(Entity<TargetingConsoleComponent> uid, ref BoundUIOpenedEvent args)
    {
        uid.Comp.RegenerateCannons = true;
        _activeConsoles.Add(uid.Owner);
    }

    private void OnConsoleClosed(Entity<TargetingConsoleComponent> uid, ref BoundUIClosedEvent args)
    {
        _activeConsoles.Remove(uid.Owner);
    }

    private void OnCannonDetach<T>(Entity<PointCannonComponent> uid, ref T args)
    {
        UnlinkCannon(uid);
    }

    private void OnLinkToolHandUse(Entity<PointCannonLinkToolComponent> uid, ref UseInHandEvent args)
    {
        if (!_playerMan.TryGetSessionByEntity(args.User, out var session))
            return;

        _dialogSys.OpenDialog(session, "Group name", "Name (case insensitive)", (string name) =>
        {
            uid.Comp.GroupName = string.IsNullOrEmpty(name) ? "all" : name.ToLower();
        });
    }

    public void LinkCannon(EntityUid cannonUid, EntityUid consoleUid, TargetingConsoleComponent console, string group)
    {
        if (!TryComp<PointCannonComponent>(cannonUid, out var cannonComponent))
            return;
        if (!console.CannonGroups.ContainsKey(group))
            console.CannonGroups[group] = new List<EntityUid>();

        if (console.CannonGroups[group].Contains(cannonUid))
        {
            // SPCR 2024 - For fixing the old ships before we added this functionality
            if (cannonComponent.LinkedConsoleId is null)
                cannonComponent.LinkedConsoleId = consoleUid;
            return;
        }

        console.CannonGroups[group].Add(cannonUid);
        if (group != "all" && !console.CannonGroups["all"].Contains(cannonUid))
            console.CannonGroups["all"].Add(cannonUid);

        console.RegenerateCannons = true;
        cannonComponent.LinkedConsoleId = consoleUid;
        cannonComponent.LinkedConsoleIds.Add(consoleUid);

        RefreshFiringRanges(cannonUid, null, null, cannonComponent, CannonCheckRange);

        //if (console.ActiveGroups.Contains(group))
            //TogglePvsOverride(new[] { cannonUid }, GetUiSessions(consoleUid), true);
    }

    public void UnlinkCannon(EntityUid cannonUid)
    {
        if (!TryComp<PointCannonComponent>(cannonUid, out var cannonComp))
            return;

        var consoleIds = cannonComp.LinkedConsoleIds.ToList();
        
        foreach (var consoleUid in consoleIds)
        {
            if (!TryComp<TargetingConsoleComponent>(consoleUid, out var console))
                continue;

            var groups = console.CannonGroups.Keys.ToList();
            
            foreach (string group in groups)
            {
                if (console.CannonGroups.TryGetValue(group, out var cannons))
                {
                    cannons.Remove(cannonUid);
                    
                    if (cannons.Count == 0 && group != "all")
                        console.CannonGroups.Remove(group);
                }
            }
            
            console.RegenerateCannons = true;
            TogglePvsOverride(new[] { cannonUid }, GetUiSessions(consoleUid), false);
        }
        
        cannonComp.LinkedConsoleIds.Clear();
    }

    public void UnlinkConsole(EntityUid cannonUid, EntityUid consoleUid, TargetingConsoleComponent comp)
    {
        if (!TryComp<PointCannonComponent>(cannonUid, out var cannonComp))
            return;
            
        cannonComp.LinkedConsoleIds.Remove(consoleUid);
        
        if (!TryComp<TargetingConsoleComponent>(consoleUid, out var console))
            return;
        
        // Get a snapshot of groups to iterate safely
        var groups = console.CannonGroups.Keys.ToList();
        
        foreach (string group in groups)
        {
            if (console.CannonGroups.TryGetValue(group, out var cannons))
            {
                cannons.Remove(cannonUid);
                
                if (cannons.Count == 0 && group != "all")
                    console.CannonGroups.Remove(group);
            }
        }
        
        console.RegenerateCannons = true;
        TogglePvsOverride(new[] { cannonUid }, GetUiSessions(consoleUid), false);
    }

    public void UpdateConsoleState(EntityUid uid, TargetingConsoleComponent console)
    {
        NavInterfaceState navState = _shuttleConSys.GetNavState(uid, _shuttleConSys.GetAllDocks());
        IFFInterfaceState iffState = _shuttleConSys.GetIFFState(uid,
            console.RegenerateCannons ? null : console.PrevState?.IFFState.Turrets);

        List<string>? groups = console.RegenerateCannons ? console.CannonGroups.Keys.ToList() : null;

        var consoleState = new TargetingConsoleBoundUserInterfaceState(
            navState,
            iffState,
            groups,
            GetNetEntityList(console.CurrentGroup));
        console.RegenerateCannons = false;
        console.PrevState = consoleState;
        _uiSys.SetUiState(uid, TargetingConsoleUiKey.Key, consoleState);
    }

    private void OnConsoleFire(EntityUid uid, TargetingConsoleComponent console, TargetingConsoleFireMessage ev)
    {
        for (int i = 0; i < console.CurrentGroup.Count;)
        {
            EntityUid cannonUid = console.CurrentGroup[i];
            if (Deleted(cannonUid))
            {
                console.CurrentGroup.RemoveAt(i);
                TogglePvsOverride(new[] { cannonUid }, GetUiSessions(uid), false);
                continue;
            }

            TryFireCannon(cannonUid, ev.Coordinates);
            i++;
        }
    }

    private void OnConsoleGroupChanged(Entity<TargetingConsoleComponent> uid, ref TargetingConsoleGroupChangedMessage args)
    {
        var sessions = GetUiSessions(uid);

        if (uid.Comp.ActiveGroups.Contains(args.GroupName))
        {
            if (args.GroupName == "all")
                uid.Comp.ActiveGroups = new();
            else
                uid.Comp.ActiveGroups.Remove(args.GroupName);
            TogglePvsOverride(uid.Comp.CannonGroups[args.GroupName], sessions, false); 
        }
        else
        {
            if (args.GroupName == "all")
                uid.Comp.ActiveGroups = new() { "all" };
            else
                uid.Comp.ActiveGroups.Add(args.GroupName);
            TogglePvsOverride(uid.Comp.CannonGroups[args.GroupName], sessions, true);
        } 

        var totalLength = 0;

        foreach (var group in uid.Comp.ActiveGroups)
            totalLength += uid.Comp.CannonGroups[group].Count;

        var selected = new List<EntityUid>(totalLength);

        foreach (var group in uid.Comp.ActiveGroups)
            selected.AddRange(uid.Comp.CannonGroups[group]);

        uid.Comp.CurrentGroup = selected;
    }

    public bool TryFireCannon(
        EntityUid uid,
        Vector2 pos,
        TransformComponent? form = null,
        GunComponent? gun = null,
        PointCannonComponent? cannon = null)
    {
        if (!Resolve(uid, ref form) || !Resolve(uid, ref gun) || !Resolve(uid, ref cannon))
            return false;

        if (form.MapUid == null || !_gunSys.CanShoot(gun))
            return false;
        if (!TryComp<HardpointAnchorableOnlyComponent>(uid, out var anchorComp) || anchorComp.anchoredTo is null)
            return false;
        if (!TryComp<ApcPowerReceiverComponent>(anchorComp.anchoredTo, out var powerComp) || !powerComp.Powered)
            return false;

        var entPos = new EntityCoordinates(uid, new Vector2(0, -1));
        if (!HasComp<HardpointFixedMountComponent>(anchorComp.anchoredTo))
        {
            Vector2 cannonPos = _formSys.GetWorldPosition(form);
            _formSys.SetWorldRotation(uid, Angle.FromWorldVec(pos - cannonPos));
            entPos = new EntityCoordinates(form.MapUid.Value, pos);
        }

        if (!SafetyCheck(form.LocalRotation - Math.PI / 2, cannon))
            return false;

        _gunSys.AttemptShoot(uid, uid, gun, entPos);
        return true;
    }

    public bool SafetyCheck(Angle ang, PointCannonComponent cannon)
    {
        foreach (var (start, width) in cannon.ObstructedRanges)
        {
            if (CrescentHelpers.AngInSector(ang, start, width))
                return false;
        }
        return true;
    }

    public void RefreshFiringRanges(EntityUid uid, TransformComponent? form = null, GunComponent? gun = null, PointCannonComponent? cannon = null, int? range = null)
    {
        if (!Resolve(uid, ref form) || !Resolve(uid, ref gun) || !Resolve(uid, ref cannon))
            return;
        range ??= 10;

        cannon.ObstructedRanges = CalculateFiringRanges(uid, form, gun, cannon, range.Value);
        Dirty(uid, cannon);
    }

    private List<(Angle, Angle)> CalculateFiringRanges(EntityUid uid, TransformComponent form, GunComponent gun, PointCannonComponent cannon, int range)
    {
        if (form.GridUid == null)
            return new();

        var gridUid = form.GridUid.Value;
        var cannonPos = form.LocalPosition;

        var entities = _lookup.GetEntitiesInRange(uid, range, LookupFlags.Static);
        var sectors = new List<(Angle start, Angle width)>();

        foreach (var childUid in entities)
        {
            var otherForm = Transform(childUid);
            if (otherForm.GridUid != gridUid)
                continue;

            var dir = otherForm.LocalPosition - cannonPos;
            if (!otherForm.Anchored)
                continue;

            if (!TryComp<PhysicsComponent>(childUid, out var body) || !body.Hard)
                continue;

            if ((body.CollisionLayer & (int)CollisionGroup.BulletImpassable) == 0)
                continue;

            var (start, width) = GetObstacleSector(dir);
            sectors.Add((start, width));
        }

        // Merge overlapping sectors efficiently: O(n log n)
        if (sectors.Count == 0)
            return new();

        sectors.Sort((a, b) => a.start.Theta.CompareTo(b.start.Theta));

        var merged = new List<(Angle start, Angle width)>();
        var current = sectors[0];

        for (int i = 1; i < sectors.Count; i++)
        {
            var next = sectors[i];
            if (CrescentHelpers.AngSectorsOverlap(current.start, current.width, next.start, next.width))
            {
                var (newStart, newWidth) = CrescentHelpers.AngCombinedSector(current.start, current.width, next.start, next.width);
                current = (newStart, newWidth);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        var maxSpread = gun.MaxAngle + Angle.FromDegrees(10);
        var clearance = maxSpread + cannon.ClearanceAngle;

        var result = new List<(Angle, Angle)>(merged.Count);
        foreach (var (start, width) in merged)
        {
            result.Add((CrescentHelpers.AngNormal(start - clearance / 2), width + clearance));
        }

        return result;
    }

    private (Angle, Angle) GetObstacleSector(Vector2 delta)
    {
        Angle dirAngle = CrescentHelpers.AngNormal(new Angle(delta));
        Vector2 a, b;

        if (dirAngle % (Math.PI * 0.5) == 0)
        {
            switch (dirAngle.Theta)
            {
                case 0:
                case Math.Tau:
                    a = delta - Vector2Helpers.Half;
                    b = new Vector2(delta.X - 0.5f, delta.Y + 0.5f);
                    break;
                case Math.PI * 0.5:
                    a = delta - Vector2Helpers.Half;
                    b = new Vector2(delta.X + 0.5f, delta.Y - 0.5f);
                    break;
                case Math.PI:
                    a = delta + Vector2Helpers.Half;
                    b = new Vector2(delta.X + 0.5f, delta.Y - 0.5f);
                    break;
                case Math.PI * 1.5:
                    a = delta + Vector2Helpers.Half;
                    b = new Vector2(delta.X - 0.5f, delta.Y + 0.5f);
                    break;
                default:
                    return (double.NaN, double.NaN);
            }
        }
        else if (dirAngle > 0 && dirAngle < Math.PI * 0.5 || dirAngle > Math.PI && dirAngle < Math.PI * 1.5)
        {
            a = new Vector2(delta.X - 0.5f, delta.Y + 0.5f);
            b = new Vector2(delta.X + 0.5f, delta.Y - 0.5f);
        }
        else
        {
            a = delta + Vector2Helpers.Half;
            b = delta - Vector2Helpers.Half;
        }

        Angle start = CrescentHelpers.AngNormal(new Angle(a));
        Angle end = CrescentHelpers.AngNormal(new Angle(b));
        Angle width = Angle.ShortestDistance(start, end);
        if (width < 0)
        {
            start = end;
            width = -width;
        }

        return (start, width);
    }

    private List<ICommonSession> GetUiSessions(EntityUid uid)
    {
        var sessions = new List<ICommonSession>();
        foreach (var actorUid in _uiSys.GetActors(uid, TargetingConsoleUiKey.Key))
        {
            if (_playerMan.TryGetSessionByEntity(actorUid, out var session))
                sessions.Add(session);
        }
        return sessions;
    }

    private void TogglePvsOverride(IEnumerable<EntityUid> uids, IEnumerable<ICommonSession> sessions, bool enable)
    {
        foreach (var session in sessions)
        {
            foreach (var uid in uids)
            {
                if (!Exists(uid))
                    continue;

                if (enable)
                    _pvsSys.AddSessionOverride(uid, session);
                else
                    _pvsSys.RemoveSessionOverride(uid, session);
            }
        }
    }
}