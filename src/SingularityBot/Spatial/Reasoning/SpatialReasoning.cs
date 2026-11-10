using System;
namespace SingularityBot.Spatial.Reasoning;

public readonly record struct SpatialPoint(float X, float Y)
{
    public static SpatialPoint operator +(SpatialPoint a, SpatialPoint b) => new(a.X+b.X,a.Y+b.Y);
    public static SpatialPoint operator -(SpatialPoint a, SpatialPoint b) => new(a.X-b.X,a.Y-b.Y);
    public float LengthSquared => X*X+Y*Y;
    public float Length => MathF.Sqrt(LengthSquared);
    public float Dot(SpatialPoint other) => X*other.X+Y*other.Y;
    public float DistanceTo(SpatialPoint other) => (this-other).Length;
    public SpatialPoint Normalized => Length <= 1e-6f ? default : new(X/Length,Y/Length);

    // Avoid recursive record formatting through the computed Normalized property.
    public override string ToString() => $"({X}, {Y})";
}
public enum BattlefieldSide { OwnSide, DividingLine, OpponentSide }
public enum RelativeBearing { Front, Back, Left, Right, CoincidentOrUnknown }
public enum MapQuadrant { SouthWest, SouthEast, NorthWest, NorthEast }
public enum GridRegion { BottomLeft, BottomCenter, BottomRight, MiddleLeft, Center, MiddleRight, TopLeft, TopCenter, TopRight }
public readonly record struct GridCell(int Column,int Row)
{
    public GridRegion Region => (Column,Row) switch {
        (0,0)=>GridRegion.BottomLeft,(1,0)=>GridRegion.BottomCenter,(2,0)=>GridRegion.BottomRight,
        (0,1)=>GridRegion.MiddleLeft,(1,1)=>GridRegion.Center,(2,1)=>GridRegion.MiddleRight,
        (0,2)=>GridRegion.TopLeft,(1,2)=>GridRegion.TopCenter,_=>GridRegion.TopRight
    };
}
public readonly record struct BattlefieldFrame(SpatialPoint Origin,SpatialPoint Forward)
{
    public SpatialPoint ForwardUnit => Forward.Normalized;
    public SpatialPoint LeftUnit => new(-ForwardUnit.Y,ForwardUnit.X);
    public static BattlefieldFrame FromStartLocations(SpatialPoint own,SpatialPoint opponent) => new(own,(opponent-own).Normalized);
    public float ForwardDistance(SpatialPoint point) => (point-Origin).Dot(ForwardUnit);
    public float LateralDistance(SpatialPoint point) => (point-Origin).Dot(LeftUnit);
    public BattlefieldSide ClassifySide(SpatialPoint point) {
        var d=ForwardDistance(point);
        return d>1e-4f?BattlefieldSide.OpponentSide:d< -1e-4f?BattlefieldSide.OwnSide:BattlefieldSide.DividingLine;
    }
    public RelativeBearing ClassifyRelative(SpatialPoint observer,SpatialPoint target,SpatialPoint facing) {
        var direction=(target-observer).Normalized; var forward=facing.Normalized;
        if(direction.LengthSquared<=1e-12f||forward.LengthSquared<=1e-12f)return RelativeBearing.CoincidentOrUnknown;
        var f=forward.Dot(direction); var right=new SpatialPoint(forward.Y,-forward.X); var r=right.Dot(direction);
        return MathF.Abs(f)>=MathF.Abs(r)?(f>=0?RelativeBearing.Front:RelativeBearing.Back):(r>=0?RelativeBearing.Right:RelativeBearing.Left);
    }
}
public readonly record struct MapBounds(float MinX,float MinY,float MaxX,float MaxY)
{
    public bool IsValid=>MaxX>MinX&&MaxY>MinY;
    public bool Contains(SpatialPoint p)=>IsValid&&p.X>=MinX&&p.X<=MaxX&&p.Y>=MinY&&p.Y<=MaxY;
    public GridCell ToGrid3x3(SpatialPoint p) {
        if(!IsValid)throw new InvalidOperationException("Map bounds must have positive width and height.");
        var x=Math.Clamp((p.X-MinX)/(MaxX-MinX),0f,1f); var y=Math.Clamp((p.Y-MinY)/(MaxY-MinY),0f,1f);
        return new GridCell(Math.Min(2,(int)(x*3f)),Math.Min(2,(int)(y*3f)));
    }
    public MapQuadrant ToQuadrant(SpatialPoint p) {
        var c=ToGrid3x3(p);
        return (c.Column>=1,c.Row>=1) switch {(false,false)=>MapQuadrant.SouthWest,(true,false)=>MapQuadrant.SouthEast,(false,true)=>MapQuadrant.NorthWest,_=>MapQuadrant.NorthEast};
    }
}
public readonly record struct SpatialCandidate(SpatialPoint Destination,int EnemiesBehind,float DistanceFromCurrent);
public static class SpatialReasoning
{
    public static int CountEnemiesBehind(SpatialPoint destination,SpatialPoint movementDirection,IReadOnlyList<SpatialPoint> enemies) {
        ArgumentNullException.ThrowIfNull(enemies); var forward=movementDirection.Normalized;
        if(forward.LengthSquared<=1e-12f)return 0;
        var left = new SpatialPoint(-forward.Y, forward.X);
        var count = 0;
        foreach (var enemy in enemies)
        {
            var offset = enemy - destination;
            var longitudinal = offset.Dot(forward);
            var lateral = MathF.Abs(offset.Dot(left));

            // "Behind" is the rear 90-degree cone, not the entire rear
            // hemisphere; distant lateral enemies do not count as screened.
            if (longitudinal < -1e-4f && lateral <= -longitudinal + 1e-4f)
                count++;
        }
        return count;
    }
    public static SpatialCandidate? FindPositionWithMostEnemiesBehind(SpatialPoint current,IReadOnlyList<SpatialPoint> candidates,IReadOnlyList<SpatialPoint> enemies) {
        ArgumentNullException.ThrowIfNull(candidates); ArgumentNullException.ThrowIfNull(enemies);
        SpatialCandidate? best=null;
        foreach(var p in candidates) {
            var move=p-current; if(move.LengthSquared<=1e-12f)continue;
            var candidate=new SpatialCandidate(p,CountEnemiesBehind(p,move,enemies),move.Length);
            if(best is null||candidate.EnemiesBehind>best.Value.EnemiesBehind||
               (candidate.EnemiesBehind==best.Value.EnemiesBehind&&candidate.DistanceFromCurrent<best.Value.DistanceFromCurrent))best=candidate;
        }
        return best;
    }
}
