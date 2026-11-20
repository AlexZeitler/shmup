using System.Collections.Generic;
using Godot;

namespace Shmup;

/// <summary>
/// Scrolling cave made of 8×8 tiles. The terrain is split into sections. Each
/// section is generated from its own seed and continues the cave where the
/// previous one ended. Sections are kept, so jumping back to the start of a
/// section shows exactly the same landscape.
/// </summary>
public partial class Terrain : Node2D
{
    public const int Tile = 8;
    public const int Rows = 23; // 184 px, the last row reaches past the bottom edge
    public const int SectionColumns = 38; // 304 px, about 10 seconds
    public const float ScrollSpeed = 0.5f;

    /// <summary>In this section the cave opens into the arena of the boss and the scrolling stops.</summary>
    public const int ArenaSection = 12;
    private const int ArenaScrollPx = ArenaSection * SectionColumns * Tile;

    private const int VisibleColumns = Main.ScreenW / Tile + 1;
    private const int OpenCeiling = 1;
    private const int OpenFloor = 2;
    // Rows 7 to 14 always stay free, which keeps the respawn point open.
    private const int MaxCeiling = 7;
    private const int MaxFloor = 8;
    private const int MinGap = 6; // rows
    // Ceiling and floor move one row per column, so they start opening before the arena.
    private const int ArenaRunUp = MaxFloor + 2; // columns

    /// <summary>Columns of a section plus the walker state it ends with.</summary>
    private sealed class Section
    {
        public readonly uint[] Columns = new uint[SectionColumns];
        public int Ceiling = OpenCeiling, Floor = OpenFloor;
        public int TargetCeiling = OpenCeiling, TargetFloor = OpenFloor, Hold;
    }

    private static readonly uint OpenColumn = ColumnBits(OpenCeiling, OpenFloor);

    private readonly List<Section> _sections = new();
    private TileMapLayer _layer = null!;
    private ulong _runSeed;
    private double _scroll;
    private int _drawnFrom, _drawnTo; // drawn columns, end exclusive

    /// <summary>Scroll position in whole pixels; drawing and collisions both use it.</summary>
    public int ScrollPx => (int)_scroll;

    /// <summary>The start of the arena has reached the left edge; the scrolling has stopped.</summary>
    public bool InArena => ScrollPx >= ArenaScrollPx;

    public override void _Ready()
    {
        var atlas = new TileSetAtlasSource
        {
            Texture = Sprites.Terrain,
            TextureRegionSize = new Vector2I(Tile, Tile),
        };
        for (var mask = 0; mask < 16; mask++)
            atlas.CreateTile(new Vector2I(mask, 0));
        var tileSet = new TileSet { TileSize = new Vector2I(Tile, Tile) };
        tileSet.AddSource(atlas, 0);
        _layer = new TileMapLayer { TileSet = tileSet };
        AddChild(_layer);
    }

    public void Reset(ulong runSeed)
    {
        _runSeed = runSeed;
        _sections.Clear();
        RewindTo(0);
    }

    /// <summary>Section that contains the given screen x.</summary>
    public int SectionAt(float screenX) =>
        Mathf.FloorToInt((ScrollPx + screenX) / (SectionColumns * Tile));

    /// <summary>Jumps back so that the start of the section is at the left edge.</summary>
    public void RewindTo(int section)
    {
        _scroll = section * SectionColumns * Tile;
        _layer.Clear();
        _drawnFrom = _drawnTo = FirstVisibleColumn;
        UpdateTiles();
    }

    public void Step()
    {
        _scroll = Mathf.Min(_scroll + ScrollSpeed, ArenaScrollPx);
        UpdateTiles();
    }

    /// <summary>True if the screen rectangle touches solid rock.</summary>
    public bool Overlaps(Rect2 rect)
    {
        var x0 = Mathf.FloorToInt((rect.Position.X + ScrollPx) / Tile);
        var x1 = Mathf.FloorToInt((rect.End.X + ScrollPx - 0.01f) / Tile);
        var y0 = Mathf.Max(0, Mathf.FloorToInt(rect.Position.Y / Tile));
        var y1 = Mathf.Min(Rows - 1, Mathf.FloorToInt((rect.End.Y - 0.01f) / Tile));
        for (var col = x0; col <= x1; col++)
            for (var row = y0; row <= y1; row++)
                if (IsSolid(col, row))
                    return true;
        return false;
    }

    /// <summary>
    /// Free vertical space in pixels for something at the screen rectangle: the
    /// run of free rows nearest to its centre, narrowed over every column it spans.
    /// </summary>
    public (float Top, float Bottom) FreeSpan(Rect2 rect)
    {
        var x0 = Mathf.FloorToInt((rect.Position.X + ScrollPx) / Tile);
        var x1 = Mathf.FloorToInt((rect.End.X + ScrollPx - 0.01f) / Tile);
        var centre = Mathf.Clamp(Mathf.FloorToInt(rect.GetCenter().Y / Tile), 0, Rows - 1);
        int top = 0, bottom = Rows;
        for (var col = x0; col <= x1; col++)
        {
            var (runTop, runBottom) = FreeRun(col, centre);
            top = Mathf.Max(top, runTop);
            bottom = Mathf.Min(bottom, runBottom);
        }
        // On a steep slope the narrowed span can close; then only the centre column counts.
        if (bottom <= top)
            (top, bottom) = FreeRun(Mathf.FloorToInt((rect.GetCenter().X + ScrollPx) / Tile), centre);
        return (top * Tile, bottom * Tile);
    }

    /// <summary>Free rows [top, bottom) around the free row nearest to the given row.</summary>
    private (int Top, int Bottom) FreeRun(int col, int row)
    {
        var start = row;
        for (var d = 0; d < Rows; d++)
        {
            if (row - d >= 0 && !IsSolid(col, row - d)) { start = row - d; break; }
            if (row + d < Rows && !IsSolid(col, row + d)) { start = row + d; break; }
        }
        int top = start, bottom = start + 1;
        while (top > 0 && !IsSolid(col, top - 1)) top--;
        while (bottom < Rows && !IsSolid(col, bottom)) bottom++;
        return (top, bottom);
    }

    private int FirstVisibleColumn => ScrollPx / Tile;

    private void UpdateTiles()
    {
        _layer.Position = new Vector2(-ScrollPx, 0);
        var from = FirstVisibleColumn;
        var to = from + VisibleColumns + 1;
        for (; _drawnFrom < from; _drawnFrom++)
            for (var row = 0; row < Rows; row++)
                _layer.EraseCell(new Vector2I(_drawnFrom, row));
        for (; _drawnTo < to; _drawnTo++)
            DrawColumn(_drawnTo);
    }

    private void DrawColumn(int col)
    {
        for (var row = 0; row < Rows; row++)
        {
            if (!IsSolid(col, row)) continue;
            // Exposed sides pick the tile: 1 = up, 2 = right, 4 = down, 8 = left.
            var mask = 0;
            if (row > 0 && !IsSolid(col, row - 1)) mask |= 1;
            if (!IsSolid(col + 1, row)) mask |= 2;
            if (row < Rows - 1 && !IsSolid(col, row + 1)) mask |= 4;
            if (!IsSolid(col - 1, row)) mask |= 8;
            _layer.SetCell(new Vector2I(col, row), 0, new Vector2I(mask, 0));
        }
    }

    private bool IsSolid(int col, int row)
    {
        var index = Mathf.FloorToInt((float)col / SectionColumns);
        // Left of the first section the cave is open.
        var bits = index < 0 ? OpenColumn : SectionAtIndex(index).Columns[col - index * SectionColumns];
        return (bits & (1u << row)) != 0;
    }

    /// <summary>Sections are generated in order, each one continues the previous.</summary>
    private Section SectionAtIndex(int index)
    {
        while (_sections.Count <= index)
            _sections.Add(Generate(_sections.Count, _sections.Count > 0 ? _sections[^1] : null));
        return _sections[index];
    }

    /// <summary>
    /// One bit per row and column. Ceiling and floor walk towards random target
    /// heights, one row per column at most, so every slope can be followed.
    /// </summary>
    private Section Generate(int index, Section? previous)
    {
        var rng = new RandomNumberGenerator { Seed = _runSeed * 1_000_003UL + (ulong)index };
        var section = new Section();
        int ceiling = OpenCeiling, floor = OpenFloor;
        // The game starts with one open screen as a run-up.
        int targetCeiling = OpenCeiling, targetFloor = OpenFloor, hold = Main.ScreenW / Tile;
        if (previous != null)
        {
            (ceiling, floor) = (previous.Ceiling, previous.Floor);
            (targetCeiling, targetFloor, hold) = (previous.TargetCeiling, previous.TargetFloor, previous.Hold);
        }

        for (var col = 0; col < SectionColumns; col++)
        {
            if (index * SectionColumns + col >= ArenaSection * SectionColumns - ArenaRunUp)
            {
                // From here on the cave stays open: the arena and everything right of it.
                targetCeiling = OpenCeiling;
                targetFloor = OpenFloor;
            }
            else if (--hold <= 0)
            {
                hold = rng.RandiRange(6, 18);
                targetCeiling = rng.RandiRange(OpenCeiling, MaxCeiling);
                targetFloor = rng.RandiRange(OpenFloor, MaxFloor);
                var excess = targetCeiling + targetFloor - (Rows - MinGap);
                if (excess > 0)
                {
                    var fromCeiling = rng.RandiRange(0, excess);
                    targetCeiling -= fromCeiling;
                    targetFloor -= excess - fromCeiling;
                }
            }
            ceiling += System.Math.Sign(targetCeiling - ceiling);
            floor += System.Math.Sign(targetFloor - floor);
            section.Columns[col] = ColumnBits(ceiling, floor);
        }

        (section.Ceiling, section.Floor) = (ceiling, floor);
        (section.TargetCeiling, section.TargetFloor, section.Hold) = (targetCeiling, targetFloor, hold);
        return section;
    }

    private static uint ColumnBits(int ceiling, int floor)
    {
        uint bits = 0;
        for (var row = 0; row < ceiling; row++) bits |= 1u << row;
        for (var row = Rows - floor; row < Rows; row++) bits |= 1u << row;
        return bits;
    }
}
