using Rinsen.WebServer;
using Rinsen.WebServer.Collections;
using System;
using System.IO;

namespace ReflowOvenController.WebGUI
{
    class FileController : Controller
    {
        public void Write()
        {
            FormCollection C = GetFormCollection();
            string Name = "\\SD\\" + C.GetValue("f");
            int DLen;

            // Decode Base64 chunk and write to end of file
            using (FileStream FS = File.OpenWrite(Name))
            {
                FS.Seek(0, SeekOrigin.End);
                byte[] Data = Base64Decode(C.GetValue("d"));
                DLen = Data.Length;
                FS.Write(Data, 0, DLen);
            }
            SetHtmlResult(DLen.ToString());
        }

        public void Delete()
        {
            FormCollection Form = GetFormCollection();
            String Filename = "\\SD\\" + Form.GetValue("f");
            File.Delete(Filename);
            SetHtmlResult("Deleted " + Filename);
        }

        private static readonly String Base64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        public static byte[] Base64Decode(String Input)
        {
            String Working = Input.TrimEnd(new char[] { '=' });
            byte[] Output = new Byte[((Input.Length >> 2) * 3) - (Input.Length - Working.Length)];
            
            while (Working.Length < Input.Length)
                Working += "A";
            Input = Working;
            
            int Pos = 0;
            int Ptr = 0;

            while (Pos < (Input.Length - 2))
            {
                Working = Input.Substring(Pos, 4);

                int Value = Base64.IndexOf(Working[0]) << 18 | Base64.IndexOf(Working[1]) << 12 | Base64.IndexOf(Working[2]) << 6 | Base64.IndexOf(Working[3]);

                Output[Ptr] = (byte)((Value & 0xff0000) >> 16);
                if (Ptr + 1 < Output.Length)
                {
                    Output[Ptr + 1] = (byte)((Value & 0xff00) >> 8);
                    if (Ptr + 2 < Output.Length)
                        Output[Ptr + 2] = (byte)(Value & 0xff);
                }

                Pos += 4;
                Ptr += 3;
            }

            return Output;
        }
    }
}
