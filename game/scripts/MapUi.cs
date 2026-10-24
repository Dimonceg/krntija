using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Миникарта: контур территории, трасса, съезды, найденные места, граница
/// и закрытые выезды. Открывается по M, мышь не нужна.
/// </summary>
public partial class MapUi : Control
{
    private List<Vector3> _road = new();
    private List<List<Vector3>> _branches = new();
    private List<MapPlace> _places = new();
    private List<int> _exits = new();
    private Vector3 _player;
    private bool _hasPlayer;

    private Vector2 _min;
    private Vector2 _max;

    public bool IsOpen => Visible;

    /// <summary>Компактный режим — маленькая карта в углу HUD.</summary>
    public bool Compact { get; set; }

    private float _redrawTimer;


    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetData(List<Vector3> road, List<List<Vector3>> branches, List<MapPlace> places, List<int> exits)
    {
        _road = road;
        _branches = branches;
        _places = places;
        _exits = exits;
        QueueRedraw();
    }

    public void Toggle()
    {
        Visible = !Visible;
        QueueRedraw();
    }

    public void UpdatePlayer(Vector3 position)
    {
        _player = position;
        _hasPlayer = true;
        if (Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.05f, 0.05f, 0.06f, 0.85f));
        if (_road.Count < 2) return;

        ComputeBounds();
        var panel = Compact
            ? new Rect2(2, 2, Size.X - 4, Size.Y - 4)
            : new Rect2(Size.X * 0.12f, Size.Y * 0.08f, Size.X * 0.76f, Size.Y * 0.84f);
        DrawRect(panel, new Color(0.16f, 0.18f, 0.14f, 0.95f));
        DrawRect(panel, new Color(0.7f, 0.65f, 0.5f, 0.9f), false, 2f);

        // Граница карты — рамка чуть внутри панели.
        DrawRect(panel.Grow(-8f), new Color(0.6f, 0.2f, 0.15f, 0.55f), false, 1.5f);

        for (int i = 0; i < _road.Count - 1; i += 2)
            DrawLine(Project(panel, _road[i]), Project(panel, _road[i + 1]), new Color(0.85f, 0.85f, 0.85f), 2f);

        foreach (var branch in _branches)
            for (int i = 0; i < branch.Count - 1; i++)
                DrawLine(Project(panel, branch[i]), Project(panel, branch[i + 1]), new Color(0.7f, 0.55f, 0.35f), 1.5f);

        foreach (var place in _places)
        {
            Color color = place.Kind switch
            {
                "house" => new Color(0.95f, 0.85f, 0.5f),
                "ruins" => new Color(0.7f, 0.7f, 0.7f),
                "barn" => new Color(0.8f, 0.6f, 0.35f),
                "well" => new Color(0.5f, 0.8f, 0.95f),
                "dealer" => new Color(0.6f, 0.95f, 0.6f),
                "wreck" => new Color(0.9f, 0.5f, 0.4f),
                "tractor" => new Color(0.75f, 0.65f, 0.35f),
                "dog" => new Color(0.9f, 0.75f, 0.55f),
                _ => new Color(0.8f, 0.8f, 0.8f),
            };

            DrawCircle(Project(panel, place.Position), 3f, color);
        }

        // Закрытые выезды: красные ромбы у краёв.
        foreach (int index in _exits)
        {
            if (index < 0 || index >= _road.Count) continue;
            Vector2 point = Project(panel, _road[index]);
            DrawCircle(point, 4f, new Color(0.85f, 0.25f, 0.2f));
            DrawLine(point + new Vector2(-3, 0), point + new Vector2(3, 0), new Color(0.9f, 0.9f, 0.9f), 1.5f);
        }

        if (_hasPlayer)
        {
            Vector2 player = Project(panel, _player);
            DrawCircle(player, 4.5f, new Color(0.95f, 0.95f, 0.95f));
            DrawCircle(player, 2.5f, new Color(0.2f, 0.6f, 0.9f));
        }

        if (Compact) return;

        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(panel.Position.X + 10, panel.Position.Y + 22),
            "M — карта   •   красные точки: закрытые выезды", HorizontalAlignment.Left, -1, 14,
            new Color(0.9f, 0.9f, 0.9f, 0.85f));
    }

    private void ComputeBounds()
    {
        _min = new Vector2(float.MaxValue, float.MaxValue);
        _max = new Vector2(-float.MaxValue, -float.MaxValue);

        void Include(Vector3 point)
        {
            _min = new Vector2(Mathf.Min(_min.X, point.X), Mathf.Min(_min.Y, point.Z));
            _max = new Vector2(Mathf.Max(_max.X, point.X), Mathf.Max(_max.Y, point.Z));
        }

        foreach (var point in _road) Include(point);
        foreach (var branch in _branches)
            foreach (var point in branch) Include(point);
        foreach (var place in _places) Include(place.Position);

        if (_max.X - _min.X < 1f) _max.X = _min.X + 1f;
        if (_max.Y - _min.Y < 1f) _max.Y = _min.Y + 1f;
    }

    private Vector2 Project(Rect2 panel, Vector3 world)
    {
        float u = Mathf.InverseLerp(_min.X, _max.X, world.X);
        float v = Mathf.InverseLerp(_min.Y, _max.Y, world.Z);

        // Ось Z вверх: север карты сверху.
        return new Vector2(panel.Position.X + u * panel.Size.X, panel.End.Y - v * panel.Size.Y);
    }
}
