using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using FacePicker.Models;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.Drawing;
using System.Drawing.Imaging;

namespace FacePicker
{
    public static class PickOne
    {
        private const string FaceServiceKey = "";
        private const string BlobConnectionString = "";

        [FunctionName("PickOne")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("PickOne started");

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            var data = JsonConvert.DeserializeObject<Request>(requestBody);

            IFaceServiceClient faceServiceClient = new FaceServiceClient(FaceServiceKey, "https://eastus.api.cognitive.microsoft.com/face/v1.0");
            var faces = await faceServiceClient.DetectAsync(data.ImageUrl);

            var request = System.Net.WebRequest.Create(data.ImageUrl);
            var imageStream = request.GetResponse().GetResponseStream();

            var image = ApplyFaces(faces, imageStream);


            var account = CloudStorageAccount.Parse(BlobConnectionString);
            var serviceClient = account.CreateCloudBlobClient();
            var container = serviceClient.GetContainerReference("pickoneresults");

            var blob = container.GetBlockBlobReference($"{Guid.NewGuid()}.jpg");

            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                ms.Position = 0;
                await blob.UploadFromStreamAsync(ms);
            }

            var response = new Response
            {
                ImageUrl = blob.Uri.AbsoluteUri
            };

            return (ActionResult)new OkObjectResult(response);
        }

        public static Bitmap ApplyFaces(Face[] faces, Stream image)
        {
            var bitmap = new Bitmap(image);
            using (var g = Graphics.FromImage(bitmap))
            {
                foreach (var face in faces)
                {
                    var rectangle = new System.Drawing.Rectangle(face.FaceRectangle.Left, face.FaceRectangle.Top, face.FaceRectangle.Width, face.FaceRectangle.Height);
                    using (var pen = new Pen(Brushes.DeepSkyBlue))
                    {
                        pen.Width = 4.0F;
                        pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Bevel;
                        g.DrawRectangle(pen, rectangle);
                    }
                }

                if (faces.Length > 0)
                {
                    var random = new Random();
                    var winner = faces[random.Next(0, faces.Length - 1)];
                    var winnerRectangle = new System.Drawing.Rectangle(winner.FaceRectangle.Left, winner.FaceRectangle.Top, winner.FaceRectangle.Width, winner.FaceRectangle.Height);
                    using (var pen = new Pen(Brushes.OrangeRed))
                    {
                        pen.Width = 4.0F;
                        pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Bevel;
                        g.DrawRectangle(pen, winnerRectangle);
                    }
                }
            }
            return bitmap;
        }
    }
}
