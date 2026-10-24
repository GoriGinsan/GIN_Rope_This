using System;
using GINRope;
class MotionTests {
 static int assertions;
 static void Check(bool condition,string description){assertions++;if(!condition)throw new Exception(description);}
 static void Run(string name,Action test){test();Console.WriteLine("PASS "+name);}
 static Solver Make(){V3[] points=new V3[17];for(int i=0;i<points.Length;i++)points[i]=new V3(i*.125,0,0);return new Solver(points);}
 static double Difference(V3[] a,V3[] b){double maximum=0;for(int i=0;i<a.Length;i++)maximum=Math.Max(maximum,(a[i]-b[i]).Length);return maximum;}
 static V3 AnchorA(double t){return new V3(t*.07,t*.04,-t*.03);}
 static V3 AnchorB(double t){return new V3(1+t*.02,t*.09,t*.05);}
 static V3 PieceA(double t){double v=t<=.5?t:1-t;return new V3(.2*v,.3*v,-.1*v);}
 static V3 PieceB(double t){double v=t<=.5?t:1-t;return new V3(1-.2*v,-.1*v,.15*v);}
 static RopeMotion Simulate(double rate,bool irregular,int cameras,bool piecewise){
  Settings settings=new Settings();RopeMotion motion=new RopeMotion(Make());motion.Reset(AnchorA(0),AnchorB(0),settings);
  V3[] output=new V3[17];double time=0,end=piecewise?1:10;int frame=0;
  while(time<end-1e-12){
   double elapsed=1/rate;if(irregular)elapsed*=frame%4==0?.3:frame%4==1?1.7:frame%4==2?.6:1.4;
   // Every input schedule samples the same piecewise-linear corner exactly.
   if(piecewise&&time<.5&&time+elapsed>.5)elapsed=.5-time;
   elapsed=Math.Min(elapsed,end-time);time+=elapsed;frame++;
   V3 a=piecewise?PieceA(time):AnchorA(time),b=piecewise?PieceB(time):AnchorB(time);
   motion.Advance(elapsed,a,b,settings);
   for(int pass=0;pass<cameras;pass++)motion.Render(a,b,settings,output);
  }
  return motion;
 }
 static void Main(){
  Run("Fixed ticks retain all output time at low and fractional capture rates",delegate{
   foreach(double rate in new double[]{1,2,10,15,24,30,50,60000.0/1001,60,90,120,144,240}){
    RopeMotion motion=Simulate(rate,false,1,false);
    Check(motion.TotalSteps==1200,"Lost physics time at output rate "+rate+": "+motion.TotalSteps);
    Check(motion.Remainder<1e-9,"Residual drifted at output rate "+rate);
    Check(motion.LastStepCount<=240,"Substep count exceeded bound");
    foreach(V3 point in motion.Solver.P)Check(point.Finite,"Capture simulation diverged");
   }
  });
  Run("Linear anchor trajectories agree across frame rates and irregular schedules",delegate{
   RopeMotion reference=Simulate(120,false,0,false);
   foreach(double rate in new double[]{1,10,24,30,50,60,90,120,144,240})foreach(bool irregular in new bool[]{false,true}){
    RopeMotion motion=Simulate(rate,irregular,5,false);
    Check(motion.TotalSteps==reference.TotalSteps,"Mixed frame intervals lost ticks");
    Check(Difference(motion.Solver.P,reference.Solver.P)<1e-9,"Linear anchor sampling depends on frame rate: "+rate+" irregular="+irregular);
   }
  });
  Run("Piecewise-linear inputs preserve crossed substep residuals",delegate{
   RopeMotion reference=Simulate(120,false,0,true);
   foreach(double rate in new double[]{24,30,50,60,90,120,144,240})foreach(bool irregular in new bool[]{false,true}){
    RopeMotion motion=Simulate(rate,irregular,3,true);
    Check(motion.TotalSteps==120,"Piecewise trajectory lost fixed ticks");
    Check(Difference(motion.Solver.P,reference.Solver.P)<1e-9,"Residual substep interpolated the wrong anchor interval");
   }
  });
  Run("Camera count cannot change non-equilibrium physics or its next-step velocity",delegate{
   Settings settings=new Settings();RopeMotion a=new RopeMotion(Make()),b=new RopeMotion(Make());
   V3 anchorA=new V3(),anchorB=new V3(1,0,0);a.Reset(anchorA,anchorB,settings);b.Reset(anchorA,anchorB,settings);V3[] output=new V3[17];
   for(int frame=1;frame<=240;frame++){
    double time=frame/60.0;anchorA=AnchorA(time);anchorB=AnchorB(time);
    a.Advance(1.0/60,anchorA,anchorB,settings);b.Advance(1.0/60,anchorA,anchorB,settings);
    V3[] before=(V3[])b.Solver.P.Clone();double residual=b.Remainder;long steps=b.TotalSteps;
    for(int pass=0;pass<20;pass++)b.Render(anchorA,anchorB,settings,output);
    Check(Difference(before,b.Solver.P)==0,"Render mutated authoritative solver positions");
    Check(Difference(a.Solver.P,b.Solver.P)==0,"Camera pass changed position or Verlet history");
    Check(b.Remainder==residual&&b.TotalSteps==steps,"Render consumed simulation time");
   }
   a.Advance(1.0/120,anchorA,anchorB,settings);b.Advance(1.0/120,anchorA,anchorB,settings);
   Check(Difference(a.Solver.P,b.Solver.P)==0,"Render changed next-step velocity");
  });
  Run("Repeated renders are deterministic and keep current visual handles pinned",delegate{
   Settings settings=new Settings();RopeMotion motion=new RopeMotion(Make());V3 a=new V3(),b=new V3(1,0,0);motion.Reset(a,b,settings);motion.Advance(17.0/120,a,b,settings);
   V3[] first=new V3[17],next=new V3[17];V3 visualA=new V3(.03,.04,.01),visualB=new V3(1.03,-.02,.03);
   motion.Render(visualA,visualB,settings,first);V3[] before=(V3[])motion.Solver.P.Clone();
   for(int pass=0;pass<100;pass++){
    motion.Render(visualA,visualB,settings,next);Check(Difference(first,next)==0,"Repeated render changed displayed shape");
    Check((next[0]-visualA).Length==0&&(next[16]-visualB).Length==0,"Visible handles were not pinned");
    Check(Difference(before,motion.Solver.P)==0,"Display-only anchor correction changed physics");
   }
  });
  Run("Fractional-frame rendering interpolates without advancing physics",delegate{
   Settings settings=new Settings();RopeMotion motion=new RopeMotion(Make());V3 a=new V3(),b=new V3(1,0,0);motion.Reset(a,b,settings);motion.Advance(17.0/120,a,b,settings);
   V3[] first=new V3[17],middle=new V3[17],last=new V3[17];motion.Render(a,b,settings,first);V3[] before=(V3[])motion.Solver.P.Clone();
   motion.Advance(RopeMotion.StepSeconds*.25,a,b,settings);motion.Render(a,b,settings,middle);
   motion.Advance(RopeMotion.StepSeconds*.25,a,b,settings);motion.Render(a,b,settings,last);
   Check(motion.TotalSteps==17&&motion.LastStepCount==0,"Display interpolation advanced a physics tick");
   Check(Difference(before,motion.Solver.P)==0,"Fractional time mutated fixed-step state");
   Check(Difference(first,middle)>1e-8&&Difference(middle,last)>1e-8,"Visible pose stayed frozen between fixed ticks");
  });
  Run("Paused time and anchor changes cannot accumulate a resume burst",delegate{
   Settings settings=new Settings();RopeMotion motion=new RopeMotion(Make());V3 a=new V3(),b=new V3(1,0,0);motion.Reset(a,b,settings);motion.Advance(.013,a,b,settings);
   V3[] before=(V3[])motion.Solver.P.Clone();long steps=motion.TotalSteps;settings.Paused=true;
   V3[] output=new V3[17];
   for(int frame=0;frame<100;frame++){
    a=new V3(frame*.01,0,0);b=a+new V3(1,0,0);motion.Advance(2,a,b,settings);motion.Render(a,b,settings,output);
    Check(Difference(before,output)==0,"Paused display followed handles instead of freezing the rope");
   }
   Check(Difference(before,motion.Solver.P)==0,"Pause changed solver positions");Check(motion.TotalSteps==steps&&motion.Remainder==0,"Pause accumulated physics time");
   settings.Paused=false;motion.Advance(0,a,b,settings);Check(Difference(before,motion.Solver.P)==0&&motion.LastStepCount==0,"Resume caught up paused time");
   motion.Advance(1.0/120,a,b,settings);Check(motion.LastStepCount==1,"Resume did not return to fixed-step simulation");
   motion.ClearClock(a,b);Check(motion.Remainder==0&&motion.LastStepCount==0,"Clock transition retained old fractional time");
  });
  Run("Invalid elapsed time and display aliases fail without changing simulation",delegate{
   Settings settings=new Settings();RopeMotion motion=new RopeMotion(Make());V3 a=new V3(),b=new V3(1,0,0);motion.Reset(a,b,settings);V3[] before=(V3[])motion.Solver.P.Clone();
   foreach(double elapsed in new double[]{-1,Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity,2.0001}){bool rejected=false;try{motion.Advance(elapsed,a,b,settings);}catch(ArgumentException){rejected=true;}Check(rejected,"Unsafe elapsed time accepted");Check(Difference(before,motion.Solver.P)==0&&motion.TotalSteps==0,"Rejected time changed physics");}
   foreach(V3[] buffer in new V3[][]{null,new V3[3],motion.Solver.P}){bool rejected=false;try{motion.Render(a,b,settings,buffer);}catch(ArgumentException){rejected=true;}Check(rejected,"Unsafe display buffer accepted");Check(Difference(before,motion.Solver.P)==0,"Rejected display buffer changed physics");}
   bool badAnchor=false;try{motion.Advance(0,new V3(Double.NaN,0,0),b,settings);}catch(ArgumentException){badAnchor=true;}Check(badAnchor,"Invalid anchor accepted");
   motion.Advance(2,a,b,settings);Check(motion.LastStepCount==240,"Maximum safe interval was truncated");
  });
  Console.WriteLine("All "+assertions+" rope motion assertions passed.");
 }
}
