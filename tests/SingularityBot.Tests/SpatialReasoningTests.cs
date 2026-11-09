using SingularityBot.Spatial.Reasoning;
using Xunit;
namespace SingularityBot.Tests;
public sealed class SpatialReasoningTests
{
 [Fact] public void BattlefieldSideUsesSpawnOrientation() {
  var f=BattlefieldFrame.FromStartLocations(new(90,20),new(10,80));
  Assert.Equal(BattlefieldSide.OwnSide,f.ClassifySide(new(100,10)));
  Assert.Equal(BattlefieldSide.OpponentSide,f.ClassifySide(new(20,70)));
 }
 [Fact] public void RelativeBearingUsesUnitFacing() {
  var f=BattlefieldFrame.FromStartLocations(new(0,0),new(100,100));
  Assert.Equal(RelativeBearing.Front,f.ClassifyRelative(new(10,10),new(12,12),new(1,1)));
  Assert.Equal(RelativeBearing.Back,f.ClassifyRelative(new(10,10),new(8,8),new(1,1)));
  Assert.Equal(RelativeBearing.Right,f.ClassifyRelative(new(10,10),new(12,8),new(1,1)));
  Assert.Equal(RelativeBearing.Left,f.ClassifyRelative(new(10,10),new(8,12),new(1,1)));
 }
 [Fact] public void BoundsProduceQuadrantsAndThreeByThreeCells() {
  var b=new MapBounds(0,0,90,90);
  Assert.Equal(MapQuadrant.NorthEast,b.ToQuadrant(new(89,89)));
  Assert.Equal(MapQuadrant.SouthWest,b.ToQuadrant(new(1,1)));
  Assert.Equal(new GridCell(1,2),b.ToGrid3x3(new(45,80)));
  Assert.Equal(GridRegion.MiddleRight,b.ToGrid3x3(new(80,45)).Region);
 }
 [Fact] public void CandidateScoringMaximizesEnemiesBehind() {
  var result=SpatialReasoning.FindPositionWithMostEnemiesBehind(new(0,0),new[]{new SpatialPoint(11,0),new SpatialPoint(0,5)},new[]{new SpatialPoint(8,0),new SpatialPoint(9,1),new SpatialPoint(10,-1)});
  Assert.NotNull(result); Assert.Equal(new SpatialPoint(5,0),result.Value.Destination); Assert.Equal(3,result.Value.EnemiesBehind);
 }
 [Fact] public void ZeroDirectionHasNoInventedRearHemisphere() =>
  Assert.Equal(0,SpatialReasoning.CountEnemiesBehind(new(0,0),new(0,0),new[]{new SpatialPoint(1,1)}));
}