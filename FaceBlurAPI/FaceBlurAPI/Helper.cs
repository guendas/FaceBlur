﻿using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FaceBlurAPI
{
    static class Helper
    {
        const string RECOGNITION_MODEL3 = RecognitionModel.Recognition03;
        static string STORAGE_CONNECTIONSTRING = null;
        static string SUBSCRIPTION_KEY = null;
        static string ENDPOINT = null;
        static string CONTAINER_NAME_LOWER_CASE = null;
        static int SAS_TOKEN_MINUTES_VALIDITY = 30;

        public static async Task<(string, string)> Main(ILogger log, string validatedUrl)
        {
            STORAGE_CONNECTIONSTRING = GetEnvironmentVariable("STORAGE_CONNECTIONSTRING");
            SUBSCRIPTION_KEY = GetEnvironmentVariable("SUBSCRIPTION_KEY");
            ENDPOINT = GetEnvironmentVariable("ENDPOINT");
            CONTAINER_NAME_LOWER_CASE = GetEnvironmentVariable("CONTAINER_NAME_LOWER_CASE").ToLower();
            Int32.TryParse(GetEnvironmentVariable("SAS_TOKEN_MINUTES_VALIDITY"), out SAS_TOKEN_MINUTES_VALIDITY);

            string checkParams = CheckParameters();

            if (checkParams == String.Empty)
            {
                log.LogDebug("STORAGE_CONNECTIONSTRING:" + GetEnvironmentVariable("STORAGE_CONNECTIONSTRING"));
                log.LogDebug("SUBSCRIPTION_KEY:" + GetEnvironmentVariable("SUBSCRIPTION_KEY"));
                log.LogDebug("ENDPOINT:" + GetEnvironmentVariable("ENDPOINT"));

                IFaceClient client = Authenticate(ENDPOINT, SUBSCRIPTION_KEY);
                log.LogInformation("CLIENT AUTHENTICATED.");

                string blurredImageUrlSASToken = await DetectFaceExtract(log, client, validatedUrl, RECOGNITION_MODEL3);
                return (blurredImageUrlSASToken, "OK");
            }
            else { 
                return ("",checkParams);
            }
        }

        /// <summary>
        /// This method check if the parameters to run the functions are provided, if every is ok, you get an empty string.
        /// </summary>
        /// <returns></returns>
        private static string CheckParameters()
        {
            var result = "";
            if (STORAGE_CONNECTIONSTRING == string.Empty) {
                result = "You need to provide a connection string to a storage account to save you blurred image.\n";
            }
            else if (SUBSCRIPTION_KEY == string.Empty) {
                result += "You need to provide a subscription key to Azure Cognittive Service Face API.\n";
            }
            else if (ENDPOINT == string.Empty)
            {
                result += "You need to provide an endpoint to Azure Cognittive Service Face API.\n";
            }
            else if (CONTAINER_NAME_LOWER_CASE == string.Empty)
            {
                result += "You need to provide a name for a container that will be created to store your blurred images.\n";
            }
            else if (SAS_TOKEN_MINUTES_VALIDITY <= 0)
            {
                result += "You need to provide a time in minutes for which the resulting url will be valid for you access directly.\n";
            }

            return result;
        }

        /// <summary>
        /// This methos create a Face API client using endpoint and key provided.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private static IFaceClient Authenticate(string endpoint, string key)
        {
            return new FaceClient(new ApiKeyServiceClientCredentials(key)) { Endpoint = endpoint };
        }

        /// <summary>
        /// Method to get an array of byte from a remote url. It performs a web call.
        /// </summary>
        /// <param name="imageFilePath"></param>
        /// <returns></returns>
        private static byte[] GetImageAsByteArray(string ImageUrl)
        {
            byte[] imageBytes = null;
            using (var webClient = new WebClient())
            {
                imageBytes = webClient.DownloadData(ImageUrl);
            }
            return imageBytes;
        }

        /// <summary>
        /// This method uses opencv to blur an Image, return a standard .NET Bitmap
        /// </summary>
        /// <param name="log"></param>
        /// <param name="bmpImageToBlur"></param>
        /// <param name="origin"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private static Bitmap BlurImage(ILogger log, Bitmap bmpImageToBlur, Point origin, int width, int height)
        {
            Image<Bgr, Byte> cvImageToBlur = bmpImageToBlur.ToImage<Bgr, byte>();
            Rectangle roi = new Rectangle(origin.Y, origin.X, width, height);
            cvImageToBlur.ROI = roi;

            Image<Bgr, Byte> cvImageBlurredROI = cvImageToBlur.CopyBlank();
            CvInvoke.GaussianBlur(cvImageToBlur, cvImageBlurredROI, new Size(91, 91), 0);

            Bitmap bmpImageBlurredROI = cvImageBlurredROI.AsBitmap();

            Graphics g = Graphics.FromImage(bmpImageToBlur);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(bmpImageBlurredROI, new Point(origin.Y, origin.X));

            log.LogInformation("======== FACE BLURRED ========");

            return bmpImageToBlur;
        }

        /// <summary>
        /// This method call the Face API from a url and estract the "faces" and coordinates
        /// </summary>
        /// <param name="log"></param>
        /// <param name="client"></param>
        /// <param name="imageToBlurUrl"></param>
        /// <param name="recognitionModel"></param>
        /// <returns></returns>
        public static async Task<string> DetectFaceExtract(ILogger log, IFaceClient client, string imageToBlurUrl, string recognitionModel)
        {
            log.LogInformation("======== DETECT FACES START ========");

            IList<DetectedFace> detectedFaces;

            // Detect faces with all attributes from image url.
            detectedFaces = await client.Face.DetectWithUrlAsync($"{imageToBlurUrl}", recognitionModel: recognitionModel);

            log.LogInformation($"{detectedFaces.Count} FACE(S) DETECTED FROM IMAGE `{imageToBlurUrl}`.");

            // I need to read the imaged to blur as byte array, to work in memory.
            byte[] byteData = GetImageAsByteArray(imageToBlurUrl);

            Bitmap bmp = null;
            using (MemoryStream ms = new MemoryStream(byteData)) {
                bmp = (Bitmap)Image.FromStream(ms);
            }

            foreach (var face in detectedFaces)
            {
                Bitmap bmpImageBlurred = BlurImage(log, bmp, new Point(face.FaceRectangle.Top, face.FaceRectangle.Left), face.FaceRectangle.Width, face.FaceRectangle.Height);
                bmp = bmpImageBlurred;
            }

            // Every run I get a new time based url for image
            string imagename = imageToBlurUrl.Split('/').Last();
            string ImageFullDirectoryname = DateTime.UtcNow.ToString().Replace(' ', '/').Replace(':', '/') + "/blurred/" + imagename;
            
            var ImageBlurredUrlSAS = await SendAsBlob(log, bmp, ImageFullDirectoryname, CONTAINER_NAME_LOWER_CASE, STORAGE_CONNECTIONSTRING);

            log.LogInformation("======== DETECT FACES END ========");

            return ImageBlurredUrlSAS;
        }

        /// <summary>
        /// This method upload an image to a storage account, provided as parameter.
        /// </summary>
        /// <param name="log"></param>
        /// <param name="bitmap"></param>
        /// <param name="fileName"></param>
        /// <param name="containerReference"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        private static async Task<string> SendAsBlob(ILogger log, Bitmap bitmap, string fileName, string containerReference, string connectionString)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Fill stream with image in JPG format
                bitmap.Save(memoryStream, ImageFormat.Jpeg);

                // Reset read position to start of stream
                memoryStream.Position = 0;

                var storageAccount = CloudStorageAccount.Parse(connectionString);

                var blobClient = storageAccount.CreateCloudBlobClient();

                await blobClient.GetContainerReference(containerReference).CreateIfNotExistsAsync();
                
                var cloudBlobContainer = blobClient.GetContainerReference(containerReference);
                var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);

                await cloudBlockBlob.UploadFromStreamAsync(memoryStream);
                var key = new StorageSharedKeyCredential(storageAccount.Credentials.AccountName, storageAccount.Credentials.ExportBase64EncodedKey());
                string ImageBlurredWithSAS = GetBlobSasUri(cloudBlobContainer, cloudBlockBlob.Name, key);

                return ImageBlurredWithSAS;
            }
        }

        /// <summary>
        /// Method used to access the parameters that are environment variables.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        /// <summary>
        /// Method used to create the SAS token used to the url of the blurred imaged. That url remain accessibile only for the time provided as parameter.
        /// </summary>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <param name="key"></param>
        /// <param name="storedPolicyName"></param>
        /// <returns></returns>
        private static string GetBlobSasUri(CloudBlobContainer container, string blobName, StorageSharedKeyCredential key, string storedPolicyName = null)
        {
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = container.Name,
                BlobName = blobName,
                Resource = "b",
            };

            if (storedPolicyName == null)
            {
                sasBuilder.StartsOn = DateTimeOffset.UtcNow;
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(SAS_TOKEN_MINUTES_VALIDITY);
                sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
            }
            else
            {
                sasBuilder.Identifier = storedPolicyName;
            }

            string sasToken = sasBuilder.ToSasQueryParameters(key).ToString();

            var r = container.StorageUri.PrimaryUri;
            return $"{r}/{blobName}?{sasToken}";
        }
    }

}
