using Content.Server.EventScheduler;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.HullrotSelfDeleteTimer;

public sealed class HullrotSelfDeleteSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly IEntityManager _IentityManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IMapManager _mappingManager = default!;
    [Dependency] private readonly EventSchedulerSystem _eventScheduler = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private TimeSpan _roundstartDelayBeforeSystemActivates = TimeSpan.FromSeconds(30); // needed because some shit like salvage is initially parented to space...for some reason
    private float _distanceToDeleteItems = 5;
    private float _distanceToDeleteGrids = 256;
    private TimeSpan _delayBetweenGridDeleteAttempts = TimeSpan.FromMinutes(10);



    public override void Initialize()
    {
        SubscribeLocalEvent<SelfDeleteComponent, ComponentInit>(OnInitEntity); // used for autodeleting stuff manually


        //_sawmill.Debug("it's showtime");
        SubscribeLocalEvent<SelfDeleteGridComponent, ComponentInit>(OnInitGrid); // used for cleaning up debris grids
        SubscribeLocalEvent<SelfDeleteInSpaceComponent, EntParentChangedMessage>(OnParentChanged); // used for cleaning up items in space

        SubscribeLocalEvent<SelfDeleteGridComponent, HullrotAttemptCleanupGrid>(DeleteGrid);
        SubscribeLocalEvent<SelfDeleteInSpaceComponent, HullrotAttemptCleanupItem>(TryDeleteEntityInSpace);
    }

    private void OnInitEntity(EntityUid uid, SelfDeleteComponent component, ComponentInit args)
    {
        Timer.Spawn(component.TimeToDelete, () => DeleteEntity(uid));
    }

    private void OnInitGrid(EntityUid uid, SelfDeleteGridComponent component, ComponentInit args)
    {
        // .2 note: i have **no idea** why this doesn't fucking work here
        // if (Name(uid) != "grid") //we ONLY want this to spawn on debris grids that break off, not on anything else
        //     return;              //as you might have guessed, these are always called "grid"
        // RemComp<SelfDeleteGridComponent>(uid); // we have to add it to all grids on gridinit in shuttlesystem, so we delete it here if it's not meant to have it.
        var dEv = new HullrotAttemptCleanupGrid();
        _eventScheduler.DelayEvent(uid, ref dEv, _delayBetweenGridDeleteAttempts);
    }

    private void OnParentChanged(EntityUid uid, SelfDeleteInSpaceComponent component, EntParentChangedMessage args)
    {
        if (uid == EntityUid.Invalid)
            return;

        if (_gameTicker.RoundDuration() < _roundstartDelayBeforeSystemActivates)
            return;


        if (args.Transform.GridUid == null)
        {
            var dEv = new HullrotAttemptCleanupItem();
            _eventScheduler.DelayEvent(uid, ref dEv, component.RetryDelay);
        }
    }

    private void DeleteEntity(EntityUid uid)
    {
        _IentityManager.DeleteEntity(uid);
    }

    // in hullrot, entities with the selfdeletinginspace component call this after their configured retry delay.
    // first, we check if the owning station is null (empty space). if so, THEN we run through a check for all the current
    // actors (players) being nearby. if yes, don't delete the item and try again later.
    private void TryDeleteEntityInSpace(EntityUid uid, SelfDeleteInSpaceComponent component, HullrotAttemptCleanupItem args)
    {

        if (!TryComp<MetaDataComponent>(uid, out var _))
            return;

        if (!TryComp<TransformComponent>(uid, out var transformComp))
            return;

        if (!_mappingManager.IsMapInitialized(transformComp.MapID))
            return;


        if (transformComp.GridUid == null)
        {
            var enumerator = EntityManager.EntityQueryEnumerator<ActorComponent>();
            while (enumerator.MoveNext(out var actorUid, out var _))
            {
                var distance = (_transform.GetWorldPosition(uid) - _transform.GetWorldPosition(actorUid)).Length();

                if (distance < _distanceToDeleteItems)
                {
                    var dEv = new HullrotAttemptCleanupItem();
                    _eventScheduler.DelayEvent(uid, ref dEv, component.RetryDelay);
                    return;
                }
            }

            _IentityManager.DeleteEntity(uid);
        }
        {
            return;
        }
    }

    // in hullrot, this is used for self-deleting grids that blast off of ships after a while.
    // we run an additional check when it's delete time to see if anybody is within 256 tiles of the deleting grid
    // so that there's no chance of accidentally deleting a player, and of grids vanishing before your eyes.
    // if that check fails, fire another timer and try again.
    private void DeleteGrid(EntityUid uid, SelfDeleteGridComponent component, HullrotAttemptCleanupGrid args)
    {
        // I KNOW THIS WORKS
        if (Name(uid) != "grid") // we ONLY want this to spawn on debris grids that break off, not on anything else
        {
            RemComp<SelfDeleteGridComponent>(uid); // as you might have guessed, these are always called "grid"
            return;
        }

        if (!TryComp<MetaDataComponent>(uid, out var _)) // was throwing errors because somehow entities with no metadata component were getting this called
            return;

        if (!TryComp<TransformComponent>(uid, out var transformComp)) // was throwing errors because somehow entities with no transform component were getting this called
            return;

        if (!_mappingManager.IsMapInitialized(transformComp.MapID)) // if the map is NOT initialized, then WE ARE IN MAPPING MODE!!! SO DON'T DO SHIT!!!!
            return;

        var enumerator = EntityManager.EntityQueryEnumerator<ActorComponent>(); // only players are actors. i think.
        while (enumerator.MoveNext(out var actorUid, out var _))
        {
            if ((_transform.GetWorldPosition(uid) - _transform.GetWorldPosition(actorUid)).Length() < _distanceToDeleteGrids)
            {
                var dEv = new HullrotAttemptCleanupGrid();
                _eventScheduler.DelayEvent(uid, ref dEv, _delayBetweenGridDeleteAttempts);
                return;
            }
        }

        _IentityManager.DeleteEntity(uid);
    }
}

[ByRefEvent]
public record struct HullrotAttemptCleanupGrid();

[ByRefEvent]
public record struct HullrotAttemptCleanupItem();
