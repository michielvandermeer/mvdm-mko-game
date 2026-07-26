namespace Underclass.Domain;

/// <summary>
///    The Areas an Expedition can be sent to. Content, not data: two Areas do not justify a content
///    pipeline, so the catalogue lives in <see cref="Catalogue" /> and the database stores only this name.
/// </summary>
public enum AreaId
{
   /// <summary>Close to Town, shallow, and poor. Survivable in Copper Gear; barely pays its Artisan's wage.</summary>
   Quarry,

   /// <summary>Far, deep, and the only real source of Iron and Steel. Kills under-equipped Explorers.</summary>
   SunkenVault
}
