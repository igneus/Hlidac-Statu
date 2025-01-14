﻿using System.Diagnostics;
using System.Reflection;

namespace HlidacStatu.Analysis.Page
{
    public static class PdfTools2
    {
        static string workingDirectory = "";
        static PdfTools2()
        {
             workingDirectory =
                     Assembly.GetExecutingAssembly().GetName(false).CodeBase
                     ?? Process.GetCurrentProcess().MainModule!.FileName!
                     ?? AppContext.BaseDirectory;
        }
        public static bool CheckEnviroment(bool throwException, out System.AggregateException exceptions)
        {
            string[] tools = null;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                tools = new[] { "pdfinfo", "pdftotext", "pdftopng" };
            else
                tools = new[] { "pdfinfo", "pdftotext", "pdftoppm" };

            List<Exception> exc = new List<Exception>();
            bool success = true;
            foreach (var cmd in tools)
            {
                try
                {
                    success = success && CheckEnviroment(cmd, throwException);
                }
                catch (Exception e)
                {
                    success = false;
                    if (throwException)
                        exc.Add(e);
                }
            }
            exceptions = null;
            if (throwException && exc.Count>0)
                exceptions = new AggregateException(exc.ToArray());
            return success;
        }

        private static bool CheckEnviroment(string tool, bool throwException = true)
        {
            var pi = new System.Diagnostics.ProcessStartInfo(tool, $"-v");
            pi.UseShellExecute = true;
            Devmasters.ProcessExecutor startProc = new Devmasters.ProcessExecutor(pi, 60 * 60 * 6);//6 hours

            startProc.StandardOutputDataReceived += (o, e) =>
            {
            };
            startProc.ErrorDataReceived += (o, e) =>
            {
                //currentSession.ScriptOutput += e.Data;
                if (e.Data?.Contains("Fontconfig error") == false)
                    Console.WriteLine(e.Data);
            };
            try
            {
                startProc.Start();

            }
            catch (Exception e)
            {
                var msg = "";
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    msg = $"Cannot find {tool} in {workingDirectory}";
                else
                    msg = $"Cannot find {tool}. Install it with 'apt-get update;apt-get install poppler-utils' 'aptitude update;aptitude install poppler-utils' 'yum -y install poppler-utils' or similar";

                if (throwException)
                {
                    throw new System.IO.FileNotFoundException(msg);
                }
                else
                {
                    Console.Error.WriteLine(msg);
                    System.Diagnostics.Debugger.Log(1, "Error", msg);
                }
                return false;
            }
            return (startProc.StandardOutput + startProc.ErrorOutput)?.Contains("pdfinfo ") == true;
        }
        public static int GetPageCount(string filename)
        {
            var pi = new System.Diagnostics.ProcessStartInfo("pdfinfo", filename);
            pi.UseShellExecute = true;
            Devmasters.ProcessExecutor startProc = new Devmasters.ProcessExecutor(pi, 60 * 60 * 6);//6 hours

            startProc.StandardOutputDataReceived += (o, e) =>
            {

            };
            startProc.ErrorDataReceived += (o, e) =>
            {
                //currentSession.ScriptOutput += e.Data;
                if (e.Data?.Contains("Fontconfig error") == false)
                    Console.WriteLine(e.Data);
            };
            try
            {
                startProc.Start();
                var output = startProc.StandardOutput;
                var pageCount = Devmasters.ParseText.ToInt(Devmasters.RegexUtil.GetRegexGroupValue(output, @"^Pages\: \s* (?<num>(\d{1,6}))", "num"),-1);
                return pageCount.Value;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                System.Diagnostics.Debugger.Log(1, "Error", e.ToString());
                throw;
            }
        }


        static byte[] pdfheader = new byte[] { 37, 80, 68, 70 };

        public static bool HasPDFHeader(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return false;
            if (!System.IO.File.Exists(filename))
                return false;

            byte[] b = new byte[4];
            using (var r = System.IO.File.OpenRead(filename))
            {
                r.Read(b, 0, 4);
            }
            bool valid = true;
            for (int i = 0; i < 4; i++)
            {
                valid = valid && b[i] == pdfheader[i];
            }
            return valid;
        }
        public static int GetPageCount(System.IO.Stream str)
        {
            return 0;
        }


        const float a4height = 854;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="jpegFileName"></param>
        /// <param name="pdfStr"></param>
        /// <param name="page">zero base page number</param>
        /// <exception cref="ApplicationException"></exception>
        public static void SaveJpeg(string jpegFileName, Stream pdfStr, int page)
        {
            //float maxSize = 0.0F;
            //try
            //{
            //    iText.Kernel.Pdf.PdfDocument pdf = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(pdfStr));
            //    var p = pdf.GetPage(page+1);
            //    var pagesize = p.GetPageSize();
            //    maxSize = Math.Max(pagesize.GetWidth(), pagesize.GetHeight());
            //    pdf.Close();

            //}
            //catch (Exception e)
            //{
            //    throw new ApplicationException("Cannot open PDF ", e);
            //}

            //int dpi = 300;
            //if (maxSize > 1200)
            //{
            //    dpi =(int)( dpi * (a4height / maxSize));
            //    if (dpi < 72)
            //        dpi = 72;
            //}

            //PDFtoImage.Conversion.SaveJpeg(jpegFileName, pdfStr, page: page, dpi:dpi);
        }
    }
}
