using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Underclass.Client.Net;
using Underclass.Client.Town;
using Underclass.Client.Ui;
using Underclass.Contracts;

namespace Underclass.Client;

/// <summary>
///    Wires the Town, the HUD and the server together, and is the only place that knows about all three.
/// </summary>
/// <remarks>
///    <para>
///       The shape of this client is deliberate and is what story 42 asks for: the Town is scenery and input, the
///       panels render a snapshot and raise what the player asked for, and every one of those requests becomes an
///       HTTP call whose answer is a new snapshot. No outcome is computed here, nothing is rolled, and no rule about
///       what is allowed lives anywhere in this assembly. When a command is refused, the server's own sentence is
///       what the player reads.
///    </para>
///    <para>
///       The Town is also re-read on a timer, because an Expedition resolves whether or not anyone is looking. That
///       read is what settles it — so a player who leaves the client open watches their Explorer come back without
///       having to do anything, and a player who closed it finds out on the next one.
///    </para>
/// </remarks>
public sealed partial class Main : Node2D
{
   /// <summary>How often the Town is re-read. Often enough to notice a return, rarely enough to be quiet.</summary>
   private const double PollSeconds = 5;

   private readonly HashSet<Guid> _readReports = [];

   private GameApi _api = null!;
   private TownScene _town = null!;
   private Hud _hud = null!;

   private Building? _standingAt;
   private bool _busy;
   private bool _greeted;

   /// <inheritdoc />
   public override void _Ready()
   {
      Actions.Register();

      _api = new GameApi();
      AddChild(_api);

      _town = new TownScene();
      _town.StandingAtChanged += OnStandingAtChanged;
      AddChild(_town);

      _hud = new Hud();
      AddChild(_hud);

      WirePanels();
      StartPolling();

      Run(LoadAsync);
   }

   /// <inheritdoc />
   public override void _UnhandledInput(InputEvent @event)
   {
      if (@event.IsActionPressed(Actions.Interact))
      {
         Enter();

         return;
      }

      if (@event.IsActionPressed(Actions.Reports))
      {
         _hud.ToggleReports();

         return;
      }

      if (@event.IsActionPressed(Actions.Close))
      {
         _hud.CloseAll();

         return;
      }

      if (@event.IsActionPressed(Actions.Refresh))
         Run(ReadTownAsync);
   }

   /// <summary>
   ///    Turns each panel's intent into a request. Every handler follows the same shape — ask the server, then take
   ///    whatever Town it answers with — which is what keeps the client from ever holding an opinion about state.
   /// </summary>
   private void WirePanels()
   {
      _hud.Workshop.CraftRequested += (baseType, material) => Run(
         async () => {
            var result = await _api.CraftAsync(baseType, material);

            if (Refused(result))
               return;

            var crafted = result.Value!;

            _hud.Say($"Crafted a {crafted.Gear.Material} {crafted.Gear.BaseType} of power {crafted.Gear.Power}.");
            Accept(crafted.Town);
         }
      );

      _hud.Workshop.SalvageRequested += gearId => Run(
         async () => {
            var result = await _api.SalvageAsync(gearId);

            if (Refused(result))
               return;

            _hud.Say("Broken down for Materials.");
            Accept(result.Value!);
         }
      );

      _hud.Board.EquipRequested += (explorerId, gearId) => Run(
         async () => {
            var result = await _api.EquipAsync(explorerId, gearId);

            if (Refused(result))
               return;

            Accept(result.Value!);
         }
      );

      _hud.Board.RecruitRequested += () => Run(
         async () => {
            var result = await _api.RecruitAsync();

            if (Refused(result))
               return;

            _hud.Say("Someone new has joined the roster.");
            Accept(result.Value!);
         }
      );

      // The estimate is never cached and never derived: it is asked for again every time the proposal changes.
      _hud.Board.PreviewRequested += (explorerId, area, depth) => Run(
         async () => {
            var result = await _api.PreviewAsync(explorerId, area, depth);

            _hud.Board.Accept(result.Value, result.Problem);
         }
      );

      _hud.Board.DispatchRequested += (explorerId, area, depth) => Run(
         async () => {
            var result = await _api.DispatchAsync(explorerId, area, depth);

            if (Refused(result))
               return;

            var dispatched = result.Value!;

            _hud.Say($"Sent out. Due back at {dispatched.Expedition.CompletesAt:HH:mm} UTC.");
            Accept(dispatched.Town);
         }
      );
   }

   /// <summary>Fetches the content that never moves, then the Town.</summary>
   private async Task LoadAsync()
   {
      var areas = await _api.ReadAreasAsync();

      if (Refused(areas))
         return;

      _hud.Board.Accept(areas.Value!);

      var options = await _api.ReadCraftingOptionsAsync();

      if (Refused(options))
         return;

      _hud.Workshop.Accept(options.Value!);

      await ReadTownAsync();
   }

   private async Task ReadTownAsync()
   {
      if (_busy)
         return;

      _busy = true;

      try
      {
         var result = await _api.ReadTownAsync();

         if (Refused(result))
            return;

         Accept(result.Value!);
      }
      finally
      {
         _busy = false;
      }
   }

   /// <summary>Hands a new snapshot to everything that draws one, then says whatever is worth saying about it.</summary>
   private void Accept(TownResponse town)
   {
      _hud.Accept(town);

      Announce(town);
   }

   /// <summary>
   ///    Says something when an Expedition has resolved since the player last looked. The reports themselves are a
   ///    keystroke away; this is only what makes them worth pressing.
   /// </summary>
   private void Announce(TownResponse town)
   {
      var arrived = new List<ReportResponse>();

      foreach (var report in town.Reports)
      {
         if (_readReports.Add(report.Id))
            arrived.Add(report);
      }

      // The first read brings back everything that ever happened, none of which is news.
      if (!_greeted)
      {
         _greeted = true;

         _hud.Say("Welcome back to your Town.");

         return;
      }

      if (arrived.Count == 0)
         return;

      var latest = arrived[0];

      _hud.Say(
         latest.Survived
            ? $"{latest.ExplorerName} is back from {latest.Area}. Press R to read it."
            : $"{latest.ExplorerName} did not come back from {latest.Area}. Press R to read it.",
         bad: !latest.Survived
      );
   }

   /// <summary>Story 4: the Workshop and the Board are opened by being at them, not from a menu.</summary>
   private void Enter()
   {
      if (_standingAt is null)
      {
         if (_hud.SomethingIsOpen)
            _hud.CloseAll();

         return;
      }

      if (_standingAt == _town.Workshop)
      {
         _hud.Open(_hud.Workshop);

         return;
      }

      if (_standingAt == _town.ExpeditionBoard)
      {
         _hud.Open(_hud.Board);
         _hud.Board.Reopened();
      }
   }

   private void OnStandingAtChanged(Building? building)
   {
      _standingAt = building;

      _hud.StandingAt(building);
   }

   private void StartPolling()
   {
      var timer = new Timer { WaitTime = PollSeconds, Autostart = true };

      timer.Timeout += () => Run(ReadTownAsync);

      AddChild(timer);
   }

   /// <summary>
   ///    True when the call did not work, having already told the player why. Named for how it reads at the call
   ///    site: a refusal and an unreachable server are the same thing to this client.
   /// </summary>
   private bool Refused<T>(ApiResult<T> result)
   {
      if (result.Ok)
         return false;

      _hud.Say(result.Problem ?? "The Town would not answer.", bad: true);

      return true;
   }

   /// <summary>
   ///    Runs a request without making the caller an async void. Anything that escapes is shown to the player
   ///    rather than swallowed into a log nobody is reading while playing.
   /// </summary>
   private void Run(Func<Task> work)
   {
      _ = Guarded(work);
   }

   private async Task Guarded(Func<Task> work)
   {
      try
      {
         await work();
      }
      catch (Exception exception)
      {
         GD.PushError(exception.ToString());

         _hud.Say($"Something went wrong talking to the Town: {exception.Message}", bad: true);
      }
   }
}
