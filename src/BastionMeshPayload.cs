using System;
using System.IO;
namespace IvoryBastion
{
    internal static class BastionMeshPayload
    {
        internal static long Bytes(int vertices,int indices,bool textured,bool fourWeights)
        {
            if(vertices<0||indices<0||fourWeights&&!textured)throw new InvalidDataException("Invalid mesh payload layout.");
            return (long)vertices*(fourWeights?64:textured?44:36)+(long)indices*4;
        }
    }
}
