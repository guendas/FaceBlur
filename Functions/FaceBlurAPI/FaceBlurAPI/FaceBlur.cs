using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using FaceBlurAPI.Model;

namespace FaceBlurAPI
{
    public static class FaceBlur
    {
        [FunctionName("FaceBlur")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            
            log.LogInformation(" ---- FACEBLUR REQUEST PROCESS START ----");

            object responseMessage = null;
            string url = req.Query["url"];

            try
            {
                bool isValidUrl = Uri.IsWellFormedUriString(url, UriKind.Absolute);

                if (isValidUrl)
                {
                    var urlImageBlurredSAS = await Helper.Main(log, url);
                    responseMessage = new ReturnUrls() { UrlOriginalImg = url, UrlBlurredSASImg = urlImageBlurredSAS.Item1, ResMsg = urlImageBlurredSAS.Item2 };

                }
                else
                {
                    responseMessage = "url parameter is null or not well formed https.";
                }
            }
            catch (Exception e)
            {
                responseMessage = "opsss ... something when wrong. See internal log for details";
                log.LogError(e.Message);
            }
            finally { 
                log.LogInformation(" ---- FACEBLUR REQUEST PROCESS END ----");
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
