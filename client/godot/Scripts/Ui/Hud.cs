using System.Collections.Generic;
using Godot;
using Underclass.Client.Town;
using Underclass.Contracts;

namespace Underclass.Client.Ui;

/// <summary>
///    Everything drawn over the Town, and which of it is on screen.
/// </summary>
/// <remarks>
///    The two standing panels are always up, because the numbers they carry are what the player decides with while
///    walking. The three that are places — the Workshop, the Board, the reports — open one at a time, and only two
///    of them open at all by walking somewhere: story 4 asks the Town's layout to carry meaning, which it cannot do
///    if the Workshop is a keystroke away from anywhere.
/// </remarks>
public sealed partial class Hud : CanvasLayer
{
   /// <summary>How long a notice stays up before it fades out of the way.</summary>
   private const double NoticeSeconds = 6;

   private Label _prompt = null!;
   private Label _notice = null!;
   private double _noticeLeft;
   private ITownView[] _views = [];

   /// <summary>Gold and Materials, always visible.</summary>
   public TownStatusPanel Status { get; private set; } = null!;

   /// <summary>Who is out and how long they have left, always visible.</summary>
   public ExpeditionsPanel Expeditions { get; private set; } = null!;

   /// <summary>Crafting. Opens by standing at the Workshop.</summary>
   public WorkshopPanel Workshop { get; private set; } = null!;

   /// <summary>Dispatch. Opens by standing at the Expedition Board.</summary>
   public ExpeditionBoardPanel Board { get; private set; } = null!;

   /// <summary>What came back. Opens from anywhere, since news is not a place.</summary>
   public ReportsPanel Reports { get; private set; } = null!;

   /// <inheritdoc />
   public override void _Ready()
   {
      Status = Corner(new TownStatusPanel(), Control.LayoutPreset.TopLeft);
      Expeditions = Corner(new ExpeditionsPanel(), Control.LayoutPreset.TopRight);

      Workshop = Centred(new WorkshopPanel());
      Board = Centred(new ExpeditionBoardPanel());
      Reports = Centred(new ReportsPanel());

      _views = [Status, Expeditions, Workshop, Board, Reports];

      _notice = Banner(offsetFromBottom: -108, size: 15);
      _prompt = Banner(offsetFromBottom: -64, size: 16);

      _prompt.Text = "WASD or the arrow keys walk the Town's lanes · R reads the reports · F5 asks the Town again";
   }

   /// <inheritdoc />
   public override void _Process(double delta)
   {
      if (_noticeLeft <= 0)
         return;

      _noticeLeft -= delta;

      if (_noticeLeft <= 0)
         _notice.Text = string.Empty;
   }

   /// <summary>True while one of the place panels is up, so walking can be left alone until it is put away.</summary>
   public bool SomethingIsOpen => Workshop.Visible || Board.Visible || Reports.Visible;

   /// <summary>
   ///    Hands a freshly read Town to every panel that draws one. The composition root does not name the panels
   ///    individually, so a new one is a line in <see cref="_Ready" /> and nothing else.
   /// </summary>
   public void Accept(TownResponse town)
   {
      foreach (var view in _views)
         view.Accept(town);
   }

   /// <summary>
   ///    Offers or withdraws the invitation to use somewhere — story 5. Walking away from a place also closes it,
   ///    so the panel and the position never disagree.
   /// </summary>
   public void StandingAt(Building? building)
   {
      if (building is null)
      {
         _prompt.Text = "WASD or the arrow keys walk the Town's lanes · R reads the reports · F5 asks the Town again";

         if (Workshop.Visible || Board.Visible)
            CloseAll();

         return;
      }

      _prompt.Text = $"Press E to {building.Invitation} at {building.Title}";
   }

   /// <summary>Opens the panel that belongs to a place, and closes whatever was up.</summary>
   public void Open(Control panel)
   {
      CloseAll();

      panel.Visible = true;
   }

   /// <summary>Puts the reports up, or takes them down again.</summary>
   public void ToggleReports()
   {
      var wanted = !Reports.Visible;

      CloseAll();

      Reports.Visible = wanted;
   }

   public void CloseAll()
   {
      Workshop.Visible = false;
      Board.Visible = false;
      Reports.Visible = false;
   }

   /// <summary>
   ///    Says something to the player: a refusal in the server's own words, or a plain report that the Town could
   ///    not be reached. Nothing clever happens either way — client-side resilience is out of scope, so a failure
   ///    is surfaced and that is the whole of it.
   /// </summary>
   public void Say(string message, bool bad = false)
   {
      _notice.Text = message;
      _notice.AddThemeColorOverride("font_color", bad ? Widgets.Bad : Widgets.Good);
      _noticeLeft = NoticeSeconds;
   }

   private T Corner<T>(T panel, Control.LayoutPreset corner) where T : Control
   {
      AddChild(panel);

      panel.SetAnchorsAndOffsetsPreset(corner, Control.LayoutPresetMode.KeepSize, margin: 16);

      return panel;
   }

   private T Centred<T>(T panel) where T : Control
   {
      AddChild(panel);

      panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.KeepSize);
      panel.Visible = false;

      return panel;
   }

   /// <summary>A line of text across the bottom of the screen.</summary>
   private Label Banner(int offsetFromBottom, int size)
   {
      var label = Widgets.Line(string.Empty, Widgets.Ink, size);

      label.HorizontalAlignment = HorizontalAlignment.Center;

      AddChild(label);

      label.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
      label.OffsetTop = offsetFromBottom;
      label.OffsetBottom = offsetFromBottom + 30;

      label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
      label.AddThemeConstantOverride("outline_size", 6);

      return label;
   }
}
