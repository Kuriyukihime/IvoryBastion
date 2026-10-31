using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using IvoryBastion;

static class ExplosionChecks
{
    static int count;
    static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
    static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.001f,why);
    internal static void Run(string root)
    {
        Near(BastionBank.MissileDamage(false,false),1f,"Normal primary missile deals 100%");
        Near(BastionBank.MissileDamage(true,false),3f,"Heavy primary missile deals 300%");
        Near(BastionWarheadGeometry.Damage,5,"Skyfall's ten extra-large warheads deal 500% each");
        foreach(bool combat in new[]{false,true})foreach(float dt in new[]{.02f,1f/60,.07f})
        {
            var bank=new BastionBank();float left=60;
            while(left>0){float step=Math.Min(left,dt);bank.Tick(step,true,combat,productionSeconds:step);left-=step;}
            Check(Math.Abs(bank.Fabrication-(combat?42:17))<.003,"Sixty seconds accrue the displayed whole-number production rate");
        }
        var preview=new List<object>();
        for(int tier=0;tier<3;tier++)
        {
            float r=tier==2?BastionWarheadGeometry.Radius:BastionBank.MissileRadius(tier==1);
            Near(BastionExplosionGeometry.At(BastionExplosionLayer.Fireball,0,tier,r,BastionExplosionGeometry.ExpansionTime(tier),0).Size,2*r,"Fireball reaches full gameplay diameter");
            for(int seed=0;seed<3;seed++)for(int layer=0;layer<4;layer++)
            {
                var kind=(BastionExplosionLayer)layer;int n=BastionExplosionGeometry.Count(kind,tier);
                for(int i=0;i<n;i++)for(int frame=0;frame<=180;frame++)
                {
                    var s=BastionExplosionGeometry.At(kind,i,tier,r,frame/60f,seed*13);
                    Check(float.IsFinite(s.Size)&&float.IsFinite(s.Position.sqrMagnitude)&&float.IsFinite(s.Alpha),"Explosion geometry is finite");
                    if(s.Alpha<=0)continue;
                    Check(s.Alpha<=1&&s.Size>0,"Active particle has valid opacity and size");
                    Check(s.Position.magnitude+s.Size*.5f<=r+.001f,"Visible gas, smoke and sparks stay inside the damage sphere");
                }
            }
            foreach(float time in new[]{.035f,.10f,.22f,.45f,.8f,1.2f})
            {
                var samples=new List<object>();
                for(int layer=0;layer<4;layer++)for(int i=0;i<BastionExplosionGeometry.Count((BastionExplosionLayer)layer,tier);i++)
                {
                    var s=BastionExplosionGeometry.At((BastionExplosionLayer)layer,i,tier,r,time,0);
                    if(s.Alpha>0&&s.Remaining>0)samples.Add(new{layer,position=new[]{s.Position.x,s.Position.y,s.Position.z},size=s.Size,alpha=s.Alpha,rotation=s.Rotation,age=1-s.Remaining/s.Life});
                }
                preview.Add(new{tier,radius=r,time,samples});
            }
        }
        Directory.CreateDirectory(Path.Combine(root,".build"));
        File.WriteAllText(Path.Combine(root,".build","detonation-geometry.json"),JsonSerializer.Serialize(preview));
        Console.WriteLine($"PASS: {count} explosion extent, missile balance and production checks.");
    }
}
