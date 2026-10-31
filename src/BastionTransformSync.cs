using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
namespace IvoryBastion
{
    internal enum BastionTransformResult:byte
    {
        None,Applied,Cooldown,TooHigh,Blocked,Unavailable,Interrupted
    }

    // A receipt belongs to one intent, not merely to a form or an elapsed timer.
    // Kept in the periodic status so a missed early snapshot cannot strand input.
    internal sealed class BastionTransformSync
    {
        internal const float Timeout=8f;
        private uint sequence;
        internal uint ReplyId {get;private set;}
        internal BastionTransformResult Result {get;private set;}
        internal static float Duration(bool siege)=>B.Get(siege?S.DeployDuration:S.PackDuration);
        private static bool Newer(uint a,uint b)=>unchecked((int)(a-b))>0;

        internal uint NextRequest()
        {
            if(Newer(ReplyId,sequence))sequence=ReplyId;
            sequence=unchecked(sequence+1);if(sequence==0)sequence=1;
            return sequence;
        }

        internal void ReceiveReply(uint id,BastionTransformResult result)
        {
            if(id==0||result<=BastionTransformResult.None||result>BastionTransformResult.Interrupted)return;
            if(ReplyId!=0&&(id==ReplyId||!Newer(id,ReplyId)))return;
            ReplyId=id;Result=result;
        }

        internal bool HasReply(uint id)=>id!=0&&ReplyId==id&&Result!=BastionTransformResult.None;
        internal bool CanRelease(uint id,bool requestedSiege,bool actualSiege)=>
            HasReply(id)&&(Result!=BastionTransformResult.Applied||requestedSiege==actualSiege);

        internal static string Notice(BastionTransformResult result)
        {
            switch(result)
            {
                case BastionTransformResult.Cooldown:return "TRANSFORM RECHARGING";
                case BastionTransformResult.TooHigh:return $"TOO HIGH: DEPLOY WITHIN {B.Get(S.DeployHeight):0.#}m OF SOLID GROUND";
                case BastionTransformResult.Blocked:return $"NO CLEAR SPACE WITHIN {B.Get(S.DeployHeight):0.#}m OF GROUND";
                case BastionTransformResult.Interrupted:return "TRANSFORMATION INTERRUPTED";
                case BastionTransformResult.Unavailable:return "TRANSFORMATION UNAVAILABLE";
                default:return "";
            }
        }
    }
}
