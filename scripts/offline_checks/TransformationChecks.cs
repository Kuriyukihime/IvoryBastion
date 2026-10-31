using System;
using IvoryBastion;

internal static class TransformationChecks
{
    private static int count;
    private static void Assert(bool value,string message)
    {
        count++;
        if(!value)throw new Exception("Transformation: "+message);
    }

    internal static void Run()
    {
        foreach(bool siege in new[]{false,true})
        {
            DelayedHost(siege);
            ReceiptsAreScopedToTheirIntent(siege);
            Rejections(siege);
        }
        SequenceRollover();
        InvalidReplies();
        Console.WriteLine($"PASS: {count} transformation handshake assertions (delayed host, both forms, rejection, stale/duplicate receipts and sequence rollover).");
    }

    private static void DelayedHost(bool siege)
    {
        var sync=new BastionTransformSync();
        uint id=sync.NextRequest();
        float duration=BastionTransformSync.Duration(siege);
        Assert(Math.Abs(duration-(siege?.8f:.25f))<.001f,"each requested form has its own animation duration");

        // The owner finishes its local animation before a delayed host even sees the
        // request. Poll the production release gate at the same milestones as the
        // state; the animation clock alone must never authorize a form transition.
        foreach(float localAge in new[]{0f,duration-.01f,duration,duration+.2f,2f})
        {
            Assert(!sync.CanRelease(id,siege,!siege),"no reply cannot release owner at local age "+localAge);
            Assert(!sync.CanRelease(id,siege,siege),"even matching form cannot replace host receipt at local age "+localAge);
        }

        // A host's state update can precede its receipt, or the two can arrive in
        // separate callbacks within a bunched network delivery.
        Assert(!sync.HasReply(id),"late host has not yet answered the intent");
        sync.ReceiveReply(id,BastionTransformResult.Applied);
        Assert(sync.HasReply(id),"matching applied receipt is recognized");
        Assert(!sync.CanRelease(id,siege,!siege),"receipt before authoritative form does not release owner");
        Assert(sync.CanRelease(id,siege,siege),"receipt plus authoritative form releases owner");

        var stateFirst=new BastionTransformSync();
        uint other=stateFirst.NextRequest();
        Assert(!stateFirst.CanRelease(other,siege,siege),"state-first ordering still awaits matching receipt");
        stateFirst.ReceiveReply(other,BastionTransformResult.Applied);
        Assert(stateFirst.CanRelease(other,siege,siege),"state-first ordering releases once receipt arrives");
    }

    private static void ReceiptsAreScopedToTheirIntent(bool siege)
    {
        var sync=new BastionTransformSync();
        uint previous=sync.NextRequest();
        sync.ReceiveReply(previous,BastionTransformResult.Applied);
        uint current=sync.NextRequest();
        Assert(current!=previous&&current!=0,"successive intents have distinct nonzero IDs");
        Assert(!sync.CanRelease(current,siege,siege),"previous success cannot complete next request");
        sync.ReceiveReply(previous,BastionTransformResult.Cooldown);
        Assert(sync.Result==BastionTransformResult.Applied,"duplicate ID cannot replace original outcome");
        Assert(!sync.HasReply(current),"duplicate reply does not become next request's receipt");

        sync.ReceiveReply(current,BastionTransformResult.Applied);
        sync.ReceiveReply(previous,BastionTransformResult.Interrupted);
        Assert(sync.ReplyId==current&&sync.Result==BastionTransformResult.Applied,"reordered old failure cannot roll back latest success");
        sync.ReceiveReply(current,BastionTransformResult.Blocked);
        Assert(sync.Result==BastionTransformResult.Applied,"duplicate conflicting outcome is ignored");
        Assert(sync.CanRelease(current,siege,siege),"latest success remains usable after old and duplicate packets");
        Assert(!sync.CanRelease(previous,siege,siege),"obsolete request cannot consume newer receipt");

        uint next=sync.NextRequest();
        Assert(!sync.CanRelease(next,!siege,!siege),"a new opposite-form request awaits its own result");
        sync.ReceiveReply(current,BastionTransformResult.Applied);
        Assert(!sync.CanRelease(next,!siege,!siege),"repeated previous snapshot cannot release opposite-form request");
    }

    private static void Rejections(bool siege)
    {
        foreach(var result in new[]{
            BastionTransformResult.Cooldown,BastionTransformResult.TooHigh,
            BastionTransformResult.Blocked,BastionTransformResult.Unavailable,
            BastionTransformResult.Interrupted})
        {
            var sync=new BastionTransformSync();
            uint id=sync.NextRequest();
            sync.ReceiveReply(id,result);
            Assert(sync.HasReply(id),"rejection acknowledges its request: "+result);
            Assert(sync.CanRelease(id,siege,!siege),"rejection releases unchanged form: "+result);
            Assert(sync.CanRelease(id,siege,siege),"rejection also releases if form already changed: "+result);
            Assert(!string.IsNullOrWhiteSpace(BastionTransformSync.Notice(result)),"rejection explains failure: "+result);
            Assert(!sync.CanRelease(sync.NextRequest(),siege,!siege),"rejection cannot cancel next intent: "+result);
        }
    }

    private static void SequenceRollover()
    {
        var sync=new BastionTransformSync();
        // Advance through valid serial-number distances without billions of calls.
        sync.ReceiveReply(int.MaxValue,BastionTransformResult.Applied);
        Assert(sync.NextRequest()==2147483648u,"new owner sequence catches up to host receipt");
        sync.ReceiveReply(uint.MaxValue-1,BastionTransformResult.Applied);
        uint last=sync.NextRequest();
        Assert(last==uint.MaxValue,"last nonzero uint is a valid request");
        sync.ReceiveReply(last,BastionTransformResult.Applied);
        uint wrapped=sync.NextRequest();
        Assert(wrapped==1,"sequence rollover skips reserved zero");
        Assert(!sync.HasReply(wrapped),"pre-wrap receipt cannot complete wrapped request");
        sync.ReceiveReply(wrapped,BastionTransformResult.Applied);
        Assert(sync.CanRelease(wrapped,true,true),"wrapped reply is newer than maximum ID");
        sync.ReceiveReply(last,BastionTransformResult.Interrupted);
        Assert(sync.ReplyId==wrapped&&sync.Result==BastionTransformResult.Applied,"pre-wrap stale reply cannot roll back post-wrap state");
        Assert(sync.NextRequest()==2,"request sequence continues after rollover");
    }

    private static void InvalidReplies()
    {
        var sync=new BastionTransformSync();
        uint id=sync.NextRequest();
        sync.ReceiveReply(0,BastionTransformResult.Applied);
        sync.ReceiveReply(id,BastionTransformResult.None);
        sync.ReceiveReply(id,(BastionTransformResult)255);
        Assert(sync.ReplyId==0&&!sync.HasReply(id),"invalid IDs and results cannot acknowledge a request");
        Assert(!sync.CanRelease(0,true,true),"zero request is never releasable");
        sync.ReceiveReply(id,BastionTransformResult.Applied);
        sync.ReceiveReply(id+1,(BastionTransformResult)255);
        Assert(sync.ReplyId==id&&sync.Result==BastionTransformResult.Applied,"invalid newer packet cannot overwrite valid receipt");
    }
}
