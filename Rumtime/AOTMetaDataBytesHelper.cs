using System.Collections.Generic;

namespace PluginSet.HybridCLR
{
    public static class AOTMetaDataBytesHelper
    {
        public static byte[] UnionBytes(params byte[][] bytesArray)
        {
            var length = 0;
            foreach (var bytes in bytesArray)
            {
                length += bytes.Length;
            }
            
            var result = new byte[length + 4 * bytesArray.Length];
            var offset = 0;
            foreach (var bytes in bytesArray)
            {
                var len = bytes.Length;
                result[offset++] = (byte)(len & 0xff);
                result[offset++] = (byte)((len >> 8) & 0xff);
                result[offset++] = (byte)((len >> 16) & 0xff);
                result[offset++] = (byte)((len >> 24) & 0xff);
                
                System.Buffer.BlockCopy(bytes, 0, result, offset, len);
                offset += len;
            }
            
            return result;
        }
        
        public static List<byte[]> SplitBytes(byte[] bytes)
        {
            var offset = 0;
            var result = new List<byte[]>();
            while (offset < bytes.Length)
            {
                var len = bytes[offset++] | (bytes[offset++] << 8) | (bytes[offset++] << 16) | (bytes[offset++] << 24);
                var subBytes = new byte[len];
                System.Buffer.BlockCopy(bytes, offset, subBytes, 0, len);
                offset += len;
                result.Add(subBytes);
            }

            return result;
        }
    }
}