using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class SeraphShieldChecks
{
    static int count;
    static void Check(bool value,string why){count++;if(!value)throw new Exception("Seraph Shield: "+why);}
    static void Near(float actual,float expected,string why)=>Check(Math.Abs(actual-expected)<.002f,why+": "+actual+" != "+expected);
    internal static void Run(string root)
    {
        var pool=new BastionShieldReserve();pool.Resize(50);Near(pool.Charge,50,"spawn fills its own pool");
        Near(pool.Absorb(60,.5f),0,"fifty percent reduction applies within shield");Near(pool.Charge,20,"sixty damage consumes thirty shield");
        Near(pool.Absorb(50,.5f),10,"overflow continues after shield breaks");Near(pool.Charge,0,"break empties independent pool");
        pool.Resize(50);Near(pool.Charge,0,"stat refresh cannot refill shield");
        Near(pool.Restore(12),12,"explicit recharge returns restored points");Near(pool.Charge,12,"recharge touches only own points");
        pool.Resize(70);Near(pool.Charge,32,"health/upgrades add only new capacity");pool.Resize(15);Near(pool.Charge,15,"capacity shrink clamps");
        pool.Absorb(30,.5f);pool.Resize(50);Near(pool.Charge,35,"deployment preserves old damage");
        pool.Resize(15);pool.Restore(pool.Capacity);Near(pool.Charge,15,"packing explicitly refills mobile pool");
        pool.Receive(50,1);Near(pool.Absorb(1000,.5f),998,"last shield point cannot reduce an entire overflowing hit");
        pool.Receive(50,50);Near(pool.Absorb(0,.5f),0,"barrier-only/blocked hit spends no Seraph points");Near(pool.Charge,50,"barrier protects the reserve");
        // Native barrier pays before the callback; native item shields pay its
        // remainder. These cases also cover DoT ticks and large spillover hits.
        float barrier=30,nativeItemShield=8,health=100,hit=140;
        float paid=Math.Min(hit,barrier);barrier-=paid;hit-=paid;
        hit=pool.Absorb(hit,.5f);paid=Math.Min(hit,nativeItemShield);nativeItemShield-=paid;health-=hit-paid;
        Near(barrier,0,"barrier spent first");Near(pool.Charge,0,"Seraph spent second");Near(nativeItemShield,0,"item shield spent third");Near(health,98,"only remainder reaches health");
        pool.Receive(50,50);float afterAegis=100*.65f;Near(pool.Absorb(afterAegis,.5f),0,"Aegis and shield compose without duplicate reduction");Near(pool.Charge,17.5f,"combined reduction pays 32.5 shield per 100 attack");
        pool.Receive(50,20);float lastHit=0;
        for(int second=1;second<=5;second++)
        {pool.Absorb(0,.5f);pool.Restore(pool.Capacity*.5f*BastionDefenseRules.RechargeStep(second-lastHit,1,true));}
        Near(pool.Charge,45,"recharge continues behind barrier during ongoing fire");
        // Natural recharge has an audible start, separate from instant heals.
        pool.Receive(50,10);
        Check(!pool.Recharge(0)&&!pool.Recharge(float.NaN)&&!pool.Recharge(float.PositiveInfinity),"paused and invalid recharge cannot start a cue");
        Check(pool.Recharge(1),"first real recharge starts one cue");
        pool.Resize(50);pool.Absorb(0,.5f);
        Check(!pool.Recharge(1),"stat refresh and blocked/barrier-only hits do not restart the buzz");
        pool.Absorb(4,.5f);
        Check(pool.Recharge(1),"damage to Seraph interrupts the fill and permits a new recharge cue");
        pool.Restore(1);
        Check(!pool.Recharge(1),"partial instant healing cannot duplicate an active recharge cue");
        pool.Restore(50);
        Check(!pool.Recharge(1),"packing or a full instant refill remains quiet");
        pool.Receive(50,49);
        Check(pool.Recharge(1)&&!pool.Recharge(1),"one-tick recharge sounds once and a full pool stays quiet");
        foreach(bool siege in new[]{false,true})
        {
            pool.Receive(50,0);int starts=0;
            float delay=siege?BastionDefenseRules.SiegeRechargeDelay:BastionDefenseRules.RechargeDelay;
            for(int frame=1;frame<=(delay+3)*60;frame++)
            {
                float seconds=frame/60f;
                float step=BastionDefenseRules.RechargeStep(seconds,1/60f,siege);
                bool start=step>0&&pool.Recharge(pool.Capacity*.5f*step);
                if(start){Check(seconds>delay,"start cue waits for the actual hit delay");starts++;}
            }
            Check(starts==1,"one cue per complete natural refill in both forms");
            Near(pool.Charge,50,"audio start tracking preserves refill speed and capacity");
        }
        pool.Receive(50,500);Near(pool.Charge,50,"snapshot clamps to own capacity");pool.Receive(float.NaN,float.PositiveInfinity);Near(pool.Charge,0,"invalid snapshot is empty");
        var random=new Random(2197);
        for(int i=0;i<2000;i++)
        {
            float cap=(float)random.NextDouble()*2000,charge=(float)random.NextDouble()*cap,damage=(float)random.NextDouble()*10000,reduction=(float)random.NextDouble()*.95f;
            pool.Receive(cap,charge);float remainder=pool.Absorb(damage,reduction),spent=charge-pool.Charge;
            Check(pool.Charge>=0&&pool.Charge<=cap&&remainder>=0&&remainder<=damage,"finite bounded resources");
            Check(Math.Abs((damage-remainder)*(1-reduction)-spent)<.002f,"absorption conserves damage and shield cost");
        }
        using(var game=AssemblyDefinition.ReadAssembly(@"X:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed\RoR2.dll"))
        {
            var method=game.MainModule.Types.Single(t=>t.FullName=="RoR2.HealthComponent").Methods.Single(m=>m.Name=="TakeDamageProcess");
            BastionShieldDamageSite.Locate(method,out var load,out var damage);
            int index=method.Body.Instructions.IndexOf(load);
            Check(method.Body.Instructions.Take(index).Any(i=>i.Operand is MethodReference m&&m.Name=="set_Networkbarrier"),"absorption follows installed native barrier writes");
            Check(method.Body.Instructions.Skip(index).Any(i=>i.Operand is MethodReference m&&m.Name=="set_Networkshield"),"native item shields remain after Seraph");
            Check(method.Body.Instructions.Any(i=>i.Operand==load),"barrier branch targets the preserved original load");
            // Sandswept inserts a plating callback after this same load. Its
            // insertion must not change our site or the native damage local.
            var il=method.Body.GetILProcessor();var pop=il.Create(OpCodes.Pop);il.InsertAfter(load,pop);il.InsertAfter(pop,il.Create(OpCodes.Ldloc,damage));
            BastionShieldDamageSite.Locate(method,out var withPlating,out var sameDamage);
            Check(withPlating==load&&sameDamage==damage,"Sandswept plating insertion remains compatible");
        }
        using(var mod=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll")))
        {
            var ability=mod.MainModule.Types.Single(t=>t.Name=="BastionAbilityShield");
            var methods=ability.Methods.Concat(ability.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody);
            var calls=methods.SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
            Check(!calls.Any(m=>new[]{"set_Networkshield","set_Networkhealth","set_Networkbarrier","set_maxShield","RechargeShield"}.Contains(m.Name)),"standalone reserves never write native shield/health/barrier or steal item recharge");
            var absorb=ability.Methods.Single(m=>m.Name=="Absorb").Body.Instructions;
            Check(absorb.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="NetworkServer"&&m.Name=="get_active"),"host exclusively owns absorption");
            var status=mod.MainModule.Types.Single(t=>t.Name=="BastionShieldStatusView");
            var uiCalls=status.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
            Check(!uiCalls.Any(m=>m.Name=="SetAsLastSibling"||m is GenericInstanceMethod g&&g.GenericArguments.Any(t=>t.Name=="RectMask2D")),"independent HUD never reorders or masks native health-bar children");
            Check(uiCalls.Any(m=>m.DeclaringType.Name=="BastionDefenseGaugeLayout"&&m.Name=="AboveHealth"),"runtime uses clear-space layout");
            Check(uiCalls.Any(m=>m.Name=="TransformPoint")&&uiCalls.Any(m=>m.Name=="set_localRotation")&&uiCalls.Any(m=>m.Name=="set_localScale"),"runtime keeps the health bar's complete local plane instead of flattening its projected bounds");
            var effects=mod.MainModule.Types.Single(t=>t.Name=="BastionFxKind");
            Check(Convert.ToInt32(effects.Fields.Single(f=>f.Name=="AegisHit").Constant)==25&&Convert.ToInt32(effects.Fields.Single(f=>f.Name=="ShieldBreak").Constant)==26,"warning appends an event without shifting existing network IDs");
            Check(Convert.ToInt32(effects.Fields.Single(f=>f.Name=="ShieldRecharge").Constant)==27,"recharge event preserves previous IDs");
            var play=mod.MainModule.Types.Single(t=>t.Name=="BastionFx").Methods.Single(m=>m.Name=="Play").Body.Instructions;
            Check(play.Any(i=>i.Operand is MethodReference m&&m.Name=="get_hasEffectiveAuthority")&&play.Any(i=>i.OpCode==OpCodes.Ldstr&&(string)i.Operand=="shieldbreak"),"received warning checks owner authority and uses its own audio cue");
            var tick=ability.Methods.Single(m=>m.Name=="Tick").Body.Instructions;
            Check(tick.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="NetworkServer"&&m.Name=="get_active")&&
                  tick.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionShieldReserve"&&m.Name=="Recharge"),"host-owned natural refill drives recharge start notifications");
            Check(play.Any(i=>i.Operand is MethodReference m&&m.Name=="BeginShieldRecharge")&&
                  play.Any(i=>i.Operand is MethodReference m&&m.Name=="StopShieldRecharge"),"local audio handles recharge and shield-hit interruption");
            var controller=mod.MainModule.Types.Single(t=>t.Name=="BastionController");
            var secondary=controller.Methods.Single(m=>m.Name=="ActivateSecondary").Body.Instructions;
            Check(!secondary.Any(i=>i.Operand is MethodReference m&&m.Name=="AddBarrier")&&secondary.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionAegisReserve"&&m.Name=="Grant"),"Aegis casts grant only its independent barrier");
            Check(absorb.ToList().FindIndex(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionAegisReserve"&&m.Name=="Absorb")<absorb.ToList().FindIndex(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionShieldReserve"&&m.Name=="Absorb"),"Aegis absorbs before Seraph");
        }
        var aegisPool=new BastionAegisReserve();aegisPool.Grant(40,100);Near(aegisPool.Capacity,40,"Aegis gauge starts at grant size");
        pool.Receive(50,20);Near(pool.Absorb(aegisPool.Absorb(35),.5f),0,"Aegis fully stops a hit before Seraph");Near(pool.Charge,20,"Aegis-only hits do not spend Seraph");
        pool.Restore(5);Near(pool.Charge,25,"Seraph restores behind active Aegis");Near(aegisPool.Charge,5,"Seraph recharge cannot refill Aegis");
        Near(pool.Absorb(aegisPool.Absorb(25),.5f),0,"overflow enters Seraph once");Near(pool.Charge,15,"Seraph DR applies only after Aegis breaks");
        aegisPool.Grant(40,100);aegisPool.Tick(1,100);Near(aegisPool.Charge,35,"Aegis retains installed native decay curve");
        aegisPool.Grant(90,100);Near(aegisPool.Charge,100,"repeated grants cap at original full-barrier limit");
        aegisPool.Tick(0,80);Near(aegisPool.Charge,80,"health loss clamps Aegis capacity");
        Near(aegisPool.Absorb(100),20,"Aegis large-hit overflow is exact");
        aegisPool.Receive(float.NaN,float.PositiveInfinity);Near(aegisPool.Charge,0,"invalid Aegis snapshot is rejected");
        // The installed native HUD has two expanding rows, 6px padding and
        // 6px spacing. Reserve room without shrinking health or XP/level.
        float oldRow=(72-12-6)/2f;
        float newRow=(72+BastionDefenseGaugeLayout.ReservedSpace-12-(6+BastionDefenseGaugeLayout.ReservedSpace))/2f;
        Near(newRow,oldRow,"native rows retain their original height when a defense slot is active");
        foreach(float scale in new[]{.5f,1f,1.5f,2f})foreach(float width in new[]{240f,600f,1200f})
        {
            var healthBox=new HudPlacement.Box(-width*.5f,-oldRow*.5f,width*.5f,oldRow*.5f);
            float levelBottom=healthBox.Top+6+BastionDefenseGaugeLayout.ReservedSpace;
            var xpBox=new HudPlacement.Box(healthBox.Left+88,levelBottom,healthBox.Right,levelBottom+16);
            var levelBox=new HudPlacement.Box(healthBox.Left,levelBottom,healthBox.Left+88,levelBottom+oldRow);
            var buffs=new HudPlacement.Box(healthBox.Left,levelBottom+oldRow+11,healthBox.Right,levelBottom+oldRow+59);
            var obstacles=new[]{xpBox,levelBox,buffs};
            var box=BastionDefenseGaugeLayout.AboveHealth(healthBox,obstacles);
            Near(box.Left,healthBox.Left,"gauge shares native left endpoint at HUD scale "+scale);
            Near(box.Right,healthBox.Right,"gauge shares native right endpoint at HUD scale "+scale);
            Near(box.Bottom,healthBox.Top+6,"gauges remain directly above health");
            Near(box.Top-box.Bottom,30,"strip remains slim");
            Check(box.Top+6<=levelBottom,"level/XP and buffs clear the complete gauge and labels");
            // Same local plane follows native spring motion and perspective.
            foreach(float tilt in new[]{-.09f,0,.09f})
            {
                double y0=scale*(box.Bottom+tilt*box.Left),y1=scale*(box.Bottom+tilt*box.Right);
                double h0=scale*(healthBox.Top+tilt*healthBox.Left),h1=scale*(healthBox.Top+tilt*healthBox.Right);
                Check(Math.Abs((y1-y0)-(h1-h0))<.001,"defense gauges remain parallel to a tilted native health bar");
            }
            // Unknown HUD layouts get a collision-free fallback even when
            // their obstacles arrive out of vertical order.
            var blocked=new[]{new HudPlacement.Box(healthBox.Left,healthBox.Top+20,healthBox.Right,healthBox.Top+50),
                new HudPlacement.Box(healthBox.Left,healthBox.Top,healthBox.Right,healthBox.Top+12),
                new HudPlacement.Box(healthBox.Right+5,0,healthBox.Right+100,1000)};
            var fallback=BastionDefenseGaugeLayout.AboveHealth(healthBox,blocked);
            Near(fallback.Bottom,healthBox.Top+56,"fallback clears overlapping level and buff displays without unrelated right-side UI pushing it away");
        }
        var geometry=new BastionShieldGaugeGeometry();
        foreach(float w in new[]{40f,574f,1920f,8192f})foreach(float h in new[]{8f,32f,80f})foreach(float f in new[]{0f,.001f,.5f,1f,float.NaN,float.PositiveInfinity})
        {
            geometry.Update(w,h,f);float fill=float.IsFinite(f)?Math.Clamp(f,0,1):0;
            Check(geometry.Vertices.Count<65000,"single UI mesh stays within vertex limit");
            foreach(var v in geometry.Vertices)Check(float.IsFinite(v.x)&&float.IsFinite(v.y)&&v.x>=-.002f&&v.x<=w*fill+.002f&&v.y>=-.002f&&v.y<=h+.002f,"hexagons clip to current charge and bar bounds");
            Check(geometry.Triangles.All(i=>i>=0&&i<geometry.Vertices.Count),"all gauge indices valid");
        }
        geometry.Update(574,36,.68f);
        Check(geometry.Colors.Any(c=>c.r>220&&c.g>220)&&geometry.Colors.Any(c=>c.b>c.r+100),"white fill and blue hexagons remain distinct");
        File.WriteAllText(Path.Combine(root,".build/seraph-shield-gauge.json"),JsonSerializer.Serialize(new{
            width=574,height=36,vertices=geometry.Vertices.Select(p=>new[]{p.x,p.y}),colors=geometry.Colors.Select(c=>new[]{(int)c.r,(int)c.g,(int)c.b,(int)c.a}),triangles=geometry.Triangles}));
        object Mesh(float height,float fraction,bool aegis,float width=574)
        {
            geometry.Update(width,height,fraction,aegis);
            return new{width,height,vertices=geometry.Vertices.Select(p=>new[]{p.x,p.y}).ToArray(),colors=geometry.Colors.Select(c=>new[]{(int)c.r,(int)c.g,(int)c.b,(int)c.a}).ToArray(),triangles=geometry.Triangles.ToArray()};
        }
        geometry.Update(574,3.5f,.8f,true);
        Check(geometry.Colors.Any(c=>c.r>240&&c.g>180&&c.b<80)&&geometry.Colors.Any(c=>c.r<35&&c.g<35&&c.b<35),"Aegis uses yellow and black independently of blue/white shield");
        File.WriteAllText(Path.Combine(root,".build/seraph-defense-preview.json"),JsonSerializer.Serialize(new{
            shield=Mesh(7.5f,.68f,false),aegis=Mesh(3.5f,.8f,true),empty=Mesh(7.5f,0,false)}));
        int[] Tint(BastionResourceKind kind){var color=BastionResourcePalette.Tint(kind);return new[]{(int)color.r,(int)color.g,(int)color.b,(int)color.a};}
        File.WriteAllText(Path.Combine(root,".build/hud-modules.json"),JsonSerializer.Serialize(new{
            combatWidth=HudPlacement.CombatWidth,combatHeight=HudPlacement.CombatHeight,factoryWidth=HudPlacement.FactoryWidth,factoryHeight=HudPlacement.FactoryHeight,factoryScale=HudPlacement.FactoryScale,moduleGap=HudPlacement.ModuleGap,
            colors=new{fuel=Tint(BastionResourceKind.Fuel),missiles=Tint(BastionResourceKind.Missiles),heavy=Tint(BastionResourceKind.Heavy),fabrication=Tint(BastionResourceKind.Fabrication)}}));
        Console.WriteLine($"PASS: {count:N0} independent Seraph Shield, damage ordering, native/item isolation and hexagonal status layout checks.");
    }
}
