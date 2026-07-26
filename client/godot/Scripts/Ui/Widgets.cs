using System;
using Godot;

namespace Underclass.Client.Ui;

/// <summary>
///    The handful of controls every panel is made of.
/// </summary>
/// <remarks>
///    There are no art assets and no theme resource, so the look is assembled here rather than in the editor. It
///    exists so the panels read as what they show instead of as a wall of style overrides.
/// </remarks>
public static class Widgets
{
   public static readonly Color Ink = new(0.92f, 0.91f, 0.86f);
   public static readonly Color Quiet = new(0.66f, 0.65f, 0.61f);
   public static readonly Color Good = new(0.55f, 0.78f, 0.50f);
   public static readonly Color Bad = new(0.88f, 0.44f, 0.40f);
   public static readonly Color Gold = new(0.90f, 0.78f, 0.40f);

   /// <summary>A framed panel with a column inside it. Returns the column, since that is what callers fill.</summary>
   public static VBoxContainer Panel(Control host, int padding = 14)
   {
      var frame = new StyleBoxFlat {
         BgColor = new Color(0.09f, 0.10f, 0.12f, 0.94f),
         BorderColor = new Color(0.35f, 0.37f, 0.42f),
         ContentMarginLeft = padding,
         ContentMarginRight = padding,
         ContentMarginTop = padding,
         ContentMarginBottom = padding,
         CornerRadiusTopLeft = 6,
         CornerRadiusTopRight = 6,
         CornerRadiusBottomLeft = 6,
         CornerRadiusBottomRight = 6
      };

      frame.SetBorderWidthAll(1);

      host.AddThemeStyleboxOverride("panel", frame);

      var column = new VBoxContainer();
      host.AddChild(column);

      return column;
   }

   public static Label Heading(string text)
   {
      var label = Line(text, Ink);

      label.AddThemeFontSizeOverride("font_size", 19);

      return label;
   }

   public static Label Line(string text, Color? colour = null, int size = 14)
   {
      var label = new Label {
         Text = text,
         AutowrapMode = TextServer.AutowrapMode.WordSmart
      };

      label.AddThemeColorOverride("font_color", colour ?? Ink);
      label.AddThemeFontSizeOverride("font_size", size);

      return label;
   }

   public static HSeparator Rule()
   {
      return new HSeparator();
   }

   public static Button Press(string text, Action onPressed)
   {
      var button = new Button { Text = text };

      button.Pressed += onPressed;

      return button;
   }

   /// <summary>
   ///    A row in a list the player picks from. The selected one is marked in its text rather than by a style, so
   ///    that what is chosen survives the list being rebuilt from a new snapshot.
   /// </summary>
   public static Button Row(string text, bool chosen, bool enabled, Action onPressed)
   {
      var button = new Button {
         Text = chosen ? $"▸ {text}" : $"   {text}",
         Alignment = HorizontalAlignment.Left,
         Disabled = !enabled
      };

      button.AddThemeFontSizeOverride("font_size", 13);
      button.Pressed += onPressed;

      return button;
   }

   /// <summary>A scrolling column, for lists that can outgrow their panel.</summary>
   public static VBoxContainer Scrolling(Control parent, int height)
   {
      var scroll = new ScrollContainer {
         CustomMinimumSize = new Vector2(0, height),
         HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
      };

      var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

      scroll.AddChild(column);
      parent.AddChild(scroll);

      return column;
   }

   /// <summary>Empties a container. Panels re-render from a whole snapshot, so this happens a lot.</summary>
   public static void Clear(Node container)
   {
      foreach (var child in container.GetChildren())
      {
         container.RemoveChild(child);
         child.QueueFree();
      }
   }

   /// <summary>
   ///    A duration as the player should read it. Anything already due reads as due rather than as a negative
   ///    number, because an Expedition past its time is waiting on a read, not overdue.
   /// </summary>
   public static string Remaining(TimeSpan left)
   {
      if (left <= TimeSpan.Zero)
         return "due now";

      if (left.TotalHours >= 1)
         return $"{(int)left.TotalHours}h {left.Minutes:00}m";

      return left.TotalMinutes >= 1 ? $"{left.Minutes}m {left.Seconds:00}s" : $"{left.Seconds}s";
   }

   /// <summary>A duration as a plain length of time, for what an Expedition would take.</summary>
   public static string Length(TimeSpan span)
   {
      return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m" : $"{(int)span.TotalMinutes}m";
   }
}
