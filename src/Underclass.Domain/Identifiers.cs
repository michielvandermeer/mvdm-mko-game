namespace Underclass.Domain;

/// <summary>Identity of an Explorer. Survives the Explorer's death, since a dead Explorer stays in the roster.</summary>
public readonly record struct ExplorerId(Guid Value)
{
   public static ExplorerId New() => new(Guid.NewGuid());

   public override string ToString() => Value.ToString();
}

/// <summary>Identity of a piece of Gear. Destroyed with its Explorer, so it is never reused.</summary>
public readonly record struct GearId(Guid Value)
{
   public static GearId New() => new(Guid.NewGuid());

   public override string ToString() => Value.ToString();
}

/// <summary>Identity of one dispatch of one Explorer to one Area.</summary>
public readonly record struct ExpeditionId(Guid Value)
{
   public static ExpeditionId New() => new(Guid.NewGuid());

   public override string ToString() => Value.ToString();
}

/// <summary>Identity of the report an Expedition leaves behind when it resolves.</summary>
public readonly record struct ReportId(Guid Value)
{
   public static ReportId New() => new(Guid.NewGuid());

   public override string ToString() => Value.ToString();
}
