using System;
using IvoryBastion;

internal static class ProductionChecks
{
    private static int count;
    private static void Near(float actual,float expected,string message)
    {
        count++;
        if(float.IsNaN(actual)||Math.Abs(actual-expected)>.003f)
            throw new Exception("Production: "+message+" ("+actual+" != "+expected+")");
    }

    internal static void Run()
    {
        foreach(bool combat in new[]{false,true})
        {
            var clock=new BastionProductionClock();var bank=new BastionBank();
            Near(clock.Sample(1000,true,true),0,"joining a running game does not award its history");
            for(int i=1;i<=3000;i++)
                bank.Tick(.02f,true,combat,productionSeconds:clock.Sample(1000+i*.02f,true,true));
            float earned=combat?42:17;
            Near(bank.Fabrication,earned,"one run minute awards the increased whole-number rate");

            bank.Ammo=0;bank.Laser=5;bank.DefenseTime=6;
            for(int i=0;i<3600;i++)
                bank.Tick(1,true,combat,productionSeconds:clock.Sample(1060,false,true));
            Near(bank.Fabrication,earned,"one hour in a paused-timer scene cannot produce currency");
            Near(bank.Ammo,bank.Capacity,"ordinary ammunition reload still works in that scene");
            Near(bank.Laser,0,"ordinary skill cooldown still advances");
            Near(bank.DefenseTime,0,"Aegis expires on gameplay time");

            // A running flag alone cannot generate money if the stopwatch is stationary.
            for(int i=0;i<100;i++)
                bank.Tick(1,true,combat,productionSeconds:clock.Sample(1060,true,true));
            Near(bank.Fabrication,earned,"a stationary stopwatch never accrues income");
            bank.Tick(1,true,combat,productionSeconds:clock.Sample(1061,true,true));
            Near(bank.Fabrication,earned+earned/60,"resume awards only newly elapsed run time");

            clock.Sample(1061,true,false);
            bank.Tick(60,false,combat,productionSeconds:clock.Sample(1121,true,false));
            Near(clock.Sample(1121,true,true),0,"redeploy or revival cannot cash out ineligible time");
            bank.Tick(1,true,combat,productionSeconds:clock.Sample(1122,true,true));
            Near(bank.Fabrication,earned+2*earned/60,"new eligible time resumes the original rate");
        }

        var sample=new BastionProductionClock();
        sample.Sample(10,true,true);
        Near(sample.Sample(500,false,true),0,"a paused timer correction is not income");
        Near(sample.Sample(501,true,true),0,"resume cannot pay out a paused interval");
        Near(sample.Sample(502,true,true),1,"subsequent resumed seconds count");
        Near(sample.Sample(5,true,true),0,"rewinding the stopwatch cannot subtract currency");
        Near(sample.Sample(6,true,true),1,"no old timestamp debt after rewind");
        sample.Reset();
        Near(sample.Sample(5000,true,true),0,"new run or re-enabled controller starts with a baseline");
        Near(sample.Sample(5001,true,true),1,"normal accrual after reset");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f})
        {
            Near(sample.Sample(invalid,true,true),0,"invalid timestamps cannot pay income");
            Near(sample.Sample(5000,true,true),0,"invalid timestamp clears the baseline");
        }

        var kills=new BastionBank();
        kills.Tick(600,true,true);
        Near(kills.Fabrication,0,"physics time without run time cannot mint currency");
        kills.Tick(1,true,true,productionSeconds:float.NaN);
        kills.Tick(1,true,true,productionSeconds:float.PositiveInfinity);
        Near(kills.Fabrication,0,"invalid elapsed production time is ignored");
        kills.AwardKillFabrication(true,false,false,false);
        kills.AwardKillFabrication(true,false,false,true);
        kills.AwardKillFabrication(true,false,true,true);
        Near(kills.Fabrication,0,"paused enemy, elite and boss kills award no currency");
        kills.AwardKillFabrication(false,true,true,true);
        Near(kills.Fabrication,0,"mobile kills do not produce currency");
        kills.AwardKillFabrication(true,true,false,false);
        kills.AwardKillFabrication(true,true,false,true);
        kills.AwardKillFabrication(true,true,true,true);
        Near(kills.Fabrication,3.5f,"running-timer kill rewards retain their original amounts");
        kills.Fabrication=99998;kills.AwardKillFabrication(true,true,true,false);
        Near(kills.Fabrication,99999,"kill rewards respect the currency cap");
        Console.WriteLine($"PASS: {count} run-time production, pause/resume, eligibility and kill-reward checks.");
    }
}
