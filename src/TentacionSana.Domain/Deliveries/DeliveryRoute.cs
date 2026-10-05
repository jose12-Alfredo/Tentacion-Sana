namespace TentacionSana.Domain.Deliveries;

public enum DeliveryRouteStatus { Planned, InRoute, Completed }

public sealed class DeliveryRoute
{
    private DeliveryRoute() { }

    public Guid Id { get; private set; }
    public Guid DriverUserId { get; private set; }
    public DateTimeOffset PromisedDateUtc { get; private set; }
    public DeliveryRouteStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public int Version { get; private set; }
    public List<DeliveryRouteStop> Stops { get; private set; } = [];

    public static DeliveryRoute Create(Guid driverUserId, DateTimeOffset promisedDateUtc, IEnumerable<Guid> deliveryIds, Guid userId, DateTimeOffset now)
    {
        if (driverUserId == Guid.Empty) throw new ArgumentException("El repartidor es obligatorio.");
        var ids = deliveryIds.Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException("La ruta necesita al menos una parada.");

        var route = new DeliveryRoute
        {
            Id = Guid.NewGuid(), DriverUserId = driverUserId, PromisedDateUtc = promisedDateUtc,
            Status = DeliveryRouteStatus.Planned, CreatedAtUtc = now, CreatedByUserId = userId, Version = 1
        };
        for (var index = 0; index < ids.Count; index++) route.Stops.Add(DeliveryRouteStop.Create(route.Id, ids[index], index + 1, now));
        return route;
    }

    public void Reorder(IReadOnlyList<Guid> deliveryIds)
    {
        var ids = deliveryIds.Distinct().ToList();
        if (Status == DeliveryRouteStatus.Completed) throw new InvalidOperationException("La ruta completada no puede reordenarse.");
        if (ids.Count != Stops.Count || ids.Except(Stops.Select(x => x.DeliveryId)).Any()) throw new ArgumentException("Las paradas de la ruta no coinciden.");
        for (var index = 0; index < ids.Count; index++) Stops.Single(x => x.DeliveryId == ids[index]).SetPosition(index + 1);
        Version++;
    }

    public void RemoveStop(Guid deliveryId)
    {
        if (Status == DeliveryRouteStatus.Completed) throw new InvalidOperationException("La ruta completada no puede modificarse.");
        var stop = Stops.SingleOrDefault(x => x.DeliveryId == deliveryId) ?? throw new ArgumentException("La entrega no pertenece a la ruta.");
        Stops.Remove(stop);
        var ordered = Stops.OrderBy(x => x.Position).ToList();
        for (var index = 0; index < ordered.Count; index++) ordered[index].SetPosition(index + 1);
        Version++;
    }

    public void Start(DateTimeOffset now)
    {
        if (Status != DeliveryRouteStatus.Planned) throw new InvalidOperationException("La ruta ya fue iniciada.");
        Status = DeliveryRouteStatus.InRoute;
        StartedAtUtc = now;
        Version++;
    }

    public void ResumeFromAssignedDeliveries(DateTimeOffset now)
    {
        if (Status != DeliveryRouteStatus.Planned) return;
        Status = DeliveryRouteStatus.InRoute;
        StartedAtUtc = now;
        Version++;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != DeliveryRouteStatus.InRoute) return;
        Status = DeliveryRouteStatus.Completed;
        CompletedAtUtc = now;
        Version++;
    }
}

public sealed class DeliveryRouteStop
{
    private DeliveryRouteStop() { }

    public Guid Id { get; private set; }
    public Guid DeliveryRouteId { get; private set; }
    public Guid DeliveryId { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    internal static DeliveryRouteStop Create(Guid routeId, Guid deliveryId, int position, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), DeliveryRouteId = routeId, DeliveryId = deliveryId, Position = position, CreatedAtUtc = now
    };

    internal void SetPosition(int position) => Position = position;
}
