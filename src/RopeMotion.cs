using System;
namespace GINRope {
 // A fixed simulation clock with sampled endpoint motion and a separate visible
 // pose. Rendering never advances time or changes the solver's Verlet history.
 public sealed class RopeMotion {
  public const double StepSeconds=1.0/120;
  const double ClockTolerance=StepSeconds*1e-8;
  readonly Solver solver;
  readonly V3[] previousState;
  readonly double[] displayScratch;
  V3 sampleA,sampleB;
  double remainder;
  bool paused;
  public Solver Solver{get{return solver;}}
  public double Remainder{get{return remainder;}}
  public int LastStepCount{get;private set;}
  public long TotalSteps{get;private set;}
  public RopeMotion(Solver value){
   if(value==null)throw new ArgumentNullException("value");solver=value;
   previousState=(V3[])solver.P.Clone();displayScratch=new double[solver.P.Length-1];
   sampleA=solver.P[0];sampleB=solver.P[solver.P.Length-1];
  }
  static void CheckAnchors(V3 a,V3 b){if(!a.Finite||!b.Finite)throw new ArgumentException("Non-finite anchor.");}
  public void Reset(V3 a,V3 b,Settings settings){
   CheckAnchors(a,b);settings.Validate();solver.Reset(a,b,settings);
   ClearClock(a,b);paused=settings.Paused;
  }
  // Use on a time-source change or pause: old fractional time and endpoint
  // samples must not leak into the first step of the new clock.
  public void ClearClock(V3 a,V3 b){
   CheckAnchors(a,b);sampleA=a;sampleB=b;remainder=0;LastStepCount=0;
   Array.Copy(solver.P,previousState,previousState.Length);
  }
  public void Advance(double elapsed,V3 a,V3 b,Settings settings){
   if(!Settings.Finite(elapsed)||elapsed<0||elapsed>2)throw new ArgumentException("Elapsed rope time must be between zero and two seconds.");
   CheckAnchors(a,b);settings.Validate();LastStepCount=0;
   if(settings.Paused){ClearClock(a,b);paused=true;return;}
   if(paused){ClearClock(a,b);paused=false;}
   double oldRemainder=remainder,total=oldRemainder+elapsed;
   int steps=(int)Math.Floor((total+ClockTolerance)/StepSeconds);
   for(int i=1;i<=steps;i++){
    // The next tick can fall partway through the current sampled interval.
    // Carried fractional time belongs to the previous interval, not this one.
    double fraction=elapsed>0?(i*StepSeconds-oldRemainder)/elapsed:1;
    fraction=Math.Max(0,Math.Min(1,fraction));
    V3 stepA=V3.Lerp(sampleA,a,fraction),stepB=V3.Lerp(sampleB,b,fraction);
    Array.Copy(solver.P,previousState,previousState.Length);
    solver.Step(StepSeconds,stepA,stepB,settings);
   }
   remainder=Math.Max(0,total-steps*StepSeconds);
   sampleA=a;sampleB=b;LastStepCount=steps;TotalSteps+=steps;
  }
  public void Render(V3 a,V3 b,Settings settings,V3[] destination){
   if(destination==null||destination.Length!=solver.P.Length||Object.ReferenceEquals(destination,solver.P))throw new ArgumentException("A separate display buffer is required.");
   CheckAnchors(a,b);settings.Validate();
   if(settings.Paused){Array.Copy(solver.P,destination,destination.Length);return;}
   double fraction=Math.Max(0,Math.Min(1,remainder/StepSeconds));
   for(int i=0;i<destination.Length;i++)destination[i]=V3.Lerp(previousState[i],solver.P[i],fraction);
   // Keep visual endpoints at their current handle positions. This projection
   // is deterministic and cannot feed render corrections back into physics.
   solver.ProjectCopy(destination,a,b,settings,displayScratch);
  }
 }
}
